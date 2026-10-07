# Repository guidance for coding agents

Read [`agents/PRODUCT_DIRECTION.md`](agents/PRODUCT_DIRECTION.md) and [`docs/SOFTWARE_DESIGN.md`](docs/SOFTWARE_DESIGN.md) before changing product behavior.

## Mandatory product constraints

- Keep the default experience local-first: inference goes only to the configured loopback Ollama service. Do not add hosted inference, telemetry, analytics, remote fallbacks, or API-key requirements without explicit product approval.
- Never apply model output without a user-visible preview and an explicit Apply action.
- Keep proofreading and optional structure-aware formatting separate. Do not decorate ordinary prose; headings, color, quotes, code blocks, lists, and tables must have a clear semantic reason.
- Treat user text and all model output as untrusted. Never execute generated content or trust model-authored HTML. HTML-encode text, validate block types/colors/table shapes, and keep Apply disabled for invalid results.
- Do not add Notion API access, browser injection, workspace crawling, or private Notion editor integration without an explicit user-approved design change and permission/privacy review.
- Make errors visible; do not silently fall back to cloud inference, pretend paste succeeded, or return an empty/success-shaped response after failure.
- Do not log personal writing or include text, tokens, or local model files in commits.

## Engineering workflow

- Preserve the existing .NET/WPF/WinForms/Win32 architecture unless a documented decision changes.
- Keep changes focused and avoid unnecessary packages. The app targets `net10.0-windows` and uses framework components.
- Build with `dotnet build .\src\NotionHelper\NotionHelper.csproj -c Release` on Windows.
- Add focused tests for output contracts, serialization, privacy-sensitive behavior, and bugs introduced by code changes.
- Update `docs/SOFTWARE_DESIGN.md` after material architecture, behavior, privacy, capability, or limitation changes. Update the README and `agents/` notes where the product or contributor workflow changes.
- Do not claim Notion rich-paste support is verified unless the manual end-to-end Notion validation was actually performed and recorded.
- Do not modify the AI behavior solely to produce more formatting. Preserve meaning and prefer the least surprising edit.
- Keep native input, clipboard, and foreground-window operations narrowly scoped and explain user-visible errors.
