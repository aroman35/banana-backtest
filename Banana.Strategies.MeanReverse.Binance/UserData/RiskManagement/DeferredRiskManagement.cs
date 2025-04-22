namespace Banana.Strategies.MeanReverse.Binance.UserData.RiskManagement;

public class DeferredRiskManagement(IServiceScopeFactory scopeFactory)
{
    public async Task Execute(CreateRiskManagementCommand command, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var riskManagement = scope.ServiceProvider.GetRequiredService<RiskManager>();
        riskManagement.Configure(command, cancellationToken);
        await riskManagement.Completion;
    }
}
