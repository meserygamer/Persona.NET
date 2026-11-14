# Библиотечный API

Сигнатуры, FQN, XML-комментарии и список членов берутся из `**/type-catalog/*.md`. Тела методов и потребители берутся из MCP-сервера, в имени которого есть `libraries-info`: tools `find` и `usedBy`. Подстрока в исходнике при неизвестном FQN — tool `textSearch`. Семантический поиск фрагментов — tool `ragSearch`, если он включён на сервере (без него — `textSearch` и каталог).

Перед первым `find`, `usedBy`, `textSearch` или `ragSearch` прочитай skill `library-api`: [SKILL.md](.cursor/skills/library-api/SKILL.md) и [mcp-contract.md](.cursor/skills/library-api/mcp-contract.md).

Вызов `find` и `usedBy` допустим только после буквального совпадения FQN и члена в каталоге. Пустой ответ этих tools не доказывает отсутствие члена, пока FQN владельца не сверен со строкой `FQN:` в блоке типа. Для оператора в `usedBy` годится каталожное имя (`operator -`, `operator +`) и символьное `op_*` из контракта. Каталожный минус возвращает унарную и бинарную формы, их различает `Signature`; одну арность выбирают `op_UnaryNegation` и `op_Subtraction`. Ключ `op_Checked*` не передавай: в каталоге checked-оператор выглядит как обычный `operator …`. `textSearch` сверку `FQN:` до вызова не требует и каталог не заменяет. `ragSearch` тоже не требует `FQN:` до вызова; `Source` в ответе — фрагмент, не полное объявление, и перед `find` строку `FQN:` из `Type` всё равно сверь.
