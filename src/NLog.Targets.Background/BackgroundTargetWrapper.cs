using System.Diagnostics;
using System.Numerics;
using BackgroundLogger.Disruptor;
using BackgroundLogger.Disruptor.Dsl;
using BackgroundLogger.Disruptor.Processing;
using NLog.Common;
using NLog.Targets.Background;
using Serilog.Sinks.Background;

namespace NLog.Targets.Wrappers;

[Target("BackgroundWrapper", IsWrapper = true)]
public sealed class BackgroundTargetWrapper : WrapperTargetBase
{
  volatile bool isAddingCompleted;
  Disruptor<AsyncLogEventInfo>? disruptor;
  WaitingStrategy waitStrategy;
  RingBuffer<AsyncLogEventInfo> ringBuffer;

  public BackgroundTargetWrapper(Target wrappedTarget)
  {
    WrappedTarget = wrappedTarget;
    ringBuffer = default!;
    waitStrategy = default!;
  }
  
  /// <summary>
  /// Gets or sets the number of log events that should be processed in a batch
  /// by the lazy writer thread.
  /// </summary>
  public int BatchSize { get; set; } = 200;

  /// <summary>
  /// Gets or sets the limit on the number of requests in the lazy writer thread request queue.
  /// </summary>
  public int QueueLimit { get; set; } = 16 * 1024;

  /// <summary>
  /// Gets or sets the limit on the number of requests in the lazy writer thread request queue.
  /// </summary>
  public bool BlockWhenFull { get; set; } = false;

  protected override void Write(AsyncLogEventInfo logEvent)
  {
    if (isAddingCompleted)
      return;

    try
    {
      // Not now, Thread.Abort under ControlledExecution mask
    }
    finally
    {
      if (BlockWhenFull)
        EmitBlockWhenFull(logEvent);
      else
        EmitDropWhenFull(logEvent);
    }
  }

  void EmitDropWhenFull(AsyncLogEventInfo logEvent)
  {
    if (!ringBuffer.TryPublishEvent(logEvent))
      Metrics.EventsDropped.Increment();
  }

  void EmitBlockWhenFull(AsyncLogEventInfo logEvent)
  {
    ringBuffer.PublishEvent(logEvent);
  }

  protected override void Write(IList<AsyncLogEventInfo> logEvents)
  {
    for (var index = 0; index < logEvents.Count; index++)
    {
      Write(logEvents[index]);
    }
  }

  protected override void WriteAsyncThreadSafe(AsyncLogEventInfo logEvent)
  {
    Write(logEvent);
  }

  protected override void WriteAsyncThreadSafe(IList<AsyncLogEventInfo> logEvents)
  {
    for (var index = 0; index < logEvents.Count; index++)
    {
      Write(logEvents[index]);
    }
  }

  protected override void InitializeTarget()
  {
    var bufferSize = QueueLimit;
    if (bufferSize <= 0)
      throw new ArgumentOutOfRangeException(nameof(bufferSize), QueueLimit, "Should be > 0");
    waitStrategy = new WaitingStrategy(128, 32, Settings.WakeupMs);

    disruptor = new Disruptor<AsyncLogEventInfo>(
      static () => default,
      (int)DisruptorUtil.RoundUpToPowerOf2((uint)bufferSize),
      waitStrategy,
      (barrier, consumerSequence, buffer) => 
        new BatchEventProcessor<
          AsyncLogEventInfo,
          EventHandlerWrapper>
        (
          consumerSequence,
          buffer,
          barrier,
          new EventHandlerWrapper(new BatchLogEventHandler(WrappedTarget, () => BatchSize)),
          "NLog_Background")
    );

    ringBuffer = disruptor.Start(Timeout.InfiniteTimeSpan);
  }

  protected override void CloseTarget()
  {
    isAddingCompleted = true;
    disruptor?.Shutdown(TimeSpan.FromMilliseconds(Settings.ShutdownTimeoutMs));
    base.CloseTarget();
  }

  protected override void FlushAsync(AsyncContinuation asyncContinuation)
  {
    if (disruptor is null)
      return;

    var cursor = ringBuffer.Cursor;
    var spinWait = new SpinWait();
    var stopwatch = Stopwatch.StartNew();

    while (disruptor.HasBacklog(cursor) && stopwatch.ElapsedMilliseconds < Settings.ShutdownTimeoutMs)
    {
      waitStrategy.SignalAllWhenBlocking();
      spinWait.SpinOnce();
    }
  }
}