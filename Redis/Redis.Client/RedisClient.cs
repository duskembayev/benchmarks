using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NRedisStack;
using NRedisStack.RedisStackCommands;
using NRedisStack.Search;
using NRedisStack.Search.Literals.Enums;
using StackExchange.Redis;

namespace Redis.Client;

public class RedisClient<T> : IDisposable, IAsyncDisposable
    where T : notnull
{
    private const string DefaultPath = "$";
    private readonly string _indexName;
    private readonly string _keyPrefix;
    private readonly string _typeName;

    private ConnectionMultiplexer? _connectionMultiplexer;
    private IDatabase? _database;
    private JsonCommands? _jsonCommands;
    private SearchCommands? _searchCommands;

    public RedisClient()
    {
        _keyPrefix = $"{_typeName}:";
        _indexName = $"idx:{_typeName}";
        _typeName = typeof(T).Name.ToLower();
    }

    [MemberNotNullWhen(true, nameof(_connectionMultiplexer))]
    [MemberNotNullWhen(true, nameof(_database))]
    [MemberNotNullWhen(true, nameof(_jsonCommands))]
    [MemberNotNullWhen(true, nameof(_searchCommands))]
    private bool IsConnected => _connectionMultiplexer != null;

    public JsonSerializerOptions SerializerOptions { get; } = new();

    public async ValueTask DisposeAsync()
    {
        if (_connectionMultiplexer != null)
            await _connectionMultiplexer.DisposeAsync();
    }

    public void Dispose()
    {
        _connectionMultiplexer?.Dispose();
    }

    public async Task ConnectAsync(string configuration)
    {
        _connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(configuration);

        _database = _connectionMultiplexer.GetDatabase();
        _jsonCommands = _database.JSON();
        _searchCommands = _database.FT();
    }

    public async Task RestoreIndexAsync(Schema schema)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Redis");

        var ftCreateParams = FTCreateParams.CreateParams()
            .On(IndexDataType.JSON)
            .AddPrefix(_keyPrefix);

        var indexes = await _searchCommands._ListAsync();

        if (indexes.Any(index => (string?) index == _indexName))
            await _searchCommands.DropIndexAsync(_indexName, true);

        await _searchCommands.CreateAsync(_indexName, ftCreateParams, schema);
    }

    public async Task<bool> SetAsync(string key, T value)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Redis");

        return await _jsonCommands.SetAsync(RedisKey(key), DefaultPath, value, serializerOptions: SerializerOptions);
    }

    public async Task<T?> GetAsync(string key)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Redis");

        return await _jsonCommands.GetAsync<T>(RedisKey(key), serializerOptions: SerializerOptions);
    }

    public async Task<bool> DeleteAsync(string key)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Redis");

        return await _jsonCommands.DelAsync(RedisKey(key)) > 0;
    }

    public async Task<IReadOnlyCollection<T>> SearchAsync(string queryString, int offset = 0, int count = 100,
        string? sortBy = null, bool? sortAscending = null)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Redis");

        var query = new Query(queryString).Limit(offset, count);

        if (sortBy != null)
        {
            query.SortBy = sortBy;
            query.SortAscending = sortAscending;
        }
        
        var searchResult = await _searchCommands.SearchAsync(_indexName, query);
        var list = new List<T>((int) searchResult.TotalResults);

        foreach (var document in searchResult.Documents)
        {
            var value = (ReadOnlyMemory<byte>) document["json"];
            var item = JsonSerializer.Deserialize<T>(value.Span, SerializerOptions);

            if (item is null)
                continue;

            list.Add(item);
        }

        return list;
    }

    private RedisKey RedisKey(string key)
    {
        return string.Concat(_keyPrefix, key);
    }
}