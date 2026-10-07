# Notion Helper — Software Design Document

**Status:** Living document; describes the Windows MVP and reliability/configuration phase
**Last reviewed:** 2026-10-07
**Owners:** Project contributors
**Repository:** `MahmoudMamdouh3/notion-helper`

This document is the source of truth for product intent and software architecture. Update it whenever the implementation changes an architectural decision, data flow, product boundary, privacy property, or material limitation.

## 1. Executive summary

Notion Helper is a Windows desktop companion that lets a user select text in Notion, invoke a lightweight editor using a global shortcut, obtain a local-language-model suggestion, inspect the result, and explicitly paste it back into the original selection. It aims to make spelling and grammar repair quick while also helping transform rough notes into clearer Notion-compatible content.

The first implementation uses .NET WPF for the user interface, Win32 interop for a global shortcut and keyboard/foreground-window operations, the Windows clipboard for selected-text capture and rich-text application, and Ollama's local HTTP API for inference. The current default is `qwen2.5:7b` (Q4_K_M); `qwen2.5:3b` remains a lower-memory setting. No Notion API token, cloud inference provider, application account, or recurring service is required.

The product is deliberately conservative: proofreading preserves the original structure; structure-aware editing may propose formatting, but only when the text benefits from it. The user sees a preview and must click Apply. The helper does not silently edit the page or crawl workspace content.

## 2. Product context and goals

### 2.1 User problem

The user writes rough text in Notion and wants:

- Fast correction of spelling, grammar, punctuation, and sentence structure.
- A context-aware helper that can turn appropriate text into headings, lists, quotes, code blocks, colored callouts, tables, and eventually attractive charts.
- A local and no-recurring-cost approach that makes use of an available RTX 4060.
- A simple interaction that is available while writing, rather than a separate general-purpose writing workflow.

### 2.2 Goals for the MVP

1. Run inference locally on Windows using free, user-installed local software.
2. Invoke the tool quickly while text is selected in Notion desktop.
3. Preserve the original selection until the user explicitly applies an inspected preview.
4. Offer a proofreading-only mode with no intentional formatting changes.
5. Offer an optional structure-aware mode with semantic formatting choices.
6. Produce output that can be pasted into Notion without relying on its private/editor-internal APIs.
7. Report missing dependencies, invalid model output, focus failures, and clipboard failures honestly.
8. Document alternatives, privacy properties, risks, constraints, and future scope.

### 2.3 Non-goals for the MVP

- An official or unofficial Notion API client, workspace crawler, or page synchronizer.
- An editor plugin embedded inside Notion's private Electron/Chromium runtime.
- Silent/autonomous replacements, live rewriting as the user types, or unreviewed page writes.
- A cloud model, paid API, telemetry service, server-side backend, or user account.
- Guaranteed perfect semantic preservation, professional copy-editing, fact-checking, or style imitation.
- Full-featured chart rendering, external image hosting, complex database creation, or control over Notion's private internal document model.
- Cross-platform support. This release targets Windows because the user's current Notion workflow and computer are Windows-based.

### 2.4 Success criteria

The MVP is useful when a user can reliably: select a passage in Notion, open the helper with the shortcut, choose a mode, receive a preview from a model running on the same machine, decide whether to apply it, and return the content to Notion. The structured mode should avoid applying a formatting block just because the block is available.

Required verification before calling the MVP production-ready:

- Shortcut registration succeeds or provides a usable tray-based fallback.
- Copying a representative selection captures the intended Unicode text.
- A stopped Ollama service and missing model produce actionable errors.
- The output validator rejects unsupported blocks, colors, and malformed tables.
- The HTML clipboard offsets/fragment boundaries are byte-correct for non-ASCII text.
- Applying an ordinary paragraph, list, quote, code block, colored text, and table is checked in the actual Notion desktop app.
- Focus restoration and selection replacement are checked on supported Windows/Notion versions.
- The user confirms the actual cost, privacy, and formatting behavior match expectations.

These criteria have not all been verified by a compilation alone. In particular, Notion rich-paste behavior needs interactive validation.

## 3. User experience

### 3.1 Primary flow

1. The application starts in the Windows notification area.
2. The user selects text in Notion and presses `Ctrl+Shift+Space`.
3. The app remembers the foreground window, briefly hides itself, sends `Ctrl+C`, and reads Unicode text from the clipboard.
4. The helper appears with the captured source and the mode selector.
5. The user chooses either:
   - **Proofread only** — repair language while retaining prose and paragraph structure.
   - **Improve structure when useful** — repair language and optionally apply semantic blocks/color.
6. The user clicks Improve text. The app sends a local request to Ollama and displays a plain-text preview.
7. The user inspects the preview and clicks Apply to Notion or closes/hides the helper.
8. Apply writes HTML and Unicode fallback content to the clipboard, returns focus to the remembered window, and sends `Ctrl+V`.

### 3.2 Alternate flow and failure behavior

- If no text is selected/copied, the helper explains that the user must select text and try again.
- If the shortcut is unavailable, the helper remains accessible using the notification-area icon.
- If Ollama is not responding, the user sees the local endpoint and installation/model guidance; the selection is not sent to a cloud service.
- If the model is absent, the user is asked to pull the configured model.
- If output cannot be parsed or fails validation, it is not enabled for Apply.
- If the original window cannot be focused again, the output remains on the clipboard for manual paste; the helper must not report that an automatic paste succeeded.
- The preview is textual rather than a faithful WYSIWYG rendering. The rich HTML block conversion itself must be validated in Notion.

### 3.3 Trust boundary

The user selection and generated answer are treated as untrusted text. Model content is HTML-encoded before insertion into clipboard markup; model-selected block names, color names, and table dimensions are allow-listed/validated. The app does not interpret the model's output as executable code or arbitrary HTML.

## 4. Requirements

### 4.1 Functional requirements

| ID | Requirement | MVP implementation |
|---|---|---|
| FR-1 | Invoke from Notion without switching to a browser | Windows global hotkey and tray icon |
| FR-2 | Capture the selected text | Simulated copy and Unicode clipboard read |
| FR-3 | Correct spelling and grammar without changing structure | Proofread-only prompt and validator |
| FR-4 | Suggest structure only where useful | Separate structure-aware model prompt |
| FR-5 | Support quotes, code, headings, lists, restrained color, and tables | Validated semantic JSON blocks rendered as HTML |
| FR-6 | Require explicit review before replacement | Preview plus Apply button |
| FR-7 | Make output available if rich paste is unsupported | Unicode text clipboard fallback |
| FR-8 | Keep model requests local | Fixed loopback endpoint at `127.0.0.1:11434` |
| FR-9 | Keep a way to exit | Notification-area menu item |

### 4.2 Non-functional requirements

- **Privacy:** user text is sent to the local Ollama listener only; no remote model/provider is configured.
- **Resilience:** local-service, model, response, clipboard, and focus failures must be surfaced.
- **Safety:** no replacement occurs without a visible preview and user action.
- **Performance:** avoid unnecessary inference until the user asks to improve the selection. Latency is model- and hardware-dependent.
- **Resource fit:** default to a model size that can usually run within an 8 GB laptop-GPU budget, while allowing future configuration/tuning.
- **Maintainability:** keep the first release dependency-light; separate UI, model protocol, content model, HTML serialization, and OS interop.
- **Compatibility:** build for Windows x64 and Windows Desktop; re-test after major Notion/Ollama/runtime changes.

## 5. System and hardware assumptions

The initial development machine is a Lenovo Windows 11 laptop with a 13th-generation Intel Core i7-13700H (14 cores/20 threads), 16 GB RAM, an NVIDIA GeForce RTX 4060 Laptop GPU with 8 GB VRAM, and approximately 5.3 GB GPU memory free at inspection time. .NET SDK 10.0.201 and Windows Desktop runtimes 8.0/10.0 were present. Notion desktop, Git, Node, Python, and GitHub CLI were installed; Ollama was not. WSL and Docker were not installed.

Implications:

- Quantized `qwen2.5:7b` was measured at about 4.75 GB VRAM allocation; while it was loaded in this environment, the GPU showed about 0.65 GB free. It performed better on the small synthetic rubric than `qwen2.5:3b`, but may compete with GPU-heavy applications.
- `qwen2.5:3b` was measured at about 2.16 GB VRAM allocation and left about 3.2 GB free on the same machine. It is available in settings as a lower-memory fallback, but scored worse on the synthetic proofreading/structure checks.
- Model quantization, context length, GPU offload, concurrency, laptop cooling/power mode, free VRAM, and prompt size affect actual speed and quality.
- No WSL/Docker dependency is justified for a native WPF app.
- These facts describe one developer machine, not a universal system requirement.

## 6. Architecture

### 6.1 Component diagram

```text
                         ┌────────────────────────┐
                         │ Notion (foreground app)│
                         └────────────┬───────────┘
                                      │ Ctrl+C / Ctrl+V
                                      │ Win32 foreground target
                                      v
┌───────────────┐     ┌───────────────────────────────┐
│ Global hotkey ├────>│ MainWindow (WPF preview/UI)  │
│ Tray menu     │     │ Capture, mode, preview, apply│
└───────────────┘     └───────┬─────────────┬─────────┘
                              │             │
                  local HTTP  │             │ HTML + text
                              v             v
                    ┌──────────────┐  ┌─────────────┐
                    │ OllamaClient │  │ Clipboard   │
                    └──────┬───────┘  └──────┬──────┘
                           │                 │
                           v                 v
                  Ollama at loopback    Original window
                   local model          (Notion/paste target)
```

### 6.2 Source layout

```text
src/NotionHelper/
  App.xaml(.cs)                         Application lifetime
  MainWindow.xaml(.cs)                  Window, tray menu, hotkey, workflow
  Models/ImprovementResult.cs           Validated block contract/plain fallback
  Models/AppSettings.cs                 Local settings model and shortcut presets
  Services/OllamaClient.cs              Local request, prompt, parsing, validation
  Services/AppSettingsStore.cs          LocalAppData settings persistence/validation
  Services/ImprovementResultValidator.cs Allow-listed block/color/table validation
  Services/HtmlClipboardFormatter.cs    Safe HTML and CF_HTML clipboard payload
  Interop/NativeMethods.cs              Win32 hotkey/window/input declarations
  Interop/KeyboardInput.cs              Copy/paste keyboard chord
  Properties/AssemblyInfo.cs            Test-only internals visibility
tests/NotionHelper.Tests/                Clipboard, settings, and local-protocol tests
tools/ModelBenchmark/                    Optional synthetic local-model benchmark CLI
agents/QUICK_START.md                    Compact agent/contributor onboarding map
agents/KNOWN_ISSUES.md                   Evidence-based current issue ledger
.github/workflows/windows-ci.yml          Windows build/test/benchmark-build gate
docs/SOFTWARE_DESIGN.md                 Living architecture and decision record
agents/                                 Product direction and contributor guidance
```

### 6.3 Responsibilities

- **Application lifetime:** starts the window hidden, sets explicit shutdown, and leaves an exit path in the tray menu.
- **MainWindow:** registers `Ctrl+Shift+Space`, remembers the foreground window, performs the UI state transitions, and gates Apply on a valid response and known target.
- **OllamaClient:** posts a non-streaming JSON-mode chat request to the fixed local endpoint using the configured model; gives proofread and structure-aware requests different constraints; parses JSON and rejects unsupported or malformed structures.
- **AppSettingsStore:** loads and validates only the model name and a supported shortcut preset in `%LOCALAPPDATA%\NotionHelper\settings.json`; it never stores source text, prompts, or model output.
- **ModelBenchmark:** runs fixed synthetic cases through the same `OllamaClient`, reports per-case quality checks and elapsed time, and does not persist generated content or change the application default.
- **ImprovementResult/ContentBlock:** represents only the semantic subset the app knows how to preview and paste; supports plain text fallback.
- **HtmlClipboardFormatter:** HTML-encodes all model/user text, renders allow-listed semantic blocks and color values, and constructs Windows CF_HTML byte offsets.
- **NativeMethods/KeyboardInput:** isolate the small set of Win32 operations required for hotkey registration and simulating copy/paste.
- **WPF/WinForms:** WPF provides the floating window; WinForms' `NotifyIcon` provides the Windows notification-area icon without an extra package.

### 6.4 Request/response contract

The chat request has `model`, `stream:false`, `format:"json"`, role/content messages, and `options.temperature:0.2` to reduce sampling variance. The system message directs the model to return:

```json
{
  "blocks": [
    {
      "type": "paragraph",
      "text": "A clear paragraph.",
      "color": "default",
      "bold": false
    }
  ]
}
```

Recognized block types: `paragraph`, `heading`, `bullet`, `numbered`, `quote`, `code`, and `table`. Recognized colors: `default`, `blue`, `purple`, `orange`, and `red`. A table carries `rows`, whose first row is used as the header. Tables are restricted to 2–30 rows and 2–10 equal-width columns. Responses are capped at 120 blocks.

Proofread-only mode accepts only default-color, non-bold paragraph blocks. Structure-aware mode permits the allow-listed semantic blocks. The client rejects an empty answer, invalid enums, invalid table shapes, missing block text, and unsupported formatting; invalid answers cannot be applied.

### 6.5 Clipboard serialization

The formatter places both HTML (`DataFormats.Html`/Windows HTML clipboard format) and Unicode text on the clipboard. CF_HTML offsets are measured in UTF-8 bytes, including the generated header and fragment markers. Model-supplied text is encoded as HTML text, not trusted markup.

The output is intentionally simple HTML and standard tags, not an undocumented Notion block representation. HTML clipboard paste is expected to retain more structure than plain text, but this expectation is not a contract with Notion. The plain-text flavor exists so manual paste remains possible when an editor ignores the HTML format.

## 7. Key design decisions and alternatives

### ADR-1: Native Windows app, not a browser extension (accepted)

**Decision:** implement a Windows WPF helper with a system-wide shortcut and notification-area icon.

**Why:** the user selected the installed Notion desktop app. The global shortcut works regardless of whether the desktop app's webview can host an extension, avoids extension review/permissions, and keeps the first interaction model simple. WPF and Win32 fit the current Windows-only goal and installed .NET SDK.

**Alternatives:**

- **Browser extension:** integrates more naturally with Notion web, can inspect selections through browser APIs, and can offer an in-page popup. It does not directly cover the desktop app, needs extension packaging/permissions, and browser DOM changes may make Notion integration fragile. It may be better if web-only operation becomes the priority.
- **Electron/Tauri desktop shell:** offers cross-platform UI/web technologies and could embed a floating widget. It brings a larger runtime/dependency/toolchain surface; it does not remove clipboard/focus issues or grant access to Notion internals. It may be preferable for a future macOS/Linux release.
- **Notion API integration:** provides supported page/database reads/writes with explicit permissions and can create rich blocks. It cannot simply replace an arbitrary active selection in the desktop editor, requires integration setup and page grants, and adds API/network/schema complexity. Better for deliberate page creation/automation, not the MVP's fast selected-text loop.
- **Clipboard-only script:** is smaller, but lacks a persistent tray widget, preview, error handling, and meaningful mode selection. The desktop app is modest extra code for better usability.

### ADR-2: Clipboard bridge rather than private editor automation (accepted)

**Decision:** use simulated copy/paste and standard HTML/text clipboard formats.

**Why:** this avoids reverse-engineering Notion's Electron internals, DOM, local storage, or undocumented editor protocol. It keeps selection scope user-directed and avoids broad workspace access.

**Trade-offs:** clipboard content is transient shared system state; copying overwrites previous clipboard data; focus can change; rich paste behavior can vary. A browser extension is better for DOM-aware web editing; the official API is better for explicit page/block creation. Neither is a direct substitute for editing the current selection in the native client without a clipboard or private editor hook.

### ADR-3: Local Ollama API over hosted inference (accepted)

**Decision:** call Ollama at the fixed loopback address using a user-installed model.

**Why:** no paid API or API key, text stays on-device during inference, many open-weight models are available, and Ollama offers a stable local HTTP interface with JSON output mode.

**Trade-offs:** the user must install software and download model weights; inference consumes local GPU/RAM and may be slower or lower quality than a hosted large model; model licenses vary; output quality is not guaranteed. A remote service could be faster or stronger but conflicts with privacy and zero recurring cost.

### ADR-4: Small default model first (superseded by ADR-8)

**Historical decision:** begin with `qwen2.5:3b`, not a large model.

**Why:** the available RTX 4060 Laptop GPU has 8 GB VRAM, but the machine has other workloads and only about 5.3 GB free at inspection. A 3B-class quantized model is a more conservative latency and memory starting point for corrections and formatting decisions.

**Trade-offs:** a 3B model can miss nuanced prose, formatting opportunities, language-specific corrections, or structured-output constraints. A 7B model (or another contemporary instruct model) may be better for structure, but should be compared on representative user text and measured on available free memory. Model version names, availability, download size, licensing, and quality must be rechecked at install time.

### ADR-5: JSON semantic blocks, not Markdown-only output (accepted)

**Decision:** request JSON with a constrained list of blocks, validate it, and render to HTML plus plain text.

**Why:** Markdown does not represent all desired output consistently (especially inline colors and semantic block distinctions), and passing model-authored raw HTML would expand the injection surface. A small typed block contract enables allow-lists and controlled serialization.

**Trade-offs:** it constrains richer formatting and asks a small model to follow JSON. More formats or sophisticated inline spans should be added only with explicit schemas and validation.

### ADR-6: Preview before paste (accepted)

**Decision:** do not auto-apply a model response.

**Why:** generated edits can change meaning, omit details, or misformat content. A preview makes the user the decision-maker and reduces accidental destructive writes.

**Trade-off:** one additional click. An auto-apply toggle is not part of the initial scope; any future proposal must revisit safety and explicit opt-in.

### ADR-7: Local model and shortcut settings, fixed inference endpoint (accepted)

**Decision:** persist an Ollama model name and a choice among three shortcut presets in a small settings file under the current user's LocalAppData. Keep the endpoint fixed at `127.0.0.1:11434`, validate all settings, and display load/save/registration errors.

**Why:** users can select a model they have already installed and avoid system-wide shortcut conflicts without requiring a cloud endpoint, secrets, new packages, or a model-download flow. The settings contain no writing or model outputs.

**Alternatives:** environment variables and command-line flags are harder for a desktop user to discover; arbitrary URLs would weaken the explicit local-only contract and need more security/UX design; an unrestricted key-capture editor is more flexible but adds focus, modifier, and conflict edge cases. Presets keep registration and testing bounded.

**Trade-offs:** settings are per-user and not synchronized/backed up. Only the listed shortcut combinations are supported; model availability is detected by Ollama when a request is made. A malformed settings file is surfaced for user correction, and the app continues with the local default until settings are saved.

### ADR-8: Promote the measured 7B model while retaining a low-memory fallback (accepted)

**Decision:** use `qwen2.5:7b` (Q4_K_M) as the new default and retain `qwen2.5:3b` as an explicitly selectable fallback.

**Evidence:** on this Windows machine with Ollama 0.35.1, the fixed three-case synthetic benchmark scored 3/3 for 7B and 1/3 for 3B using the same client, prompts, JSON validation, and temperature 0.2. This includes spelling correction, useful structure for labeled notes, and avoiding unnecessary formatting of normal prose.

**Trade-off:** 7B is approximately 4.75 GB on disk and allocated about 4.75 GB of VRAM. With it loaded, `nvidia-smi` reported 7.29 GB of 8.19 GB used (about 0.65 GB free) on the observed system; with 3B loaded, it reported 4.74 GB used (about 3.2 GB free). The 7B model better fits the writing-quality objective, but users running GPU-heavy applications may need to select 3B or unload the model. A three-case synthetic result is not a general quality guarantee.

## 8. Data flow and privacy

1. The user initiates capture in the foreground app. The helper sends `Ctrl+C`; Notion/browser/app behavior determines what enters the Windows clipboard.
2. The helper reads Unicode text from the clipboard. The target text is also placed into the source preview.
3. On Improve text, the app sends the selection, mode prompt, and locally configured Ollama model name to `http://127.0.0.1:11434/api/chat`.
4. Ollama loads the local model and returns JSON. The app parses and validates it.
5. The user inspects the result and explicitly applies it.
6. The helper writes a rich HTML clipboard payload plus plain-text fallback and simulates `Ctrl+V` into the remembered foreground window.

There is no configurable remote endpoint, telemetry, edit database, logging of user text, or app-side storage of selections. The only persisted app settings are the local model name and shortcut preset under `%LOCALAPPDATA%\NotionHelper`; model output and selection text are not written to that file. Content exists temporarily in the clipboard, the app's process memory, and Ollama's process memory/context. A local Ollama install may have its own operational behavior; this app's request uses a loopback address and bypasses configured HTTP proxies, but users should review their local model/runtime and firewall settings. This repository does not prove that every possible machine configuration is network-isolated.

The helper cannot distinguish Notion from another foreground app in the current MVP. The user can invoke the global hotkey elsewhere; only invoke it where replacing the current selection is intended. A future safety improvement could detect the process/window and require confirmation for other applications.

## 9. Security and safety considerations

- **Loopback endpoint:** host is fixed to `127.0.0.1`, and the local HTTP client bypasses system proxies; do not make it an arbitrary URL without validating schemes, loopback scope, TLS expectations, and disclosure.
- **HTML injection:** encode every model-generated and user-derived string as HTML text; render only known tags, attributes, colors, and block types. Do not paste raw model HTML.
- **JSON validation:** invalid output must disable Apply and explain the issue. Do not silently coerce an unsupported block into a success-shaped result.
- **Selection targeting:** foreground-window handles can go stale or be reused. Revalidate focus when applying, and do not claim success if the target could not be focused.
- **Clipboard side effects:** selection capture overwrites previous clipboard content and can interact with clipboard managers. Exact preservation of arbitrary formats is not implemented.
- **Prompt injection in selected content:** treat the selected content as data, not instructions. The system prompt should explicitly state this; add adversarial tests before claiming robust resistance. A local model can still follow malicious or confusing text.
- **Model output correctness:** a valid JSON object can still be wrong, offensive, misleading, or a meaning-changing edit. Keep preview and user confirmation mandatory.
- **Generated code:** code blocks should only be formatting for user-supplied code/commands; never execute generated or selected code.
- **Hotkey handling:** a global hotkey is observable system-wide while the app is running. Provide an obvious exit and allow choosing only the supported preset combinations.
- **No secrets:** this app should not require API keys. Ignore local configuration and do not commit personal writing/model files.

This section is a design threat analysis, not a formal security audit or a claim that all risks are eliminated.

## 10. Error handling and operational behavior

- Ollama connection failure: show local install/start guidance and explicitly state text was not sent to a hosted service.
- HTTP error/model missing: preserve the source and give the `ollama pull` command for the default model.
- Invalid model JSON/output: do not enable Apply; show actionable retry guidance.
- Clipboard unavailable: show the underlying Windows error where useful, not a silent fallback.
- Empty selection: prompt the user to select text.
- Hotkey registration conflict: keep the tray path active and state that the shortcut is unavailable.
- Foreground restoration failure: retain the rich result on the clipboard and tell the user to paste manually.
- Paste input injection failure: surface an error; do not claim application to Notion.
- App exit: the tray Exit item must release the registered hotkey and dispose the icon/client.

Error paths must not log selection text by default.

## 11. Resource and performance expectations

- Text is submitted only after Improve text is clicked.
- The default 7B model performed better on a three-case synthetic benchmark, but this is not a general quality guarantee. Its VRAM use may compete with GPU-heavy applications; the 3B alternative uses less memory but performed worse in that benchmark.
- GPU use depends on Ollama's model placement and free memory; CPU inference remains possible but slower.
- Long selections can increase context use and latency. The current application does not impose an explicit character limit before sending; add one with a clear user-facing explanation if tests show reliability or memory problems.
- Model downloads are separate from runtime and can be multiple gigabytes; the application does not automatically download weights or trigger unexpected network transfers.
- An inference timeout is set to two minutes. If the client times out, show the failure; do not substitute a cloud fallback.
- Do not run multiple model requests concurrently from repeated clicks; the UI disables its Improve button while a request is in progress.

## 12. Build, distribution, and operations

### Development build

```powershell
dotnet build .\src\NotionHelper\NotionHelper.csproj -c Release
dotnet test .\tests\NotionHelper.Tests\NotionHelper.Tests.csproj -c Release
dotnet run --project .\src\NotionHelper\NotionHelper.csproj
```

Target framework: `net10.0-windows`. Current project dependencies are framework-only (WPF and WinForms). Build and run on Windows; Linux/macOS are not supported.

### Distribution

The initial delivery is source code and a framework-dependent executable. A self-contained single-file release, installer, code signing, update mechanism, startup-on-login option, and winget distribution are future work. Distribution should not bundle model weights by default: that increases release size and may introduce separate licenses.

### Configuration

The model name defaults to `qwen2.5:7b` and can be changed in the collapsed **Local settings** expander (including selecting `qwen2.5:3b` for lower GPU use). The global shortcut can be selected from `Ctrl+Shift+Space`, `Ctrl+Alt+Space`, and `Ctrl+Shift+N`. Both values are persisted in `%LOCALAPPDATA%\NotionHelper\settings.json`; selection text, prompts, and generated content are never settings. Model names are validated, enum values are explicit, and malformed settings are reported in the UI instead of silently accepted. Ollama's HTTP endpoint and two-minute timeout remain fixed; requests use the production client's loopback address, bypass system proxies, and specify temperature `0.2` to reduce sampling variance. The app never discovers remote model hosts or downloads models. A missing configured model reports the exact `ollama pull <model>` command.

## 13. Testing strategy

### Automated tests

The xUnit suite covers HTML encoding, grouping consecutive numbered-list items, UTF-8 CF_HTML offsets with non-ASCII content, tab-separated table fallback, proofread-mode formatting restrictions, rectangular table validation, local settings round-trip and validation, supported shortcut mappings, and Ollama request/response behavior through an in-memory fake HTTP handler. These tests do not require Ollama, a network connection, Notion, or real user writing.

The Windows GitHub Actions workflow builds the WPF app, runs the deterministic test suite, and builds the benchmark CLI on pushes and pull requests. It deliberately does not download model weights or run nondeterministic inference.

Run the optional benchmark with `dotnet run --project .\tools\ModelBenchmark\ModelBenchmark.csproj -c Release -- <model> [<model> ...]`. It uses three hard-coded synthetic cases and the exact app client/prompt/validator path. It reports response time and pass/check status only, never prints or persists model outputs, and must not be used to automatically select a default. Measurements are machine/runtime dependent; quality checks are deliberately small indicators, not proof of semantic correctness.

Remaining high-value automated tests:

1. **UI state transitions:** capture, empty selection, valid result, invalid response, Apply disabled before a response, close without replacement, and explicit Apply only.
2. **Clipboard/native workflow:** use an isolated local editor fixture if it can be run without altering a user's clipboard or page; never mutate a real Notion workspace as an unattended test.
3. **Model quality:** extend synthetic benchmark cases only when they represent a stable measurable requirement; keep real user text out of tests and CI.

### Manual end-to-end matrix

| Scenario | Expected result |
|---|---|
| One English paragraph in Notion | Corrected preview; proofread mode retains paragraph |
| Unicode (accented text, emoji, CJK) | Capture/output does not corrupt text; CF_HTML offsets remain valid |
| Rough meeting notes | Structure mode adds only useful headings/lists |
| A real quotation | Quote block is proposed only when recognizable |
| A source snippet/command | Code block is appropriate; content is never executed |
| Consistent tabular data | A rectangular table is proposed and pasted as a table |
| Ordinary prose | No arbitrary color/decorative blocks |
| Ollama stopped | Actionable local error; source retained; no cloud fallback |
| Model missing/invalid output | Actionable error; Apply remains disabled |
| User closes after preview | Original Notion selection is unchanged |
| Apply to Notion | Preview is pasted into the intended selection; inspect semantic blocks and color |
| Clipboard/image formats before capture | Document current limitation; do not claim arbitrary clipboard restoration |
| Shortcut conflict | Tray icon still opens app and communicates the conflict |

Compilation alone does not verify these integration behaviors. Test against the actual supported Notion desktop build.

## 14. Roadmap and decision gates

### Phase 1 — MVP (in progress)

- Local selected-text proofreading and structure improvement.
- Tray/window, global hotkey, local Ollama call, preview, explicit rich-clipboard apply.
- Comprehensive repository and design documentation.

**Exit gate:** build and focused tests pass; a human validates the full selection-to-apply flow with the actual desktop app; privacy/setup instructions are understandable.

### Phase 2 — Daily reliability

- Validate the shortcut, focus and clipboard flow across Notion updates.
- Preserve clipboard contents to the extent Windows clipboard APIs allow; clearly disclose lossy formats and contention.
- Add a visible diff or side-by-side preview.
- Add model and keyboard-shortcut settings with safe validation. (Implemented: model name and three supported shortcut presets are saved locally.)
- Add automatic tests for response validation and CF_HTML offsets. (Implemented: focused serializer, settings, prompt, and loopback protocol tests.)
- Add Windows CI for build, deterministic tests, and benchmark-tool compilation. (Implemented; live inference is intentionally not part of CI.)
- Add a repeatable synthetic model benchmark using the same production prompts and validated response contract. (Implemented; candidate comparison remains to be run and recorded.)
- Package an installer and evaluate signing/update options.
- Verify target focus, selection, clipboard, and paste behavior safely; actual Notion rich paste remains unverified.

**Decision gate:** choose between native clipboard continuation, browser extension, or official API based on failures reported by users rather than assumptions.

### Phase 3 — Drafting and preferences

- Offer a prompt input when no selection is present.
- Let users save local, explicit writing/formatting preferences.
- Add language selection only if automatic language preservation proves unreliable.
- Keep user settings and writing local; document storage and deletion behavior.

### Phase 4 — Explicit Notion page/database workflows

- Investigate official Notion integration/API for creating pages, databases, chart-ready tables/data, and block structures.
- Require the user to connect an integration, select target pages, and review permission scopes.
- Keep page creation separate from selected-text replacement.
- Confirm current Notion API capabilities/limits before promising charts, styling, or database automation.

**Decision gate:** implement only when a concrete workflow cannot be achieved reliably with rich clipboard paste and the user explicitly opts into workspace access.

## 15. Limitations and validation plan

Known limitations:

1. **Not yet end-to-end tested in Notion.** The HTML clipboard uses interoperable tags, but Notion may normalize, strip, or reinterpret styles and table/list structure.
2. **Plain-text preview.** The preview communicates the text but does not visually render final rich formatting.
3. **Clipboard capture replaces clipboard contents.** The implementation does not restore arbitrary clipboard formats after `Ctrl+C`; rich output also becomes the new clipboard contents.
4. **Focus and selection are timing-sensitive.** Copy and paste use simulated keyboard input; app switching, dialogs, focus restrictions, or the user's actions can disrupt the intended target.
5. **App-agnostic shortcut.** It can capture selected text from any application and attempt to paste back there; it does not currently prove the target is Notion.
6. **Model quality is variable.** A 3B model may follow the JSON schema imperfectly or miss nuanced corrections/formatting. Structural validation is not semantic verification.
7. **No formatting diff, undo, or built-in rollback.** Notion's own undo may recover a paste, but this must be checked manually.
8. **Partially configurable.** The model name and three global-hotkey presets are locally configurable. The Ollama endpoint and timeout remain fixed; there is no model download management or auto-update.
9. **Windows-only.** WPF, WinForms notification icon, and Win32 input/shortcut calls prevent direct macOS/Linux use.
10. **No Notion API, chart renderer, or database builder.** Rich tables may paste; charts and managed databases are later, separate workflows.
11. **No persistence of edits or prompt history.** This protects privacy but also means no built-in history or recovery.
12. **No formal security certification.** Threat assumptions and defenses are documented but need tests/review.
13. **The local model/runtime has its own lifecycle and licensing.** This project does not manage or re-license it.

Validation sequence:

1. Build the application on the intended Windows environment.
2. Install Ollama and pull the default model; verify the exact loopback API behavior.
3. Test copy, focus recovery, and paste with short and long selections in Notion.
4. Test each supported rich block and colors with both default and accessibility themes.
5. Test clipboard-content impacts and document what is preserved/lost.
6. Measure GPU memory, response latency, and output quality on representative, user-approved notes.
7. Compare an alternative larger instruct model only after the baseline works; use the same examples, time/memory measurements, and correction/formatting criteria.
8. Update this design document and README with verified behavior, not assumptions.

## 16. Living decision log

| Date | Decision/change | Reason |
|---|---|---|
| 2026-10-07 | Start with Windows WPF, local Ollama, and clipboard interaction | Matches the installed Notion desktop workflow and avoids paid services and private editor APIs |
| 2026-10-07 | Separate conservative proofreading from optional structure-aware formatting | Avoids unrequested restructuring/color while enabling richer output when it helps |
| 2026-10-07 | Use a validated semantic JSON contract and standard HTML clipboard output | Avoids passing through arbitrary model-authored HTML and supports rich paste plus plain-text fallback |
| 2026-10-07 | Require a preview and explicit Apply | Prevents silent destructive rewrites and keeps the user in control |
| 2026-10-07 | Persist model/shortcut preferences locally while pinning Ollama to loopback | Adds user control without widening inference to remote services or persisting writing |
| 2026-10-07 | Promote qwen2.5:7b Q4_K_M after a 3/3 vs 1/3 synthetic comparison | Improves measured writing/structure quality; keeps qwen2.5:3b selectable due to its lower VRAM use |

Add entries when decisions change; do not erase superseded decisions without preserving their history and rationale.

## 17. Verification record

**2026-10-07**

- Release build of `src/NotionHelper/NotionHelper.csproj`: passed with zero warnings and zero errors.
- Focused tests: 8 passed, covering HTML encoding, ordered-list grouping, Unicode CF_HTML offsets, table fallback, proofread-mode restrictions, table validation, null model output, and the Win32 `INPUT` structure size.
- Ollama 0.35.1 recognizes the downloaded `qwen2.5:3b` model and completes loopback inference with 100% GPU placement; the reported loaded model allocation was about 2.2 GB. Ollama 0.40.0 on this machine failed to open its generated `manifests-v2` model-tag symbolic link with `The path cannot be traversed because it contains an untrusted mount point`. This was observed on one machine and is not a general compatibility conclusion; the README links to Ollama's official installer, and users should validate their installed runtime.
- Historical quality check on `qwen2.5:3b` was mixed. Its initial prompt example also incorrectly showed `rows` on a paragraph; a later automated benchmark exposed and the prompt was fixed. The earlier Qwen3 4B evaluation was not performed under the installed 0.35.1 runtime.
- The manual selection/focus/rich-paste flow has not yet been validated inside the Notion desktop app.

**Next phase implementation verification (2026-10-07)**

- `dotnet build .\src\NotionHelper\NotionHelper.csproj -c Release`: passed with 0 warnings and 0 errors.
- `dotnet test .\tests\NotionHelper.Tests\NotionHelper.Tests.csproj -c Release --logger "console;verbosity=normal"`: passed, 29/29 tests; reported test duration 0.5826 seconds.
- `dotnet build .\tools\ModelBenchmark\ModelBenchmark.csproj -c Release --no-restore`: passed with 0 warnings and 0 errors.
- Added Windows GitHub Actions for the app build, deterministic tests, and benchmark CLI build. Live Ollama inference is not run in CI.
- Ollama 0.35.1 locally listed both `qwen2.5:3b` (Q4_K_M, 1.9 GB on disk) and `qwen2.5:7b` (Q4_K_M, 4.7 GB on disk). The 7B model was fully GPU placed at 4,748,056,984 bytes; while loaded, `nvidia-smi` reported 7,291 MiB used of 8,188 MiB (666 MiB free). The 3B model was fully GPU placed at 2,159,374,499 bytes; while loaded, 4,742 MiB was used (3,215 MiB free).
- With the final prompt, separate table shape, and temperature 0.2, a single synthetic run scored **qwen2.5:7b 3/3** (proofreading, labeled-note structure, no unnecessary prose decoration; mean 2,854 ms including model-load time) and **qwen2.5:3b 1/3** (missed all three checked spelling corrections and did not structure labeled notes; mean 1,606 ms). Neither changed ordinary-prose formatting. This is a tiny synthetic sample, not representative-quality certification; 7B became the default, with 3B retained for lower VRAM use.
- Model output contract failure found by the benchmark was traced to the generic JSON example showing a `rows` property on a paragraph. The prompt now shows distinct paragraph/table examples and explicitly forbids `rows` on non-table blocks. This made both models produce schema-valid responses in the final observed runs.
- WPF startup smoke test: the Release app remained running for three seconds in tray mode, then the exact smoke-test process was stopped. It did not use the clipboard, Ollama, or Notion page content.
- The Ollama model was stopped after benchmarking; the downloaded model files remain available locally.
- No actual Notion page, clipboard, or user writing was used by the automated model benchmark. Notion selection/focus/rich-paste behavior remains unverified.

Update this record after subsequent build, model-runtime, and Notion end-to-end checks; do not turn an unverified behavior into a success claim.
