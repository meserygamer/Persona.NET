using ProjectPersona.RagTextSearch.Utils;

namespace ProjectPersona.RagTextSearch.Llama.TextSplitter;

/// <summary> Нарезка документа под лимит токенов эмбеддинга с учётом шаблона запроса. </summary>
public class LlamaTextSplitter : ITextSplitter
{
    /// <summary> Токенизатор модели эмбеддинга. </summary>
    private readonly ITextTokenizer _textTokenizer;

    /// <summary> Лимит батча, шаблон документа и параметры overlap/clearance. </summary>
    private readonly LlamaSettings _llamaSettings;

    /// <summary> Создаёт разделитель под лимит <see cref="LlamaSettings.BatchSize"/>. </summary>
    /// <param name="textTokenizer">Токенизатор той же модели, что и эмбеддинг.</param>
    /// <param name="settings">Настройки Llama, включая шаблон документа.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="textTokenizer"/> или <paramref name="settings"/> равны <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Некорректные поля <paramref name="settings"/>.</exception>
    public LlamaTextSplitter(ITextTokenizer textTokenizer, LlamaSettings settings)
    {
        _textTokenizer = textTokenizer
                         ?? throw new ArgumentNullException(nameof(textTokenizer));
        _llamaSettings = settings
                         ?? throw new ArgumentNullException(nameof(settings));
        ValidateSettings();
    }

    /// <summary> Делит текст на фрагменты payload, влезающие в батч после подстановки в шаблон. </summary>
    /// <param name="text">Исходный текст; <see langword="null"/> и пустая строка дают пустой массив.</param>
    /// <param name="title">Заголовок для <c>{0}</c> в шаблоне документа.</param>
    /// <param name="cancellationToken">Токен отмены и обращений к токенизатору.</param>
    /// <returns>Фрагменты исходного текста в порядке следования.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> равен <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Бюджет токенов исчерпан шаблоном или минимальный фрагмент не влезает в лимит.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Токены не сопоставляются с исходной строкой (см. <see cref="TokenTextAligner"/>).
    /// </exception>
    /// <exception cref="OperationCanceledException">Операция отменена.</exception>
    public async Task<string[]> SplitTextAsync(string text, string title, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(title);

        if (string.IsNullOrEmpty(text))
            return [];

        var budget = await ComputeBudgetForTitleAsync(title, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        var tokenCount = await _textTokenizer
            .GetTokenCountAsync(text, addSpecialTokens: false, cancellationToken)
            .ConfigureAwait(false);

        if (tokenCount <= budget.PayloadWindowTokens)
            return [text];

        // Токены с кусками текста нужны только если текст не влезает в окно.
        var payloadTokens = await _textTokenizer
            .TokenizeAsync(text, addSpecialTokens: false, cancellationToken)
            .ConfigureAwait(false);

        var charOffsets = TokenTextAligner.BuildCharOffsets(text, payloadTokens);
        var windows = TokenWindowPlanner.PlanWindows(
            payloadTokens.Length,
            budget.PayloadWindowTokens,
            budget.OverlapTokens);

        var chunks = new string[windows.Count];
        for (var i = 0; i < windows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (start, end) = windows[i];
            chunks[i] = await FitChunkToBudgetAsync(
                text,
                charOffsets,
                start,
                end,
                title,
                budget,
                cancellationToken).ConfigureAwait(false);
        }

        return chunks;
    }

    /// <summary> Подбирает правую границу окна токенов, чтобы обёрнутый фрагмент влез в батч. </summary>
    private async Task<string> FitChunkToBudgetAsync(
        string text,
        int[] charOffsets,
        int tokenStart,
        int tokenEnd,
        string title,
        TitlePayloadBudget budget,
        CancellationToken cancellationToken)
    {
        var maxTokenCount = _llamaSettings.BatchSize - budget.ClearanceTokensCount;
        var low = tokenStart + 1;
        var high = tokenEnd;

        while (low < high)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mid = (low + high + 1) / 2;
            var candidate = TokenTextAligner.Slice(text, charOffsets, tokenStart, mid);
            if (await GetWrappedTokenCountAsync(title, candidate, cancellationToken).ConfigureAwait(false) <= maxTokenCount)
                low = mid;
            else
                high = mid - 1;
        }

        var chunk = TokenTextAligner.Slice(text, charOffsets, tokenStart, low);
        if (await GetWrappedTokenCountAsync(title, chunk, cancellationToken).ConfigureAwait(false) > maxTokenCount)
            ThrowBudgetExceeded(budget);

        return chunk;
    }

    /// <summary> Считает токены строки после подстановки фрагмента в шаблон документа. </summary>
    private async Task<long> GetWrappedTokenCountAsync(
        string title,
        string chunk,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var wrapped = _llamaSettings.FormatDocument(title, chunk);
        return await _textTokenizer
            .GetTokenCountAsync(wrapped, addSpecialTokens: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Заполняет размер окна payload, запас и перекрытие для конкретного заголовка. </summary>
    private async Task<TitlePayloadBudget> ComputeBudgetForTitleAsync(
        string title,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var wrapper = _llamaSettings.FormatDocument(title, string.Empty);

        var wrapperTokenCount = await _textTokenizer
            .GetTokenCountAsync(wrapper, addSpecialTokens: true, cancellationToken)
            .ConfigureAwait(false);
        var clearanceTokensCount = _llamaSettings.FreeSpaceClearance ?? _llamaSettings.BatchSize / 20;

        if (wrapperTokenCount + clearanceTokensCount >= _llamaSettings.BatchSize)
        {
            throw new ArgumentException(
                "При текущих параметрах нельзя надёжно разбить текст: шаблон и запас занимают весь батч.\n" +
                $"wrapperTokenCount: {wrapperTokenCount}\n" +
                $"clearance: {clearanceTokensCount}\n" +
                $"BatchSize: {_llamaSettings.BatchSize}\n");
        }

        var payloadWindowTokens = (int)(_llamaSettings.BatchSize - wrapperTokenCount - clearanceTokensCount);
        var overlapTokens = ResolveOverlapTokens(payloadWindowTokens);

        return new TitlePayloadBudget(
            wrapperTokenCount,
            clearanceTokensCount,
            payloadWindowTokens,
            overlapTokens);
    }

    /// <summary> Возвращает перекрытие окон в токенах с учётом <see cref="LlamaSettings.OverlapTokens"/>. </summary>
    private int ResolveOverlapTokens(int payloadWindow)
    {
        if (payloadWindow < 1)
            return 0;

        if (_llamaSettings.OverlapTokens is null)
        {
            var defaultOverlap = payloadWindow / 10;
            return (int)Math.Min(defaultOverlap, payloadWindow - 1);
        }

        return (int)Math.Clamp(_llamaSettings.OverlapTokens.Value, 0, payloadWindow - 1);
    }

    /// <summary> Сообщает, что даже минимальный фрагмент не помещается в <see cref="LlamaSettings.BatchSize"/>. </summary>
    private void ThrowBudgetExceeded(TitlePayloadBudget budget)
    {
        throw new ArgumentException(
            "При текущих параметрах нельзя надёжно разбить текст: даже минимальный фрагмент не влезает в батч.\n" +
            $"wrapperTokenCount: {budget.WrapperTokenCount}\n" +
            $"clearance: {budget.ClearanceTokensCount}\n" +
            $"BatchSize: {_llamaSettings.BatchSize}\n");
    }

    /// <summary> Проверяет шаблон и числовые поля настроек в конструкторе. </summary>
    private void ValidateSettings()
    {
        var argumentsCount = StringUtils.GetFormatArgumentsCount(_llamaSettings.DocumentQueryTemplate);

        if (argumentsCount != 2)
            throw new ArgumentException(
                $"Шаблон {nameof(LlamaSettings.DocumentQueryTemplate)} должен содержать ровно два аргумента формата.");

        if (_llamaSettings.BatchSize <= 0)
            throw new ArgumentException($"{nameof(LlamaSettings.BatchSize)} должен быть больше нуля.");

        if (_llamaSettings.FreeSpaceClearance is not null && _llamaSettings.FreeSpaceClearance < 0)
            throw new ArgumentException($"{nameof(LlamaSettings.FreeSpaceClearance)} не может быть отрицательным.");

        if (_llamaSettings.OverlapTokens is not null && _llamaSettings.OverlapTokens < 0)
            throw new ArgumentException($"{nameof(LlamaSettings.OverlapTokens)} не может быть отрицательным.");
    }

    /// <summary> Бюджет нарезки payload для одного заголовка документа. </summary>
    private readonly record struct TitlePayloadBudget(
        long WrapperTokenCount,
        long ClearanceTokensCount,
        int PayloadWindowTokens,
        int OverlapTokens);
}
