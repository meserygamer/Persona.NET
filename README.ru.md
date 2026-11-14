# Persona.NET

[English](README.md) | Русский

Даёт ИИ-агентам точные знания о C#-кодовой базе, **не заставляя их читать все исходники**.

Persona.NET анализирует .NET-решение через Roslyn и создаёт:

- **каталог типов** — компактный markdown с сигнатурами, FQN и XML-документацией, один файл на сборку;
- **индекс исходников** — один файл SQLite с полными исходниками объявлений, исходящими ссылками, потребителями (`usedBy`) и полнотекстовым поиском;
- необязательный **векторный индекс** (RAG) в LanceDB для семантического поиска; эмбеддинги считаются локально через llama.cpp.

**MCP-сервер** (stdio или HTTP) открывает индекс агентам через четыре инструмента:

| Инструмент | Назначение |
|---|---|
| `find` | Исходный код и исходящие ссылки типа или отдельных членов |
| `usedBy` | Потребители типа или члена |
| `textSearch` | Точный поиск подстроки в исходниках типов и членов (SQLite FTS5) |
| `ragSearch` | Семантический поиск по фрагментам объявлений (необязательно) |

Агент читает сигнатуры из каталога, а тела методов и потребителей запрашивает по необходимости, поэтому контекст остаётся небольшим.

## Требования

- .NET 10 SDK (или новее) — для сборки Persona.NET. Для запуска MCP-серверов достаточно .NET 10 Runtime.
- **.NET SDK, способный собрать анализируемое решение** — нужен только для генерации (`CliTools`). MCP-серверы не используют Roslyn и MSBuild, а лишь читают готовый индекс, поэтому этот SDK им не нужен. Генератор открывает ваш `.sln` через `MSBuildWorkspace` из Roslyn, а `MSBuildLocator` подгружает MSBuild из SDK, установленного на машине. Этот SDK должен поддерживать целевые фреймворки и версию языка вашего решения (учитывайте `global.json`, если он есть). Если SDK слишком старый, проекты не загрузятся или типы окажутся нерезолвленными.
- Анализируемое решение должно быть **восстановлено** (`dotnet restore`), чтобы пакеты были найдены; для проектов .NET Framework должны быть доступны targeting packs или reference assemblies.
- Только для RAG: Windows x64, embedding-модель в формате GGUF (например, `embeddinggemma-2-Q8_0.gguf`) и сборка `llama.dll` из [llama.cpp](https://github.com/ggml-org/llama.cpp) **b12359**. Другие сборки могут быть несовместимы по ABI.

### Embedding-модель

RAG тестировался с `embeddinggemma-2-Q8_0.gguf` от [Unsloth](https://huggingface.co/unsloth/embeddinggemma-2-GGUF). Можно использовать и другие embedding-модели в формате GGUF, но учтите:

- укажите размерность модели в `--vector-dimension` (CLI) и `lanceDb.vectorDimension` (сервер);
- подберите `documentQueryTemplate` и `searchQueryTemplate` под формат промптов вашей модели (значения по умолчанию рассчитаны на EmbeddingGemma);
- после смены модели пересоберите векторный индекс командой `vectorize-type-source-index`: отпечаток в манифесте старого индекса не совпадёт, и `ragSearch` будет возвращать ошибку, пока вы этого не сделаете;
- проверьте лицензию самой модели (EmbeddingGemma распространяется под Gemma Terms of Use, см. [THIRD-PARTY-NOTICES.ru.md](THIRD-PARTY-NOTICES.ru.md)).

## Быстрый старт

```
git clone https://github.com/meserygamer/Persona.NET.git
cd Persona.NET
dotnet build ProjectPersona/ProjectPersona.sln
```

### 1. Генерация каталога и индекса исходников

```
dotnet run --project ProjectPersona/ProjectPersona.CliTools generate-type-artifacts <путь/к/ВашемуРешению.sln> <каталог-для-каталога-типов> <каталог-для-индекса>
```

Команда пишет файлы `{Assembly}[.{tfm}].md` в каталог типов и `type-source-index.db` в каталог индекса. Другие подкоманды: `generate-type-catalog`, `generate-type-source-index`.

### 2. (Необязательно) Построение векторного индекса

Скопируйте `ProjectPersona/ProjectPersona.CliTools/llama-options.example.json`, укажите `modelPath` и `nativeLibraryDirectory`, затем:

```
dotnet run --project ProjectPersona/ProjectPersona.CliTools vectorize-type-source-index <каталог-индекса> <каталог-lancedb> --vector-dimension 768 --llama-options <ваш-файл.json>
```

Позже `revectorize-assemblies` обновит только выбранные сборки.

### 3. Запуск MCP-сервера

Задайте `typeSourceIndexPath` в `appsettings.json` сервера (и секции `llama` / `lanceDb`, если RAG включён, либо `ragSearchEnabled: false`), затем запустите один из проектов:

- `ProjectPersona.StdioMcpAPI` — stdio, запускается клиентом;
- `ProjectPersona.WebMcpAPI` — HTTP на `/mcp` (порт по умолчанию 5095, HTTPS по желанию).

### 4. Подключение агента

Готовые рабочие пространства для копирования:

- [`AgentInteraction.Claude`](AgentInteraction.Claude) — Claude Code (`CLAUDE.md`, `.mcp.json`)
- [`AgentInteraction.Cursor`](AgentInteraction.Cursor) — Cursor (`AGENTS.md`, rules, skill, `mcp.json`)

Положите сгенерированную папку `type-catalog/` рядом с инструкциями и направьте `.mcp.json` / `mcp.json` на ваш сервер.

## Структура репозитория

```
docs/project-structure.md     индекс документации (начните с него, чтобы разобраться в коде)
AgentInteraction.Claude/      рабочее пространство для Claude Code
AgentInteraction.Cursor/      рабочее пространство для Cursor
ProjectPersona/               решение: Core, SourceIndex, SourceIndex.Sqlite, Application,
                              LlamaWrapper, RagTextSearch, CliTools, StdioMcpAPI, WebMcpAPI, Tests
```

Документация устроена как дерево: [docs/project-structure.md](docs/project-structure.md) ссылается на `STRUCTURE.md` в каждом проекте, поэтому вам (или агенту) нужно читать только то, что требуется для задачи. Документация написана на английском.

## Участие в разработке

См. [CONTRIBUTING.ru.md](CONTRIBUTING.ru.md) ([English](CONTRIBUTING.md)).

## Лицензия

[BSD Zero Clause License (0BSD)](LICENSE). Сторонние компоненты сохраняют свои лицензии, см. [THIRD-PARTY-NOTICES.ru.md](THIRD-PARTY-NOTICES.ru.md).
