# Quick start for contributors and coding agents

## What this project is

Notion Helper is a Windows .NET 10 WPF tray application. A global shortcut captures selected text through the Windows clipboard, sends it to a user-installed Ollama model at `127.0.0.1`, shows a preview, then pastes HTML plus a Unicode text fallback only after the user selects Apply.

```text
WPF/tray + hotkey -> clipboard capture -> OllamaClient -> validated block model
                                                   -> preview -> explicit Apply
                                                   -> HTML/text clipboard -> foreground app
```

## Hard stops

1. Inference remains local to loopback Ollama; never add hosted fallbacks, telemetry, or API keys.
2. Model output is untrusted and must be validated and HTML-encoded.
3. Never apply without a visible preview and an explicit user action.
4. Do not use Notion API access, editor injection, or workspace crawling without a separately approved design.
5. Do not claim rich-paste success inside Notion without recorded end-to-end validation.

## Where to look first

| Work area | Read first |
|---|---|
| Product constraints | [`PRODUCT_DIRECTION.md`](./PRODUCT_DIRECTION.md) |
| Architecture and decisions | [`../docs/SOFTWARE_DESIGN.md`](../docs/SOFTWARE_DESIGN.md) |
| Current gaps and risks | [`KNOWN_ISSUES.md`](./KNOWN_ISSUES.md) |
| Window, tray, hotkey, capture/apply flow | [`../src/NotionHelper/MainWindow.xaml.cs`](../src/NotionHelper/MainWindow.xaml.cs) |
| WPF layout and controls | [`../src/NotionHelper/MainWindow.xaml`](../src/NotionHelper/MainWindow.xaml) |
| Local model protocol and prompts | [`../src/NotionHelper/Services/OllamaClient.cs`](../src/NotionHelper/Services/OllamaClient.cs) |
| Settings and shortcut validation | [`../src/NotionHelper/Models/AppSettings.cs`](../src/NotionHelper/Models/AppSettings.cs), [`../src/NotionHelper/Services/AppSettingsStore.cs`](../src/NotionHelper/Services/AppSettingsStore.cs) |
| Output contract and HTML clipboard | [`../src/NotionHelper/Models/ImprovementResult.cs`](../src/NotionHelper/Models/ImprovementResult.cs), [`../src/NotionHelper/Services/ImprovementResultValidator.cs`](../src/NotionHelper/Services/ImprovementResultValidator.cs), [`../src/NotionHelper/Services/HtmlClipboardFormatter.cs`](../src/NotionHelper/Services/HtmlClipboardFormatter.cs) |

## Verify changes on Windows

```powershell
dotnet build .\src\NotionHelper\NotionHelper.csproj -c Release
dotnet test .\tests\NotionHelper.Tests\NotionHelper.Tests.csproj -c Release
dotnet build .\tools\ModelBenchmark\ModelBenchmark.csproj -c Release
```

The deterministic tests use a fake HTTP handler and do not require Ollama or Notion. To measure installed models with fixed synthetic examples only:

```powershell
dotnet run --project .\tools\ModelBenchmark\ModelBenchmark.csproj -c Release -- qwen2.5:7b qwen2.5:3b
```

The benchmark does not download models or save prompt/output text. A `CHECK` is a quality observation, not a deterministic test-suite failure. See the README and design document for model setup and interpretation.

## Change discipline

Keep presentation, local services, and validated content models separate. Preserve explicit errors and test failure paths. Update the README, product notes, living design/verification record, and known-issues ledger whenever a feature, privacy property, limitation, or contributor workflow changes.
