# Third-party notices

English | [Русский](THIRD-PARTY-NOTICES.ru.md)

Persona.NET's own source code is licensed under [0BSD](LICENSE). It depends on the third-party components below, which keep their own licenses. All of them are permissive (MIT or Apache-2.0); none is copyleft.

The repository contains only source code and does not redistribute these components. If you **distribute binaries** (a published build, a container image, a zip with the output folder), the licenses below apply to the bundled components:

- **MIT** — keep the component's copyright notice and the MIT permission text with the distribution.
- **Apache-2.0** — include a copy of the Apache-2.0 license, keep copyright/attribution notices, and include the contents of a `NOTICE` file if the package ships one. Mark files you modified.

NuGet packages carry their license files inside the package (`dotnet publish` copies many of them next to the binaries); keep them.

## Direct NuGet dependencies

| Package | Version | License |
|---|---|---|
| LanceDB | 2.5.0 | Apache-2.0 |
| ModelContextProtocol, ModelContextProtocol.AspNetCore | 2.2.0 | Apache-2.0 |
| Serilog, Serilog.AspNetCore | 4.4.0, 10.0.0 | Apache-2.0 |
| Microsoft.CodeAnalysis.CSharp, .CSharp.Workspaces, .Workspaces.MSBuild | 5.9.0 | MIT |
| Microsoft.Build.Locator, Microsoft.Build.Framework | 1.11.2, 17.11.48 | MIT |
| Microsoft.Data.Sqlite | 10.0.14 | MIT |
| Microsoft.Extensions.* (Configuration, DependencyInjection, Hosting, Http, Logging, Options) | 10.0.x | MIT |
| System.CommandLine | 2.0.12 | MIT |
| xunit, xunit.runner.visualstudio (tests only) | 2.9.3, 3.1.4 | Apache-2.0 |
| Microsoft.NET.Test.Sdk, coverlet.collector (tests only) | 17.14.1, 6.0.4 | MIT |

## Notable transitive dependencies

| Package | License | Note |
|---|---|---|
| Apache.Arrow, FlatSharp.* (via LanceDB) | Apache-2.0 | Apache Arrow © The Apache Software Foundation |
| SQLitePCLRaw.* (via Microsoft.Data.Sqlite) | Apache-2.0 | © SourceGear, LLC; bundles SQLite, which is in the public domain |
| Newtonsoft.Json | MIT | © James Newton-King |
| Humanizer.Core | MIT | © .NET Foundation and Contributors |
| Serilog.Sinks.*, Serilog.Extensions.* | Apache-2.0 | © Serilog Contributors |

LanceDB also ships native libraries (`runtimes/`) built from Rust code with its own dependency licenses; check the package's contents if you redistribute them.

## External components you supply yourself (not in this repository)

| Component | License | Note |
|---|---|---|
| [llama.cpp](https://github.com/ggml-org/llama.cpp) `llama.dll` (b12359) | MIT | Required only for RAG. Keep its license if you redistribute the binary |
| `embeddinggemma-2-Q8_0.gguf` (EmbeddingGemma, Google; GGUF quantization by [Unsloth](https://huggingface.co/unsloth/embeddinggemma-2-GGUF), used for testing — other models can be used instead) | Gemma Terms of Use | **Not** an open-source license: it has use restrictions and requires passing them on if you redistribute the model. Review the terms before redistributing, or use another embedding model |

## Verification

Licenses were read from the `.nuspec` files of the exact package versions restored in the local NuGet cache. Re-check after upgrading dependencies.
