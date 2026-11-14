namespace ProjectPersona.LlamaWrapper;

/// <summary>
/// Токен словаря Llama: числовой идентификатор и кусок текста, который он занимает.
/// </summary>
/// <param name="Id">Идентификатор токена в словаре модели.</param>
/// <param name="Piece">
/// Кусок исходного текста. Если байты токенов склеиваются в UTF-8 строки, это её срез;
/// иначе декодированный <c>llama_token_to_piece</c> (одиночный байт вне UTF-8 — <c>&lt;0xHH&gt;</c>).
/// </param>
public readonly record struct LlamaTokenPiece(int Id, string Piece);
