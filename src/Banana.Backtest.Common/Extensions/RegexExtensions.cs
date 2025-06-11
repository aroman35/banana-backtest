using System.Text.RegularExpressions;
using Banana.Backtest.Common.Models;

namespace Banana.Backtest.Common.Extensions;

public partial class RegexExtensions
{
    public static bool HashFromFileName(string fileName, out MarketDataHash hash)
    {
        try
        {
            var regex = FileNameRegex().Match(fileName.Replace('\\', '/'));
            if (regex.Success)
            {
                var symbol = Models.Root.Symbol.Parse(regex.Groups["symbol"].Value);
                var date = DateOnly.Parse(regex.Groups["date"].Value);
                var type = Enum.Parse<FeedType>(regex.Groups["type"].Value, true);
                hash = MarketDataHash.Create(symbol, date, type);
                return true;
            }

            hash = default;
            return false;
        }
        catch
        {
            hash = default;
            return false;
        }
    }

    [GeneratedRegex(@"(?<symbol>\w*@\w*.\w*)\/(?<date>\d{4}-\d{2}-\d{2})_(?<type>(levelupdates)|(trades))(.dat)$", RegexOptions.Compiled)]
    private static partial Regex FileNameRegex();
}
