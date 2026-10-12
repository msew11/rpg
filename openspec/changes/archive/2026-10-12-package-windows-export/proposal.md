# Proposal

## Why

The Windows .NET export leaves `rpg.exe` and its runtime files in a directory. Sharing only the executable omits the runtime, so `-ExportWindows` should also produce one ready-to-distribute archive.

## What Changes

- After a successful Windows export and launch check, package the complete `build/windows/` output into `build/rpg-windows.zip`.
- Document the archive path and extraction requirement, and rename the Rider export configuration to reflect the ZIP output.

## Capabilities

### Modified Capabilities

- `windows-export-check`: A successful Windows export check also creates a ZIP containing the executable and runtime files.

## Impact

`tools/check.ps1`, Windows export guidance in `README.md`, the shared Rider run configuration, and the Windows export specification.
