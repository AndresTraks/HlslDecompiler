using NUnit.Framework;

// Four workers: more gains little with fxc under Wine and leaves nothing for the
// OS. NumberOfTestWorkers in a runsettings file overrides this. Fixtures are
// stateless and write to their own paths; CostSweepTests is the exception.
[assembly: LevelOfParallelism(4)]

// Each fixture says for itself whether its cases run in parallel.
[assembly: Parallelizable(ParallelScope.Children)]
