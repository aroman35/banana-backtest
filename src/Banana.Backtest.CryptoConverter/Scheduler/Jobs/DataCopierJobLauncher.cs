using Hangfire;
using static Banana.Backtest.CryptoConverter.Scheduler.HangfireDefaults;

namespace Banana.Backtest.CryptoConverter.Scheduler.Jobs;

public class DataCopierJobLauncher(IBackgroundJobClientV2 backgroundJobClient)
{
    public void Handle(string sourceDirectory)
    {
        var directoryInfo = new DirectoryInfo(sourceDirectory);
        var sourceFiles = directoryInfo.EnumerateFiles("*.dat", SearchOption.AllDirectories);
        var allFiles = sourceFiles.OrderBy(x => x.Length).ToList();

        allFiles
            .AsParallel()
            .ForAll(fileInfo => backgroundJobClient.Enqueue<DataCopierJob>(DATA_MIGRATION_QUEUE, job => job.HandleAsync(fileInfo.FullName)));
    }
}
