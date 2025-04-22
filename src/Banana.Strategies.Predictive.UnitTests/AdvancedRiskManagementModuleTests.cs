using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.Contracts;
using Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;
using Serilog;
using Shouldly;

namespace Banana.Strategies.Predictive.UnitTests;

public class AdvancedRiskManagementModuleTests
{
    private readonly ILogger _logger;

    public AdvancedRiskManagementModuleTests()
    {
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();
    }

    private void PrintRiskManagerState(double price, IRiskManagementModule riskModule)
    {
        _logger.Information(
            "(Price: {Price}) Side: \"{Side}\" | Status: \"{Status}\" | PlannedOrderPrice: {PlannedOrderPrice} | PlannedOrderQuantity: {PlannedOrderQuantity} | BalancePrice: {BalancePrice} | BalanceQuantity: {BalanceQuantity} | StopPrice: {StopPrice} | CurrentUnrealizedPnl: {CurrentUnrealizedPnl} | FeeExecuted: {FeeExecuted}",
            price,
            riskModule.Side,
            riskModule.Status,
            riskModule.PlannedOrderPrice,
            riskModule.PlannedOrderQuantity,
            riskModule.BalancePrice,
            riskModule.BalanceQuantity,
            riskModule.StopPrice,
            riskModule.CurrentUnrealizedPnl,
            riskModule.FeeExecuted);
    }

    #region Long Position Tests

    [Fact(DisplayName = "Long Positive Scenario - Price rising triggers new orders")]
    public void TestLongPositiveScenario()
    {
        // Настройки для длинной позиции
        var settings = new RiskManagementSettings(
            Side.Long,
            4.0,
            2,
            0.001,
            0.00025,
            0.3,
            8408.0,
            500_000,
            8592.0,
            0.001,
            3);

        IRiskManagementModule riskModule = new AdvancedRiskManagementModule(settings);

        double nextOrderPrice = double.NaN;
        double nextOrderQuantity = double.NaN;

        // Симулируем рост цены (для Long – движение в нашу сторону)
        for (var i = 0; i < 30; i++)
        {
            double price = 4.0 + i / 1000.0;
            bool orderRequested = riskModule.CreateOrderRequest(price);
            PrintRiskManagerState(price, riskModule);

            if (riskModule.Status == RiskStatus.OrderRequest)
            {
                nextOrderPrice = riskModule.PlannedOrderPrice;
                nextOrderQuantity = riskModule.PlannedOrderQuantity;
            }

            // Если достигли уровня заявки, симулируем исполнение лимитного ордера
            if (!double.IsNaN(nextOrderPrice) && price >= nextOrderPrice)
            {
                var execution = new UserExecution
                {
                    ExecutedQuantity = nextOrderQuantity,
                    ExecutionPrice = nextOrderPrice,
                    Side = riskModule.Side
                };
                riskModule.ExecutionReceived(execution);
                _logger.Information("{Side} order executed at {Price}x{Quantity}", riskModule.Side,
                    execution.ExecutionPrice, execution.ExecutedQuantity);
                nextOrderPrice = double.NaN;
                nextOrderQuantity = double.NaN;
            }
        }

        // Ожидаем, что позиция открыта и не закрыта
        riskModule.BalanceQuantity.ShouldBeGreaterThan(0);
        riskModule.Status.ShouldBe(RiskStatus.Finished);
    }

    [Fact(DisplayName = "Long Negative Scenario - Price falling triggers stop")]
    public void TestLongNegativeScenario()
    {
        var settings = new RiskManagementSettings(
            Side.Long,
            4.0,
            2,
            0.001,
            0.00025,
            0.3,
            8408.0,
            500_000,
            8592.0,
            0.001,
            3);

        IRiskManagementModule riskModule = new AdvancedRiskManagementModule(settings);

        // Открываем позицию при росте
        riskModule.CreateOrderRequest(4.003);
        riskModule.ExecutionReceived(new UserExecution
        {
            ExecutedQuantity = riskModule.PlannedOrderQuantity,
            ExecutionPrice = riskModule.PlannedOrderPrice,
            Side = riskModule.Side
        });
        _logger.Information("Long order executed at {Price}x{Quantity}", riskModule.PlannedOrderPrice,
            riskModule.PlannedOrderQuantity);

        // Симулируем падение цены, приводящую к срабатыванию стопа
        for (int i = 0; i < 10; i++)
        {
            double price = 4.003 - i / 1000.0;
            bool orderRequested = riskModule.CreateOrderRequest(price);
            PrintRiskManagerState(price, riskModule);
            if (riskModule.Status == RiskStatus.CloseRequest)
            {
                // Симулируем рыночное исполнение закрытия (противоположной стороной)
                riskModule.ExecutionReceived(new UserExecution
                {
                    ExecutedQuantity = riskModule.BalanceQuantity,
                    ExecutionPrice = price,
                    Side = (Side)((int)riskModule.Side * -1)
                });
                break;
            }
        }

        riskModule.Status.ShouldBe(RiskStatus.Finished);
    }

    #endregion

    #region Short Position Tests

    [Fact(DisplayName = "Short Positive Scenario - Price falling triggers new orders")]
    public void TestShortPositiveScenario()
    {
        var settings = new RiskManagementSettings(
            Side.Short,
            4.0,
            2,
            0.001,
            0.00025,
            0.3,
            8408.0,
            500_000,
            8592.0,
            0.001,
            3);

        IRiskManagementModule riskModule = new AdvancedRiskManagementModule(settings);

        double nextOrderPrice = double.NaN;
        double nextOrderQuantity = double.NaN;

        // Для Short – благоприятное движение, если цена падает
        for (var i = 0; i < 30; i++)
        {
            double price = 4.0 - i / 1000.0;
            riskModule.CreateOrderRequest(price);
            PrintRiskManagerState(price, riskModule);

            if (riskModule.Status == RiskStatus.OrderRequest)
            {
                nextOrderPrice = riskModule.PlannedOrderPrice;
                nextOrderQuantity = riskModule.PlannedOrderQuantity;
            }

            if (!double.IsNaN(nextOrderPrice) && price <= nextOrderPrice)
            {
                var execution = new UserExecution
                {
                    ExecutedQuantity = nextOrderQuantity,
                    ExecutionPrice = nextOrderPrice,
                    Side = riskModule.Side
                };
                riskModule.ExecutionReceived(execution);
                _logger.Information("{Side} order executed at {Price}x{Quantity}", riskModule.Side,
                    execution.ExecutionPrice, execution.ExecutedQuantity);
                nextOrderPrice = double.NaN;
                nextOrderQuantity = double.NaN;
            }
        }

        riskModule.BalanceQuantity.ShouldBeGreaterThan(0);
        riskModule.Status.ShouldBe(RiskStatus.Finished);
    }

    [Fact(DisplayName = "Short Negative Scenario - Price rising triggers stop")]
    public void TestShortNegativeScenario()
    {
        var settings = new RiskManagementSettings(
            Side.Short,
            4.0,
            2,
            0.001,
            0.00025,
            0.3,
            8408.0,
            500_000,
            8592.0,
            0.001,
            3);

        IRiskManagementModule riskModule = new AdvancedRiskManagementModule(settings);

        // Открываем позицию
        riskModule.CreateOrderRequest(3.997);
        riskModule.ExecutionReceived(new UserExecution
        {
            ExecutedQuantity = riskModule.PlannedOrderQuantity,
            ExecutionPrice = riskModule.PlannedOrderPrice,
            Side = riskModule.Side
        });
        _logger.Information("Short order executed at {Price}x{Quantity}", riskModule.PlannedOrderPrice,
            riskModule.PlannedOrderQuantity);

        // Симулируем рост цены, приводящий к срабатыванию стопа.
        for (int i = 0; i < 10; i++)
        {
            double price = 3.997 + i / 1000.0;
            riskModule.CreateOrderRequest(price);
            PrintRiskManagerState(price, riskModule);
            if (riskModule.Status == RiskStatus.CloseRequest)
            {
                riskModule.ExecutionReceived(new UserExecution
                {
                    ExecutedQuantity = riskModule.BalanceQuantity,
                    ExecutionPrice = price,
                    Side = (Side)((int)riskModule.Side * -1)
                });
                break;
            }
        }

        riskModule.Status.ShouldBe(RiskStatus.Finished);
    }

    #endregion

    #region Realistic Data Scenario

    [Fact(DisplayName = "Realistic Data Scenario - Simulation with trade-like price updates")]
    public void TestRealisticDataScenario()
    {
        // Пример настроек, приближенных к реальным условиям
        var settings = new RiskManagementSettings(
            Side.Long,
            4.0,
            2,
            0.001,
            0.00025,
            0.3,
            8408.0,
            500_000,
            8592.0,
            0.001,
            3);

        IRiskManagementModule riskModule = new AdvancedRiskManagementModule(settings);

        // Массив цен, имитирующий реальные торги (ISO-формат времени можно сопоставить с временными метками, здесь только цены)
        double[] tradePrices = new double[]
        {
            4.000, 4.001, 4.002, 4.003, 4.004, 4.005, 4.006, 4.007, 4.008, 4.009,
            4.010, 4.0095, 4.009, 4.0085, 4.008, 4.0075, 4.007, 4.0065, 4.006, 4.0055,
            4.005, 4.0055, 4.006, 4.0065, 4.007, 4.0075, 4.008, 4.0085, 4.009, 4.0095,
        };

        // Симулируем проход по торгам
        for (int i = 0; i < tradePrices.Length; i++)
        {
            double price = tradePrices[i];
            riskModule.CreateOrderRequest(price);
            PrintRiskManagerState(price, riskModule);

            // Если появляется заявка, симулируем исполнение лимитного ордера.
            if (riskModule.Status == RiskStatus.OrderRequest)
            {
                var execution = new UserExecution
                {
                    ExecutedQuantity = riskModule.PlannedOrderQuantity,
                    ExecutionPrice = riskModule.PlannedOrderPrice,
                    Side = riskModule.Side
                };
                riskModule.ExecutionReceived(execution);
                _logger.Information("{Side} order executed at {Price}x{Quantity}", riskModule.Side,
                    execution.ExecutionPrice, execution.ExecutedQuantity);
            }

            // Если сигнал закрытия – симулируем рыночное исполнение закрытия.
            if (riskModule.Status == RiskStatus.CloseRequest)
            {
                riskModule.ExecutionReceived(new UserExecution
                {
                    ExecutedQuantity = riskModule.BalanceQuantity,
                    ExecutionPrice = price,
                    Side = (Side)((int)riskModule.Side * -1)
                });
                _logger.Information("Position closed at {Price}", price);
                break;
            }
        }
    }

    #endregion
}
