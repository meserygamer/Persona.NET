namespace ProjectPersona.Application.Services.RagSearchService.Pipes.Dto;

/// <summary> Пачка данных вместе с границей объявлений, которые в ней разобраны. </summary>
/// <param name="DeclarationEnd">
/// Число объявлений с начала индекса, чьи фрагменты уже в этой пачке или в предыдущих.
/// </param>
/// <param name="Items">Данные пачки.</param>
public readonly record struct DeclarationBatch<T>(int DeclarationEnd, IReadOnlyCollection<T> Items);
