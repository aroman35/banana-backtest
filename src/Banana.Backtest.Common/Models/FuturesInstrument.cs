using System.Runtime.InteropServices;
using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.Common.Models;

[StructLayout(LayoutKind.Sequential)]
public struct FuturesInstrument
{
    public Symbol Symbol;
    public Asset BasicAsset;
    public decimal DiscountShort;
    public decimal DiscountLong;
    public DateOnly FirstTradeDate;
    public DateOnly LastTradeDate;
    public DateOnly ExpirationDate;
    public decimal MinPriceIncrement;
    public decimal MinPriceIncrementAmount;
    public decimal InitialMarginOnBuy;
    public decimal InitialMarginOnSell;
}
