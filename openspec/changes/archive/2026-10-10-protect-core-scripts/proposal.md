# Proposal

## Why

The shared `DataSource` contract belongs to the framework maintained on `main`, but its current path does not mark it as core code. Development work could also change shared core scripts without an automated check.

## What Changes

- Establish `scripts/core/` as a directory owned by `main`; `develop` inherits its exact tree from `main`.
- Move the `DataSource` script and its Godot UID file from `scripts/data/` to `scripts/core/` without changing the contract.
- Update the shared ownership instruction and data layer document to use the new path.
- Extend the branch workflow so `develop` fails when its `scripts/core/` tree differs from the latest `main`.

## Capabilities

This changes file placement and repository enforcement only. The `data-access` behavior remains as specified, so `skip_specs: true` applies.

## Impact

`AGENTS.md`, `docs/data-layer.md`, the `DataSource` file and UID, and the GitHub Actions workflow. `develop` will receive the move through a merge from `main`.
