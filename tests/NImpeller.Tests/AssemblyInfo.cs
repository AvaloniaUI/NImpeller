using Xunit;

// A GL context is thread-affine and we share a single one across the suite, so tests must not
// run in parallel. (Renders are still marshalled onto the context's own worker thread.)
[assembly: CollectionBehavior(DisableTestParallelization = true)]
