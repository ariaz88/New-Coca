using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single shared lookup that turns a <see cref="Soda.SodaColor"/> into the two
/// presentation values the Orders feature needs:
///
///   1. iconSprite  - the flat sprite drawn inside an Orders panel slot. It is
///                    produced by the editor tool SodaIconBaker, which renders
///                    the real in-game soda model so the panel matches the
///                    board instead of using unrelated artwork.
///   2. effectColor - the tint used by the flying streak, its sparkle particles
///                    and the "+1" label, so a green soda always sends a green
///                    trail and a blue soda a blue one.
///
/// Keeping both in one asset means artists can retune the whole feature from a
/// single place, and no runtime script has to hard-code a color table.
///
/// Missing entries never throw. iconSprite falls back to null (the slot simply
/// draws no icon) and effectColor falls back to <see cref="fallbackColor"/>, so
/// an unfinished asset degrades quietly instead of breaking a level.
/// </summary>
[CreateAssetMenu(
    fileName = "SodaVisualLibrary",
    menuName = "Coca Sorting/Soda Visual Library",
    order = 0)]
public sealed class SodaVisualLibrary : ScriptableObject
{
    /// <summary>One row of the lookup table.</summary>
    [System.Serializable]
    public sealed class Entry
    {
        public Soda.SodaColor color = Soda.SodaColor.Red;

        [Tooltip("Sprite shown in an Orders slot. Bake it from the real soda model with Tools > Coca Sorting > Bake Soda Icons.")]
        public Sprite iconSprite;

        [Tooltip("Tint for the flying streak, its sparkles and the +1 label.")]
        public Color effectColor = Color.white;
    }

    [SerializeField]
    private List<Entry> entries = new List<Entry>();

    [SerializeField, Tooltip("Used when a color has no entry, so a missing row can never break a level.")]
    private Color fallbackColor = Color.white;

    private Dictionary<Soda.SodaColor, Entry> lookup;

    private static SodaVisualLibrary resolvedInstance;

    /// <summary>Read-only access for the editor tools that fill this asset.</summary>
    public IReadOnlyList<Entry> Entries => entries;

    private void OnEnable()
    {
        // Cleared rather than rebuilt: the dictionary is created lazily so a
        // domain reload or an inspector edit always produces a fresh cache.
        lookup = null;
    }

    private void OnValidate()
    {
        lookup = null;
    }

    /// <summary>Returns the Orders-slot sprite for a color, or null when unset.</summary>
    public Sprite GetIcon(Soda.SodaColor color)
    {
        Entry entry = FindEntry(color);
        return entry != null ? entry.iconSprite : null;
    }

    /// <summary>Returns the VFX tint for a color, or the fallback when unset.</summary>
    public Color GetEffectColor(Soda.SodaColor color)
    {
        Entry entry = FindEntry(color);
        return entry != null ? entry.effectColor : fallbackColor;
    }

    /// <summary>
    /// Creates or updates the row for a color. Used by the icon baking tool so
    /// it can write results back without the artist wiring rows by hand.
    /// </summary>
    public Entry GetOrCreateEntry(Soda.SodaColor color)
    {
        Entry entry = FindEntry(color);
        if (entry != null)
        {
            return entry;
        }

        entry = new Entry { color = color, effectColor = fallbackColor };
        entries.Add(entry);
        lookup = null;
        return entry;
    }

    /// <summary>
    /// Finds a library when a component has none assigned.
    ///
    /// Forgetting to drag this asset onto OrderPanelUI is the single easiest
    /// mistake to make in this feature, and it fails silently: every slot simply
    /// draws no drink. This resolver removes that failure mode.
    ///
    /// Order of attempts:
    ///   1. A previously resolved instance, cached for the session.
    ///   2. Resources/SodaVisualLibrary, which also works in a player build if
    ///      the asset is placed in a Resources folder.
    ///   3. In the editor only, the first SodaVisualLibrary anywhere in the
    ///      project, so development never blocks on asset placement.
    ///
    /// A build with the asset outside Resources and no inspector reference still
    /// gets null, which is why the bake tool also writes the reference directly
    /// into the open scene.
    /// </summary>
    public static SodaVisualLibrary Resolve()
    {
        if (resolvedInstance != null)
        {
            return resolvedInstance;
        }

        resolvedInstance = Resources.Load<SodaVisualLibrary>("SodaVisualLibrary");

#if UNITY_EDITOR
        if (resolvedInstance == null)
        {
            string[] found = UnityEditor.AssetDatabase.FindAssets("t:SodaVisualLibrary");
            if (found.Length > 0)
            {
                resolvedInstance = UnityEditor.AssetDatabase.LoadAssetAtPath<SodaVisualLibrary>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(found[0]));
            }
        }
#endif

        return resolvedInstance;
    }

    /// <summary>
    /// Approximate tint for a soda color, used when no library asset is assigned
    /// yet. It exists so the travelling streak is still color-coded during early
    /// setup instead of every drink sending the same white trail.
    /// </summary>
    public static Color DefaultColorFor(Soda.SodaColor color)
    {
        switch (color)
        {
            case Soda.SodaColor.Red: return new Color(0.95f, 0.25f, 0.25f);
            case Soda.SodaColor.Blue: return new Color(0.35f, 0.62f, 1f);
            case Soda.SodaColor.Orange: return new Color(1f, 0.65f, 0.20f);
            // Matches Mat003 on Bottle.prefab, the material this colour actually
            // renders with in game.
            case Soda.SodaColor.Pink: return new Color(1f, 0.38f, 0.66f);
            case Soda.SodaColor.Green: return new Color(0.45f, 0.90f, 0.40f);
            case Soda.SodaColor.Purple: return new Color(0.72f, 0.45f, 0.95f);
            // Unspawnable, so this never reaches an order card in normal play -
            // it is here so a hand-authored Yellow reads as yellow instead of
            // falling through to the white fallback and looking like a bug.
            case Soda.SodaColor.Yellow: return new Color(1f, 0.83f, 0.20f);
            default: return Color.white;
        }
    }

    private Entry FindEntry(Soda.SodaColor color)
    {
        if (lookup == null)
        {
            lookup = new Dictionary<Soda.SodaColor, Entry>();
            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                if (entry != null)
                {
                    // First row wins so a duplicated color cannot flip behavior
                    // depending on list order.
                    lookup[entry.color] = lookup.ContainsKey(entry.color)
                        ? lookup[entry.color]
                        : entry;
                }
            }
        }

        return lookup.TryGetValue(color, out Entry found) ? found : null;
    }
}
