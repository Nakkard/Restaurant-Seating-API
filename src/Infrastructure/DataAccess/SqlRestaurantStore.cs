using System.Data;
using BusinessLogic.InfrastructureInterfaces;
using BusinessLogic.Models;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.DataAccess;

public sealed class SqlRestaurantStore(RestaurantDbContext database) : IRestaurantStore
{
    public async Task<IRestaurantChange> BeginChangeAsync(CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            await RestaurantLock.AcquireAsync(database, cancellationToken);
            // All state is loaded after acquiring the lock. Historical visits are excluded.
            var tables = await database.Tables.AsNoTracking().OrderBy(table => table.Id).ToListAsync(cancellationToken);
            var groups = await database.Groups
                .Where(group => group.Status == GroupStatus.Waiting || group.Status == GroupStatus.Seated)
                .OrderBy(group => group.ArrivalOrder)
                .ToListAsync(cancellationToken);
            return new RestaurantChange(database, transaction, tables, groups);
        }
        catch
        {
            await transaction.DisposeAsync();
            database.ChangeTracker.Clear();
            throw;
        }
    }

    private sealed class RestaurantChange(
        RestaurantDbContext database,
        IDbContextTransaction transaction,
        IReadOnlyList<RestaurantTable> tables,
        List<GuestGroup> groups) : IRestaurantChange
    {
        public IReadOnlyList<RestaurantTable> Tables => tables;
        public IReadOnlyList<GuestGroup> Groups => groups;

        public async Task RegisterAsync(GuestGroup group, CancellationToken cancellationToken)
        {
            database.Groups.Add(group);
            // Allocate the arrival sequence inside the lock; this insert is still uncommitted.
            await database.SaveChangesAsync(cancellationToken);
            groups.Add(group);
        }

        public async Task<GuestGroup?> FindGroupAsync(Guid id, CancellationToken cancellationToken) =>
            groups.FirstOrDefault(group => group.Id == id)
            ?? await database.Groups.SingleOrDefaultAsync(group => group.Id == id, cancellationToken);

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            // Disposing an uncommitted transaction rolls back registration and seating together.
            await transaction.DisposeAsync();
            database.ChangeTracker.Clear();
        }
    }
}
