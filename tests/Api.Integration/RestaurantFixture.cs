using BusinessLogic.Models;
using Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Api.Integration;

public sealed class RestaurantFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = null!;
    public RestaurantApiFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    private bool _databaseCreated;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("RESTAURANT_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "Real SQL Server is required. Run 'docker compose --profile tests run --build --rm tests' " +
                "or set RESTAURANT_TEST_CONNECTION_STRING (see README).");

        // Always create and remove a uniquely named test database, never the configured database.
        var connection = new SqlConnectionStringBuilder(configured)
        {
            InitialCatalog = "RestaurantSeatingTests_" + Guid.NewGuid().ToString("N")
        };
        ConnectionString = connection.ConnectionString;
        await using var database = CreateContext();
        await database.Database.MigrateAsync();
        _databaseCreated = true;
        Factory = new RestaurantApiFactory(ConnectionString);
        Client = Factory.CreateClient();
    }

    public RestaurantDbContext CreateContext() => new(
        new DbContextOptionsBuilder<RestaurantDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task ResetAsync(params int[] capacities)
    {
        await using var database = CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        await database.Groups.ExecuteDeleteAsync();
        await database.Tables.ExecuteDeleteAsync();
        database.Tables.AddRange(capacities.Select((capacity, index) => new RestaurantTable(index + 1, capacity)));
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (Factory is not null)
            await Factory.DisposeAsync();
        if (_databaseCreated)
        {
            await using var database = CreateContext();
            await database.Database.EnsureDeletedAsync();
        }
    }
}
