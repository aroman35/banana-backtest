using Tinkoff.InvestApi.V1;

namespace Banana.Backtest.Launcher.Cache.Catalog;

public static class QuotationExtensions
{
    private const decimal NANO_FACTOR = 1_000_000_000;

    public static decimal ToDecimal(this Quotation? value)
    {
        if (value is null)
            return decimal.Zero;
        return value.Units + value.Nano / NANO_FACTOR;
    }

    public static decimal ToDecimal(this MoneyValue? value)
    {
        if (value is null)
            return decimal.Zero;
        return value.Units + value.Nano / NANO_FACTOR;
    }
}
