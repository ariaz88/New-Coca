using UnityEngine;

public class Soda : MonoBehaviour
{
    public enum SodaColor
    {
        Red,
        Blue,
        Orange,

        // Renamed from Yellow. The gameplay material for this slot (Mat003 on
        // Bottle.prefab) has always been pink - RGB(1.00, 0.38, 0.66) - but the
        // name, the Orders icon and the effect tint were never updated, so the
        // same drink appeared pink on the board and yellow in the UI.
        // The position in the enum is unchanged, so every scene, level asset and
        // saved order that stores this value as an integer keeps working.
        Pink,

        Green,
        Purple,

        // Added for the new Coke Pack art, which ships a yellow can.
        // It is LAST on purpose: every scene, LevelDefinition and saved order
        // stores this enum as an int, so inserting anywhere else would silently
        // repaint the whole campaign.
        //
        // Yellow is NOT spawnable. It exists so the new art has an enum slot to
        // sit in; nothing may put it on the rail or the board. Use
        // SpawnableColors below rather than Enum.GetValues, which would include it.
        Yellow
    }

    /// <summary>
    /// The colours the game is allowed to spawn, in enum order. Yellow is
    /// deliberately absent - see the enum. Enumerate this, never
    /// Enum.GetValues(typeof(SodaColor)), or the new art's yellow can starts
    /// appearing in levels that were never designed or solved for it.
    /// </summary>
    public static readonly SodaColor[] SpawnableColors =
    {
        SodaColor.Red,
        SodaColor.Blue,
        SodaColor.Orange,
        SodaColor.Pink,
        SodaColor.Green,
        SodaColor.Purple
    };

    /// <summary>True when this colour may be placed by a spawner or an order.</summary>
    public static bool IsSpawnable(SodaColor color)
    {
        return color != SodaColor.Yellow;
    }

    public Material[] sodaMaterials;  // Array to hold the materials for each soda color
    [SerializeField]private Renderer rend;
    public SodaColor sodaColor;  // Variable to store the soda's color

    [Header("Second renderer (new Coke Pack art)")]
    [Tooltip("Optional. The new can is TWO meshes - body and lid - and each colour " +
             "is a pair of materials, so one Renderer cannot carry a colour. Leave " +
             "both empty for the legacy single-mesh Bottle.prefab.")]
    [SerializeField] private Renderer topRend;
    [SerializeField] private Material[] topMaterials;

    private void Start()
    {
        //rend = GetComponentInChildren<Renderer>();
    }

    /// <summary>
    /// Applies one colour index to every renderer this soda has. The legacy
    /// Bottle is a single mesh; the new Coke Pack can is a body plus a lid, and
    /// a colour is only complete when both are set - setting the body alone
    /// leaves a red can wearing a green lid.
    /// A missing entry warns and leaves the renderer alone rather than throwing,
    /// because Orange and Pink have no new art yet.
    /// </summary>
    private void ApplyMaterials(SodaColor color)
    {
        int index = (int)color;
        ApplyTo(rend, sodaMaterials, index, "sodaMaterials");
        if (topRend != null)
        {
            ApplyTo(topRend, topMaterials, index, "topMaterials");
        }
    }

    private void ApplyTo(Renderer target, Material[] table, int index, string tableName)
    {
        if (target == null)
        {
            return;
        }

        if (table == null || index >= table.Length || table[index] == null)
        {
            Debug.LogWarning(
                $"'{name}' has no {tableName} entry for {(SodaColor)index}.", this);
            return;
        }

        target.material = table[index];
    }

    public void SetColor(SodaColor color)
    {
        // Store the chosen color in the sodaColor variable
        sodaColor = color;

        // Was a six-case switch that hard-indexed sodaMaterials[0..5]. Every case
        // assigned exactly (int)color, so the switch only restated the enum
        // order while hiding the fact that it covered one renderer and six
        // colours. ApplyMaterials covers both renderers and warns on a gap.
        ApplyMaterials(color);
    }

    public void SetColor1(SodaColor color)
    {
        sodaColor = color;
        ApplyMaterials(color);
    }
}
