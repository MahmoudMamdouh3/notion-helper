# Known issues and validation ledger

This concise living ledger complements the detailed limitations in [`../docs/SOFTWARE_DESIGN.md`](../docs/SOFTWARE_DESIGN.md). Change status only when evidence supports it.

| ID | Status | Area | Issue and evidence | Next action |
|---|---|---|---|---|
| UI-01 | OPEN | Notion integration | Clipboard formats and foreground-window flow have not been verified against a real Notion page. | Keep integration claims explicitly unverified; consider a non-destructive isolated-editor harness before real-page automation. |
| CORE-01 | OPEN | Selection safety | The app captures from the foreground app and relies on a remembered native window handle; the target can change before Apply. | Consider verifying process/window identity and selection continuity without private Notion APIs. |
| CORE-02 | OPEN | Clipboard | Capturing selection replaces previous clipboard data, and Apply replaces it with formatted output. Arbitrary clipboard formats are not preserved. | Design a preservation policy and automate supported-format coverage before implementing restoration. |
| PERF-01 | OPEN | Model quality/latency | One synthetic run scored qwen2.5:7b 3/3 vs qwen2.5:3b 1/3, but the rubric has only three cases and 7B left about 0.65 GB GPU memory free on the observed machine. | Treat 7B as a better measured candidate, not a universal winner; add stable synthetic cases and observe normal GPU-memory contention before changing the default again. |
| UI-02 | OPEN | Preview | The preview is plain text and does not show headings, colors, or tables as they may paste. | Design a safe semantic preview and add tests before changing apply behavior. |
| CORE-03 | OPEN | Model semantics | Schema checks cannot determine whether a valid suggestion changes meaning or misses an error. | Keep mandatory preview and add synthetic quality checks without treating them as proof of correctness. |

No known issue authorizes bypassing preview, local-only inference, or model-output validation.
