using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.Root;
using Tinkoff.InvestApi;
using Tinkoff.InvestApi.V1;
using Asset = Banana.Backtest.Common.Models.Root.Asset;

namespace Banana.Backtest.Launcher.Cache.Catalog;

public class CatalogLoader(InvestApiClient investApiClient, InstrumentsCatalog instrumentsCatalog, ILogger logger) : IHostedService
{
    private readonly ILogger _logger = logger.ForContext<CatalogLoader>();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await LoadFutures();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task LoadFutures()
    {
        // var emulatedInstruments = await instrumentsCatalog
        //     .FuturesByBasicAsset(Asset.Get("NG"), new DateOnly(2020, 04, 01), new DateOnly(2025, 04, 30))
        //     .OrderByDescending(x => x.ExpirationDate)
        //     .Select(x => $"{x.Symbol.Ticker}:{x.ExpirationDate}")
        //     .ToArrayAsync();
        try
        {
            var futuresResponse = await investApiClient.Instruments.FuturesAsync(new InstrumentsRequest
            {
                InstrumentStatus = InstrumentStatus.All
            });
            var instruments = futuresResponse.Instruments
                .Where(x => x.BasicAsset.Length <= 8)
                .Select(x => new FuturesInstrument
                {
                    Symbol = Symbol.Parse(x.Ticker, x.ClassCode, Exchange.MoexFutures),
                    BasicAsset = Asset.Get(x.BasicAsset),
                    DiscountShort = x.Dshort.ToDecimal(),
                    DiscountLong = x.Dlong.ToDecimal(),
                    FirstTradeDate = DateOnly.FromDateTime(x.FirstTradeDate.ToDateTime()),
                    LastTradeDate = DateOnly.FromDateTime(x.LastTradeDate.ToDateTime()),
                    ExpirationDate = DateOnly.FromDateTime(x.ExpirationDate.ToDateTime()),
                    MinPriceIncrement = x.MinPriceIncrement.ToDecimal(),
                    MinPriceIncrementAmount = x.MinPriceIncrementAmount.ToDecimal(),
                    InitialMarginOnBuy = x.InitialMarginOnBuy.ToDecimal(),
                    InitialMarginOnSell = x.InitialMarginOnSell.ToDecimal()
                });
            await instrumentsCatalog.SetInstruments(instruments);
            _logger.Information("Catalog was updated");
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Error while loading futures");
            throw;
        }
    }
}
