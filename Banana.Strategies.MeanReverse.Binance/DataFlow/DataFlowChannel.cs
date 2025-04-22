using System.Buffers;

namespace Banana.Strategies.MeanReverse.Binance.DataFlow;

public class DataFlowChannel<T> : IDisposable
{
    private readonly T[] _buffer;
    private readonly int _bufferLength;
    private readonly ManualResetEventSlim _channelWriterSignal = new(false);
    private Action _action = () => { };
    private int _writeIndex = -1;

    public DataFlowChannel()
    {
        _buffer = ArrayPool<T>.Shared.Rent(4);
        _bufferLength = _buffer.Length;
    }

    public void Write(T data)
    {
        Interlocked.Increment(ref _writeIndex);
        if (Interlocked.CompareExchange(ref _writeIndex, 0, _bufferLength) == _bufferLength)
        {
            _channelWriterSignal.Reset();
        }
        _buffer[_writeIndex] = data;
        _action.Invoke();
        _channelWriterSignal.Set();
    }

    public IEnumerable<T> Read(CancellationToken cancellationToken)
    {
        using (var readerResetEvent = new ManualResetEventSlim(_channelWriterSignal.IsSet))
        {
            foreach (var readData in SetReaderLoop(readerResetEvent, cancellationToken))
            {
                yield return readData;
            }
        }
    }

    private IEnumerable<T> SetReaderLoop(ManualResetEventSlim readerResetEvent, CancellationToken cancellationToken)
    {
        var set = readerResetEvent.Set;
        _action += set;
        readerResetEvent.Wait(cancellationToken);
        var readIndex = -1;

        while (!cancellationToken.IsCancellationRequested)
        {
            readerResetEvent.Wait(cancellationToken);
            readIndex++;
            Interlocked.CompareExchange(ref readIndex, 0, _bufferLength);

            yield return _buffer[readIndex];
            _channelWriterSignal.Reset();
        }

        _action -= set;
    }

    public void Dispose()
    {
        ArrayPool<T>.Shared.Return(_buffer, true);
    }
}
