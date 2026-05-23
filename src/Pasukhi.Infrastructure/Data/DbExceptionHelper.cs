using Microsoft.EntityFrameworkCore;

namespace Pasukhi.Infrastructure.Data;

internal static class DbExceptionHelper
{
    // Npgsql PostgresException.SqlState "23505" = unique_violation.
    internal static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        while (inner is not null)
        {
            var sqlState = inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string;
            if (sqlState == "23505") return true;
            inner = inner.InnerException;
        }
        return false;
    }
}
