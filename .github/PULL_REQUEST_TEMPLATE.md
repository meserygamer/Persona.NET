## Summary / Описание

<!-- What does this change and why? / Что меняется и зачем? -->

## Checklist / Чек-лист

- [ ] The solution builds / Решение собирается (`dotnet build ProjectPersona/ProjectPersona.sln`)
- [ ] Tests pass / Тесты проходят (`dotnet test ProjectPersona/ProjectPersona.sln`)
- [ ] The `STRUCTURE.md` of every affected project is updated (and `docs/project-structure.md` if a project was added or removed) / Обновлены `STRUCTURE.md` затронутых проектов (и `docs/project-structure.md`, если проект добавлен или удалён)
- [ ] If the catalog format or the `find` / `usedBy` / `textSearch` / `ragSearch` contract changed: both `AgentInteraction.Claude` and `AgentInteraction.Cursor` are updated / Если изменился формат каталога или контракт tools: обновлены оба — `AgentInteraction.Claude` и `AgentInteraction.Cursor`
- [ ] If an MCP tool changed: both `StdioMcpAPI` and `WebMcpAPI` are updated / Если изменился MCP-tool: обновлены оба — `StdioMcpAPI` и `WebMcpAPI`
