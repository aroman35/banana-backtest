using System.Threading.Channels;
using Banana.Backtest.Common.Models.MarketData;

namespace Banana.Backtest.Emulator.Abstractions;

/// <summary>
/// Хранилище каналов для рыночных данных
/// </summary>
/// <typeparam name="TMarketData">Тип данных</typeparam>
public interface IMarketDataChannelsProvider<TMarketData>
    where TMarketData : unmanaged
{
    /// <summary>
    /// Канал исходных данных для матчера
    /// </summary>
    Channel<MarketDataItem<TMarketData>> MarketDataSourceChannel { get; }

    /// <summary>
    /// Канал рыночных данных для основных потребителей
    /// </summary>
    Channel<MarketDataItem<TMarketData>> MarketDataGatewayChannel { get; }
}
