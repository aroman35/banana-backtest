namespace Banana.Backtest.Emulator.ExchangeEmulator;

public interface IStrategy : IEmulatorGateway
{
    void PlaceOrder(UserOrder order);
    void SimulationFinished();
}