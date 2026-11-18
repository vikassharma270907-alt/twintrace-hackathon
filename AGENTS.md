<!-- ANTIGRAVITY-TO-CODEX -->
# Codex project guidance

- This repository contains the reusable Antigravity-to-Codex installer at `scripts/setup-codex-from-antigravity.ps1`.
- Preserve existing Antigravity files unless the user explicitly asks to remove them. `.mcp.json` is retained for Antigravity; Codex MCP servers are configured globally.
- Use the Ralph files under `scripts/ralph/` only when the user requests the Ralph loop or supplies a PRD.
- For implementation work, delegate independent exploration and verification where useful, keep edits non-overlapping, and run checks appropriate to the files changed.
