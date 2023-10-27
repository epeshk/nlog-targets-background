using System.Runtime.InteropServices;
using BackgroundLogger.Disruptor;
using NLog.Common;


namespace NLog.Targets.Wrappers;

sealed class BatchLogEventHandler : IBatchEventHandler<AsyncLogEventInfo>,
  IEventProcessorSequenceAware
{
  Sequence? sequenceCallback;
  readonly List<AsyncLogEventInfo> list = new();
  readonly Target wrappedTarget;
  readonly Func<int> getBatchSize1;

  public BatchLogEventHandler(Target target, Func<int> getBatchSize)
  {
    wrappedTarget = target;
    getBatchSize1 = getBatchSize;
  }

  public void OnBatch(Span<AsyncLogEventInfo> batch, long sequence)
  {
    var batchSize = getBatchSize1();

    while (!batch.IsEmpty)
    {
      try
      {
        var fragment = batch.Slice(0, Math.Min(batch.Length, batchSize));
        list.Clear();
#if NET8_0_OR_GREATER
        list.AddRange(fragment);
#else
        foreach (var logEvent in fragment)
          list.Add(logEvent);
#endif
        wrappedTarget.WriteAsyncLogEvents(list);
        batch = batch.Slice(fragment.Length);
      }
      catch (Exception e)
      {
        InternalLogger.Error(e, e.GetType().FullName);
      }
    }
  }

  public void OnShutdown()
  {
  }

  public void OnStart()
  {
  }

  public void SetSequenceCallback(Sequence sequence)
  {
    this.sequenceCallback = sequence;
  }
}