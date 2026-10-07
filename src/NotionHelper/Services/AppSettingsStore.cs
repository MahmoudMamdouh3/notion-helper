using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NotionHelper.Models;

namespace NotionHelper.Services;

public sealed class AppSettingsStore
{
    private static readonly Regex ModelNamePattern = new(
        @"^[A-Za-z0-9][A-Za-z0-9._:/-]{0,127}$",
        RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public AppSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NotionHelper",
            "settings.json"))
    {
    }

    internal AppSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(_settingsPath);
        ValidateShortcutValue(json);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
            ?? throw new InvalidDataException("The local settings file is empty or invalid.");
        Validate(settings);
        return settings;
    }

    public void Save(AppSettings settings)
    {
        Validate(settings);
        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("The settings file has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{_settingsPath}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }

    internal static void Validate(AppSettings settings)
    {
        if (settings is null || string.IsNullOrWhiteSpace(settings.Model) ||
            !ModelNamePattern.IsMatch(settings.Model) ||
            settings.Model.Contains("://", StringComparison.OrdinalIgnoreCase) ||
            settings.Model.Split('/').Any(segment => segment is "." or "..") ||
            !Enum.IsDefined(settings.Shortcut))
        {
            throw new InvalidDataException(
                "Settings are invalid. Use an Ollama model name and one of the supported shortcut presets.");
        }
    }

    private static void ValidateShortcutValue(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The local settings file must contain a JSON object.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                throw new InvalidDataException("The local settings file contains duplicate properties.");
            }

            if (property.Name.Equals("shortcut", StringComparison.OrdinalIgnoreCase) &&
                (property.Value.ValueKind != JsonValueKind.String ||
                 !Enum.TryParse<ShortcutPreset>(property.Value.GetString(), ignoreCase: false, out var preset) ||
                 !Enum.IsDefined(preset)))
            {
                throw new InvalidDataException("The local settings file contains an unsupported shortcut preset.");
            }
        }
    }
}
