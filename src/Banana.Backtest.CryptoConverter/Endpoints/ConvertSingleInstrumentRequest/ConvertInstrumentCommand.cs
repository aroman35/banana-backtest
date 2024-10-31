using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.CryptoConverter.Endpoints.ConvertSingleInstrumentRequest;

public record ConvertInstrumentCommand(Symbol Symbol, DateOnly TradeDate);