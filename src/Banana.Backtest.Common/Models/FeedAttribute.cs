namespace Banana.Backtest.Common.Models;

[AttributeUsage(AttributeTargets.Struct)]
public class FeedAttribute(FeedType feed) : Attribute
{
    public FeedType Feed { get; } = feed;
}