using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mortgage.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // Trade-off: SQLite so the API runs from a clone with nothing to install.
        // For SQL Server or PostgreSQL, change this line and the connection string.
        services.AddDbContext<MortgageDbContext>(options => options.UseSqlite(connectionString));
        return services;
    }
}
