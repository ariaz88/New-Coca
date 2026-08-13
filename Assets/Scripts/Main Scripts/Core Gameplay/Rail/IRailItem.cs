/// <summary>
/// Anything that can occupy a rail slot.
///
/// This exists because of a soft-lock. SpawnContoller only refills the rail once
/// spawnedBoxes is empty (NoBoxInList), and BoardController.RemoveSpawnerList
/// pruned that list with "GetComponent&lt;Box&gt;() != null &amp;&amp; box.IsOnBoard".
/// A Defuser is not a Box, so it could never satisfy that predicate: one unused
/// Defuser sitting on the rail would stop the level receiving another box for
/// the rest of the run, and would also mask the rail-exhausted lose check, which
/// requires the same list to be empty.
///
/// Every rail occupant now answers one question - am I finished with my slot -
/// and the prune asks that instead of asking whether something is a Box.
/// </summary>
public interface IRailItem
{
    /// <summary>
    /// True once this item has left the rail for good: placed on the board,
    /// spent, or otherwise no longer waiting to be dragged.
    /// </summary>
    bool IsConsumed { get; }
}
