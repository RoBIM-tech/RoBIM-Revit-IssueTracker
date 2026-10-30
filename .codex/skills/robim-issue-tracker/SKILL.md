---
name: robim-issue-tracker
description: Develop this RoBIM native Revit feature, package its dependencies, and maintain Manager registration and TEST/stable release metadata.
---

# Native feature

Read README.md and AGENTS.md. Implement the IExternalCommand in src/Command.cs; keep package/robim-module.json IDs stable. Use version-pinned Common packages; include private dependencies in the module ZIP. Do not add a pyRevit command or a second button. Shared implementation belongs in version-pinned RoBIM-Libraries packages, not copied source or sibling ProjectReference.

Read the [button authoring contract](https://github.com/RoBIM-tech/RoBIM-Workspace/blob/main/docs/developer/BUTTON_CONTRACT.md) when changing registration, dependencies or distribution. Register the button in Workspace repositories.json. Default discovery uses robim-addon topic, main descriptor, TEST Release and correct account permissions. Manager catalog registration is optional. Use a codex/ work branch and submit linked PRs when shared repositories change. User permission for this feature does not authorize unrelated deployment or PR merging.

Keep new releases TEST until actual Revit verification is recorded. Promote a new version explicitly using channel=stable and validation.by/hostBuild/evidence. Do not claim host testing from compilation. Use Native Reload for command-only changes; fixed Host changes require restart. Add appropriate tests and preserve user models and settings.
