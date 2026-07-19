# Custom agents

This directory holds [GitHub Copilot custom agents](https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/create-custom-agents)
for this repository. Each `*.agent.md` file is one agent (Markdown + YAML frontmatter). Once merged to
the default branch, these agents appear in the agent picker when you start a session — including from
the **GitHub mobile app**.

## Agents

| Agent | Invoke as | What it does |
|-------|-----------|--------------|
| [`anvil`](./anvil.agent.md) | `anvil` | Evidence-first coding agent: verifies before presenting, attacks its own output with adversarial multi-model review, records every check in a SQL-tracked verification ledger, and never shows broken code. |

## Attribution

`anvil` is **vendored from [burkeholland/anvil](https://github.com/burkeholland/anvil)** (MIT License,
© Burke Holland) — originally a GitHub Copilot **CLI** plugin.

### Adaptations for this repo

The upstream agent targets the Copilot CLI, which offers interactive prompts, IDE diagnostics, an
internal `session_store` SQLite, and local `git push`. This vendored copy is adapted so the **same
evidence-first behavior also runs in Copilot cloud (coding) agent sessions**, via an
[Environment adaptation](./anvil.agent.md#environment-adaptation) table that maps each CLI capability
onto its cloud equivalent:

- **Verification ledger / baseline / evidence bundle** — uses the session's writable **SQLite** when a
  SQL tool is available (as in cloud agent sessions and the CLI). When no SQL tool exists, it falls
  back to a **file-based ledger** under `/tmp/anvil/{task_id}.json` with the same fields. The
  anti-hallucination rule is unchanged: a check only counts if it was written to the ledger first. The
  ledger is never committed to the repo.
- **Interactive pushback / `ask_user`** — in a non-interactive cloud session it does not block: it
  surfaces the concern, picks the safest reversible default, records the assumption, and stops only for
  irreversible (🔴) or genuinely ambiguous decisions.
- **IDE diagnostics** — substitutes the compiler / type-checker / linter when no diagnostics tool is
  present.
- **Recall / history** — uses the read-only cross-session store (DuckDB, `ILIKE` instead of FTS) when
  available, and skips silently otherwise.
- **Commit / publish** — uses `report_progress` to commit and push to the PR branch instead of raw
  `git push`.
- **Adversarial reviewers** — uses `code-review` subagents with models available in the current
  environment.

It also carries a few repo-specific notes (🔴 classification for credential handling, the
`dotnet build` / `dotnet test` / `RUN_TESTS=true ./scripts/install.sh` commands, and the "never log or
persist tokens" rules) drawn from [`.github/copilot-instructions.md`](../copilot-instructions.md).

To refresh from upstream, re-vendor `agents/anvil.agent.md` from
[burkeholland/anvil](https://github.com/burkeholland/anvil) and re-apply the adaptations above.
