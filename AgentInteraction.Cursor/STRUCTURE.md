# AgentInteraction.Cursor

Parent: [project index](../docs/project-structure.md)

A sample consumer workspace for **Cursor**. It is opened independently of the Persona.NET sources and shows how an agent should use the generated documentation. It is not part of the generator.

| File | Role |
|---|---|
| `AGENTS.md` | Entry instruction: signatures, FQNs, XML docs and member lists come from `**/type-catalog/*.md`; method bodies and consumers from the MCP server whose name contains `libraries-info` (`find`, `usedBy`); substring search via `textSearch`; semantic search via `ragSearch` when enabled. Points to the `library-api` skill |
| `.cursor/mcp.json` | stdio server `libraries-info` → `dotnet ./ProjectPersona.StdioMcpAPI.dll` ([StdioMcpAPI](../ProjectPersona/ProjectPersona.StdioMcpAPI/STRUCTURE.md)) |
| `.cursor/rules/library-api.mdc` | Rule for using the library API documentation |
| `.cursor/rules/type-catalog-format.mdc` | Rule describing the catalog snapshot layout |
| `.cursor/skills/library-api/SKILL.md` | Skill: how to look up the API with the catalog and MCP tools |
| `.cursor/skills/library-api/mcp-contract.md` | Response shapes, errors, empty results, operators, index boundaries |
| `.cursor/skills/library-api/examples.md` | Worked lookup examples |

The `type-catalog/` folder (generated markdown) is not committed; produce it with [CliTools](../ProjectPersona/ProjectPersona.CliTools/STRUCTURE.md) and place it here.

## Keeping in sync

The rules here share one contract with [AgentInteraction.Claude](../AgentInteraction.Claude/STRUCTURE.md). When the catalog format written by `TypeCatalogMarkdownWriter` or the contract of `find` / `usedBy` / `textSearch` / `ragSearch` changes, update both workspaces in the same change.

An agent working on the Persona.NET repository itself does not apply these rules as its own.
