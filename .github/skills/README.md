# Agent skills

This directory holds [GitHub Copilot agent skills](https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/add-skills)
for this repository. Each subdirectory is one skill defined by a `SKILL.md` file.
Skills placed here work with the Copilot cloud (coding) agent, Copilot code
review, the Copilot CLI, and the GitHub app — including when you start an agent
session from the **GitHub mobile app**.

## Skills

| Skill | Invoke with | What it does |
|-------|-------------|--------------|
| [`grill-with-docs`](./grill-with-docs) | `/grill-with-docs` | A relentless, one-question-at-a-time interview to sharpen a plan or design, writing the vocabulary and hard decisions down as a `CONTEXT.md` glossary and ADRs as it goes. |
| [`grilling`](./grilling) | `/grilling` | The same stress-test interview without producing docs. Dependency of `grill-with-docs`. |
| [`domain-modeling`](./domain-modeling) | `/domain-modeling` | Build and sharpen the project's domain model (glossary + ADRs). Dependency of `grill-with-docs`. |

`grill-with-docs` is explicit-invocation only (it will not trigger on its own);
type `/grill-with-docs` in your agent session to start it. It composes the
`grilling` and `domain-modeling` skills, so all three are vendored together.

## Attribution

`grill-with-docs`, `grilling`, and `domain-modeling` are vendored from
[mattpocock/skills](https://github.com/mattpocock/skills) (MIT License,
Copyright (c) Matt Pocock). Upstream can be installed or updated with:

```bash
npx skills add mattpocock/skills --skill=grill-with-docs
npx skills update grill-with-docs
```
