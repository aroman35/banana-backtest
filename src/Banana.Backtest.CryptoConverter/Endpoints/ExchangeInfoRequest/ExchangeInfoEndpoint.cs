using Banana.Backtest.CryptoConverter.Services;
using FastEndpoints;

namespace Banana.Backtest.CryptoConverter.Endpoints.ExchangeInfoRequest;

public class ExchangeInfoEndpoint(TardisClient tardisClient) : Endpoint<ExchangeInfoQuery, ExchangeInfoResponse>
{
    public override void Configure()
    {
        Get("/exchange/{Exchange}");
        AllowAnonymous();
    }

    public override async Task<ExchangeInfoResponse> ExecuteAsync(ExchangeInfoQuery request, CancellationToken cancellationToken)
    {
        var instruments = await tardisClient
            .GetExchangeInstrumentsAsync(request.Exchange, cancellationToken)
            .ToArrayAsync(cancellationToken: cancellationToken);

        return new ExchangeInfoResponse(instruments);
    }
}