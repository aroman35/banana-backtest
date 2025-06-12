using Banana.Backtest.Crypto.Core.Catalog.Models.Tardis;

namespace Banana.Backtest.CryptoConverter.Endpoints.ExchangeInfoRequest;

public record ExchangeInfoResponse(ICollection<InstrumentInfo> Instruments);