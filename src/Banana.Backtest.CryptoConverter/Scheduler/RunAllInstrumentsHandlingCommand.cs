using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.CryptoConverter.Scheduler;

public class RunAllInstrumentsHandlingCommand
{
    public RunAllInstrumentsHandlingCommand()
    {
    }

    public RunAllInstrumentsHandlingCommand(Exchange exchange)
    {
        Exchange = exchange;
    }

    public RunAllInstrumentsHandlingCommand(DateOnly startDate, Exchange exchange) : this(exchange)
    {
        StartDate = startDate;
    }

    public RunAllInstrumentsHandlingCommand(DateOnly startDate, DateOnly endDate, Exchange exchange) : this(startDate, exchange)
    {
        EndDate = endDate;
    }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Exchange Exchange { get; set; }

    public void ValidateAndThrow()
    {
        if (StartDate > EndDate)
            throw new ArgumentException("End date cannot be earlier than start date");
    }
}
