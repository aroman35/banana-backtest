using System.ComponentModel;

namespace Banana.Backtest.Common.Models.Root;

[Flags]
public enum Exchange : ulong
{
    None = 0,
    Spot = 1 << 0,
    Futures = 1 << 1,
    Currency = 1 << 2,
    Swap = 1 << 3,
    Binance = 1 << 4,
    Okex = 1 << 5,
    Moex = 1 << 6,
    Kucoin = 1 << 7,

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

    Crypto = Binance | Okex | Kucoin,
    Classic = Moex,
}
