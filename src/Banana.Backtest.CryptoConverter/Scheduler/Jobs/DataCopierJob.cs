using System.Buffers;
using Banana.Backtest.Common.Extensions;
using Banana.Backtest.Common.Models.Options;
using Microsoft.Extensions.Options;
// ReSharper disable ConvertToUsingDeclaration

namespace Banana.Backtest.CryptoConverter.Scheduler.Jobs;

public class DataCopierJob(
    IHostApplicationLifetime appLifetime,
    IOptions<MarketDataParserOptions> marketDataParserOptions,
    ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<DataCopierJob>();

    public async Task HandleAsync(string sourceFile)
    {
        if (!File.Exists(sourceFile))
        {
            _logger.Warning("File not found: {FileName}", sourceFile);
            return;
        }

        if (!RegexExtensions.HashFromFileName(sourceFile, out var hash))
        {
            _logger.Warning("Couldn't create hash for {FileName}", sourceFile);
            return;
        }

        var destinationFileName = hash.FilePath(marketDataParserOptions.Value.OutputDirectory);
        await CopyFileWithPooledBufferAsync(
            NormalizePath(sourceFile),
            NormalizePath(destinationFileName),
            8 * 1024 * 1024,
            appLifetime.ApplicationStopping);
    }

    private async Task CopyFileWithPooledBufferAsync(
        string sourcePath,
        string destinationPath,
        int bufferSize = 8 * 1024 * 1024,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            // Открываем потоки с асинхронными и sequential flags
            await using (var sourceFileStream = new FileStream(
                             sourcePath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             bufferSize,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await using (var destinationFileStream = new FileStream(
                                 destinationPath,
                                 FileMode.Create,
                                 FileAccess.Write,
                                 FileShare.None,
                                 bufferSize,
                                 FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    // Арендуем буфер из пула
                    using var owner = MemoryPool<byte>.Shared.Rent(bufferSize);
                    var buffer = owner.Memory[..bufferSize];

                    int bytesRead;
                    while ((bytesRead = await sourceFileStream.ReadAsync(buffer, cancellationToken)
                               .ConfigureAwait(false)) > 0)
                    {
                        // Пишем только прочитанные байты
                        await destinationFileStream.WriteAsync(buffer[..bytesRead], cancellationToken)
                            .ConfigureAwait(false);
                    }
                    _logger.Information("Copied file {SourceFile} -> {DestinationName}", sourcePath, destinationPath);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Failed to copy file: {SourceFile} -> {DestinationName}", sourcePath, destinationPath);
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            throw;
        }
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
