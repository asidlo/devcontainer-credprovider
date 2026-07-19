# Auto-Rebase Open PRs (GitHub Agentic Workflow)

`auto-rebase-prs.md` is a [GitHub Agentic Workflow](https://github.com/github/gh-aw)
(`gh aw`) that keeps open pull requests up to date with `main`. When `main` is updated it
merges the latest `main` into each eligible open PR branch, resolves merge conflicts, runs the
build/tests to validate, and comments on each PR describing what it did.

## How it works

- **Triggers:** `push` to `main`, plus `workflow_dispatch` (manual, with an optional
  `pr_number` input for testing) and a weekday `schedule` as a safety net.
- **Permissions:** the agent job runs read-only. All writes go through sanitized
  `safe-outputs` (`push-to-pull-request-branch`, `add-comment`, `add-labels`).
- **Selection / guardrails:** skips draft PRs, fork PRs (comments instead of pushing),
  PRs labelled `no-auto-rebase`, and PRs already up to date. Processes at most 10 PRs per run
  and is capped by `max-turns`.
- **Conflict handling:** the agent resolves conflicts and validates with `dotnet build` /
  `dotnet test`; if a conflict is too ambiguous to resolve safely it leaves the branch
  untouched, adds `needs-manual-rebase` / `auto-rebase-conflict` labels, and comments.

## Files

- `auto-rebase-prs.md` — the human-authored workflow (edit this).
- `auto-rebase-prs.lock.yml` — the compiled GitHub Actions workflow (generated; do not edit).

## Setup

1. Install the extension: `gh extension install github/gh-aw` (or the
   [install script](https://github.com/github/gh-aw#quick-start)).
2. Configure the AI engine. This workflow uses `engine: copilot`, which needs
   `copilot-requests: write` (already set) with centralized Copilot billing, **or** a
   `COPILOT_GITHUB_TOKEN` secret. To use a different engine (Claude/Codex/Gemini), change the
   `engine:` block and add the corresponding API-key secret.
3. Recompile after any frontmatter change: `gh aw compile auto-rebase-prs`.

## Testing iteratively

Run manually against a single PR before relying on the `push: main` trigger:

```bash
gh workflow run "Auto-Rebase Open PRs" -f pr_number=<PR_NUMBER>
```

Review the agent's pushed commits and comments, tune the instructions in
`auto-rebase-prs.md`, recompile, and repeat.

## Notes

- The workflow **merges** `main` into PR branches rather than rebasing, to avoid force-pushing
  and rewriting PR history.
- `push-to-pull-request-branch` uses `target: "*"` so it can update any open PR; this is
  intentional for this workflow. The safety constraints (drafts, forks, labels, PR cap) are
  enforced in the workflow instructions.
