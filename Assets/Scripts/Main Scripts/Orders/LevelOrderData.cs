using UnityEngine;

/// <summary>
/// One designer-authored order line for a single level.
///
/// A level's Orders panel is built from a list of these. Each entry means
/// "the player must pack <see cref="requiredCount"/> boxes whose sodas are all
/// <see cref="color"/>". The list lives on the scene's Board component so a
/// level keeps its layout and its orders in the same place, exactly like the
/// existing blocked-cell and initial-box level data.
///
/// This type is intentionally a plain serializable class rather than a struct
/// so Unity's SerializedProperty editing (insert, delete, reorder) behaves the
/// same way it already does for InitialBoardBoxData.
/// </summary>
[System.Serializable]
public sealed class LevelOrderData
{
    [Tooltip("Which soda color this order asks for. Uses the project's Soda.SodaColor enum.")]
    public Soda.SodaColor color = Soda.SodaColor.Red;

    [Min(1)]
    [Tooltip("How many packed boxes of this color the level requires.")]
    public int requiredCount = 1;

    public LevelOrderData()
    {
    }

    public LevelOrderData(Soda.SodaColor color, int requiredCount)
    {
        this.color = color;
        this.requiredCount = Mathf.Max(1, requiredCount);
    }
}
