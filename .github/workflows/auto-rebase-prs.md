---
emoji: 🔀
name: Auto-Rebase Open PRs
description: When main is updated, update open PR branches with the latest main and resolve merge conflicts.
on:
  push:
    branches: [main]
  workflow_dispatch:
    inputs:
      pr_number:
        description: "Optional: only process this single PR number (for testing)."
        required: false
        type: string
  schedule:
    # Safety net: run once a day on weekdays in case a push event was missed.
    - cron: "0 6 * * 1-5"
permissions:
  contents: read
  pull-requests: read
  issues: read
  actions: read
  copilot-requests: write
strict: true
timeout-minutes: 30
engine:
  id: copilot
  max-turns: 40
network:
  allowed: [defaults, github]
tools:
  github:
    mode: gh-proxy
    toolsets: [default]
  bash:
    - "git *"
    - "dotnet *"
    - "./scripts/*"
checkout:
  fetch: ["*"]
  fetch-depth: 0
safe-outputs:
  push-to-pull-request-branch:
    target: "*"
    if-no-changes: "ignore"
    fallback-as-pull-request: false
  add-comment:
    target: "*"
    max: 20
  add-labels:
    allowed: [auto-rebase-conflict, needs-manual-rebase]
    target: "*"
    max: 5
---

# Auto-Rebase Open PRs onto `main`

You keep open pull requests up to date with the `main` branch of this repository.
The default branch `main` has just been updated (or you were triggered manually or on a
schedule). Your job is to bring open PR branches up to date with the latest `main`,
resolving merge conflicts when they occur, and to clearly report what you did.

## Scope and selection

1. Determine the latest commit SHA of `main`.
2. List the currently **open** pull requests that target `main` using `gh`.
   - If the manual `pr_number` input (`${{ inputs.pr_number }}`) is provided and non-empty,
     process **only** that single PR and ignore all others.
3. **Skip** a PR (do nothing to it) when any of the following is true:
   - It is a **draft** PR.
   - It comes from a **fork** (its head repository is not this repository). Branch pushes to
     forks are not possible from this workflow, so instead post a short comment explaining
     that the branch is behind `main` and must be updated manually by the author — but only
     if it is actually behind `main`.
   - It has the label `no-auto-rebase`.
   - Its head branch is already **up to date** with `main` (i.e. `main` is fully contained in
     the PR branch — there is nothing to rebase).
4. Process **at most 10** PRs in a single run to keep runs bounded. If more than 10 qualify,
   process the 10 least-recently-updated ones and note in your summary that others remain.

## Updating each qualifying PR

For each PR that is behind `main` and eligible for update:

1. Check out the PR's head branch locally from `origin` and make sure you have the latest
   `main` fetched.
2. Merge the latest `main` into the PR branch (a merge commit from `main` is preferred over a
   rebase, because it avoids force-pushing and rewriting the PR's history, which would disrupt
   reviewers).
3. **If the merge is clean**, keep the resulting changes staged so they can be pushed to the
   PR branch, then post a short comment on the PR noting that its branch was updated with the
   latest `main` and no conflicts were found.
4. **If there are merge conflicts**, attempt to resolve them:
   - Prioritize correctness and preserve the intent of **both** sides of the conflict. Do not
     blindly discard either side.
   - After resolving, validate the result by building and running the tests where practical,
     for example `dotnet build` and `dotnet test` (or `RUN_TESTS=true ./scripts/install.sh`).
   - If the build/tests pass and you are confident the resolution is correct, keep the merged
     result staged for pushing, then post a comment summarizing exactly which files had
     conflicts and how you resolved them.
5. **If conflicts are too complex or ambiguous to resolve safely**, or validation fails and you
   cannot fix it with confidence:
   - Do **not** push a risky or guessed resolution.
   - Abandon the merge for that PR (leave the PR branch unchanged).
   - Add the `needs-manual-rebase` label (and `auto-rebase-conflict` if the failure was a
     conflict) to the PR.
   - Post a comment clearly explaining which files/hunks conflicted and what a human needs to
     decide, so the author can finish the rebase manually.

## Pushing changes

- Use the `push-to-pull-request-branch` safe output to push your updated branch back to the
  correct PR. Only push branches you actually modified and validated.
- Never force-push or rewrite existing PR history — only add the merge of `main`.
- Never push to a PR that originates from a fork.

## Reporting

- Post exactly one summary comment per PR you touched (updated, or skipped-with-explanation for
  fork PRs that are behind, or flagged for manual rebase).
- Keep comments concise: what state the PR was in, what you did, and any follow-up needed.
- If there was nothing to do for any PR, do nothing and produce no output.
