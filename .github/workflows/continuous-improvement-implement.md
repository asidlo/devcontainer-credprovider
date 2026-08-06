---
emoji: 🛠️
name: Continuous Improvement — Implement
description: Implements a single continuous-improvement tracking issue by running the repository's anvil agent to produce one focused draft pull request. Dispatched per-issue by the Continuous Improvement — Dispatch workflow.
on:
  workflow_dispatch:
    inputs:
      issue_number:
        description: "The continuous-improvement issue number to implement (e.g. 62)."
        required: true
        type: string
permissions:
  contents: read
  issues: read
  pull-requests: read
  actions: read
  copilot-requests: write
strict: true
timeout-minutes: 60
engine:
  id: copilot
  agent: anvil
  # Uses the Copilot engine's default model. Do not set a Claude-Code model alias such as
  # `opusplan` here — it is not a valid model for the copilot engine and fails at runtime with
  # "model 'opusplan' is retired or unsupported".
  max-turns: 80
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
  # Full history so the agent can inspect churn/hotspots and build a correct fix.
  fetch-depth: 0
safe-outputs:
  create-pull-request:
    title-prefix: "[continuous-improvement] "
    labels: [continuous-improvement, automated]
    draft: true
    if-no-changes: "ignore"
    max: 1
  add-comment:
    target: "*"
    max: 1
---

# Continuous Improvement — Implement

You are running as the repository's **anvil** agent to implement **one** continuous-improvement
tracking issue and turn it into a single, focused **draft pull request**. This workflow was
dispatched for exactly one issue; do not look for other work.

## The task

The issue number to implement is **`#${{ inputs.issue_number }}`**.

1. Fetch that issue first with `gh issue view ${{ inputs.issue_number }} --json number,title,body,labels,state`.
2. Treat the issue **title and body as your specification** — in particular the
   `Theme / Where / Why it matters / Proposed change` fields written by the Research workflow.
   That proposed change is your acceptance criteria.
3. If the issue is already **closed**, or already has a **linked pull request**, do not implement
   it: post a short comment saying it looks already handled and stop without opening a PR.

## Environment: you are anvil in a degraded (gh-aw) environment

You are the anvil agent, but you are running **inside a GitHub Actions gh-aw job**, not the Copilot
CLI. Adapt accordingly — this is the "Cloud / degraded" column of your Environment-adaptation table:

- **Verification ledger:** there is no SQL session store here. Use the **file-ledger fallback** —
  keep `/tmp/gh-aw/agent/anvil-ledger-${{ inputs.issue_number }}.json` as a JSON array, appending one
  object per verification check (the same fields you would put in `anvil_checks`). Everything under
  `/tmp/gh-aw/agent/` is uploaded as a run artifact, so the ledger is preserved for debugging. If the
  append didn't happen, the verification didn't happen. **Because the ledger is published as an
  artifact, redact any tokens or secrets from build/test output before you append it** — store only
  short pass/fail summaries and sanitized snippets, never raw credential material.
- **Adversarial review:** multi-model `code-review` subagents are **not** available here. Do not try
  to launch them. Substitute rigorous self-review plus the verification cascade below, and clearly
  state in the PR description that automated multi-model review did not run.
- **Diagnostics:** there is no IDE diagnostics tool. Substitute the compiler/test runner (below) on
  the files you changed.
- **Recall / memory / `store_memory`:** not available. Skip silently.

## How to build the change

1. **Baseline first.** Before editing, capture current state: run `dotnet build` and `dotnet test`
   (or the fuller `RUN_TESTS=true ./scripts/install.sh` when the change touches install/packaging).
   Record the results in your file ledger with `phase: "baseline"`. If the baseline is already
   broken, note it — you are not responsible for pre-existing failures, only for not making them
   worse.
2. **Implement the proposed change** in the working tree. Keep the diff **minimal and surgical** —
   change only what the issue requires, follow the existing code style, and prefer extending
   existing abstractions over adding new ones. Add or update tests alongside the change when test
   infrastructure exists.
3. **🔴 Credential-handling rule:** the authentication chain in
   `src/CredentialProvider.Devcontainer/Program.cs` is high-risk. **Never** log, print, persist, or
   commit tokens. Prefer returning `NotApplicable` over emitting bad credentials. Treat any change
   here as 🔴 and be conservative.

## Verify (the forge)

Run the applicable checks and append each to the file ledger with `phase: "after"`:

- **Build:** `dotnet build` — must succeed.
- **Test:** `dotnet test` (or a targeted `dotnet test --filter ...` for the area you changed);
  use `RUN_TESTS=true ./scripts/install.sh` when the change affects install/packaging.
- Fix any failure you introduced and re-run (max 2 attempts). If you **cannot** get to a green,
  regression-free state after 2 attempts, **do not open a broken PR**: revert your working-tree
  changes (`git checkout -- <files>`) and instead post an explanatory comment on the issue (see
  below). Never present code that introduces a new build or test failure.

## Produce output

- **On success** (change implemented and verified green): use the **`create-pull-request`** safe
  output to open a single **draft** PR with your working-tree changes. The PR description **must**:
  - include the line `Fixes #${{ inputs.issue_number }}` so the PR is linked to the issue and closes
    it on merge;
  - summarize what changed and why, referencing the issue's proposed change;
  - include a short **Evidence** section: the build/test commands you ran and their results
    (pass/fail), pulled from your file ledger;
  - note that this PR was produced automatically by the anvil agent and that **multi-model
    adversarial review did not run**, so a human reviewer should read the diff carefully.
- **On graceful failure** (the change is too large, risky, or ambiguous to implement safely, or you
  could not reach a green build/test state): do **not** open a PR. Instead use the **`add-comment`**
  safe output to comment on issue `#${{ inputs.issue_number }}` explaining exactly what you tried,
  what blocked you, and what a human needs to decide or provide. This is a valid, honest outcome —
  the Dispatch workflow will retry later, and after enough attempts will mark the issue as needing
  manual attention.

## Guardrails

- **Do not** `git commit`, `git push`, force-push, or call `report_progress`/`git push` yourself.
  Leave your changes in the **working tree** — the `create-pull-request` safe output is what collects
  the diff and opens the PR. Committing or pushing yourself will break that flow.
- Open **at most one** pull request, for this one issue only. Do not touch other issues or PRs.
- **Never** log, print, store, or commit secrets or tokens.
- Do not modify unrelated code, reformat files wholesale, or delete tests to make a change look
  clean. Stay within the scope of issue `#${{ inputs.issue_number }}`.
