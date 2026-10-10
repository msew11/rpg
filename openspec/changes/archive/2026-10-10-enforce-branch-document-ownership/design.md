# Design

## Context

`main` already excludes the three development documents. `develop` retains them and currently has byte-identical `AGENTS.md` and `README.md`. No GitHub Actions workflow exists yet. Both branches must receive the same check without editing their owned documents.

## Goals / Non-Goals

**Goals:** Reject commits and proposed merges that violate branch document ownership, with clear failure messages.

**Non-Goals:** Configure GitHub branch protection or validate game behavior in this workflow.

## Decisions

- Put one workflow on `main`, then merge it into `develop`. Run on pushes to and pull requests targeting either branch. The target branch selects the rule; the pull request merge tree is the checked content.
- On `main`, inspect the checked-out tree for the three forbidden paths. On `develop`, fetch `main` during the job and compare the checked-out `AGENTS.md` and `README.md` against `origin/main`. Fetching during the job gives the latest available main commit, including changes after the workflow commit.
- Use `actions/checkout` and standard Git commands on Ubuntu. A single fixed job name can be selected as a required status check for both branch rules.

## Risks / Trade-offs

- [A new main commit lands after the develop job fetches] → A later push or pull request run rechecks against the newer main; branch protection may need a fresh run before merging.
- [A workflow exists on a branch but branch protection does not require it] → The repository owner must add the job as a required status check on GitHub.

## Migration Plan

Commit the workflow and archived change on `main`, then merge `main` into `develop` while retaining its three documents. Run local checks before any push. Revert the workflow commit on both branches if it must be rolled back.
