using System.ComponentModel;

namespace Banana.Backtest.Common.Models.Root;

[Flags]
public enum Exchange : ulong
{
    None = 0b_0000_0000_0000_0000,
    Spot = 0b_0000_0000_0000_0001,
    Futures = 0b_0000_0000_0000_0010,
    Currency = 0b_0000_0000_0000_0100,
    Swap = 0b_0000_0000_0000_1000,
    Binance = 0b_0000_0000_0001_0000,
    Okex = 0b_0000_0000_0010_0000,
    Moex = 0b_0000_0000_0100_0000,
    Kucoin = 0b_0000_0000_1000_0000,

    MoexFutures = Moex | Futures,
    MoexSpot = Moex | Spot,
    MoexSelt = Moex | Currency,

    [Description("binance")]
    BinanceSpot = Binance | Spot,
    [Description("binance-futures")]
    BinanceFutures = Binance | Futures,

    [Description("okex")]
    OkexSpot = Okex | Spot,
    [Description("okex-futures")]
    OkexFutures = Okex | Futures,
    [Description("okex-swap")]
    OkexSwap = Okex | Swap,

    [Description("kucoin")]
    KucoinSpot = Kucoin | Spot,
    [Description("kucoin-futures")]
    KucoinFutures = Kucoin | Futures,

    Crypto = Binance | Okex,
    Classic = Moex,
}
