---
trigger: always_on
description: Consult the CodeGraph symbol index at .codegraph/ for how-code-works questions.
---

## CodeGraph

This project has a CodeGraph symbol index at .codegraph/ (SQLite, auto-refreshed by git hooks).

Rules:
- For symbol-level questions (definitions, callers/callees, call paths, refactor impact), use `codegraph explore "<question or symbols>"` before grep or reading files. It returns line-numbered source plus call paths in one shot.
- Drill down with `codegraph node`, `codegraph callers`, `codegraph callees`, `codegraph impact`.
- If the index is stale after local edits, run `codegraph sync`.
- CodeGraph is for symbol-level questions; use graphify (see rules/graphify.md) for architecture/community-level questions.