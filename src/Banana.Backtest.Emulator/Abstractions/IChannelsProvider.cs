using System.Threading.Channels;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Emulator.Contracts;

namespace Banana.Backtest.Emulator.Abstractions;

/// <summary>
/// Общее хранилище каналов
/// </summary>
public interface IChannelsProvider
{
    /// <summary>
    /// Канал исходных данных для матчера
    /// </summary>
    /// <typeparam name="TMarketData">Тип данных канала</typeparam>
    Channel<MarketDataItem<TMarketData>> GetMarketDataSourceChannel<TMarketData>()
        where TMarketData : unmanaged;

    /// <summary>
    /// Канал рыночных данных для основных потребителей
    /// </summary>
    /// <typeparam name="TMarketData">Тип данных канала</typeparam>
    Channel<MarketDataItem<TMarketData>> GetMarketDataGatewayChannel<TMarketData>()
        where TMarketData : unmanaged;

    /// <summary>
    /// Канал для передачи сделок пользователя
    /// </summary>
    Channel<UserExecution> UserExecutionChannel { get; }

    /// <summary>
    /// Канал для передачи выставления заявок пользователя
    /// </summary>
    Channel<PlaceOrderRequest> UserOrdersChannel { get; }

    /// <summary>
    /// Канал для отмены заявок пользователя
    /// </summary>
    Channel<CancelOrderRequest> CancelOrdersChannel { get; }

    /// <summary>
    /// Канал для передачи обновлений статусов по заявкам
    /// </summary>
    Channel<OrderInfo> OrderStatusesChannel { get; }

    /// <summary>
    /// Канал для передачи тиков для часов
    /// </summary>
    Channel<long> TimestampsFeed { get; }


    Channel<MarketDataItem> MarketDataCommonProviderChannel { get; }
}
