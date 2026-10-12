# Design

## Context

The current Windows preset embeds game resources in `rpg.exe`; Godot .NET also writes a sibling `data_rpg_windows_x86_64` runtime directory. Godot .NET export requires the repository's `rpg.sln`, even though `tools/check.ps1` builds `rpg.csproj` directly. The script exports into `build/windows/` and starts the executable. The `build/` directory is ignored by Git.

## Goals / Non-Goals

**Goals:** Produce a ZIP whose root contains the executable and its sibling runtime directory, only after a successful launch check.

**Non-Goals:** Changing the Godot export preset or making the .NET runtime truly single-file.

## Decisions

- Keep the existing export directory and use .NET's `ZipFile.CreateFromDirectory` to preserve the complete exported tree, including hidden files. Put the ZIP at `build/rpg-windows.zip` so it cannot include itself. Normalize ZIP entry separators when validating the contents because Windows PowerShell 5.1 writes backslashes.
- Exclude `build/**` in the Godot export preset so a later export cannot embed earlier binaries or ZIPs as game resources.
- Create a unique temporary ZIP and replace the prior ZIP only after creation succeeds. An export or launch failure leaves the prior successful archive intact.
- Rename the shared Rider configuration to `Export Windows ZIP` and update README with the archive path and extraction instructions.

## Risks / Trade-offs

- Existing unrelated files in `build/windows/` would enter the ZIP → The export directory is dedicated to this preset; packaging is verified against the actual export contents. A future need for multiple presets should use separate output directories.
