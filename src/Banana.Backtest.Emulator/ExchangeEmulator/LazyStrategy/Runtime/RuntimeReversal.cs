namespace Banana.Backtest.Emulator.ExchangeEmulator.LazyStrategy.Runtime;

public class RuntimeReversal(double thresholdPercent)
{
    private double _lastExtremePrice = double.NaN;
    private int _currentTrend;

    public int FindTrendReversal(double price)
    {
        if (double.IsNaN(_lastExtremePrice))
            _lastExtremePrice = price;

        if (_currentTrend == 0)
        {
            if (price >= _lastExtremePrice * (1 + thresholdPercent))
            {
                _currentTrend = 1;
                _lastExtremePrice = price;
            }
            else if (price <= _lastExtremePrice * (1 - thresholdPercent))
            {
                _currentTrend = -1;
                _lastExtremePrice = price;
            }
        }
        else if (_currentTrend == 1)
        {
            if (price > _lastExtremePrice)
            {
                _lastExtremePrice = price;
            }
            else if (price <= _lastExtremePrice * (1 - thresholdPercent))
            {
                _currentTrend = -1;
                _lastExtremePrice = price;
            }
        }
        else if (_currentTrend == -1)
        {
            if (price < _lastExtremePrice)
            {
                _lastExtremePrice = price;
            }
            else if (price >= _lastExtremePrice * (1 + thresholdPercent))
            {
                _currentTrend = 1;
                _lastExtremePrice = price;
            }
        }

        return _currentTrend;
    }
}
