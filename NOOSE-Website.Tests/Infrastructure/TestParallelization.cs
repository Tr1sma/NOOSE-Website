// Shared in-memory SQLite connections and a global seed counter make cross-class parallelism
// nondeterministic (and unstable under coverage instrumentation); run collections sequentially.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
