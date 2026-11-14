using ProjectPersona.RagTextSearch.Llama.TextSplitter;

namespace ProjectPersona.Tests.RagTextSearch.Llama.TextSplitter;

/// <summary> <see cref="TokenWindowPlanner.PlanWindows"/>: покрытие без дыр и перекрытие. </summary>
public class TokenWindowPlannerTests
{
    /// <summary> Нулевое число токенов — пустой план. </summary>
    [Fact]
    public void PlanWindows_ReturnsEmpty_WhenTokenCountIsZero()
    {
        Assert.Empty(TokenWindowPlanner.PlanWindows(0, 10, 0));
    }

    /// <summary> Токенов не больше окна — одно окно на весь диапазон. </summary>
    [Theory]
    [InlineData(1, 10)]
    [InlineData(10, 10)]
    public void PlanWindows_ReturnsSingleWindow_WhenTokenCountWithinWindow(int tokenCount, int window)
    {
        var windows = TokenWindowPlanner.PlanWindows(tokenCount, window, 0);
        Assert.Equal([(0, tokenCount)], windows);
    }

    /// <summary> Без перекрытия окна стыкуются и покрывают все индексы. </summary>
    [Fact]
    public void PlanWindows_CoversAllTokens_WithoutOverlap()
    {
        const int n = 100;
        const int w = 30;
        var windows = TokenWindowPlanner.PlanWindows(n, w, 0);
        Assert.True(IsFullCoverage(n, windows));
        Assert.Equal((n - w, n), windows[^1]);
    }

    /// <summary> Регрессия: старый алгоритм оставлял дыры при четырёх и более окнах. </summary>
    [Fact]
    public void PlanWindows_CoversAllTokens_WhenManyWindowsRequired()
    {
        const int n = 100;
        const int w = 30;
        var windows = TokenWindowPlanner.PlanWindows(n, w, 0);
        Assert.True(windows.Count >= 4);
        Assert.True(IsFullCoverage(n, windows));
    }

    /// <summary> С перекрытием соседние окна пересекаются не меньше чем на <c>O</c> (кроме хвоста). </summary>
    [Fact]
    public void PlanWindows_OverlapsAdjacentWindows_ByAtLeastOverlap()
    {
        const int n = 100;
        const int w = 30;
        const int overlap = 10;
        var windows = TokenWindowPlanner.PlanWindows(n, w, overlap);

        for (var i = 1; i < windows.Count - 1; i++)
        {
            var prev = windows[i - 1];
            var current = windows[i];
            var intersection = Math.Min(prev.End, current.End) - Math.Max(prev.Start, current.Start);
            Assert.True(intersection >= overlap);
        }
    }

    /// <summary> Последнее окно имеет длину <c>W</c> и заканчивается на <c>N</c>. </summary>
    [Fact]
    public void PlanWindows_LastWindowEndsAtTokenCount_WithFullWidth()
    {
        const int n = 77;
        const int w = 20;
        var windows = TokenWindowPlanner.PlanWindows(n, w, 5);
        var last = windows[^1];
        Assert.Equal(n, last.End);
        Assert.Equal(w, last.End - last.Start);
    }

    /// <summary> Минимальный шаг <c>hop = 1</c> не зацикливается и не дублирует окна. </summary>
    [Fact]
    public void PlanWindows_ProgressesWithHopOne_WhenOverlapIsWindowMinusOne()
    {
        const int n = 15;
        const int w = 5;
        var windows = TokenWindowPlanner.PlanWindows(n, w, w - 1);
        Assert.True(windows.Count > 1);
        Assert.Equal(n, windows[^1].End);
        Assert.Equal(1, windows[1].Start - windows[0].Start);
        Assert.Equal(windows.Distinct().Count(), windows.Count);
    }

    /// <summary> Некорректные параметры окна и перекрытия отклоняются. </summary>
    [Theory]
    [InlineData(10, 0, 0)]
    [InlineData(10, 5, -1)]
    [InlineData(10, 5, 5)]
    [InlineData(10, 5, 10)]
    public void PlanWindows_Throws_WhenParametersInvalid(int tokenCount, int window, int overlap)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            TokenWindowPlanner.PlanWindows(tokenCount, window, overlap));
    }

    private static bool IsFullCoverage(int tokenCount, List<(int Start, int End)> windows)
    {
        var covered = new bool[tokenCount];
        foreach (var (start, end) in windows)
        {
            for (var i = start; i < end; i++)
                covered[i] = true;
        }

        return covered.All(static x => x);
    }
}
