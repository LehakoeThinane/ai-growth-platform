using AiGrowthPlatform.Business.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using System.Runtime.CompilerServices;

namespace AiGrowthPlatform.Api.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AI_GROWTH_TEST_POSTGRES")))
            Skip = "Set AI_GROWTH_TEST_POSTGRES to enable tests in a new temporary PostgreSQL database.";
    }
}

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string _database = "aigrowth_test_" + Guid.NewGuid().ToString("N");
    private string? _server;
    private bool _created;
    public string ConnectionString { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("AI_GROWTH_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured)) return;
        _server = new NpgsqlConnectionStringBuilder(configured) { Pooling = false, Timeout = 5 }.ConnectionString;
        await using var connection = new NpgsqlConnection(_server);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        // The identifier is generated exclusively from a constant and Guid N-format; no external SQL identifiers.
        await using var create = new NpgsqlCommand($"CREATE DATABASE {_database}", connection);
        await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        _created = true;
        ConnectionString = new NpgsqlConnectionStringBuilder(_server) { Database = _database }.ConnectionString;
        await using var db = CreateDb();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public BusinessDbContext CreateDb() => new(new DbContextOptionsBuilder<BusinessDbContext>().UseNpgsql(ConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        if (!_created) return;
        // Only the uniquely named database created by this fixture is removed.
        await using var connection = new NpgsqlConnection(_server);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE {_database} WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }
}
