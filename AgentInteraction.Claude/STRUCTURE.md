# AgentInteraction.Claude

Parent: [project index](../docs/project-structure.md)

A sample consumer workspace for **Claude Code**. It is opened independently of the Persona.NET sources and shows how an agent should use the generated documentation. It is not part of the generator.

| File | Role |
|---|---|
| `CLAUDE.md` | Main instruction: sources (catalog and MCP), question → source table, catalog layout, checklist before `find` / `usedBy`, tool parameters with defaults, `ragSearch` being optional (`ragSearchEnabled`), condensed rules for reading answers and errors |
| `mcp-contract.md` | Details on demand: response shapes (numeric `Kind` in `References`, `textSearch` row order, `ChunkIndex` / `Warnings` in `ragSearch`), errors, empty results, operators and `op_*`, "who creates instances", index boundaries. `CLAUDE.md` links to it with ordinary links (not an `@` import), so it is not loaded into context on its own |
| `.mcp.json` | HTTP server `libraries-info` → [WebMcpAPI](../ProjectPersona/ProjectPersona.WebMcpAPI/STRUCTURE.md) (`http://host.docker.internal:55095/mcp`) |
| `.claude/settings.local.json` | Enables the project MCP servers (`enableAllProjectMcpServers`, `libraries-info`) |

The `type-catalog/` folder (generated markdown, one file per assembly compilation) is not committed; produce it with [CliTools](../ProjectPersona/ProjectPersona.CliTools/STRUCTURE.md) and place it here.

## Keeping in sync

The rules here share one contract with [AgentInteraction.Cursor](../AgentInteraction.Cursor/STRUCTURE.md). When the catalog format written by `TypeCatalogMarkdownWriter` or the contract of `find` / `usedBy` / `textSearch` / `ragSearch` changes, update both workspaces in the same change. Tool semantics are described in [StdioMcpAPI](../ProjectPersona/ProjectPersona.StdioMcpAPI/STRUCTURE.md).

An agent working on the Persona.NET repository itself does not apply these instructions as its own rules.
