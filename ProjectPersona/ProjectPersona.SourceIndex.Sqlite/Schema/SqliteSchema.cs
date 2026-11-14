namespace ProjectPersona.SourceIndex.Sqlite.Schema;

/// <summary>
/// DDL индекса исходников. Индексы создаются отдельно, после массовой вставки.
/// </summary>
internal static class SqliteSchema
{
    /// <summary>
    /// Таблицы индекса.
    /// </summary>
    public const string Tables = """
        CREATE TABLE assembly (
            id INTEGER PRIMARY KEY,
            name TEXT NOT NULL,
            target_framework TEXT NOT NULL DEFAULT '',
            UNIQUE (name, target_framework)
        ) STRICT;

        CREATE TABLE declaration (
            id INTEGER PRIMARY KEY,
            assembly_id INTEGER NOT NULL REFERENCES assembly(id),
            fqn TEXT NOT NULL,
            source_code TEXT NOT NULL
        ) STRICT;

        CREATE TABLE member_slice (
            id INTEGER PRIMARY KEY,
            declaration_id INTEGER NOT NULL REFERENCES declaration(id),
            name TEXT NOT NULL,
            start INTEGER NOT NULL,
            length INTEGER NOT NULL,
            signature TEXT NOT NULL DEFAULT ''
        ) STRICT;

        CREATE TABLE reference (
            id INTEGER PRIMARY KEY,
            declaration_id INTEGER NOT NULL REFERENCES declaration(id),
            member_slice_id INTEGER REFERENCES member_slice(id),
            kind INTEGER NOT NULL,
            display TEXT NOT NULL,
            fqn TEXT NOT NULL,
            name TEXT NOT NULL,
            assembly_name TEXT NOT NULL,
            signature TEXT NOT NULL
        ) STRICT;

        CREATE TABLE used_by (
            id INTEGER PRIMARY KEY,
            target_fqn TEXT NOT NULL,
            target_member TEXT,
            target_signature TEXT NOT NULL DEFAULT '',
            consumer_assembly TEXT NOT NULL,
            consumer_tfm TEXT NOT NULL,
            consumer_type TEXT NOT NULL,
            consumer_member TEXT NOT NULL,
            consumer_signature TEXT NOT NULL
        ) STRICT;
        """;

    /// <summary>
    /// Имя FTS5-таблицы по <c>declaration.source_code</c>.
    /// </summary>
    public const string FullTextTable = "declaration_fts";

    /// <summary>
    /// FTS5-индекс подстрок (токенайзер <c>trigram</c>) поверх <c>declaration</c>: текст не дублируется, хранятся только триграммы.
    /// Строится одним <c>rebuild</c> после массовой вставки.
    /// </summary>
    public const string FullTextIndex = $"""
        CREATE VIRTUAL TABLE {FullTextTable} USING fts5(
            source_code,
            content = 'declaration',
            content_rowid = 'id',
            tokenize = 'trigram'
        );
        INSERT INTO {FullTextTable}({FullTextTable}) VALUES ('rebuild');
        """;

    /// <summary>
    /// Таблицы, без которых индекс непригоден для чтения; проверяются при открытии файла.
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredTables =
    [
        "assembly",
        "declaration",
        "member_slice",
        "reference",
        "used_by",
        FullTextTable
    ];

    /// <summary>
    /// Индексы поиска по FQN и по цели потребителей.
    /// </summary>
    public const string Indexes = """
        CREATE INDEX ix_declaration_fqn ON declaration(fqn, assembly_id);
        CREATE INDEX ix_member_slice_decl ON member_slice(declaration_id);
        CREATE INDEX ix_reference_decl ON reference(declaration_id, member_slice_id);
        CREATE INDEX ix_used_by_target ON used_by(target_fqn, target_member, target_signature);
        """;
}
