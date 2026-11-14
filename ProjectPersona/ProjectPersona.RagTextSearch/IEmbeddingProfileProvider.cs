using ProjectPersona.RagTextSearch.Manifest;

namespace ProjectPersona.RagTextSearch;

/// <summary> Источник описания текущего эмбеддера для сверки с манифестом хранилища. </summary>
public interface IEmbeddingProfileProvider
{
    /// <summary> Возвращает описание текущего эмбеддера. </summary>
    /// <param name="ct"> Токен отмены до начала загрузки модели. </param>
    /// <returns> Отпечаток, параметры построения и шаблон поиска. </returns>
    /// <remarks> Может загрузить модель, если она ещё не загружена. </remarks>
    EmbeddingProfile GetProfile(CancellationToken ct = default);
}
