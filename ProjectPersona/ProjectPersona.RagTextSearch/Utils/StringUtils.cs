using System.Text;

namespace ProjectPersona.RagTextSearch.Utils;

/// <summary> Утилиты работы со строками. </summary>
public static class StringUtils
{
    /// <summary> Получить количество аргументов в форматной строке. </summary>
    /// <param name="s"> Форматная строка. </param>
    /// <returns> Количество аргументов в форматной строке (Или 0, если строка не форматная). </returns>
    public static int GetFormatArgumentsCount(string s)
    {
        try
        {
            var cf = CompositeFormat.Parse(s);
            return cf.MinimumArgumentCount;
        }
        catch (FormatException)
        {
            return 0;
        }
    }
}