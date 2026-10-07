# Notion Helper

**A local-first Windows writing and formatting companion for Notion.**

Notion Helper is a small desktop assistant for people who want to write naturally and clean up their notes without sending their writing to a hosted AI service. Select text in the Notion desktop app, open the helper with a keyboard shortcut, review a suggested edit, and apply it only when it looks right.

The first release focuses on a deliberately narrow, useful workflow:

1. Capture selected text from Notion.
2. Ask a model running locally through Ollama to proofread it or improve its structure.
3. Review a readable preview.
4. Paste the result back into Notion as rich HTML, with plain text as a clipboard fallback.

The structure-aware mode can choose headings, bullets, numbered lists, quotes, code blocks, restrained colors, and simple tables **when the content warrants them**. It should leave ordinary text alone rather than decorate it for decoration's sake. Proofread-only mode is intentionally more conservative and must preserve the existing structure.

> **Early development:** this repository starts with the Windows desktop MVP. Notion's clipboard/import behavior, local-model quality, and keyboard focus edge cases need real-world validation. Read [the limitations and validation plan](docs/SOFTWARE_DESIGN.md#15-limitations-and-validation-plan) before relying on generated formatting.

## Product principles

- **Local by default:** send prompt text only to the Ollama service at `127.0.0.1`; no hosted AI APIs, telemetry, or account required.
- **Human in control:** never replace a selection without showing a preview and requiring an explicit Apply action.
- **Meaning before polish:** correct mechanics without inventing facts, changing intent, or dropping meaningful details.
- **Formatting only when useful:** structure, color, quotes, code blocks, and tables are suggestions, not a mandate.
- **Fast to invoke:** keep the helper in the system tray and offer a global shortcut for the selected-text workflow.
- **Fail visibly:** missing models, invalid output, and paste problems should be explained; do not pretend an edit succeeded.
- **Keep the first version small:** clipboard integration is simpler to install and safer to debug than page-wide automation or a Notion API integration.

## What works in this first implementation

- A Windows WPF floating window, launched from the notification-area icon or a configurable global shortcut preset.
- Capturing selected Unicode text in the foreground application using the Windows clipboard.
- **Proofread only** and **Improve structure when useful** modes.
- Local Ollama chat requests using `qwen2.5:7b` by default; the local model name is configurable.
- Local settings for the model and `Ctrl+Shift+Space`, `Ctrl+Alt+Space`, or `Ctrl+Shift+N` shortcut presets.
- A JSON response contract, validation of supported blocks/colors and table dimensions, and a semantic rich-format preview that approximates headings, lists, quotes, code, colors, and tables without interpreting model output as markup.
- A side-by-side text comparison with removed wording struck through and additions highlighted; large inputs fall back to a clearly disclosed whole-text replacement view.
- Checks that the captured window still belongs to the same process before clipboard replacement and checks focus/identity again before simulated paste; stale targets or changed focus stop the automatic paste.
- A captured target-window and process identity check before replacing the clipboard and again before sending paste; stale/changed targets disable or block automatic paste.
- HTML clipboard output for paragraphs, headings, lists, quotes, code, colors, and tables, plus a Unicode plain-text fallback.
- Explicit Apply and Close actions; closing the window hides it so the helper remains available in the tray.
- No Notion token, browser extension, hosted model account, or recurring service charge.

## Requirements

- Windows 10 or 11 (64-bit).
- .NET 10 Desktop Runtime to run a framework-dependent build; the .NET 10 SDK to build from source.
- [Ollama for Windows](https://ollama.com/download).
- A locally downloaded model. The default is `qwen2.5:7b` (about 4.7 GB at Q4_K_M); `qwen2.5:3b` is a lower-memory option.
- Notion desktop app or another Windows app that accepts pasted text.

The project targets `net10.0-windows` and uses WPF and WinForms components already included in the .NET Windows Desktop framework. The first version does not require NuGet packages.

## Get started

### 1. Install Ollama and download the model

Install Ollama from its official download page. In PowerShell, run:

```powershell
ollama pull qwen2.5:7b
```

Ollama normally starts its local server automatically after installation. Confirm that it is available:

```powershell
ollama list
Invoke-RestMethod http://127.0.0.1:11434/api/tags
```

The first model download requires an internet connection and several gigabytes of disk space. After download, inference is local; the application sends its request to the loopback address `127.0.0.1`, not to a cloud endpoint.

### 2. Build and run

```powershell
dotnet build .\src\NotionHelper\NotionHelper.csproj -c Release
dotnet run --project .\src\NotionHelper\NotionHelper.csproj
```

The helper opens in the notification area. Right-click its icon to show or exit. In Notion, select text and press the configured shortcut (default **Ctrl+Shift+Space**). The helper captures the selection, opens its preview window, and waits for you to choose a mode and click **Improve text**.

If the shortcut is already registered by another application, open the helper from the notification area. Expand **Local settings** to choose an installed Ollama model or shortcut preset, then select **Save**. A shortcut conflict is reported and the prior registered shortcut is retained where possible. Settings are stored in `%LOCALAPPDATA%\NotionHelper\settings.json`; the file contains the model name and shortcut only, never selected writing. The Ollama endpoint is not configurable and remains loopback-only.

### 3. Review and apply

Choose **Proofread only** to retain paragraph structure and correct mechanics. Choose **Improve structure when useful** to allow semantic blocks when they make the content clearer. Inspect the **Formatted result** tab for semantic formatting and the **Text changes** tab for side-by-side word and punctuation changes, then click **Apply to Notion** to paste to the captured target. Formatting-only differences are visible in the formatted preview, not the text comparison. When an input exceeds the detailed-diff work budget, the tabs show a complete original/proposed replacement instead of an inline diff. The formatting preview is only an approximation; the target editor may paste differently.

The helper records the captured window handle and owning process ID. During capture it checks target identity and foreground focus before and after simulated copy, then checks that the clipboard did not change while it read the copied text. Before applying, it checks that the captured identity is still current and checks again after returning focus, immediately before simulated paste. A detected focus/target/clipboard change cancels that step; if the window closed, its handle was reused, or focus changed during Apply, automatic paste is blocked and the preview remains available for manual paste where applicable. These checks cannot make OS clipboard and keyboard operations atomic. The helper does **not** detect if the selection/caret moved inside the same still-open window; verify that the original selection is unchanged before applying. The helper does not verify that the target is Notion. Rich HTML clipboard support depends on the target editor; if formatting is not accepted, the clipboard includes plain text to allow a manual paste.

### Optional local model benchmark

To compare the configured default with the lower-memory model using the app's production prompts and validated response contract, first pull the candidate locally:

```powershell
ollama pull qwen2.5:3b
dotnet run --project .\tools\ModelBenchmark\ModelBenchmark.csproj -c Release -- qwen2.5:7b qwen2.5:3b
```

The benchmark uses three fixed synthetic examples (proofreading, useful structure, and avoiding decoration of ordinary prose), reports pass/check counts and response times, and does not print or save model outputs. It never downloads a model or changes the app's default. On the development laptop, one run scored 3/3 for the Q4_K_M 7B model and 1/3 for the Q4_K_M 3B model. The larger model used about 4.75 GB VRAM and left about 0.65 GB free on that machine while loaded; choose the 3B model if other GPU workloads need more memory. This small synthetic benchmark is not proof of correctness. Compare behavior on representative writing, RAM/VRAM use, licenses, and latency before changing settings. Measurements depend on machine load, Ollama version, and thermal/power state.

## Privacy and cost

The model runs on your computer via Ollama. This project has no analytics, telemetry, sign-in, API key, or hosted inference integration. The default endpoint is hard-coded to loopback. The model must be downloaded once, and internet access may be required for updates; neither condition implies a per-use charge.

The workflow temporarily uses the Windows clipboard to read the selected text and provide rich output. Copying a selection replaces the clipboard contents. This initial version **does not promise to preserve arbitrary previous clipboard formats** (such as images, files, or rich office content). Do not use it with text subject to a policy that prohibits copying it to the clipboard. Local settings contain no writing or prompt history. The local model may also use system RAM and GPU memory while running.

Ollama/model downloads have their own licenses and notices. Review the specific model's license and usage terms before using it; a zero-cost local workflow does not itself grant rights to model weights or generated output.

## Supported formatting contract

The model can return these semantic blocks:

| Block | Intended use |
|---|---|
| Paragraph | Ordinary prose |
| Heading | A real section or topic transition |
| Bullet / numbered | A genuine list of related items or steps |
| Quote | Quoted speech, a quotation, or cited material |
| Code | Actual code or commands, not technical-sounding prose |
| Table | Data that naturally has consistent columns |

Optional colors are limited to blue, purple, orange, and red. A color is appropriate only when it communicates a meaningful callout. Unsupported block names/colors are rejected before they reach the clipboard.

Formatting is conveyed through the Windows HTML clipboard format. Notion may transform, normalize, or discard parts of pasted HTML; exact color preservation, syntax highlighting, and every block conversion are not guaranteed. The implementation does not use undocumented Notion editor APIs or automate Notion's private data model.

## Architecture

```text
Global shortcut / tray icon
          |
          v
Windows WPF preview window
  | capture selected text      | explicit Apply
  v                            v
Windows clipboard       HTML + Unicode clipboard
  |                            |
  +------> OllamaClient        +----> original foreground app
              |
              v
       127.0.0.1:11434
       local Ollama model
```

- `src/NotionHelper/` contains the WPF application, local Ollama client, response contract, HTML clipboard formatter, and small Win32 interop layer.
- `docs/SOFTWARE_DESIGN.md` is the detailed living design document.
- `AGENTS.md` and `agents/` preserve product goals, implementation rules, decisions, and current limitations for future contributors and coding agents.

See the [software design document](docs/SOFTWARE_DESIGN.md) for the requirements, component responsibilities, data flow, security/privacy considerations, alternatives, and planned validation.

## Development

Build with:

```powershell
dotnet build .\src\NotionHelper\NotionHelper.csproj
dotnet test .\tests\NotionHelper.Tests\NotionHelper.Tests.csproj -c Release
```

The app project uses only Windows Desktop framework components. The xUnit suite exercises clipboard serialization, settings validation, Ollama protocol behavior using an in-memory fake handler, safe WPF preview rendering, and paste sequencing through a fake environment. It does not require a model server, network access, Notion, or access to the system clipboard. The app requires Windows for WPF and Win32 interop.

Build the optional benchmark CLI with:

```powershell
dotnet build .\tools\ModelBenchmark\ModelBenchmark.csproj -c Release
```

GitHub Actions runs the Windows app build, deterministic test suite, and benchmark build on pushes and pull requests. It does not download model weights or run nondeterministic inference.

### Contributing

1. Read [`AGENTS.md`](AGENTS.md), [`agents/QUICK_START.md`](agents/QUICK_START.md), [`agents/PRODUCT_DIRECTION.md`](agents/PRODUCT_DIRECTION.md), [`agents/KNOWN_ISSUES.md`](agents/KNOWN_ISSUES.md), and the design document before changing user-visible behavior.
2. Keep editing opt-in and make errors visible.
3. Update the design document whenever an architectural decision, data flow, requirement, privacy property, or known limitation changes.
4. Run the focused build and deterministic tests. Use the optional model benchmark for local quality/latency observations; do not make it a CI test.
5. Do not commit tokens, personal page contents, model files, build output, or local configuration.

## Roadmap

1. **Selected-text MVP (current):** local proofreading, structure-aware formatting, preview, clipboard apply.
2. **Reliable daily use:** validate capture/focus and rich-paste behavior across Notion app versions; handle clipboard contention and accessibility; improve the preview and selection safety; package a signed or easily installable Windows release. Model and shortcut configuration and deterministic protocol tests are implemented, but Notion integration remains unverified.
3. **Writing assistance:** prompt box for drafting when no text is selected, reusable user-approved style preferences, and clearer output-diff presentation.
4. **Optional page/database workflows:** investigate an explicitly enabled Notion API integration for creating pages, databases, and chart-ready data. Request only the access required and explain that Notion integration permissions and API capabilities differ from direct local clipboard pasting.

No future Notion connection should silently broaden the application's access to workspace content. Chart rendering, database creation, richer styling, and selection-free editing require separate design and capability checks before implementation.

## License

This project is distributed under the MIT License; see [`LICENSE`](LICENSE).
