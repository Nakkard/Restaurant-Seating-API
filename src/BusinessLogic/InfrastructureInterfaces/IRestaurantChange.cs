using BusinessLogic.Models;

namespace BusinessLogic.InfrastructureInterfaces;

// A locked, transactional view of the active restaurant state.
public interface IRestaurantChange : IAsyncDisposable
{
    IReadOnlyList<RestaurantTable> Tables { get; }
    IReadOnlyList<GuestGroup> Groups { get; }
    Task RegisterAsync(GuestGroup group, CancellationToken cancellationToken);
    Task<GuestGroup?> FindGroupAsync(Guid id, CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
}
