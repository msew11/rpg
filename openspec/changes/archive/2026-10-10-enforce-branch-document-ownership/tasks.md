# Tasks

## 1. Enforce branch ownership

- [x] 1.1 Add a GitHub Actions workflow for pushes and pull requests to `main` and `develop`; verify it selects the target branch and reports a stable job name.
- [x] 1.2 Check that all three development documents are absent from the `main` tree; verify a clean tree passes and a tree containing one of the paths fails.
- [x] 1.3 Compare `develop` shared documents with freshly fetched `main`; verify matching files pass and a changed file fails.

## 2. Verify branch states

- [x] 2.1 Run OpenSpec validation and `tools/check.ps1` on `main`, and confirm its document inventory.
- [x] 2.2 Confirm the current `develop` retains its three development documents and has shared documents equal to `main`.

## Workflow follow-up

- Archive this change after all checks pass, then include the archive in the final branch commits.
- Commit on `main`, merge into `develop`, and run both checks there before committing the merge.
- Have the repository owner require the workflow job in GitHub branch protection for both target branches.
