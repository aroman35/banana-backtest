using Hangfire.Dashboard;

namespace Banana.Backtest.CryptoConverter.Scheduler;

public class EmptyDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public static readonly EmptyDashboardAuthorizationFilter Instance = new();

    private EmptyDashboardAuthorizationFilter()
    {
    }

    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}
