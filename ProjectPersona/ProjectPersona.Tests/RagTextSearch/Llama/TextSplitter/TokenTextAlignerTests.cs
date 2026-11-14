using ProjectPersona.RagTextSearch.Llama.TextSplitter;
using ProjectPersona.RagTextSearch.Llama.Tokenizer;

namespace ProjectPersona.Tests.RagTextSearch.Llama.TextSplitter;

/// <summary> <see cref="TokenTextAligner"/>: срезы по токенам совпадают с исходной строкой. </summary>
public class TokenTextAlignerTests
{
    /// <summary> Прямая склейка <c>piece</c> совпадает с текстом. </summary>
    [Fact]
    public void BuildCharOffsets_SlicesMatchText_WhenPiecesConcatToText()
    {
        const string text = "alpha-beta";
        var tokens = TokenizePerChar(text);
        var offsets = TokenTextAligner.BuildCharOffsets(text, tokens);

        Assert.Equal(text, TokenTextAligner.Slice(text, offsets, 0, tokens.Length));
        Assert.Equal("alpha", TokenTextAligner.Slice(text, offsets, 0, 5));
        Assert.Equal("beta", TokenTextAligner.Slice(text, offsets, 6, 10));
    }

    /// <summary> SentencePiece <c>▁</c> и байтовый <c>&lt;0x0A&gt;</c> нормализуются к пробелу и переводу строки. </summary>
    [Fact]
    public void BuildCharOffsets_NormalizesSentencePieceMarkers()
    {
        const string text = " \n";
        var tokens = new[]
        {
            new LlamaModelToken { Id = 0, Piece = "\u2581" },
            new LlamaModelToken { Id = 1, Piece = "<0x0A>" }
        };

        var offsets = TokenTextAligner.BuildCharOffsets(text, tokens);
        Assert.Equal(text, TokenTextAligner.Slice(text, offsets, 0, tokens.Length));
    }

    /// <summary> Несовместимые <c>piece</c> и текст — исключение. </summary>
    [Fact]
    public void BuildCharOffsets_Throws_WhenPiecesDoNotReconstructText()
    {
        var tokens = new[]
        {
            new LlamaModelToken { Id = 0, Piece = "abc" }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TokenTextAligner.BuildCharOffsets("xyz", tokens));

        Assert.Contains("восстановить исходный текст", ex.Message);
    }

    private static LlamaModelToken[] TokenizePerChar(string text)
    {
        var tokens = new LlamaModelToken[text.Length];
        for (var i = 0; i < text.Length; i++)
            tokens[i] = new LlamaModelToken { Id = i, Piece = text[i].ToString() };
        return tokens;
    }
}
