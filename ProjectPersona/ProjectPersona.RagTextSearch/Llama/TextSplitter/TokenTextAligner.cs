using System.Globalization;
using System.Text;
using ProjectPersona.RagTextSearch.Llama.Tokenizer;

namespace ProjectPersona.RagTextSearch.Llama.TextSplitter;

/// <summary> Сопоставляет индексы токенов смещениям в исходной строке. </summary>
internal static class TokenTextAligner
{
    /// <summary> Строит смещения символов для каждого токена (длина = число токенов + 1). </summary>
    /// <param name="text">Исходный текст, переданный в токенизатор.</param>
    /// <param name="tokens">Токены без специальных, в порядке API.</param>
    /// <returns>Смещения в символах (не в байтах UTF-8).</returns>
    /// <exception cref="InvalidOperationException">
    /// Склейка <c>piece</c> не восстанавливает <paramref name="text"/> даже после нормализации SentencePiece.
    /// </exception>
    public static int[] BuildCharOffsets(string text, ReadOnlySpan<LlamaModelToken> tokens)
    {
        if (tokens.Length == 0)
            return [0];

        var pieceLengths = new int[tokens.Length];
        var concat = new StringBuilder();
        for (var i = 0; i < tokens.Length; i++)
        {
            var piece = tokens[i].Piece ?? string.Empty;
            pieceLengths[i] = piece.Length;
            concat.Append(piece);
        }

        if (concat.ToString() == text)
            return BuildOffsetsFromPieceLengths(pieceLengths, text.Length);

        concat.Clear();
        for (var i = 0; i < tokens.Length; i++)
        {
            var normalized = NormalizePiece(tokens[i].Piece ?? string.Empty);
            pieceLengths[i] = normalized.Length;
            concat.Append(normalized);
        }

        if (concat.ToString() != text)
            throw new InvalidOperationException(
                "Токенизатор вернул куски piece, из которых нельзя восстановить исходный текст.");

        return BuildOffsetsFromPieceLengths(pieceLengths, text.Length);
    }

    /// <summary> Вырезает подстроку по полуинтервалу токенов <c>[tokenStart, tokenEnd)</c>. </summary>
    /// <param name="text">Исходный текст.</param>
    /// <param name="charOffsets">Смещения из <see cref="BuildCharOffsets"/>.</param>
    /// <param name="tokenStart">Индекс первого токена окна.</param>
    /// <param name="tokenEnd">Индекс токена после конца окна.</param>
    /// <returns>Фрагмент исходника без пересборки из <c>piece</c>.</returns>
    public static string Slice(string text, int[] charOffsets, int tokenStart, int tokenEnd)
    {
        var charStart = charOffsets[tokenStart];
        var charEnd = charOffsets[tokenEnd];
        return text[charStart..charEnd];
    }

    /// <summary> Собирает префиксные смещения по длинам <c>piece</c>. </summary>
    private static int[] BuildOffsetsFromPieceLengths(int[] pieceLengths, int textLength)
    {
        var offsets = new int[pieceLengths.Length + 1];
        var position = 0;
        for (var i = 0; i < pieceLengths.Length; i++)
        {
            offsets[i] = position;
            position += pieceLengths[i];
        }

        offsets[pieceLengths.Length] = textLength;
        if (position != textLength)
            throw new InvalidOperationException(
                "Сумма длин piece не совпадает с длиной исходного текста.");

        return offsets;
    }

    /// <summary> Приводит один <c>piece</c> к виду, совместимому с исходной строкой. </summary>
    private static string NormalizePiece(string piece)
    {
        if (piece.Length == 0)
            return piece;

        if (piece[0] == '\u2581')
            piece = ' ' + piece[1..];

        return TryUnescapeByteToken(piece, out var unescaped) ? unescaped : piece;
    }

    /// <summary> Разбирает литерал вида <c>&lt;0xHH&gt;</c> в один символ. </summary>
    private static bool TryUnescapeByteToken(string piece, out string unescaped)
    {
        unescaped = string.Empty;
        if (piece.Length != 6 || !piece.StartsWith("<0x", StringComparison.Ordinal) || piece[^1] != '>')
            return false;

        if (!byte.TryParse(piece.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return false;

        unescaped = ((char)value).ToString();
        return true;
    }
}
