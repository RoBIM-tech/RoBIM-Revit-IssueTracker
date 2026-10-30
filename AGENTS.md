# RoBIM independent feature

- Read README.md and `.codex/skills/robim-issue-tracker/SKILL.md`.
- One button/command/package per repository. Create a separate repository for another button. Do not use long-lived button branches.
- New functionality is a native Revit Add-in, not a new pyRevit command.
- Keep module/command IDs stable. Use robim-addon topic, main descriptor and TEST Release for discovery; central catalog registration is optional.
- Start in TEST. Stable needs real host verification, reviewer, host build and HTTPS evidence in robim-release.json.
- Run build.ps1 with CommonFeed; ManagerPath is optional for central-catalog compatibility. Never commit generated DLLs or Autodesk binaries.
- command-only modules must not register long-lived events or startup hooks implicitly. Keep document changes in command Transactions.
