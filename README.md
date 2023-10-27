# NLog.Targets.Background
## BackgroundTargetWrapper

An asynchronous wrapper for NLog. This target reduces the overhead of logging calls by delegating work to a background thread. This is especially suited to targets that may be affected by I/O bottlenecks.

This library uses the LMAX Disruptor inter-threaded message passing framework to achieve scalability and high throughput even with millions of events per second. The modified version of [disruptor-net](https://github.com/disruptor-net/Disruptor-net) is embedded in the assembly, so there are no references to external dependencies.

This library is:
 - low-latency: adding log events to the queue has minimal impact on the performance of the calling code (when `blockWhenFull` is set to `false`)
 - low-overhead: distributes workload between threads, does not waste time and resources only on synchronization
 - scalable: queue throughput doesn't degrade as the number of threads increases
