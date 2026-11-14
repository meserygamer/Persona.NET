# Участие в разработке

[English](CONTRIBUTING.md) | Русский

## Подготовка

- .NET 10 SDK
- Для запуска RAG нужен Windows x64 (нативная обёртка работает с `llama.dll`); сборка, генерация каталога и индекса и тесты этого не требуют.

```
dotnet build ProjectPersona/ProjectPersona.sln
dotnet test ProjectPersona/ProjectPersona.sln
```

## Прежде чем начать

Прочитайте [docs/project-structure.md](docs/project-structure.md), а затем только `STRUCTURE.md` тех проектов, которых вы касаетесь. Документация в репозитории написана на английском.

## Правила

- Сохраняйте слои: у `SourceIndex` нет зависимостей; Roslyn использует только `Core`; MCP-серверы никогда не вызывают Roslyn и не занимаются векторизацией.
- Классы MCP-tools продублированы в `StdioMcpAPI` и `WebMcpAPI`. Меняйте их вместе.
- Если вы меняете формат markdown-каталога или контракт `find` / `usedBy` / `textSearch` / `ragSearch`, обновите `AgentInteraction.Claude` и `AgentInteraction.Cursor` в том же pull request.
- Если вы меняете формат вектора или метаданных, увеличьте `VectorizationPipeline.Version`.
- Поддерживайте файлы `STRUCTURE.md` актуальными при добавлении, перемещении и удалении файлов.
- Не коммитьте сгенерированные данные: `type-source-index/`, `type-catalog/`, каталоги LanceDB, модели `.gguf`, сборки `llama.dll`.

## Pull request

Откройте PR в ветку по умолчанию и заполните шаблон.
