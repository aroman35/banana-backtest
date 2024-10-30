using Banana.Backtest.CryptoConverter.Services.Models.Tardis;

namespace Banana.Backtest.CryptoConverter.Endpoints.ExchangeInfoRequest;

public record ExchangeInfoResponse(ICollection<InstrumentInfo> Instruments);