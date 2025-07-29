using Banana.Strategies.MeanReverse.Binance.Persistence;
using FastEndpoints;

namespace Banana.Strategies.MeanReverse.Binance.Endpoints.Reports.TradingReport;

public class DownloadTradingReportEndpoint(ReportsProvider reportsProvider) : Endpoint<DownloadTradingReportRequest>
{
    private const string CONTENT_TYPE = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public override void Configure()
    {
        Get("/reports/");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DownloadTradingReportRequest request, CancellationToken cancellationToken)
    {
        using var reportOutputStream = new MemoryStream();
        await reportsProvider.GenerateCustomReport(reportOutputStream, request.StartDate, request.EndDate, cancellationToken);
        reportOutputStream.Seek(0, SeekOrigin.Begin);
        await SendStreamAsync(
            reportOutputStream,
            $"report_{request.StartDate:dd.MM.yyyy}_{request.EndDate:dd.MM.yyyy}.xlsx",
            reportOutputStream.Length,
            CONTENT_TYPE, cancellation: cancellationToken);
    }
}
