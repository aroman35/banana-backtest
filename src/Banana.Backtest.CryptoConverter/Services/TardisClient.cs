using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Banana.Backtest.Common.Models;
using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.CryptoConverter.Extensions;
using Banana.Backtest.CryptoConverter.Options;
using Banana.Backtest.CryptoConverter.Services.Models.Tardis;
using Flurl;
using Microsoft.Extensions.Options;

namespace Banana.Backtest.CryptoConverter.Services;

public class TardisClient(
    HttpClient httpClient,
    IOptionsSnapshot<TardisHttpClientOptions> tardisOptions,
    ILogger logger)
{
    private static readonly Dictionary<FeedType, string> TardisFeedNames = new()
    {
        [FeedType.Trades] = "trades",
        [FeedType.LevelUpdates] = "incremental_book_L2",
    };

    private readonly ILogger _logger = logger.ForContext<TardisClient>();

    public async IAsyncEnumerable<InstrumentInfo> GetExchangeInstrumentsAsync(Exchange exchange, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var filter = new InstrumentFilter
        {
            Type = "perpetual",
            Active = true
        };

        ArgumentException.ThrowIfNullOrWhiteSpace(tardisOptions.Value.ApiUrl);
        var url = tardisOptions.Value.ApiUrl
            .AppendPathSegment("v1/instruments")
            .AppendPathSegment(exchange.GetDescription())
            .AppendQueryParam("filter", JsonSerializer.Serialize(filter));

        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var instruments = response.Content
            .ReadFromJsonAsAsyncEnumerable<TardisContract>(cancellationToken: cancellationToken)
            .Where(instrument => instrument is { Active: true, BaseCurrency.Length: <= 8, QuoteCurrency.Length: <= 8 })
            .OrderByDescending(x => x?.AvailableSince);

        await foreach (var instrument in instruments)
        {
            var info = instrument!.ToInstrumentInfo();
            yield return info;
        }
    }

    public async Task<Stream> DownloadDatasetFileAsync(MarketDataHash hash, InstrumentInfo instrumentInfo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tardisOptions.Value.DatasetsUrl);
        if (!TardisFeedNames.TryGetValue(hash.Feed, out var feedName))
            throw new InvalidOperationException($"Tardis feed name is not configured for {hash.Feed}");
        if (instrumentInfo.Symbol != hash.Symbol)
            throw new ArgumentException("Provided instrument info doesn't match requested hash", nameof(instrumentInfo));

        var url = tardisOptions.Value.DatasetsUrl
            .AppendPathSegment("v1")
            .AppendPathSegment(hash.Symbol.Exchange.GetDescription())
            .AppendPathSegment(feedName)
            .AppendPathSegment(hash.Date.Year.ToString("0000"))
            .AppendPathSegment(hash.Date.Month.ToString("00"))
            .AppendPathSegment(hash.Date.Day.ToString("00"))
            .AppendPathSegment((instrumentInfo.DatasetId ?? hash.Symbol.CryptoSymbolString) + ".csv.gz");

        var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorResponse = await response.Content.ReadFromJsonAsync<TardisErrorResponse>(cancellationToken);
                if (!errorResponse?.IsTradingDate(hash.Date) ?? false)
                {
                    _logger.Error("Error downloading tardis data for {Hash}: no data for requested hash", hash);
                    return Stream.Null;
                }
            }
            throw new HttpRequestException($"Error downloading tardis data: {response.StatusCode}");
        }
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    private class InstrumentFilter
    {
        public string? Type { get; set; }
        public bool Active { get; set; }
    }
}