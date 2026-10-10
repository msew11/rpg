# Design

## Context

`DataSource` is a global GDScript class under `scripts/data/`; its consumers use the class name, and `docs/data-layer.md` names its path. The Godot UID file travels with the script. The branch workflow introduced by `enforce-branch-document-ownership` already compares shared documents.

## Goals / Non-Goals

**Goals:** Keep the core directory identical between branches while preserving `DataSource` behavior and UID.

**Non-Goals:** Move feature-specific scripts or change the data access API.

## Decisions

- Move both `data_source.gd` and `data_source.gd.uid` with their contents unchanged. This preserves the Godot script identity and global class use.
- Add `scripts/core/` to the `develop` comparison against freshly fetched `origin/main`. Git tree comparison catches modifications, additions, and deletions, including UID files.
- State ownership in `AGENTS.md` on `main`, then merge that file into `develop`; do not edit it directly on `develop`.

## Risks / Trade-offs

- [A direct resource path still points to the old script] → Search the repository for path references and run the Godot project check after the move on both branches.
- [A concurrent main core update makes develop stale] → The workflow compares against the latest main available at fetch time; merge main and rerun checks.

## Migration Plan

Commit the move and workflow extension on `main`, then merge into `develop`. If rollback is needed, revert the main commit and merge that revert into `develop`.
