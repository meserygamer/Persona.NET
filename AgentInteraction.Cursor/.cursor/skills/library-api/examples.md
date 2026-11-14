# Разборы

Фрагменты ниже показывают форму каталога. Имена вымышлены.

## В каталоге нет FQN — MCP не вызывать

Нужно поведение `Sample.Storage.Session`. Grep по `FQN: \`Sample.Storage.Session\`` ничего не находит. Ближайший тип в файле — `Sample.Storage.SessionBase`.

Остановись. Скажи, что `Sample.Storage.Session` в каталоге нет. `find` и `usedBy` с этим FQN не вызывай и членов ему не придумывай. Если вопрос можно закрыть через `SessionBase`, открой его блок и работай уже с его строкой `FQN:`.

## Чужой тип и пустой Matches

Grep по `Open(` даёт два попадания:

```
### `public sealed class CacheStore` {#type-sample-storage-cachestore}

FQN: `Sample.Storage.CacheStore`

#### Members

- `public bool Open(string name)`
  - param `name`: `string`
  - returns: `bool`

### `public sealed class Session` {#type-sample-storage-session}

FQN: `Sample.Storage.Session`
Base: `Sample.Storage.SessionBase`

#### Members

- `public void Bind(Sample.Storage.CacheStore store)`
  - param `store`: `Sample.Storage.CacheStore`
  - returns: `void`
  > Требует, чтобы store уже вызвал Open().
```

Владелец `Open` — ближайший заголовок `###` над строкой члена, то есть `CacheStore`, и его `FQN: \`Sample.Storage.CacheStore\``. Упоминание `Open()` в XML у `Session` владельцем не является. В MCP уходит:

```
find(fqn: "Sample.Storage.CacheStore", assemblyName: "Sample.Storage", memberNames: ["Open"])
```

Запрос `find(fqn: "Sample.Storage.Session", memberNames: ["Open"])` вернёт `Matches: []`. Это не доказательство, что метода нет. Сверь блок `Session`: строки члена `Open` там нет. Открой `Base: \`Sample.Storage.SessionBase\`` и повтори Grep. Объявление по-прежнему одно — на `CacheStore`. Только если и у владельца со строкой члена `find` снова пустой, скажи, что тела в индексе нет.

Тот же порядок, если метод ждут на наследнике, а в его блоке члена нет: унаследованные члены в снимок не копируются, ищи их на непосредственных `Base` и `Implements`, затем бери FQN того типа, где строка члена реально лежит.

## Вызов, совместимый с библиотекой

Проект собирается под `net472`. В каталоге открыт файл с `targetFramework: net472`.

```
### `public sealed class DocumentWriter` {#type-sample-docs-documentwriter}

FQN: `Sample.Docs.DocumentWriter`

#### Members

- `public void Write(string path, bool overwrite)`
  - attr `System.ObsoleteAttribute("use WriteAsync")`
  - param `path`: `string`
  - param `overwrite`: `bool`
  - returns: `void`
- `public System.Threading.Tasks.Task WriteAsync(string path, bool overwrite)`
  - param `path`: `string`
  - param `overwrite`: `bool`
  - returns: `System.Threading.Tasks.Task`
```

`Write` помечен устаревшим, замена названа. Строка `WriteAsync` в том же блоке есть — вызывай её, не `Write`.

`find` с `memberNames: ["WriteAsync"]` показывает: при `path == null` метод бросает `ArgumentNullException` и каталог не создаёт. `usedBy` с `memberNames: ["WriteAsync"]` и `limit: 100` даёт вызывающих того же TFM; в их коде путь корневой и `overwrite` равен `false`. `UsedByTotal` равен длине списка — выборка полная.

Новый код вызывает `WriteAsync`, передаёт уже проверенный путь и `overwrite: false`. Сигнатуру, порядок параметров и тип возврата бери из каталога, не из памяти о другом TFM.

## Подстрока, FQN ещё неизвестен

Нужно место, где в исходнике встречается литерал `store-closed`. Строки `FQN:` под рукой нет.

```
textSearch(query: "store-closed", assemblyName: null, ignoreCase: false, limit: 50)
```

`Error` = null, `Total` = 1, в `Matches` одна строка: `AssemblyName` = `Sample.Storage`, `Type` = `Sample.Storage.Store`, `Level` = `Member`, `MemberName` = `Save`, `Snippet` содержит литерал. Сниппет короче метода.

Дальше Grep по `FQN: \`Sample.Storage.Store\``. В блоке этого типа есть строка `Save`. Тогда:

```
find(fqn: "Sample.Storage.Store", assemblyName: "Sample.Storage", memberNames: ["Save"])
```

Если рядом есть вторая строка с тем же `Type` и `Level: Type`, для тела члена бери `MemberName` строки `Member`. `find` всего типа ради одного `Save` не вызывай.

`Matches: []` при `Error` = null — литерала в индексе нет. Тип, который «должен» его содержать, не выдумывай. `Error` про таймаут или отмену — повтори тот же вызов один раз.

## Оператор преобразования

В каталоге:

```
- `public static Sample.Storage.Display implicit operator Sample.Storage.Display(Sample.Storage.Store store)`
  - returns: `Sample.Storage.Display`
```

В `find` уходит `implicit operator Sample.Storage.Display`: тип после `implicit operator` тот же, что в сигнатуре. В `usedBy` подойдут `op_Implicit` или полная каталожная строка преобразования. Строка `implicit operator Display` без типа из сигнатуры в `memberNames` не уходит.

## Поиск по смыслу

Нужно место, где закрывают хранилище, точной строки в запросе нет.

```
ragSearch(query: "where is store closed handled", limit: 10, includeFragmentSource: true)
```

`Error` = null, первая строка `Matches` ближайшая: `Type` = `Sample.Storage.Store`, `Level` = `Member`, `MemberName` = `Close`, `ChunkIndex` = `0`, `Source` — окно кода, не весь метод. Сверь `FQN: \`Sample.Storage.Store\`` в каталоге и строку члена `Close`. Затем:

```
find(fqn: "Sample.Storage.Store", assemblyName: "Sample.Storage", memberNames: ["Close"])
```

`Source` телом не заменяй. При `includeFragmentSource: false` поле `Source` равно null, а `ChunkIndex` приходит как `0`. `Matches: []` при `Error` = null — близких фрагментов нет, тип не выдумывай. `Error` про таймаут или отмену — повтори тот же запрос один раз. Если `Error` говорит, что векторного индекса нет, у него нет манифеста или он несовместим с моделью сервера, — семантический поиск недоступен до перезапуска сервера: сообщи пользователю и продолжай через `textSearch` и каталог.

## Пустой usedBy и reflection

В каталоге у приватного метода может быть XML вроде «used by reflection in unit tests». `usedBy` с именем этого метода вернёт строку в `Members` с `UsedBy: []` — это не доказательство мёртвого кода. При необходимости сам вызови `textSearch` по имени метода (≥ 3 символа) и проверь сниппеты; для тела потребителя дальше `find`.
