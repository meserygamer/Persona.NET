namespace ProjectPersona.RagTextSearch.Manifest;

/// <summary> Описание текущего эмбеддера для сверки с манифестом. </summary>
/// <param name="Fingerprint">Жёсткая часть отпечатка.</param>
/// <param name="BuildParameters">Мягкие параметры построения.</param>
/// <param name="DocumentQueryTemplate">Шаблон промпта документа.</param>
/// <param name="SearchQueryTemplate">Шаблон поискового запроса.</param>
public sealed record EmbeddingProfile(
    EmbeddingFingerprint Fingerprint,
    EmbeddingBuildParameters BuildParameters,
    string DocumentQueryTemplate,
    string SearchQueryTemplate);
