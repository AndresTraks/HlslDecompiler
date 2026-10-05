using NUnit.Framework;

// How many tests run at once.
//
// What a run costs is fxc, and what fxc costs is mostly not fxc: see
// WinePrefixWarmUp, which holds the Wine prefix open and is worth twelve times more
// than this file is. With the prefix warm, what is left of a case really is the
// compile, and those overlap - a child process each, so the gain flattens as soon as
// the launches do. Measured over the whole corpus on an eight core machine, all
// three runs passing the same 3058:
//
//     serial, cold prefix (what this replaced)    896s
//     one worker, prefix held open                 73s
//     four workers                                 35s
//     eight workers                                31s
//
// Four, then. Eight buys four seconds and leaves nothing for the OS and for Wine's
// own services, and a shared prefix is the one part of this not ours to reason
// about. A runsettings file can say otherwise without a rebuild - NumberOfTestWorkers
// overrides this.
//
// Nothing is shared to race over: every fixture here is stateless, every case writes
// to a path of its own named after its profile and shader, and the decompiler holds
// no mutable static state. The exception is CostSweepTests, which writes
// process-global flags and says so where it is marked.
[assembly: LevelOfParallelism(4)]

// Fixtures run beside each other; each one says for itself whether its cases do.
[assembly: Parallelizable(ParallelScope.Children)]
