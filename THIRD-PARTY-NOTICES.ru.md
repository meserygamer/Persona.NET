# Сторонние компоненты

[English](THIRD-PARTY-NOTICES.md) | Русский

Исходный код Persona.NET распространяется под лицензией [0BSD](LICENSE). Проект зависит от перечисленных ниже сторонних компонентов, которые сохраняют свои лицензии. Все они разрешительные (MIT или Apache-2.0), копилефта нет.

Репозиторий содержит только исходный код и не распространяет эти компоненты. Если вы **распространяете бинарники** (опубликованную сборку, образ контейнера, архив с выходной папкой), к включённым компонентам применяются лицензии ниже:

- **MIT** — сохраните уведомление об авторских правах компонента и текст разрешения MIT.
- **Apache-2.0** — приложите копию лицензии Apache-2.0, сохраните уведомления об авторстве и содержимое файла `NOTICE`, если пакет его содержит. Помечайте изменённые вами файлы.

Пакеты NuGet содержат файлы лицензий внутри (`dotnet publish` копирует многие из них рядом с бинарниками); сохраняйте их.

## Прямые зависимости NuGet

| Пакет | Версия | Лицензия |
|---|---|---|
| LanceDB | 2.5.0 | Apache-2.0 |
| ModelContextProtocol, ModelContextProtocol.AspNetCore | 2.2.0 | Apache-2.0 |
| Serilog, Serilog.AspNetCore | 4.4.0, 10.0.0 | Apache-2.0 |
| Microsoft.CodeAnalysis.CSharp, .CSharp.Workspaces, .Workspaces.MSBuild | 5.9.0 | MIT |
| Microsoft.Build.Locator, Microsoft.Build.Framework | 1.11.2, 17.11.48 | MIT |
| Microsoft.Data.Sqlite | 10.0.14 | MIT |
| Microsoft.Extensions.* (Configuration, DependencyInjection, Hosting, Http, Logging, Options) | 10.0.x | MIT |
| System.CommandLine | 2.0.12 | MIT |
| xunit, xunit.runner.visualstudio (только тесты) | 2.9.3, 3.1.4 | Apache-2.0 |
| Microsoft.NET.Test.Sdk, coverlet.collector (только тесты) | 17.14.1, 6.0.4 | MIT |

## Заметные транзитивные зависимости

| Пакет | Лицензия | Примечание |
|---|---|---|
| Apache.Arrow, FlatSharp.* (через LanceDB) | Apache-2.0 | Apache Arrow © The Apache Software Foundation |
| SQLitePCLRaw.* (через Microsoft.Data.Sqlite) | Apache-2.0 | © SourceGear, LLC; содержит SQLite, который находится в общественном достоянии |
| Newtonsoft.Json | MIT | © James Newton-King |
| Humanizer.Core | MIT | © .NET Foundation and Contributors |
| Serilog.Sinks.*, Serilog.Extensions.* | Apache-2.0 | © Serilog Contributors |

LanceDB также поставляет нативные библиотеки (`runtimes/`), собранные из кода на Rust со своими лицензиями зависимостей; при их распространении проверьте содержимое пакета.

## Внешние компоненты, которые вы подключаете сами (их нет в репозитории)

| Компонент | Лицензия | Примечание |
|---|---|---|
| [llama.cpp](https://github.com/ggml-org/llama.cpp) `llama.dll` (b12359) | MIT | Нужен только для RAG. При распространении бинарника сохраните его лицензию |
| `embeddinggemma-2-Q8_0.gguf` (EmbeddingGemma, Google; GGUF-квантизация от [Unsloth](https://huggingface.co/unsloth/embeddinggemma-2-GGUF), использовалась для тестов — вместо неё можно взять другую модель) | Gemma Terms of Use | **Не** открытая лицензия: есть ограничения на использование, и их нужно передавать дальше при распространении модели. Изучите условия перед распространением или используйте другую embedding-модель |

## Проверка

Лицензии прочитаны из файлов `.nuspec` точных версий пакетов в локальном кэше NuGet. Перепроверьте их после обновления зависимостей.
