namespace ProjectPersona.RagTextSearch;

/// <summary> Сплиттер большого текста на окна для векторизации. </summary>
public interface ITextSplitter
{
    /// <summary> Разбить текст под лимит токенов с учётом заголовка в шаблоне документа. </summary>
    /// <param name="text"> Исходный текст фрагмента. </param>
    /// <param name="title"> Заголовок для подстановки в шаблон (<c>{0}</c>). </param>
    /// <param name="cancellationToken"> Токен отмены. </param>
    /// <returns> Окна разбивки текста. </returns>
    Task<string[]> SplitTextAsync(string text, string title, CancellationToken cancellationToken = default);
}
