## CodeGraph

This project has a symbol-level CodeGraph index at .codegraph/ (SQLite, built by `codegraph init`). It refreshes automatically via git hooks (`.git/hooks/post-{commit,checkout,merge}`), so live file watching is not needed.

Rules:
- Use `codegraph explore "<question or symbols>"` FIRST for questions about how code works: what calls what, where a symbol is defined, error traces, or refactor impact. It returns verbatim source with line numbers plus call paths — faster and more precise than grep for symbol questions. `codegraph node "<symbol>"`, `codegraph callers`, `codegraph callees`, and `codegraph impact` are the drill-down tools.
- If the index is stale (e.g. edited files with no commit yet), run `codegraph sync` to refresh it.
- CodeGraph answers symbol-level questions; graphify answers architecture/community-level questions — use the section below for those.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
