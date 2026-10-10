# Proposal

## Why

The branch document ownership rule is currently enforced by review alone. A routine merge can reintroduce development documents on `main` or leave `develop` with stale shared documents.

## What Changes

- Add a GitHub Actions check for pushes and pull requests targeting `main` and `develop`.
- Fail the `main` check if any of the three development documents is present.
- Fail the `develop` check if `AGENTS.md` or `README.md` differs from the latest fetched `main`.
- Verify the check with passing and failing branch states, then merge the workflow from `main` into `develop`.

## Capabilities

This change only adds repository tooling. It does not change game behavior or existing capability requirements; `skip_specs: true` applies.

## Impact

GitHub Actions and the OpenSpec change record. Branch protection settings remain a GitHub repository configuration step.
