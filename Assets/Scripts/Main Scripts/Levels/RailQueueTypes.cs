/// <summary>
/// Where a level's rail boxes come from.
/// </summary>
public enum RailMode
{
    /// <summary>
    /// The original behaviour: every batch is rolled from the level's palette and
    /// fill/mix ranges. Kept as the default so any scene that predates authored
    /// queues keeps playing exactly as before.
    /// </summary>
    RandomPerBatch = 0,

    /// <summary>
    /// Boxes are drawn in order from an authored queue. This is what makes a level
    /// a designed puzzle rather than a dice roll, and what lets the headless
    /// solver prove a level is winnable.
    /// </summary>
    AuthoredQueue = 1
}

/// <summary>
/// What happens when an authored rail queue runs out before the orders are done.
/// </summary>
public enum RailExhaustionPolicy
{
    /// <summary>
    /// The rail stays empty and the level is lost. Closed-puzzle semantics: the
    /// initial boxes plus the queue are exactly the material available, which is
    /// the assumption the solver verifies against.
    /// </summary>
    StopSpawning = 0,

    /// <summary>
    /// The queue repeats from the start. Fully deterministic, but unbounded, so a
    /// level using this cannot carry a meaningful "solved in N moves" proof.
    /// </summary>
    LoopQueue = 1,

    /// <summary>
    /// Falls back to seeded random boxes from the level's palette.
    ///
    /// This is the shipped default. Levels are still designed and verified against
    /// the authored queue alone, so solvability never depends on the fallback -
    /// it exists so a player who squanders the queue is not hard-stuck staring at
    /// an empty rail.
    /// </summary>
    RandomFallback = 2
}
