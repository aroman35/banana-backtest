using Banana.Backtest.Common.Models.Root;
using Banana.Backtest.CryptoConverter.Services.Models.Tardis;

namespace Banana.Backtest.CryptoConverter.Endpoints.ConvertSingleInstrumentRequest;

public record ConvertInstrumentCommand(Symbol Symbol, DateOnly TradeDate, InstrumentInfo InstrumentInfo);