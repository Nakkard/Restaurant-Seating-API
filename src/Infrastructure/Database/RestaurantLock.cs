using System.Data;
using BusinessLogic.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

internal static class RestaurantLock
{
    public static async Task AcquireAsync(RestaurantDbContext database, CancellationToken cancellationToken)
    {
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await database.Database.ExecuteSqlRawAsync("""
            EXEC @result = sys.sp_getapplock
                @Resource = N'RestaurantSeating:State',
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 5000;
            """, new object[] { result }, cancellationToken);

        // Negative return codes do not necessarily roll back the transaction themselves.
        if ((int)result.Value < 0)
            throw new RestaurantBusyException();
    }
}
