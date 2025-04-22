using System.Threading.Channels;
using Banana.Backtest.Common.Extensions;
using Serilog;

namespace Banana.Backtest.Emulator.Abstractions;

/// <summary>
/// Набор контрактов и зависимостей для реализации подписчика канала
/// </summary>
/// <typeparam name="TChannelData">Тип данных получаемых из канала</typeparam>
public interface IChannelSubscriber<TChannelData> : IAsyncDisposable
{
    /// <summary>
    /// Логгер
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// Канал для чтения
    /// </summary>
    ChannelReader<TChannelData> Reader { get; }

    /// <summary>
    /// Метод запускающий в фоне чтение канала
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Задача с фоновым чтением канала. Успешное ожидание гарантирует завершение чтения данных</returns>
    Task SubscribeAsync(CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(Reader);
        ArgumentNullException.ThrowIfNull(Logger);
        return Task.Run(async () =>
        {
            try
            {
                Logger.Debug("Subscribing to {TypeName} channel", Helpers.FriendlyTypeName<TChannelData>());
                await foreach (var item in Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        Logger.Verbose("Received an item for subscription");
                        await HandleChannelDataAsync(item, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        Logger.Error(exception, "Error occured while trying to handle {TypeName} channel data", Helpers.FriendlyTypeName<TChannelData>());
                        throw;
                    }
                }
                Logger.Debug("Channel handler of {TypeName} completed", Helpers.FriendlyTypeName<TChannelData>());
            }
            catch (TaskCanceledException)
            {
                Logger.Debug("Channel handler {TypeName} was canceled", Helpers.FriendlyTypeName<TChannelData>());
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Метод обработки для получаемых данных
    /// </summary>
    /// <param name="channelData">Экземпляр данных прочитанных из канала</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Процедура</returns>
    ValueTask HandleChannelDataAsync(TChannelData channelData, CancellationToken cancellationToken);
}
