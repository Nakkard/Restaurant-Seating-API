namespace BusinessLogic.InfrastructureInterfaces;

public interface IRestaurantStore
{
    Task<IRestaurantChange> BeginChangeAsync(CancellationToken cancellationToken);
}
