namespace BusinessLogic.Models;

public sealed record TableResult(int Id, int Capacity, int OccupiedSeats)
{
    public int AvailableSeats => Capacity - OccupiedSeats;
}
