using System.ComponentModel;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NotionHelper.Interop;
using NotionHelper.Models;
using NotionHelper.Services;
using WpfApplication = System.Windows.Application;
using WpfClipboard = System.Windows.Clipboard;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDataObject = System.Windows.DataObject;
using WpfTextDataFormat = System.Windows.TextDataFormat;
using Forms = System.Windows.Forms;

namespace NotionHelper;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x4E48;
    private const int WmHotkey = 0x0312;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint VkSpace = 0x20;

    private readonly OllamaClient _ollama = new();
    private readonly Forms.NotifyIcon _trayIcon;
    private HwndSource? _source;
    private nint _targetWindow;
    private ImprovementResult? _result;
    private bool _allowClose;
    private bool _hotkeyRegistered;
    private bool _isImproving;

    public MainWindow()
    {
        InitializeComponent();

        _trayIcon = CreateTrayIcon();
        SourceInitialized += OnSourceInitialized;
        Closed += (_, _) => Cleanup();
    }

    private static Forms.NotifyIcon CreateTrayIcon()
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
            Text = "Notion Helper — Ctrl+Shift+Space",
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
        _hotkeyRegistered = NativeMethods.RegisterHotKey(handle, HotkeyId, ModControl | ModShift, VkSpace);

        StatusText.Text = _hotkeyRegistered
            ? "Press Ctrl+Shift+Space with text selected in Notion."
            : "The global shortcut is unavailable. Open the helper from its tray icon.";
    }

    private nint WindowMessageHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _ = CaptureSelectionAsync();
        }

        return 0;
    }

    private async Task CaptureSelectionAsync()
    {
        _targetWindow = NativeMethods.GetForegroundWindow();
        if (_targetWindow == IntPtr.Zero || _targetWindow == new WindowInteropHelper(this).Handle)
        {
            StatusText.Text = "Select text in Notion first, then press Ctrl+Shift+Space.";
            ShowAndActivate();
            return;
        }

        var clipboardSequence = NativeMethods.GetClipboardSequenceNumber();
        Hide();
        await Task.Delay(180);
        try
        {
            KeyboardInput.SendCopy();
        }
        catch (InvalidOperationException exception)
        {
            StatusText.Text = exception.Message;
            ShowAndActivate();
            return;
        }

        await Task.Delay(250);

        string selectedText;
        try
        {
            if (NativeMethods.GetClipboardSequenceNumber() == clipboardSequence)
            {
                StatusText.Text = "No text was copied. Select text in Notion and try again.";
                ShowAndActivate();
                return;
            }

            selectedText = WpfClipboard.ContainsText()
                ? WpfClipboard.GetText(WpfTextDataFormat.UnicodeText)
                : string.Empty;
        }
        catch (ExternalException exception)
        {
            StatusText.Text = $"Could not read the selection from the clipboard: {exception.Message}";
            ShowAndActivate();
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedText))
        {
            StatusText.Text = "No text was copied. Select text in Notion and try again.";
            ShowAndActivate();
            return;
        }

        SourceText.Text = selectedText;
        PreviewText.Clear();
        _result = null;
        ApplyButton.IsEnabled = false;
        StatusText.Text = "Selection captured. Choose a mode and improve it.";
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
            StatusText.Text = "Capture a text selection with Ctrl+Shift+Space before improving it.";
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
            PreviewText.Text = _result.ToPlainText();
            ApplyButton.IsEnabled = _targetWindow != IntPtr.Zero;
            StatusText.Text = $"Preview ready. {_result.Blocks.Count} content block(s); nothing changes until you apply.";
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

    private async void ApplyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_result is null || _targetWindow == IntPtr.Zero)
        {
            StatusText.Text = "Capture a Notion selection and generate a preview before applying.";
            return;
        }

        try
        {
            var html = HtmlClipboardFormatter.ToClipboardHtml(_result);
            var data = new WpfDataObject();
            data.SetData(WpfDataFormats.Html, html);
            data.SetData(WpfDataFormats.UnicodeText, _result.ToPlainText());
            WpfClipboard.SetDataObject(data, true);

            ApplyButton.IsEnabled = false;
            Hide();
            if (!NativeMethods.SetForegroundWindow(_targetWindow))
            {
                StatusText.Text = "Could not return focus to the original window. The preview is on the clipboard; paste it into Notion manually.";
                ShowAndActivate();
                return;
            }

            await Task.Delay(180);
            KeyboardInput.SendPaste();
            StatusText.Text = "Paste sent to the original window. Verify the result in Notion.";
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
            NativeMethods.UnregisterHotKey(new WindowInteropHelper(this).Handle, HotkeyId);
        }
        _ollama.Dispose();
    }
}
