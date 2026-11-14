# Library API lookup

This workspace has no library sources. API facts come from two places only, so never guess from memory and never search for library sources on disk:

1. `type-catalog/*.md` — generated signatures, FQNs, XML docs, member lists. One file per assembly compilation: `{Assembly}.md`, or `{Assembly}.{tfm}.md` when an assembly is built for several TFMs. Files are large: Grep first, then Read a narrow range.
2. MCP server `libraries-info` (`.mcp.json`) — method bodies, outgoing references, consumers, text search and (when enabled on the server) semantic search over the indexed sources.

Read [mcp-contract.md](mcp-contract.md) before interpreting an empty or truncated result, passing an operator/indexer/explicit-interface member name, or quoting a `ragSearch` result. The rules below are enough for ordinary lookups.

## Which source answers what

| Question | Use |
|---|---|
| What is it: signature, FQN, type params, Base, Implements, Attributes, XML, member list | Catalog (Grep → narrow Read). Don't call `find` for this |
| Method body, implementation, outgoing references | `find` with the catalog FQN and a narrow `memberNames` |
| Who calls a member / who references a type | `usedBy` |
| A literal (string, identifier, setting name) with unknown FQN | `textSearch`, then catalog by the hit's `Type`, then `find` |
| Behavior described by meaning, no exact string | `ragSearch`, then catalog by the hit's `Type`, then `find`. No `ragSearch` tool: `textSearch` with likely identifiers |
| Who creates instances | No single call; see [mcp-contract.md](mcp-contract.md#who-creates-instances) |

Keep calls narrow: pass the member name instead of fetching a whole type, don't add `usedBy` to every `find`, don't use `ragSearch` for exact literals, don't sweep the catalog where one `textSearch` would do.

## Catalog layout

- File header (YAML): `assembly`, `targetFramework`, `namespaces`, `types` (counts). Then a namespace list, namespace heading `## Ns`, type heading ``### `public class Name` ``.
- Under a type heading: ``FQN: `Ns.Name` `` line, then optional `Base:`, `Implements:`, `Attributes:`, then `> XML doc`, then `#### Members` with one member per `- ` line (parameter docs nested under it).
- The `FQN:` line of the **nearest** `###` heading is the owner of the members below it. A type mentioned in a signature, a parameter or XML text is not the owner.
- Types in several files (different assemblies or TFMs) are different declarations. Narrow with `assemblyName`; if the answer differs between TFMs, say so instead of picking one silently.
- Read `Obsolete` on the type and member. If it names a replacement, confirm the replacement in the catalog before recommending it.

## Before calling `find` or `usedBy`

1. The catalog must contain a `FQN:` line for this exact type. If it doesn't, don't call the tool and don't invent the API; tell the user the type is not in the catalog.
2. Copy `memberNames` from that same type's block (rules in the table below). Don't turn an unresolved short name into a guessed FQN.
3. `textSearch` and `ragSearch` need no `FQN:` check first, but check the catalog for the hit's `Type` before following up with `find`/`usedBy`.

`fqn`: the text after `FQN:` without the backticks, with no `global::` and no generic argument list (`Soft.Core.Manager`, not `Manager<TItem, TInstance>`). Nested types: `Namespace.Outer.Inner`.

`assemblyName`: the YAML `assembly` value, or `null` for all assemblies. For `usedBy` it filters the target declaration, not the consumers.

| Member kind | `memberNames` entry |
|---|---|
| Method, property, event, enum member | identifier as in the catalog (`Login`) |
| Field | with underscore as in the catalog (`_name`) |
| Constructor | short type name (`CompositeObject`), not `.ctor` |
| Indexer | `this` |
| Explicit interface implementation | bare identifier (`GetEnumerator`), not `IEnumerable.GetEnumerator` |
| Operator | see [mcp-contract.md](mcp-contract.md#operators) |
| Whole type | `null` or `[]` |

One name covers all overloads that share it in the catalog.

## MCP tools

Full names are `mcp__libraries-info__find`, `__usedBy`, `__textSearch`, `__ragSearch`. If a tool isn't callable yet, load its schema with ToolSearch (`select:mcp__libraries-info__find,mcp__libraries-info__usedBy,mcp__libraries-info__textSearch,mcp__libraries-info__ragSearch`); if still missing, ToolSearch for `libraries-info`. Don't guess other prefixes.

`find`, `usedBy` and `textSearch` are always published. `ragSearch` is published only when semantic search is enabled in the server configuration: if `find` loads and `ragSearch` doesn't, RAG is off for this session. Use the other tools and don't keep looking for it.

The schema marks nullable parameters without defaults as required: pass `null` explicitly.

| Tool | Parameters (defaults) |
|---|---|
| `find` | `fqn`, `assemblyName`, `memberNames` |
| `usedBy` | `fqn`, `assemblyName`, `memberNames`, `limit` (100; `<= 0` = everything) |
| `textSearch` | `query` (≥ 3 chars), `assemblyName`, `ignoreCase` (false = case-sensitive), `limit` (50; `<= 0` = everything) |
| `ragSearch` | `query`, `limit` (10, ≥ 1), `includeFragmentSource` (true). No assembly filter. Optional tool, see above |

Keep `limit` at the default unless the `Total` / `UsedByTotal` shows the sample is too small. `textSearch` rows are sorted by assembly and type name, not by relevance: when `Total` exceeds the list, narrow with `assemblyName` or a longer `query` before raising `limit`. Never run `usedBy` with empty `memberNames` on hub types (`UserConnection`, `Entity`, schema managers): narrow to a member. `limit <= 0` on a large type floods the context.

## Reading results (essentials)

- `Error` set, `Matches` null: the call failed. Pass the `Error` text to the user; don't invent a reindex or restart procedure.
- Empty `Matches` is not proof of absence until you've checked the catalog `FQN:` and the member line (and `Base`/`Implements` if the member isn't in the block).
- `UsedBy: []` for a member means "no static edges", not "dead code": reflection, DI by name, deserialization and `Activator` aren't in the graph. BCL types aren't in it either.
- `UsedByTotal` or `Total` larger than the returned list means truncation. Don't claim there are no other consumers or hits.
- Method bodies come only from `find`. `textSearch` snippets and `ragSearch` `Source` are windows of the declaration, not bodies.
- `Error` saying the request was cancelled or timed out: retry the same call once.
- `ragSearch` `Error` about a missing, invalid or incompatible vector index: semantic search is unavailable for this server session. Tell the user and continue with `textSearch` and the catalog; don't try to rebuild the index.
- `ragSearch` `Warnings` name assemblies whose results may be incomplete. Mention them when the answer depends on those assemblies.
- Quote a body only when the question is about behavior; otherwise answer from the catalog (signature, `FQN`, type params, Base, Implements, Attributes, XML).
