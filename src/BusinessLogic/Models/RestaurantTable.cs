namespace BusinessLogic.Models;

public sealed class RestaurantTable
{
    private RestaurantTable() { }

    public RestaurantTable(int id, int capacity)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id));
        if (capacity is < 2 or > 6)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Id = id;
        Capacity = capacity;
    }

    public int Id { get; private set; }
    public int Capacity { get; private set; }
}
