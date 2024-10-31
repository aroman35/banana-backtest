using System.Buffers;
using Banana.Backtest.Common.Models.MarketData;
using Banana.Backtest.Common.Models.Root;

namespace Banana.Backtest.Common.Services;

public abstract class AbstractCsvParser<T> : IDisposable
    where T : unmanaged
{
    private const int BUFFER_SIZE = 1024 * 4; // 4 KB buffer
    protected const string TIMESTAMP_FORMAT = "yyyyMMddHHmmssfff";
    protected const byte COMMA = 44; // ','
    protected const byte BUY_SIDE = 66; // 'B'

    private readonly byte[] _lineSeparator = "\r\n"u8.ToArray();
    private readonly IParserHandler<T> _parserHandler;
    private readonly Stream _fileStream;

    protected AbstractCsvParser(string filePath, IParserHandler<T> parserHandler)
    {
        _parserHandler = parserHandler;
        _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
    }

    protected AbstractCsvParser(Stream stream, IParserHandler<T> parserHandler, byte[]? lineSeparator = null)
    {
        _parserHandler = parserHandler;
        _fileStream = stream;
        if (lineSeparator is not null)
            _lineSeparator = lineSeparator;
    }

    public void ProcessCsvFile()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);

        try
        {
            int bytesRead;
            var spanBuffer = buffer.AsSpan();
            var lastRemainder = 0;

            while ((bytesRead = _fileStream.Read(buffer, lastRemainder, buffer.Length - lastRemainder)) > 0)
            {
                var totalBytes = bytesRead + lastRemainder;
                var currentSpan = spanBuffer.Slice(0, totalBytes);

                // Process lines in the current buffer
                int lineEndIndex;
                var processedBytes = 0;

                while ((lineEndIndex = currentSpan.IndexOf(_lineSeparator)) >= 0)
                {
                    var line = currentSpan.Slice(0, lineEndIndex);
                    if (ParseLine(line, out var entry, out var ticker))
                        _parserHandler.Handle(entry, ticker);

                    // Skip past the '\r\n' characters
                    currentSpan = currentSpan.Slice(lineEndIndex + _lineSeparator.Length);
                    processedBytes += lineEndIndex + _lineSeparator.Length;
                }

                // If there is remaining unprocessed data, move it to the front of the buffer
                lastRemainder = totalBytes - processedBytes;
                if (lastRemainder > 0)
                {
                    currentSpan.Slice(0, lastRemainder).CopyTo(spanBuffer);
                }
            }

            // Handle any remaining data as a final line (without '\r\n')
            if (lastRemainder > 0)
            {
                var finalLine = spanBuffer.Slice(0, lastRemainder);
                if (ParseLine(finalLine, out var entry, out var ticker))
                    _parserHandler.Handle(entry, ticker);
            }
        }
        finally
        {
            // Return the buffer back to the pool
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    protected abstract bool ParseLine(Span<byte> line, out MarketDataItem<T> marketDataItem, out Symbol symbol);

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fileStream.Dispose();
            _parserHandler.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
