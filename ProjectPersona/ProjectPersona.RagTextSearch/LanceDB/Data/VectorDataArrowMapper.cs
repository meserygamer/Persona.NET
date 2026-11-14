using System.ComponentModel.DataAnnotations;
using System.Text;
using Apache.Arrow;
using Apache.Arrow.Types;
using ProjectPersona.RagTextSearch.LanceDB.Structure;

namespace ProjectPersona.RagTextSearch.LanceDB.Data;

/// <summary> Проверка и преобразование <see cref="VectorData"/> в Arrow и обратно. Без доступа к БД и блокировок. </summary>
public class VectorDataArrowMapper
{
    /// <summary> Настройки LanceDB. </summary>
    private readonly LanceDbSettings _settings;
    /// <summary> Схема таблицы. </summary>
    private readonly LanceDbTableManager _tableManager;

    /// <summary> Создать преобразователь записей. </summary>
    /// <param name="settings"> Настройки LanceDB. </param>
    /// <param name="tableManager"> Менеджер таблицы. </param>
    /// <exception cref="ArgumentNullException"> tableSchema равен null. </exception>
    public VectorDataArrowMapper(
        LanceDbSettings settings,
        LanceDbTableManager tableManager)
    {
        _settings = settings 
                    ?? throw new ArgumentNullException(nameof(settings));
        _tableManager = tableManager
                       ?? throw new ArgumentNullException(nameof(tableManager));
        
        Validator.ValidateObject(settings, new ValidationContext(settings), true);
    }

    /// <summary> Гарантировать валидность записей. </summary>
    /// <param name="records"> Записи на проверку. </param>
    /// <exception cref="ArgumentException"> Пустые метаданные, null-вектор или неверная длина вектора. </exception>
    public void EnsureValidRecords(IEnumerable<VectorData> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var vectorDimension = _settings.VectorDimension;

        foreach (var record in records)
        {
            if (string.IsNullOrEmpty(record.Metadata))
                throw new ArgumentException("Метаданные вектора не могут быть пустыми.", nameof(records));

            if (string.IsNullOrEmpty(record.Assembly))
                throw new ArgumentException(
                    $"Код сборки вектора с метаданными «{record.Metadata}» не может быть пустым.",
                    nameof(records));

            if (string.IsNullOrEmpty(record.TargetFramework))
                throw new ArgumentException(
                    $"TFM вектора с метаданными «{record.Metadata}» не может быть пустым.",
                    nameof(records));

            if (record.Vector is null)
                throw new ArgumentException(
                    $"Вектор с метаданными «{record.Metadata}» равен null.",
                    nameof(records));

            if (record.Vector.Length != vectorDimension)
                throw new ArgumentException(
                    $"Вектор с метаданными «{record.Metadata}» имеет длину {record.Vector.Length}; ожидается {vectorDimension}.",
                    nameof(records));
        }
    }

    /// <summary> Упаковать записи в <see cref="RecordBatch"/>. </summary>
    /// <param name="records"> Список записей. </param>
    /// <param name="offset"> Отступ от начала списка. </param>
    /// <param name="count"> Число записей. </param>
    /// <returns> <see cref="RecordBatch"/> по выделенному промежутку. </returns>
    public RecordBatch Pack(IReadOnlyList<VectorData> records, int offset, int count)
    {
        var assemblyBuilder = new StringArray.Builder();
        var targetFrameworkBuilder = new StringArray.Builder();
        var metadataBuilder = new StringArray.Builder();
        var itemField = new Field("item", FloatType.Default, nullable: false);
        var listBuilder = new FixedSizeListArray.Builder(itemField, _settings.VectorDimension);
        var floatBuilder = (FloatArray.Builder)listBuilder.ValueBuilder;

        for (var i = 0; i < count; i++)
        {
            var entry = records[offset + i];
            assemblyBuilder.Append(entry.Assembly);
            targetFrameworkBuilder.Append(entry.TargetFramework);
            metadataBuilder.Append(entry.Metadata);
            listBuilder.Append();
            floatBuilder.Append(entry.Vector);
        }

        return new RecordBatch(
            _tableManager.Schema,
            [assemblyBuilder.Build(), targetFrameworkBuilder.Build(), metadataBuilder.Build(), listBuilder.Build()],
            count);
    }

    /// <summary> Распаковать <see cref="RecordBatch"/> в массив записей. </summary>
    /// <param name="recordBatch"> Батч записей. </param>
    /// <returns> Массив записей из батча. </returns>
    /// <exception cref="ApplicationException"> Колонка не найдена или имеет другой тип. </exception>
    public VectorData[] Unpack(RecordBatch recordBatch)
    {
        ArgumentNullException.ThrowIfNull(recordBatch);
        var vectorDimension = _settings.VectorDimension;

        var listArray = recordBatch.Column("vector") as FixedSizeListArray;
        var valuesArray = listArray?.Values as FloatArray;
        var metadataColumn = recordBatch.Column("metadata") as StringArray;
        var assemblyColumn = recordBatch.Column("assembly") as StringArray;
        var targetFrameworkColumn = recordBatch.Column("target_framework") as StringArray;

        if (valuesArray is null)
            throw new ApplicationException("Column vector was not found or is not Float[]!");

        if (metadataColumn is null)
            throw new ApplicationException("Column metadata was not found!");

        if (assemblyColumn is null)
            throw new ApplicationException("Column assembly was not found!");

        if (targetFrameworkColumn is null)
            throw new ApplicationException("Column target_framework was not found!");

        var rowCount = recordBatch.Length;
        var result = new VectorData[rowCount];

        for (var i = 0; i < rowCount; i++)
        {
            var offset = i * vectorDimension;
            var vector = new float[vectorDimension];

            for (var j = 0; j < vectorDimension; j++)
                vector[j] = valuesArray.GetValue(offset + j)!.Value;

            result[i] = new VectorData
            {
                Vector = vector,
                Assembly = assemblyColumn.GetString(i, Encoding.UTF8),
                TargetFramework = targetFrameworkColumn.GetString(i, Encoding.UTF8),
                Metadata = metadataColumn.GetString(i, Encoding.UTF8)
            };
        }

        return result;
    }
}
