# Контракт find, usedBy, textSearch и ragSearch

`find` и `usedBy` принимают `fqn`, `assemblyName`, `memberNames`. У `usedBy` есть ещё `limit`. `textSearch` принимает `query`, `assemblyName`, `ignoreCase`, `limit`. `ragSearch` принимает `query`, `limit`, `includeFragmentSource`. Ответ всех четырёх tools — JSON в PascalCase.

Имена `memberNames` для `find` — из каталога, не сигнатура целиком и не `References.Name`. Одно имя покрывает все перегрузки. В `usedBy` то же каталожное имя; для операторов дополнительно принимается символьное `op_*` (сервер сопоставляет алиасы с `operator …`).

| Что в каталоге | `find` | `usedBy` |
|---|---|---|
| `` `public void Login(` `` | `Login` | `Login` |
| `` `public Store()` `` | `Store` (короткое имя типа, не `.ctor`) | `Store` |
| `` `this[string key]` `` | `this` | `this` |
| `` `private readonly field string _name` `` | `_name` | `_name` |
| `` `public event Sample.Storage.ItemHandler ItemAdded` `` | `ItemAdded` | `ItemAdded` |
| член enum `` `Red` `` | `Red` | `Red` |
| сигнатура с `operator +` | `operator +` | `operator +` или `op_Addition` |
| `implicit operator Sample.Storage.Display` | `implicit operator Sample.Storage.Display` | `op_Implicit` |
| `explicit operator Sample.Storage.Display` | `explicit operator Sample.Storage.Display` | `op_Explicit` |
| `System.Collections.IEnumerable.GetEnumerator()` | `GetEnumerator` | `GetEnumerator` |

`null` или `[]` — весь тип (`find`) или потребители типа (`usedBy`).

Запрос `GetEnumerator` покрывает и публичный метод, и явную реализацию. В ответе это разные элементы: `Name` = `GetEnumerator` и `Name` = `System.Collections.IEnumerable.GetEnumerator`. Одинаковая `Signature` `()` их не склеивает.

После слов `implicit operator` или `explicit operator` в `find` копируется тот же текст типа, что стоит в сигнатуре: FQN, keyword или нерезолвленное имя. `Sample.Storage.Display` до `Display` не укорачивается. В `usedBy` тип преобразования в имя не входит: ключ всегда `op_Implicit` или `op_Explicit`.

Остальные операторы в `usedBy`:

| В каталоге | `usedBy` |
|---|---|
| бинарный `+` | `op_Addition` |
| бинарный `-` | `op_Subtraction` |
| унарный `-` | `op_UnaryNegation` |
| унарный `+` | `op_UnaryPlus` |
| `*` | `op_Multiply` |
| `/` | `op_Division` |
| `%` | `op_Modulus` |
| `&` | `op_BitwiseAnd` |
| `\|` | `op_BitwiseOr` |
| `^` | `op_ExclusiveOr` |
| `<<` | `op_LeftShift` |
| `>>` | `op_RightShift` |
| `>>>` | `op_UnsignedRightShift` |
| `==` | `op_Equality` |
| `!=` | `op_Inequality` |
| `<` | `op_LessThan` |
| `>` | `op_GreaterThan` |
| `<=` | `op_LessThanOrEqual` |
| `>=` | `op_GreaterThanOrEqual` |
| `!` | `op_LogicalNot` |
| `~` | `op_OnesComplement` |
| `++` | `op_Increment` |
| `--` | `op_Decrement` |
| `true` | `op_True` |
| `false` | `op_False` |

Запрос `operator -` возвращает унарные и бинарные перегрузки вместе; их различает `Signature` (один параметр и два). `op_UnaryNegation` и `op_Subtraction` выбирают одну арность. То же для `operator +` (`op_UnaryPlus` и `op_Addition`). Checked-оператор в каталоге записан тем же токеном, что и обычный (`operator +`): слова `checked` в снимке нет. Ключ `op_Checked*` в `memberNames` не передавай — объявлению он не соответствует. Имени `op_*` вне этой таблицы не выдумывай: для `find` остаётся каталожная строка `operator …`.

## Общие параметры find и usedBy

- `fqn` — текст после `FQN:` в каталоге. Без `global::` и без списка аргументов generic. Вложенный тип: `Namespace.Outer.Inner`. В запрос идёт `Sample.Storage.Store`, не `Store<TItem>`.
- `assemblyName` — имя сборки из YAML `assembly`, либо `null` (все сборки). Сужай, если тип есть в нескольких сборках или TFM. У `usedBy` это фильтр объявления цели, не фильтр потребителей.
- Несколько TFM возвращаются разными элементами `Matches` со своим `TargetFramework`.

## find

```
find(fqn: "Sample.Storage.Store", assemblyName: "Sample.Storage", memberNames: ["Store"])
```

- `Error` = `null`, `Matches` — массив. `Matches: []` после сверки `FQN:` и строки члена (и прохода по непосредственным `Base` и `Implements`, если в блоке члена нет) — в индексе нет такой пары. До этой сверки отсутствие не объявляй.
- `Error` ≠ `null`, `Matches` = `null` — поиск не выполнен (неверные параметры, таймаут и другое). Передай текст `Error` пользователю. Таймаут или отмену повтори один раз. Способ пересобрать индекс не выдумывай.
- Весь тип: у совпадения есть `SourceCode` и `References`, `Members` = `null`.
- Срез членов: `Members[].Name`, `SourceCode`, `References`; у самого типа `SourceCode` и `References` = `null`.

`Kind` в `References`: `0` Type, `1` Method, `2` Constructor, `3` Property, `4` Indexer, `5` Field, `6` Event, `7` Unresolved, `8` константа перечисления. У ссылки на член `Fqn` — FQN типа, в котором лежит член. У `Unresolved` поле `Fqn` пустое: не заполняй его сам. Константу перечисления не читай как поле.

У ссылки также есть `Display` (текст из исходника), `Name`, `AssemblyName` и `Signature`. `Signature` метода и конструктора — список типов параметров в скобках (`(string, bool)`, `(Sample.Storage.IItem)`); у индексатора — `[Тип]`; иначе пустая строка. Примитивы в `Signature` остаются keywords. В `memberNames` для `find` поле `Name` не подставляй: у конструктора там часто `.ctor`, у оператора — `op_Addition`, у индексатора — не `this`.

## usedBy

Вызывай, когда нужен граф потребителей и FQN уже подтверждён каталогом. Тело и исходящие ссылки этот tool не возвращает.

```
usedBy(fqn: "Sample.Storage.Store", assemblyName: "Sample.Storage", memberNames: ["Store"], limit: 100)
```

| `memberNames` | Что в ответе |
|---|---|
| `null` / `[]` | Ссылки на тип: сигнатуры, поля, базы, атрибуты |
| `["Open"]` | Вызовы и обращения к этому члену |
| `["op_Addition"]` | Обращения к оператору `+` |

- `limit` по умолчанию 100. При `> 0` каждый список `UsedBy` не длиннее этого числа. При `<= 0` приходит весь список. Поднимай `limit` или ставь `0`, только если `UsedByTotal` показывает, что выборки не хватает. `limit <= 0` на типе с огромным графом раздувает контекст.
- `Error` и `Matches` — тот же смысл, что у `find`.
- Элемент `Matches`: `AssemblyName`, `Type`, `TargetFramework` цели.
- Весь тип: `UsedBy` и `UsedByTotal`, `Members` = `null`.
- Срез членов: `Members[].Name`, `Signature`, `UsedBy`, `UsedByTotal`. У типа при срезе `UsedBy` = `null`, `UsedByTotal` = `0`. Одно имя в запросе может дать несколько элементов `Members` с одним `Name` и разными `Signature` (перегрузки). Явная реализация того же идентификатора — отдельный элемент с каталожным `Name`, даже если `Signature` совпадает с публичным методом. `Name` всегда каталожное (`operator +`), даже если в запросе было `op_Addition`. Пустая `Signature` — группа без разреза по параметрам.
- Элемент `UsedBy`: `AssemblyName`, `TargetFramework`, `Type` (FQN потребителя), `MemberName` (имя члена потребителя в формате каталога), `Signature` (сигнатура вызывающей перегрузки, тот же формат, что у `References`). Пустой `MemberName` — ссылка на уровне объявления типа: база или атрибут. Пустая `Signature` — у вызывающего нет списка параметров (поле, свойство, событие, уровень типа) либо ребро из индекса без подписи вызывающего; тогда перегрузку `operator +` видно только через `find` её тела.

Три формы «пустого» у `usedBy` (после сверки `FQN:` в каталоге):

1. `Matches: []` — FQN цели нет в индексе.
2. `Matches` есть, `Members: []` — ни одно имя из `memberNames` не совпало с объявлением.
3. Строка в `Members` с `UsedBy: []` и `UsedByTotal: 0` — член в индексе есть, статических потребителей в графе нет. Не объявляй «мёртвый код»: reflection, десериализация и `Activator` в граф не входят. Учитывай XML в каталоге. При необходимости сам вызови `textSearch` по имени члена (≥ 3 символа); совпадение не ребро графа.

Ограничения ответа:

- `UsedByTotal` больше длины `UsedBy` — список обрезан. Не утверждай, что потребителей больше нет.
- В граф попадают только индексированные сборки. BCL и `Unresolved` в него не входят.
- Перегрузки различаются полем `Signature` в `Members[]` (формат как у `References` в `find`).
- Это не живой поиск ссылок IDE. TFM потребителей отфильтрован по TFM цели.
- Статический граф не покрывает reflection и неявное создание без явного вызова в исходнике.

## textSearch

Вызывай, когда известна подстрока исходника, а FQN ещё нет. Сниппет и метаданные этот tool возвращает; полный `SourceCode` — только `find` после сверки `FQN:` в каталоге.

```
textSearch(query: "store-closed", assemblyName: null, ignoreCase: false, limit: 50)
```

- `query` — точная подстрока, минимум 3 символа. Короче — `Error`, запрос дописывать не нужно. Пустая строка — тоже `Error`.
- `assemblyName` — имя сборки или `null` (все сборки). Сравнение имени сборки точное.
- `ignoreCase` по умолчанию `false`. `true` — то же сравнение без учёта регистра, не поиск по сходству.
- `limit` по умолчанию 50. При `> 0` в `Matches` не больше этого числа. При `<= 0` приходит весь список. Поднимай `limit` или ставь `0`, только если `Total` больше длины `Matches`.

Ответ:

- `Error` = `null`, `Total` — число совпадений до обрезки, `Matches` — массив. Пустой `Matches` — подстроки в индексе нет.
- `Error` ≠ `null`, `Matches` = `null`, `Total` = `0` — поиск не выполнен. Передай текст `Error`. Таймаут или отмену повтори один раз. Способ пересобрать индекс не выдумывай.
- Элемент `Matches`: `AssemblyName`, `TargetFramework`, `Type` (FQN), `Level` (`Type` или `Member`), `MemberName` (`null` на уровне типа), `OccurrenceCount` (число неперекрывающихся вхождений в этом документе), `Snippet` (окно вокруг первого вхождения, не длиннее 200 символов).
- Одна и та же подстрока внутри члена может дать две строки: `Level: Type` и `Level: Member`. Для тела члена дальше `find` с `memberNames` из `MemberName` строки `Member`.
- Порядок строк — сборка, TFM, FQN, уровень типа перед уровнем члена. Первая строка не означает лучшее совпадение.

Дальше открой в каталоге строку `FQN:` из `Type`. Нет такой строки — `find` и `usedBy` с этим именем не вызывай. Есть строка и `MemberName` — `find` с этим именем члена.

## ragSearch

Вызывай, когда нужно найти место в исходниках по смыслу, а не по точной подстроке (литерал и неизвестный FQN — по-прежнему `textSearch`). Tool есть, только если семантический поиск включён на сервере; нет tool — ищи через `textSearch` и каталог. Возвращает ближайшие фрагменты объявлений; полное объявление — только `find` после сверки `FQN:` в каталоге.

```
ragSearch(query: "where is store closed handled", limit: 10, includeFragmentSource: true)
```

- `query` — непустой текст запроса. Пустая строка — `Error`. Слишком длинный запрос для модели эмбеддинга — `Error` с просьбой укоротить.
- `limit` по умолчанию 10, минимум 1. При `<= 0` — `Error` (полного списка соседей, как у `textSearch` при `limit <= 0`, нет).
- `includeFragmentSource` по умолчанию `true`. `false` — только метаданные фрагмента без поля `Source`.

Ответ:

- `Error` = `null`, `Matches` — массив в порядке близости (первая строка — наиболее релевантная). Пустой `Matches` — в индексе нет векторов или поиск не нашёл соседей.
- `Warnings` — `null` или список предупреждений о полноте результата. Если векторный индекс обновлён не полностью, предупреждение перечисляет сборки, результаты по которым могут отсутствовать. Если ответ зависит от этих сборок, передай предупреждение пользователю.
- `Error` ≠ `null`, `Matches` = `null` — поиск не выполнен. Передай текст `Error`. Таймаут или отмену повтори один раз. Если `Error` говорит, что векторного индекса нет, у него нет манифеста или он несовместим с моделью сервера, — семантический поиск недоступен до перезапуска сервера: сообщи пользователю и продолжай через `textSearch` и каталог. Способ пересобрать индекс не выдумывай.
- Элемент `Matches`: `AssemblyName`, `TargetFramework`, `Type` (FQN), `Level` (`Type` или `Member`), `MemberName` (`null` на уровне типа), `ChunkIndex`, `Source`. `Source` — окно нарезки при `includeFragmentSource: true`, иначе `null`. `ChunkIndex` заполнен только вместе с текстом фрагмента; при `includeFragmentSource: false` он приходит как `0`. `Source` не заменяет `find`: это фрагмент из векторного индекса, не полное объявление.

Дальше открой в каталоге строку `FQN:` из `Type`. Нет такой строки — `find` и `usedBy` с этим именем не вызывай. Есть строка и `MemberName` — `find` с этим именем члена.
