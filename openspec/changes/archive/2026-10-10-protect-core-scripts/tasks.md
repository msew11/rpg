# Tasks

## 1. Core location and ownership

- [x] 1.1 Move `data_source.gd` and its UID file to `scripts/core/` without content changes; verify old paths are gone and UID is preserved.
- [x] 1.2 Update `AGENTS.md` and `docs/data-layer.md` for main ownership and the new path; verify repository path searches find no stale reference.
- [x] 1.3 Extend the branch workflow to compare `scripts/core/` with latest `main`; verify equal trees pass and a changed core tree fails.

## 2. Verify migration

- [x] 2.1 Run OpenSpec validation and `tools/check.ps1` on `main`; verify the data source contract test passes.
- [x] 2.2 Confirm the current `develop` DataSource and UID contents match `main` before the move, so a merge can carry both without conflict.

## Workflow follow-up

- Archive this change after all checks pass.
- Commit on `main`, merge into `develop`, and run its complete project check before committing the merge.
