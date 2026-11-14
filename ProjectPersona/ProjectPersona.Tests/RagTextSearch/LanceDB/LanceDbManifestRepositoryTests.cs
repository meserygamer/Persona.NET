using System.ComponentModel.DataAnnotations;
using ProjectPersona.RagTextSearch.LanceDB;
using ProjectPersona.RagTextSearch.LanceDB.Data;
using ProjectPersona.RagTextSearch.Manifest;
using ProjectPersona.RagTextSearch.Utils;
using ProjectPersona.Tests.RagTextSearch.Manifest;

namespace ProjectPersona.Tests.RagTextSearch.LanceDB;

public sealed class LanceDbManifestRepositoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "manifest-tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<IDisposable> _disposables = [];

    [Fact]
    public async Task Read_EmptyDatabase_ReturnsNull()
    {
        var repository = CreateRepository("main");

        Assert.Null(await repository.ReadAsync());
    }

    [Fact]
    public async Task Write_ThenRead_ReturnsManifest_AndReplacesPrevious()
    {
        var repository = CreateRepository("main");
        await repository.WriteAsync(ManifestTestData.Manifest());
        await repository.WriteAsync(ManifestTestData.Manifest() with { VectorDimension = 42 });

        var manifest = await repository.ReadAsync();

        Assert.NotNull(manifest);
        Assert.Equal(42, manifest.VectorDimension);
        Assert.Equal(ManifestTestData.Profile().Fingerprint, manifest.EmbeddingFingerprint);
    }

    [Fact]
    public async Task Tables_HaveSeparateRows()
    {
        var first = CreateRepository("first");
        var second = CreateRepository("second");
        await first.WriteAsync(ManifestTestData.Manifest() with { VectorDimension = 1 });
        await second.WriteAsync(ManifestTestData.Manifest() with { VectorDimension = 2 });

        await first.DeleteAsync();

        Assert.Null(await first.ReadAsync());
        Assert.Equal(2, (await second.ReadAsync())!.VectorDimension);
    }

    [Fact]
    public async Task Update_AppliesChange()
    {
        var repository = CreateRepository("main");
        await repository.WriteAsync(ManifestTestData.Manifest());

        await repository.UpdateAsync(manifest => manifest.WithUpdating([("A", null)]));

        var updated = await repository.ReadAsync();
        Assert.Equal(VectorStoreState.Updating, updated!.State);
        Assert.Equal([new PendingAssembly("A", null)], updated.PendingAssemblies);
    }

    [Fact]
    public async Task Update_WithoutManifest_Throws()
    {
        var repository = CreateRepository("main");

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await repository.UpdateAsync(manifest => manifest));
    }

    [Fact]
    public void Settings_ReservedTableName_FailsValidation()
    {
        var settings = new LanceDbSettings { TableName = LanceDbManifestRepository.MetaTableName };

        Assert.Throws<ValidationException>(
            () => Validator.ValidateObject(settings, new ValidationContext(settings), true));
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
            disposable.Dispose();

        if (Directory.Exists(_path))
            Directory.Delete(_path, recursive: true);
    }

    private LanceDbManifestRepository CreateRepository(string tableName)
    {
        var settings = new LanceDbSettings { PathToLanceDb = _path, TableName = tableName };
        var connections = new LanceDbConnectionProvider(settings);
        var accessLock = new AsyncReaderWriterLock();
        _disposables.Add(connections);
        _disposables.Add(accessLock);
        return new LanceDbManifestRepository(settings, connections, accessLock);
    }
}
