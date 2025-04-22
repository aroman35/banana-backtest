using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Emulator.Contracts;
using Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;
using Serilog;
using Shouldly;
using Xunit.Abstractions;

namespace Banana.Strategies.Predictive.UnitTests;

public class RiskManagementTests(ITestOutputHelper testOutputHelper)
{
    private readonly ILogger _logger = new LoggerConfiguration()
        .WriteTo.TestOutput(testOutputHelper)
        .CreateLogger();

    [Fact]
    public void RiskModelForLongPosition()
    {
        var settings = new RiskManagementSettings(
            Side.Long,
            4.0,
            2,
            0.001,
            0.00025,
            0.3,
            8408.0,
            5_00_000,
            8592.0,
            0.001,
            3);

        var riskManagement = new AdvancedRiskManagementModule(settings);

        var nextOrderPrice = double.NaN;
        var nexOrderQuantity = double.NaN;

        for (var i = 0; i < 30; i++)
        {
            var price = 4.0 + i / 1000.0;
            riskManagement.CreateOrderRequest(price);

            if (riskManagement.Status is RiskStatus.CloseRequest)
            {
                nextOrderPrice = double.NaN;
                nexOrderQuantity = double.NaN;
                // close market order
                riskManagement.ExecutionReceived(new UserExecution
                {
                    ExecutedQuantity = riskManagement.BalanceQuantity,
                    ExecutionPrice = price,
                    Side = (Side)((int)settings.Side * -1),
                });
            }

            // Limit order executed
            if (!double.IsNaN(nextOrderPrice) && price.IsGreaterOrEquals(nextOrderPrice))
            {
                var execution = new UserExecution
                {
                    ExecutionPrice = nextOrderPrice,
                    ExecutedQuantity = nexOrderQuantity,
                    Side = Side.Long,
                };
                riskManagement.ExecutionReceived(execution);
                nextOrderPrice = double.NaN;
                nexOrderQuantity = double.NaN;
                _logger.Information("{Side} order executed {Price}x{Quantity}", execution.Side, execution.ExecutionPrice, execution.ExecutedQuantity);
            }

            if (riskManagement.Status is RiskStatus.OrderRequest)
            {
                nextOrderPrice = riskManagement.PlannedOrderPrice;
                nexOrderQuantity = riskManagement.PlannedOrderQuantity;
                // Limit order
            }

            PrintRiskManagerState(price, riskManagement);
        }
    }

    [Fact]
    public void RiskManagerFullPositiveTest()
    {
        var settings = new RiskManagementSettings(
            Side.Long,
            4.0,
            2,
            0.001,
            0.00025,
            0.3,
            8408.0,
            5_00_000,
            8592.0,
            0.001,
            3);

        var riskManagement = new SimpleRiskManagementModule(settings);
        var price = 4.0;
        // Шаг 1: Инициируем заявку по цене 4.0
            riskManagement.CreateOrderRequest(price).ShouldBeTrue();
            riskManagement.BalancePrice.ShouldBe(0.0, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(0.0, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(0.0, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(0.0, MathExtensions.PRECISION);
            riskManagement.PlannedOrderPrice.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(2.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(3.998, MathExtensions.PRECISION);
            riskManagement.Side.ShouldBe(Side.Long);
            riskManagement.Status.ShouldBe(RiskStatus.OrderRequest);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 2: Приход исполнения
            riskManagement.ExecutionReceived(CreateUserExecution(riskManagement));
            riskManagement.BalancePrice.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(2.0, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(-17.18, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(17.18, MathExtensions.PRECISION);
            riskManagement.PlannedOrderPrice.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(2.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(3.998, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.Waiting);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 3: Цена = 4.001
            price = 4.001;
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            riskManagement.PlannedOrderPrice.ShouldBe(4.003, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(2.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(0.0, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(17.18, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.Waiting);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 4: Цена = 4.002
            price = 4.002;
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            riskManagement.PlannedOrderPrice.ShouldBe(4.003, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(2.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.001, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(17.18, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(17.18, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.Waiting);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 5: Цена = 4.003, заявка принимается
            price = 4.003;
            riskManagement.CreateOrderRequest(price).ShouldBeTrue();
            riskManagement.PlannedOrderPrice.ShouldBe(4.003, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(2.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.002, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(34.37, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(17.18, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.OrderRequest);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 6: Приход исполнения по заявке 4.003
            riskManagement.ExecutionReceived(CreateUserExecution(riskManagement));
            riskManagement.PlannedOrderPrice.ShouldBe(4.003, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.002, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(6.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.002, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(-0.02, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(51.57, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.Waiting);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 7: Цена = 4.004
            price = 4.004;
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            riskManagement.PlannedOrderPrice.ShouldBe(4.008, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.002, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(6.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.003, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(51.53, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(51.57, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.Waiting);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 8: Цена = 4.005
            price = 4.005;
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            riskManagement.PlannedOrderPrice.ShouldBe(4.008, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.002, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(6.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.004, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(103.08, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(51.57, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.Waiting);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 9: Цена = 4.006
            price = 4.006;
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            riskManagement.PlannedOrderPrice.ShouldBe(4.008, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.002, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(6.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.004, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(154.63, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(51.57, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.Waiting);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 10: Цена = 4.007 (первый вызов)
            price = 4.007;
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            riskManagement.PlannedOrderPrice.ShouldBe(4.008, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.002, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(6.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.005, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(206.18, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(51.57, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.Waiting);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 11: Цена = 4.007 (второй вызов, состояние не меняется)
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            PrintRiskManagerState(price, riskManagement);

            // Шаг 12: Цена = 4.008, заявка принимается
            price = 4.008;
            riskManagement.CreateOrderRequest(price).ShouldBeTrue();
            riskManagement.PlannedOrderPrice.ShouldBe(4.008, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.002, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(6.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.006, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(257.74, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(51.57, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.OrderRequest);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 13: Приход исполнения по заявке 4.008
            riskManagement.ExecutionReceived(CreateUserExecution(riskManagement));
            riskManagement.PlannedOrderPrice.ShouldBe(4.008, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(4.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.004, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(10.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.006, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(257.68, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(86.0, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.Waiting);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 14: Цена = 4.009 (первый вызов)
            price = 4.009;
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            riskManagement.PlannedOrderPrice.ShouldBe(4.012, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(5.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.004, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(10.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.007, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(343.6, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(86.0, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.Waiting);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 15: Цена = 4.009 (второй вызов)
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            PrintRiskManagerState(price, riskManagement);

            // Шаг 16: Цена = 4.01 (первый вызов)
            price = 4.01;
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            riskManagement.PlannedOrderPrice.ShouldBe(4.012, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(5.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.004, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(10.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.008, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(429.52, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(86.0, MathExtensions.PRECISION);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 17: Цена = 4.01 (второй вызов)
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            PrintRiskManagerState(price, riskManagement);

            // Шаг 18: Цена = 4.011 (первый вызов)
            price = 4.011;
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            riskManagement.PlannedOrderPrice.ShouldBe(4.012, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(5.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.004, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(10.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.008, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(515.44, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(86.0, MathExtensions.PRECISION);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 19: Цена = 4.011 (второй вызов)
            riskManagement.CreateOrderRequest(price).ShouldBeFalse();
            PrintRiskManagerState(price, riskManagement);

            // Шаг 20: Цена = 4.012, заявка принимается
            price = 4.012;
            riskManagement.CreateOrderRequest(price).ShouldBeTrue();
            riskManagement.PlannedOrderPrice.ShouldBe(4.012, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(5.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.004, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(10.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.009, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(601.36, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(86.0, MathExtensions.PRECISION);
            riskManagement.Status.ShouldBe(RiskStatus.OrderRequest);
            PrintRiskManagerState(price, riskManagement);

            // Шаг 21: Приход исполнения по заявке 4.012
            riskManagement.ExecutionReceived(CreateUserExecution(riskManagement));
            riskManagement.PlannedOrderPrice.ShouldBe(4.012, MathExtensions.PRECISION);
            riskManagement.PlannedOrderQuantity.ShouldBe(5.0, MathExtensions.PRECISION);
            riskManagement.BalancePrice.ShouldBe(4.006, MathExtensions.PRECISION);
            riskManagement.BalanceQuantity.ShouldBe(15.0, MathExtensions.PRECISION);
            riskManagement.StopPrice.ShouldBe(4.01, MathExtensions.PRECISION);
            riskManagement.CurrentUnrealizedPnl.ShouldBe(644.2, MathExtensions.PRECISION);
            riskManagement.FeeExecuted.ShouldBe(129.07, MathExtensions.PRECISION);
            PrintRiskManagerState(price, riskManagement);
    }

    private void PrintRiskManagerState(double price, SimpleRiskManagementModule simpleRiskManagement)
    {
        _logger.Information(
            "(Price: {Price}) Side: {Side} | Status: {Status} | PlannedOrderPrice: {PlannedOrderPrice} | " +
            "PlannedOrderQuantity: {PlannedOrderQuantity} | BalancePrice: {BalancePrice} | " +
            "BalanceQuantity: {BalanceQuantity} | StopPrice: {StopPrice} | " +
            "CurrentUnrealizedPnl: {CurrentUnrealizedPnl} | FeeExecuted: {FeeExecuted}",
            price,
            simpleRiskManagement.Side,
            simpleRiskManagement.Status,
            simpleRiskManagement.PlannedOrderPrice,
            simpleRiskManagement.PlannedOrderQuantity,
            simpleRiskManagement.BalancePrice,
            simpleRiskManagement.BalanceQuantity,
            simpleRiskManagement.StopPrice,
            simpleRiskManagement.CurrentUnrealizedPnl,
            simpleRiskManagement.FeeExecuted);
    }

    private void PrintRiskManagerState(double price, AdvancedRiskManagementModule advancedRiskManagement)
    {
        _logger.Information(
            "(Price: {Price}) Side: {Side} | Status: {Status} | PlannedOrderPrice: {PlannedOrderPrice} | " +
            "PlannedOrderQuantity: {PlannedOrderQuantity} | BalancePrice: {BalancePrice} | " +
            "BalanceQuantity: {BalanceQuantity} | StopPrice: {StopPrice} | " +
            "CurrentUnrealizedPnl: {CurrentUnrealizedPnl} | FeeExecuted: {FeeExecuted}",
            price,
            advancedRiskManagement.Side,
            advancedRiskManagement.Status,
            advancedRiskManagement.PlannedOrderPrice,
            advancedRiskManagement.PlannedOrderQuantity,
            advancedRiskManagement.BalancePrice,
            advancedRiskManagement.BalanceQuantity,
            advancedRiskManagement.StopPrice,
            advancedRiskManagement.CurrentUnrealizedPnl,
            advancedRiskManagement.FeeExecuted);
    }

    private static UserExecution CreateUserExecution(SimpleRiskManagementModule simpleRiskManagement)
    {
        return new UserExecution
        {
            ExecutionPrice = simpleRiskManagement.PlannedOrderPrice,
            ExecutedQuantity = simpleRiskManagement.PlannedOrderQuantity,
            Side = simpleRiskManagement.Side,
        };
    }
}
