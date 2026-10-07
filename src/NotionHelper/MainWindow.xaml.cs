using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NotionHelper.Interop;
using NotionHelper.Models;
using NotionHelper.Presentation;
using NotionHelper.Services;
using WpfApplication = System.Windows.Application;
using Forms = System.Windows.Forms;

namespace NotionHelper;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x4E48;
    private const int WmHotkey = 0x0312;

    private readonly AppSettingsStore _settingsStore = new();
    private readonly WindowsPasteEnvironment _pasteEnvironment = new();
    private readonly string? _settingsLoadError;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly PasteCoordinator _pasteCoordinator;
    private readonly SelectionCaptureCoordinator _selectionCaptureCoordinator;
    private AppSettings _settings;
    private OllamaClient _ollama;
    private HwndSource? _source;
    private WindowTargetSnapshot _target;
    private ImprovementResult? _result;
    private bool _allowClose;
    private bool _hotkeyRegistered;
    private bool _isImproving;
    private int _hotkeyId = HotkeyId;
    private ShortcutPreset _activeShortcut;

    public MainWindow()
    {
        try
        {
            _settings = _settingsStore.Load();
        }
        catch (Exception exception) when (
            exception is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            _settings = new AppSettings();
            _settingsLoadError = $"Could not load local settings: {exception.Message} Save valid settings to continue.";
        }

        _ollama = new OllamaClient(_settings.Model);
        _pasteCoordinator = new PasteCoordinator(_pasteEnvironment);
        _selectionCaptureCoordinator = new SelectionCaptureCoordinator(_pasteEnvironment);
        InitializeComponent();
        ModelNameInput.Text = _settings.Model;
        ShortcutSelector.SelectedValue = _settings.Shortcut.ToString();

        _trayIcon = CreateTrayIcon();
        SourceInitialized += OnSourceInitialized;
        Closed += (_, _) => Cleanup();
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Notion Helper", null, (_, _) =>
            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                if (WpfApplication.Current.MainWindow is MainWindow window)
                {
                    window.Show();
                    window.Activate();
                }
            }));
        menu.Items.Add("Exit", null, (_, _) =>
            WpfApplication.Current.Dispatcher.Invoke(() =>
            {
                if (WpfApplication.Current.MainWindow is MainWindow window)
                {
                    window._allowClose = true;
                    WpfApplication.Current.Shutdown();
                }
            }));

        var icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = $"Notion Helper — {ShortcutPresets.GetBinding(_settings.Shortcut).DisplayName}",
            ContextMenuStrip = menu,
            Visible = true
        };
        icon.DoubleClick += (_, _) => WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            if (WpfApplication.Current.MainWindow is MainWindow window)
            {
                window.Show();
                window.Activate();
            }
        });
        return icon;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source.AddHook(WindowMessageHook);
        var binding = ShortcutPresets.GetBinding(_settings.Shortcut);
        _hotkeyRegistered = NativeMethods.RegisterHotKey(handle, _hotkeyId, binding.Modifiers, binding.VirtualKey);
        if (_hotkeyRegistered)
        {
            _activeShortcut = _settings.Shortcut;
        }

        StatusText.Text = _settingsLoadError ?? (_hotkeyRegistered
            ? $"Press {binding.DisplayName} with text selected in Notion."
            : $"The {binding.DisplayName} shortcut is unavailable. Open the helper from its tray icon.");
    }

    private nint WindowMessageHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == _hotkeyId)
        {
            handled = true;
            _ = CaptureSelectionAsync();
        }

        return 0;
    }

    private async Task CaptureSelectionAsync()
    {
        _target = default;
        _result = null;
        ApplyButton.IsEnabled = false;
        SourceText.Clear();
        PreviewViewer.Document = new System.Windows.Documents.FlowDocument();

        var targetWindow = NativeMethods.GetForegroundWindow();
        if (targetWindow == IntPtr.Zero || targetWindow == new WindowInteropHelper(this).Handle ||
            !NativeMethods.IsWindow(targetWindow))
        {
            StatusText.Text = $"Select text first, then press {ShortcutPresets.GetBinding(_settings.Shortcut).DisplayName}.";
            ShowAndActivate();
            return;
        }

        var targetThread = NativeMethods.GetWindowThreadProcessId(targetWindow, out var targetProcessId);
        if (targetThread == 0 || targetProcessId == 0)
        {
            StatusText.Text = "Could not identify the selected-text target window. Try the shortcut again.";
            ShowAndActivate();
            return;
        }

        var processName = GetProcessName(targetProcessId);
        _target = new WindowTargetSnapshot(targetWindow, targetProcessId, processName);
        var clipboardSequence = NativeMethods.GetClipboardSequenceNumber();
        Hide();
        try
        {
            var outcome = await _selectionCaptureCoordinator.CaptureAsync(_target, clipboardSequence);
            if (outcome.Disposition != SelectionCaptureDisposition.Captured)
            {
                StatusText.Text = outcome.Disposition switch
                {
                    SelectionCaptureDisposition.TargetChanged =>
                        $"Selection capture was cancelled because the target changed: {outcome.Error}",
                    SelectionCaptureDisposition.FocusChanged =>
                        "Selection capture was cancelled because the foreground window changed.",
                    SelectionCaptureDisposition.ClipboardUnchanged or SelectionCaptureDisposition.EmptyText =>
                        "No text was copied. Select text in Notion and try again.",
                    SelectionCaptureDisposition.ClipboardChanged =>
                        "The clipboard changed while reading the selection. Nothing was sent to the model; try capturing again.",
                    _ => throw new InvalidOperationException("Unexpected selection capture outcome.")
                };
                ShowAndActivate();
                return;
            }

            SourceText.Text = outcome.Text
                ?? throw new InvalidOperationException("The selection capture returned no text.");
        }
        catch (Exception exception) when (exception is InvalidOperationException or ExternalException)
        {
            StatusText.Text = exception is ExternalException
                ? $"Could not read the selection from the clipboard: {exception.Message}"
                : exception.Message;
            ShowAndActivate();
            return;
        }
        StatusText.Text = $"Selection captured from {_target.ProcessName}. Keep its selection unchanged before applying.";
        ShowAndActivate();
    }

    private async void ImproveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isImproving)
        {
            return;
        }

        var source = SourceText.Text;
        if (string.IsNullOrWhiteSpace(source))
        {
            StatusText.Text = $"Capture a text selection with {ShortcutPresets.GetBinding(_settings.Shortcut).DisplayName} before improving it.";
            return;
        }

        _isImproving = true;
        ImproveButton.IsEnabled = false;
        ApplyButton.IsEnabled = false;
        StatusText.Text = "Improving text with your local model…";
        try
        {
            var mode = ModeSelector.SelectedIndex == 1
                ? ImprovementMode.StructureWhenUseful
                : ImprovementMode.Proofread;
            _result = await _ollama.ImproveAsync(source, mode);
            PreviewViewer.Document = PreviewDocumentBuilder.Create(_result);
            var targetIsCurrent = _pasteEnvironment.IsTargetCurrent(_target, out var targetError);
            ApplyButton.IsEnabled = targetIsCurrent;
            StatusText.Text = targetIsCurrent
                ? $"Preview ready for {_target.ProcessName}. {_result.Blocks.Count} content block(s); verify the selection is unchanged before applying."
                : $"Preview is ready, but Apply is disabled: {targetError}";
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or InvalidOperationException
                or System.Text.Json.JsonException)
        {
            StatusText.Text = exception.Message;
            _result = null;
        }
        finally
        {
            _isImproving = false;
            ImproveButton.IsEnabled = true;
        }
    }

    private void SaveSettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isImproving)
        {
            StatusText.Text = "Wait for the current local model request to finish before changing settings.";
            return;
        }

        if (ShortcutSelector.SelectedValue is not string shortcutName ||
            !Enum.TryParse<ShortcutPreset>(shortcutName, out var shortcut))
        {
            StatusText.Text = "Choose one of the supported keyboard shortcuts.";
            return;
        }

        var updatedSettings = new AppSettings
        {
            Model = ModelNameInput.Text.Trim(),
            Shortcut = shortcut
        };

        try
        {
            AppSettingsStore.Validate(updatedSettings);
        }
        catch (InvalidDataException exception)
        {
            StatusText.Text = exception.Message;
            return;
        }

        if (!TryChangeShortcut(updatedSettings.Shortcut))
        {
            return;
        }

        try
        {
            _settingsStore.Save(updatedSettings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var shortcutRestored = TryChangeShortcut(_settings.Shortcut);
            StatusText.Text = $"Could not save local settings: {exception.Message}" +
                (shortcutRestored ? string.Empty : $" {StatusText.Text}");
            return;
        }

        _ollama.UpdateModel(updatedSettings.Model);
        _settings = updatedSettings;
        ModelNameInput.Text = updatedSettings.Model;
        _trayIcon.Text = $"Notion Helper — {ShortcutPresets.GetBinding(shortcut).DisplayName}";
        StatusText.Text = $"Settings saved locally. Model: {updatedSettings.Model}; shortcut: {ShortcutPresets.GetBinding(shortcut).DisplayName}. Inference remains local.";
    }

    private bool TryChangeShortcut(ShortcutPreset shortcut)
    {
        if (_source is null)
        {
            StatusText.Text = "The shortcut cannot be changed until the window is initialized.";
            return false;
        }

        if (shortcut == _activeShortcut && _hotkeyRegistered)
        {
            return true;
        }

        var handle = new WindowInteropHelper(this).Handle;
        var hadRegisteredShortcut = _hotkeyRegistered;
        var previousShortcut = _activeShortcut;
        if (_hotkeyRegistered)
        {
            if (!NativeMethods.UnregisterHotKey(handle, _hotkeyId))
            {
                var unregisterFailure = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
                StatusText.Text = $"Could not release the current shortcut: {unregisterFailure}. Settings were not changed.";
                return false;
            }

            _hotkeyRegistered = false;
        }

        var binding = ShortcutPresets.GetBinding(shortcut);
        if (NativeMethods.RegisterHotKey(handle, _hotkeyId, binding.Modifiers, binding.VirtualKey))
        {
            _hotkeyRegistered = true;
            _activeShortcut = shortcut;
            return true;
        }

        var registrationFailure = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
        var previous = ShortcutPresets.GetBinding(previousShortcut);
        _hotkeyRegistered = hadRegisteredShortcut &&
            NativeMethods.RegisterHotKey(handle, _hotkeyId, previous.Modifiers, previous.VirtualKey);
        if (_hotkeyRegistered)
        {
            _activeShortcut = previousShortcut;
        }

        StatusText.Text = $"Could not register {binding.DisplayName}: {registrationFailure}. " +
            (_hotkeyRegistered ? "The previous shortcut remains active." : "Use the tray icon to open the helper.");
        return false;
    }

    private async void ApplyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_result is null)
        {
            StatusText.Text = "Capture a Notion selection and generate a preview before applying.";
            return;
        }

        if (!_pasteEnvironment.IsTargetCurrent(_target, out var targetError))
        {
            ApplyButton.IsEnabled = false;
            StatusText.Text = $"Apply is disabled because the original target is no longer available: {targetError} Capture the selection again.";
            return;
        }

        try
        {
            ApplyButton.IsEnabled = false;
            Hide();
            var outcome = await _pasteCoordinator.ApplyAsync(_result, _target);
            StatusText.Text = outcome.Message;
            ShowAndActivate();
        }
        catch (Exception exception) when (exception is ExternalException or InvalidOperationException)
        {
            StatusText.Text = $"Could not apply the formatted content: {exception.Message}";
            ShowAndActivate();
        }
    }

    private void ModeSelector_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (StatusText is null)
        {
            return;
        }

        StatusText.Text = ModeSelector.SelectedIndex == 1
            ? "Formatting is optional: headings, lists, quotes, code, color, or tables only when useful."
            : "Proofreading preserves the existing structure and fixes spelling and grammar only.";
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Hide();

    private static string GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return "unknown application";
        }
    }

    private void ShowAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private void Cleanup()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _source?.RemoveHook(WindowMessageHook);
        if (_hotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(new WindowInteropHelper(this).Handle, _hotkeyId);
        }
        _ollama.Dispose();
    }
}
