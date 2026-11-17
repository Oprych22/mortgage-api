using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mortgage.Infrastructure.Queue;

namespace Mortgage.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // Trade-off: SQLite so the API runs from a clone with nothing to install.
        // For SQL Server or PostgreSQL, change this line and the connection string.
        services.AddDbContext<MortgageDbContext>(options => options.UseSqlite(connectionString));
        // One instance for the whole app: the endpoints write to it and the worker reads from it.
        services.AddSingleton<IAssessmentQueue, ChannelAssessmentQueue>();
        return services;
    }
}
