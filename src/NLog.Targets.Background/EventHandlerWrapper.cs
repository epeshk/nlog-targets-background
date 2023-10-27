using System.Runtime.CompilerServices;
using BackgroundLogger.Disruptor;
using NLog.Common;

namespace NLog.Targets.Wrappers;

struct EventHandlerWrapper : IBatchEventHandler<AsyncLogEventInfo>, IEventProcessorSequenceAware
{
  readonly BatchLogEventHandler handler1;

  public EventHandlerWrapper(BatchLogEventHandler handler)
  {
    handler1 = handler;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void OnBatch(Span<AsyncLogEventInfo> batch, long sequence)
  {
    handler1.OnBatch(batch, sequence);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void OnShutdown()
  {
    handler1.OnShutdown();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void OnStart()
  {
    handler1.OnStart();
  }

  public void SetSequenceCallback(Sequence sequenceCallback)
  {
    handler1.SetSequenceCallback(sequenceCallback);
  }
}