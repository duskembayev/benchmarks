using BenchmarkDotNet.Attributes;
using Bogus;
using Redis.Client;

namespace Producer;

public class Benchmark
{
    private readonly RedisClient<Account> _redisClient = new()
    {
        SerializerOptions =
        {
            Converters = {new JsonUnixTimeSecondsConverter()}
        }
    };

    private readonly Randomizer _randomizer = new(57892);

    [Params(100, 1_000, 10_000, 100_000, 500_000, 1_000_000)] public int AccountCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        await _redisClient.ConnectAsync("localhost:6379");
        await _redisClient.RestoreIndexAsync(Account.Schema);
        await GenerateAccountsAsync(AccountCount);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _redisClient.DisposeAsync();
    }

    [Benchmark]
    public async Task<Account?> Get()
    {
        var accountId = _randomizer.Number(1, AccountCount);
        return await _redisClient.GetAsync(accountId.ToString());
    }

    [Benchmark]
    public async Task<Account?> Update()
    {
        var accountId = _randomizer.Number(1, AccountCount);
        var amount = _randomizer.Decimal(-10m, 10m);
        var account = await _redisClient.GetAsync(accountId.ToString());

        account = account with
        {
            Balance = account.Balance + amount
        };

        await _redisClient.SetAsync(accountId.ToString(), account);
        return account;
    }
    
    [Benchmark]
    [ArgumentsSource(nameof(SearchArguments))]
    public async Task<IReadOnlyCollection<Account>> Search(string queryString, int offset, int count, string sortBy, bool? sortAscending)
    {
        return await _redisClient.SearchAsync(queryString, offset, count, sortBy, sortAscending);
    }
    
    public static IEnumerable<object[]> SearchArguments()
    {
        yield return ["@department:Toys", 50, 50, "name", true];
        yield return ["@balance:[10 100]", 50, 50, "balance", false];
        yield return ["@balance:[10 100]", 50, 50, "balance", null];
        yield return ["", 0, 1000, "balance", false];
    }

    private async Task GenerateAccountsAsync(int count)
    {
        var faker = new Faker<Account>()
            .UseSeed(56475)
            .CustomInstantiator(f => new Account(
                ++f.IndexVariable,
                f.Name.FullName(),
                f.Commerce.Department(1),
                f.Finance.Iban(true),
                f.Finance.Amount(),
                f.Date.PastOffset(10)));

        for (var i = 0; i < count; i++)
        {
            var account = faker.Generate();
            await _redisClient.SetAsync(account.Id.ToString(), account);
        }
    }
}