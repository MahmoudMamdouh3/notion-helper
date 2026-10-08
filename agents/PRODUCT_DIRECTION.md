# Product direction and working notes

## Vision

Make the user's existing Notion workspace a more effective place to write by adding a fast, local, human-controlled writing assistant. The intended end state is more than spell-check: it can clarify rough prose, suggest structure, and help author attractive, useful Notion content such as quotes, code blocks, callouts, tables, databases, and eventually charts. Build toward that vision in deliberate, testable steps.

## User and problem

The initial user writes quickly in Notion and makes spelling, grammar, and organization mistakes. They may know what they want to say but not how to turn it into a polished Notion page. They prefer zero recurring cost and local processing, and have a Windows 11 laptop with an RTX 4060 Laptop GPU, i7-13700H, and 16 GB RAM.

## Product promise

- Invoke the helper while working in the Notion desktop app.
- Keep text on the user's computer for inference.
- Correct spelling and grammar quickly.
- Offer structural formatting only if content benefits.
- Let the user inspect and control every replacement.
- Explain setup, model requirements, privacy, and limitations plainly.

## Interaction principles

1. **Shortcut first, widget second.** A click can steal focus from a text selection; a global shortcut is the reliable primary interaction. The tray/floating window remains available for inspection and fallback.
2. **Proofread conservatively.** Preserve facts, intent, language, tone, and paragraph boundaries.
3. **Format semantically.** Use headings for section boundaries, lists for actual items/steps, quotes for quoted words, code for actual code/commands, tables for aligned data, and color for a meaningful callout. Use no decoration when structure is already clear.
4. **Preview before paste.** Never change the active selection automatically. Closing is cancellation.
5. **Never silently switch privacy modes.** Missing local dependencies must produce actionable errors rather than sending content to a hosted service.
6. **Make capability boundaries clear.** Clipboard paste is not a Notion API; charts/databases require a separate, permissioned design.

## Current implementation baseline

- Platform: Windows desktop application using .NET 10 WPF, WinForms `NotifyIcon`, and Win32 hotkey/input/foreground-window APIs.
- Entry: one tray instance; a repeated app launch signals the primary process to show its window. `Ctrl+Shift+Space` with a selection and notification-area menu remain the normal entry points.
- Model runtime: Ollama HTTP API at `http://127.0.0.1:11434`.
- Default model: `qwen2.5:7b` Q4_K_M, promoted after a 3/3 synthetic benchmark result versus 1/3 for `qwen2.5:3b`; the 3B model remains available as a lower-VRAM fallback. Model name and shortcut preset are stored in local settings.
- Modes: proofreading-only and optional structure-aware formatting.
- Output: validated JSON semantic blocks, semantic rich-format preview plus side-by-side textual changes, HTML clipboard flavor plus Unicode plain-text fallback.
- Target: capture records the foreground window handle and process ID; identity/focus are checked around simulated copy, and clipboard sequence is checked around capture read. Apply rechecks identity/focus before simulated paste. It does not verify Notion identity or detect selection/caret changes within the same window, and OS clipboard/input operations cannot be made atomic.
- State: local settings only; no user-text history database, telemetry, or hosted services.
- Automated engineering checks: offline protocol/settings tests, a Windows GitHub Actions build/test gate, and a separate opt-in synthetic local-model benchmark.

## Open product questions (do not make irreversible choices silently)

- Should the default mode remain proofreading-only, or should the app remember the last selected mode?
- How much of the previous clipboard can/should be restored, and how should lossy formats be disclosed?
- How should the preview communicate formatting-only changes, block moves, and structural changes beyond the current text comparison?
- Should the helper refuse operation outside Notion, or remain a general selected-text editor?
- Does the 7B model's improvement on synthetic checks generalize to representative writing, and is its measured GPU memory use acceptable during the user's normal workload?
- Which Notion API workflows are actually needed, and what minimum integration/page permissions would they require?
- Is Windows the intended long-term platform, or should the UI eventually move to a cross-platform shell?

## Near-term priorities

1. Complete manual validation of tray exit, shortcut registration/conflict, and repeated-launch activation on Windows.
2. Broaden local model-quality evaluation beyond the three fixed synthetic benchmark cases; keep candidate/default changes evidence-based.
3. Verify clipboard selection, restoration focus, and paste in the real Notion desktop client.
4. Test CF_HTML offsets and HTML escaping with automated tests, including non-ASCII content.
5. Improve disclosure and failure handling for clipboard operations; the semantic preview, text comparison, and stale-window guard are implemented, but selection continuity remains unknown.
6. Compare the baseline and a compatible larger local model with the synthetic benchmark; do not change the default without measured latency and quality evidence.
7. Consider API-based page/database workflows only after the selected-text MVP is reliable and the user opts in.

## Current known limitations

- No verified end-to-end Notion paste test yet; CSS colors and semantic block conversion may be normalized or discarded.
- Clipboard capture replaces whatever had been copied; arbitrary previous formats are not preserved.
- The helper uses simulated copy/paste. Window/process, foreground, and clipboard-sequence checks reduce stale-target and clipboard-read races, but cannot verify the focused editor selection and may still target the wrong application.
- The shortcut is global and configurable only among the supported presets; the app can currently operate on any foreground application.
- The formatted preview is not WYSIWYG. The text comparison is word/punctuation-token based, does not align moved blocks or show formatting-only changes, and uses a full replacement view for large edits.
- The default 7B model may leave limited GPU memory for other workloads; the configurable 3B model is a lower-memory fallback. Model name is configurable, while the Ollama endpoint and timeout remain fixed; the app does not install Ollama or download weights automatically.
- Model output may be valid but incorrect; schema validation cannot verify semantic correctness.
- The synthetic model benchmark is a proxy, not a substitute for user-approved writing examples or real Notion behavior.
- No Notion API, database builder, chart rendering, browser extension, undo/history, or non-Windows build.

## Maintenance rule

After each meaningful implementation batch, update the software design document with actual behavior, the rationale for changed decisions, verification performed, and any newly discovered limitation. This is a maintained design record, not a one-time proposal.
