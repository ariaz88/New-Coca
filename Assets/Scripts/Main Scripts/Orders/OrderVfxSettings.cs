using DG.Tweening;
using UnityEngine;

/// <summary>
/// Every tunable value of the match-confirmation and order-delivery effect, in one
/// asset.
///
/// Why an asset rather than fields on the director
/// -----------------------------------------------
/// The director is a component that lives in five level scenes. Tuning a value on
/// it means opening a scene, changing it, saving, and repeating four more times, and
/// the five copies drift apart. A ScriptableObject is edited once and every scene
/// follows, which is what makes this effect actually tunable in practice.
///
/// The director falls back to <see cref="CreateDefault"/> when no asset is assigned,
/// so a scene that has not been given one still plays the correct effect rather than
/// nothing at all. That fallback is also the authoritative record of the intended
/// values: the numbers below are the ones the look was designed around.
/// </summary>
[CreateAssetMenu(
    fileName = "OrderVfxSettings",
    menuName = "Coca Sorting/Order VFX Settings",
    order = 1)]
public sealed class OrderVfxSettings : ScriptableObject
{
    // ------------------------------------------------------------------ palette

    [Header("Palette")]
    [SerializeField, Tooltip("The hot centre of everything. Always the brightest element on screen.")]
    private Color coreWhite = new Color(1f, 1f, 1f, 1f);

    [SerializeField, Tooltip("Immediately behind the head. Barely tinted, so the core still reads as white.")]
    private Color iceCyan = HexColor(0xDD, 0xFB, 0xFF);

    [SerializeField, Tooltip("The dominant colour of the middle glow.")]
    private Color electricCyan = HexColor(0x65, 0xD9, 0xFF);

    [SerializeField, Tooltip("Middle of the trail, where the cyan cools toward blue.")]
    private Color softBlue = HexColor(0x75, 0xAF, 0xFF);

    [SerializeField, Tooltip("Older trail. The first point where the streak stops being blue.")]
    private Color paleLavender = HexColor(0xC8, 0xA7, 0xFF);

    [SerializeField, Tooltip("Outermost halo only. Never allowed to dominate.")]
    private Color pinkLavender = HexColor(0xF0, 0xB8, 0xFF);

    [SerializeField, Tooltip("The warm accent at the box. Used by the confirmation flash and its dust.")]
    private Color confirmGold = HexColor(0xFF, 0xC8, 0x4A);

    [SerializeField, Tooltip("Deeper gold on the outside of the confirmation flash, so its rim stays warm.")]
    private Color confirmGoldDeep = HexColor(0xFF, 0x9E, 0x1B);

    [SerializeField, Range(0f, 1f)]
    [Tooltip("How far the middle and outer trail bend toward the soda's own colour. " +
             "0 is the fixed magic palette; 1 is fully soda-tinted. The white core and " +
             "ice-cyan inner glow are never tinted at any value.")]
    private float sodaTintStrength = 0.35f;

    [SerializeField]
    [Tooltip("Rotates warm soda tints (red/orange/yellow) toward magenta before blending, " +
             "so a warm drink still produces a pink-lavender halo instead of the gold " +
             "explosion this effect replaces. Turn off to let raw soda colours through. " +
             "Never applied to the core, which must show the drink's real colour.")]
    private bool guardAgainstWarmTints = true;

    [Header("Palette - Core Follows Soda")]
    [SerializeField]
    [Tooltip("Colours the core of the trail with the packed drink's own colour, so a red " +
             "soda sends a red streak. The outer lavender/pink halo is unaffected, which is " +
             "what keeps every delivery reading as the same magical effect.")]
    private bool coreFollowsSodaColor = true;

    [SerializeField, Range(0f, 0.5f)]
    [Tooltip("Fraction of the trail nearest the head that stays white-hot before the soda " +
             "colour takes over. 0 by default: any white lead makes the streak read as " +
             "coloured at the front and white behind, which hides the drink's colour.")]
    private float coreWhiteLead = 0f;

    [SerializeField, Range(0.3f, 1f)]
    [Tooltip("Saturation of the soda-coloured core. High enough that blue reads as blue and " +
             "red as red; below about 0.8 the colour washes out against the bright glow.")]
    private float coreSodaSaturation = 0.95f;

    // ------------------------------------------------------- phase 1, the box

    [Header("Phase 1 - Match Confirmation")]
    [SerializeField, Min(0.01f), Tooltip("Lifetime of the soft flash at the centre of the completed box.")]
    private float confirmFlashDuration = 0.18f;

    [SerializeField, Min(0f), Tooltip("Diameter of that flash, in canvas units.")]
    private float confirmFlashSize = 150f;

    [SerializeField, Min(0), Tooltip("Tiny warm-white motes thrown outward from the box. 0 disables them.")]
    private int confirmDustCount = 11;

    [SerializeField, Tooltip("Lifetime range of the confirmation dust.")]
    private Vector2 confirmDustLifetime = new Vector2(0.25f, 0.40f);

    [SerializeField, Tooltip("Size range of the confirmation dust, in canvas units.")]
    private Vector2 confirmDustSize = new Vector2(9f, 20f);

    [SerializeField, Tooltip("How far the dust drifts outward and upward before fading.")]
    private Vector2 confirmDustRadius = new Vector2(30f, 78f);

    [SerializeField, Tooltip("Optional subtle ring at the box. Off by default: a large ring at the " +
                             "source reads as an explosion and fights the delivery streak.")]
    private bool confirmRingEnabled = false;

    [SerializeField, Min(0.01f)] private float confirmRingDuration = 0.16f;
    [SerializeField, Min(0f), Tooltip("Final diameter of the ring, in canvas units.")]
    private float confirmRingSize = 190f;
    [SerializeField, Range(0f, 1f)] private float confirmRingAlpha = 0.35f;

    // ------------------------------------------------------------- plus one

    [Header("Phase 1 - Plus One Popup")]
    [SerializeField] private bool showPlusOne = true;
    [SerializeField] private bool showPlusOneIcon = true;
    [SerializeField, Min(0.01f)] private float plusOneDuration = 0.62f;

    [SerializeField, Min(0f), Tooltip("How far the popup rises, in canvas units.")]
    private float plusOneRise = 34f;

    [SerializeField, Min(1f)] private float plusOneFontSize = 52f;
    [SerializeField, Min(1f)] private float plusOneIconSize = 76f;
    [SerializeField, Tooltip("Distance between the centre of the text and the centre of the icon.")]
    private float plusOneIconGap = 92f;

    [SerializeField, Range(0f, 1f), Tooltip("Scale it starts at, before the overshoot.")]
    private float plusOneStartScale = 0.70f;

    [SerializeField, Min(1f), Tooltip("Scale at the top of the overshoot, before settling to 1.")]
    private float plusOnePeakScale = 1.15f;

    // -------------------------------------------------------------- flight

    [Header("Phase 2 - Flight")]
    [SerializeField, Min(0f), Tooltip("Pause between the confirmation and the launch, so the +1 is read first.")]
    private float deliveryDelay = 0.14f;

    [SerializeField, Min(0.05f)] private float flightDuration = 0.48f;

    [SerializeField, Tooltip("Ease applied to progress along the Bezier.")]
    private Ease flightEase = Ease.InOutCubic;

    [SerializeField, Range(-0.4f, 0.4f)]
    [Tooltip("Sideways bow of the path as a fraction of its length. Kept small on purpose: " +
             "the reference flight is nearly direct, and a dramatic arc reads as a lob.")]
    private float curvature = 0.05f;

    // -------------------------------------------------------------- the head

    [Header("Phase 2 - Travelling Head")]
    [SerializeField, Min(0f), Tooltip("Diameter of the tiny pure-white centre, in canvas units.")]
    private float headCoreSize = 26f;

    [SerializeField, Min(0f), Tooltip("Diameter of the cyan-blue glow around it.")]
    private float headGlowSize = 62f;

    [SerializeField, Min(0f), Tooltip("Diameter of the wide lavender halo. The head's visible extent.")]
    private float headHaloSize = 108f;

    [SerializeField, Range(0f, 0.4f), Tooltip("Depth of the head's pulse. 0.10 is a 0.90-1.10 range.")]
    private float headPulseAmount = 0.10f;

    [SerializeField, Min(0f), Tooltip("Pulses over the whole flight. Two or three reads as alive; more wobbles.")]
    private float headPulseCycles = 2.5f;

    // ------------------------------------------------------------- the ribbon

    [Header("Phase 2 - Trail Ribbon")]
    [SerializeField, Min(0f)]
    [Tooltip("Width of the soda-coloured core, in canvas units, at the 1125x2436 reference " +
             "resolution. Measured off the reference footage, where the bright centre is " +
             "roughly a quarter the width of the glow around it.")]
    private float ribbonCoreWidth = 30f;

    [SerializeField, Min(0f)]
    [Tooltip("Width of the white-hot line running down the centre of the coloured core. " +
             "This is what makes the streak read as light rather than as a coloured stripe: " +
             "in the reference the centre is white and the drink's colour surrounds it. " +
             "Set to 0 to remove it and leave the core purely the drink's colour.")]
    private float ribbonHotWidth = 13f;

    [SerializeField, Range(0f, 1f), Tooltip("Opacity of that white-hot centre line.")]
    private float ribbonHotAlpha = 0.95f;

    [SerializeField, Min(0f), Tooltip("Width of the transition band. Roughly twice the core.")]
    private float ribbonMiddleWidth = 34f;

    [SerializeField, Min(0f)]
    [Tooltip("Width of the outer glow. Kept well clear of the middle band so the halo is " +
             "visibly separate from the core rather than merged into it. Measured off the " +
             "reference, where the soft bloom is five to six times the bright centre.")]
    private float ribbonOuterWidth = 100f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("How far the middle band is lifted from the core colour toward the halo colour. " +
             "0 makes it identical to the core; 1 makes it identical to the halo.")]
    private float middleIceLift = 0.45f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("How much of the drink's colour the outer halo takes. The halo is white-based " +
             "on purpose: it should read as a soft icy glow carrying only a hint of the " +
             "match colour. Pushing this up turns the halo into a coloured band and it stops " +
             "separating from the core.")]
    private float haloSodaTint = 0.18f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Fraction of the trail held at full opacity before it starts fading. Low on " +
             "purpose: a high value makes the streak an even-brightness cylinder that stops " +
             "abruptly, while a low one lets the tail dissolve away gradually the way real " +
             "exhaust does.")]
    private float ribbonOpacityHold = 0.22f;

    [SerializeField, Min(0.05f)]
    [Tooltip("How long a point of the trail stays visible. This is what decides whether the " +
             "streak spans the route or trails a stub behind the head; it must be a large " +
             "fraction of the flight duration.")]
    private float ribbonLifetime = 0.42f;

    [SerializeField, Min(0.001f), Tooltip("Minimum seconds between recorded trail points. Caps the mesh size.")]
    private float ribbonSampleInterval = 0.008f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Peak opacity of the ribbon as a whole. Slightly below 1 on purpose, so the " +
             "board stays faintly readable through the streak and it looks like light " +
             "rather than a painted stripe.")]
    private float ribbonAlpha = 0.85f;

    [SerializeField, Range(0f, 1f), Tooltip("Opacity of the middle band relative to the core.")]
    private float ribbonMiddleAlpha = 0.5f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Opacity of the outer glow. Its cross-section is hollow in the middle, so this " +
             "can be raised until the halo is clearly visible without whitening the core.")]
    private float ribbonOuterAlpha = 0.42f;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Width of the tail relative to the head. 1 keeps the streak an even cylinder; " +
             "low values narrow it to a wisp so it looks like it is dispersing rather than " +
             "being cut off.")]
    private float ribbonTailTaper = 0.28f;

    // -------------------------------------------------------------- particles

    [Header("Phase 2 - Star Trail")]
    [SerializeField, Min(0)]
    [Tooltip("Emission points along the flight. Each one releases a small cluster, so the " +
             "total number of stars is this times the average cluster size.")]
    private int starCount = 60;

    [SerializeField]
    [Tooltip("Stars released at each emission point. Releasing several at once, rather than " +
             "one at a time, is what turns a line of evenly spaced stars into a dense drift " +
             "where some overlap and some sit side by side.")]
    private Vector2Int starCluster = new Vector2Int(3, 6);

    [SerializeField, Min(0f)]
    [Tooltip("How far a star may be displaced from the path at birth, in canvas units, both " +
             "sideways and along the direction of travel. This is the main control for how " +
             "scattered the trail looks. The reference spreads stars into a wide cloud around " +
             "the streak, not a line beside it.")]
    private float starSpawnScatter = 62f;

    [SerializeField]
    [Tooltip("Lifetime range. Long enough that stars accumulate along the route and hang in " +
             "the air behind the head instead of winking out just after they are born.")]
    private Vector2 starLifetime = new Vector2(0.32f, 0.7f);

    [SerializeField]
    [Tooltip("Unused by the trail, which picks from the three discrete tiers below. Kept " +
             "because the impact burst still reads a plain range.")]
    private Vector2 starSize = new Vector2(9f, 34f);

    [SerializeField, Min(1f)]
    [Tooltip("Diameter of a large star, in canvas units. The medium and small tiers are " +
             "fractions of this, so the whole star field scales from one number.")]
    private float starLargeSize = 34f;

    [SerializeField, Range(0.1f, 1f), Tooltip("Medium star size, as a fraction of a large one.")]
    private float starMediumRatio = 0.58f;

    [SerializeField, Range(0.05f, 1f), Tooltip("Small star size, as a fraction of a large one.")]
    private float starSmallRatio = 0.32f;

    [SerializeField]
    [Tooltip("How often each tier is chosen: x large, y medium, z small. Weighted toward the " +
             "small end on purpose, so a few large stars punctuate a field of smaller ones " +
             "rather than every star competing for attention.")]
    private Vector3 starTierWeights = new Vector3(0.18f, 0.34f, 0.48f);

    [SerializeField, Tooltip("Sideways speed given to a star at birth, in canvas units per second.")]
    private Vector2 starDrift = new Vector2(10f, 55f);

    [SerializeField, Tooltip("Degrees per second a star turns while it lives. Slow, so it reads as float not spin.")]
    private Vector2 starSpin = new Vector2(-70f, 70f);

    [SerializeField, Min(0f), Tooltip("Turbulence applied over a star's life, in canvas units.")]
    private float starNoise = 16f;

    [Header("Phase 2 - Dust Trail")]
    [SerializeField, Min(0)] private int dustCount = 40;
    [SerializeField] private Vector2 dustLifetime = new Vector2(0.20f, 0.35f);
    [SerializeField] private Vector2 dustSize = new Vector2(7f, 18f);
    [SerializeField] private Vector2 dustDrift = new Vector2(6f, 30f);

    [SerializeField, Range(0f, 1f), Tooltip("Dust opacity relative to the stars. Deliberately dimmer.")]
    private float dustAlpha = 0.55f;

    // ----------------------------------------------------------------- impact

    [Header("Phase 3 - Destination Impact")]
    [SerializeField, Min(0.01f)] private float impactFlashDuration = 0.2f;
    [SerializeField, Min(0f)] private float impactFlashSize = 130f;

    [SerializeField, Min(0), Tooltip("Stars thrown radially from the icon on arrival.")]
    private int impactStarCount = 8;

    [SerializeField] private Vector2 impactStarSize = new Vector2(14f, 32f);
    [SerializeField] private Vector2 impactStarRadius = new Vector2(28f, 84f);
    [SerializeField, Min(0.05f)] private float impactStarLifetime = 0.4f;

    [SerializeField, Tooltip("Thin cyan-white ring that expands out of the icon.")]
    private bool impactRingEnabled = true;

    [SerializeField, Min(0.01f)] private float impactRingDuration = 0.18f;
    [SerializeField, Min(0f)] private float impactRingSize = 150f;
    [SerializeField] private Vector2 impactRingScale = new Vector2(0.40f, 1.30f);
    [SerializeField, Range(0f, 1f)] private float impactRingAlpha = 0.75f;

    [SerializeField, Min(0f)]
    [Tooltip("Seconds the ribbon is allowed to keep fading after the head has landed. " +
             "The head is hidden at impact, but pooling the ribbon before its tail has " +
             "faded would cut the streak off mid-air.")]
    private float trailLingerAfterImpact = 0.35f;

    [Header("Phase 3 - Slot Reaction")]
    [SerializeField]
    [Tooltip("Halo flashed behind the slot icon on arrival. Overrides the colour serialized " +
             "on OrderSlotUI, so the palette has one source and existing scenes need no edit.")]
    private Color slotGlowColor = HexColor(0xDD, 0xFB, 0xFF);

    [SerializeField, Min(1f), Tooltip("Peak of the icon punch. 1.18 is the overshoot.")]
    private float iconPunchPeak = 1.18f;

    [SerializeField, Range(0.5f, 1f), Tooltip("Dip after the overshoot, before settling back to 1.")]
    private float iconPunchDip = 0.95f;

    [SerializeField, Min(0.05f), Tooltip("Total seconds of the icon punch.")]
    private float iconPunchDuration = 0.22f;

    // ---------------------------------------------------------------- layering

    [Header("Layering")]
    [SerializeField]
    [Tooltip("Keeps the effect layer as the canvas's last child, so streaks draw over the " +
             "Orders card. Turn off if the card must occlude them.")]
    private bool drawAbovePanel = true;

    // ----------------------------------------------------------------- access

    public Color CoreWhite => coreWhite;
    public Color IceCyan => iceCyan;
    public Color ElectricCyan => electricCyan;
    public Color SoftBlue => softBlue;
    public Color PaleLavender => paleLavender;
    public Color PinkLavender => pinkLavender;
    public Color ConfirmGold => confirmGold;
    public Color ConfirmGoldDeep => confirmGoldDeep;
    public float SodaTintStrength => sodaTintStrength;
    public bool GuardAgainstWarmTints => guardAgainstWarmTints;
    public bool CoreFollowsSodaColor => coreFollowsSodaColor;
    public float CoreWhiteLead => coreWhiteLead;

    /// <summary>
    /// The drink's own colour, brightened for use as the core of the trail.
    ///
    /// Deliberately does NOT go through <see cref="Tint"/>: the warm-hue guard
    /// exists to stop the outer halo turning gold, but the core is supposed to show
    /// the drink's real colour, so a red soda must produce a red core rather than a
    /// magenta one.
    ///
    /// Value is pushed to full and saturation eased back slightly, which keeps a
    /// dark drink from producing a muddy streak while stopping a vivid one from
    /// looking like flat paint.
    /// </summary>
    public Color SodaCore(Color sodaColor)
    {
        Color.RGBToHSV(sodaColor, out float hue, out float saturation, out float value);

        // A nearly colourless soda has no meaningful hue to show, so it falls back
        // to the palette's ice cyan rather than to a washed-out grey.
        if (saturation < 0.05f)
        {
            return iceCyan;
        }

        Color result = Color.HSVToRGB(hue, saturation * coreSodaSaturation, 1f);
        result.a = 1f;
        return result;
    }

    public float ConfirmFlashDuration => confirmFlashDuration;
    public float ConfirmFlashSize => confirmFlashSize;
    public int ConfirmDustCount => confirmDustCount;
    public Vector2 ConfirmDustLifetime => confirmDustLifetime;
    public Vector2 ConfirmDustSize => confirmDustSize;
    public Vector2 ConfirmDustRadius => confirmDustRadius;
    public bool ConfirmRingEnabled => confirmRingEnabled;
    public float ConfirmRingDuration => confirmRingDuration;
    public float ConfirmRingSize => confirmRingSize;
    public float ConfirmRingAlpha => confirmRingAlpha;

    public bool ShowPlusOne => showPlusOne;
    public bool ShowPlusOneIcon => showPlusOneIcon;
    public float PlusOneDuration => plusOneDuration;
    public float PlusOneRise => plusOneRise;
    public float PlusOneFontSize => plusOneFontSize;
    public float PlusOneIconSize => plusOneIconSize;
    public float PlusOneIconGap => plusOneIconGap;
    public float PlusOneStartScale => plusOneStartScale;
    public float PlusOnePeakScale => plusOnePeakScale;

    public float DeliveryDelay => deliveryDelay;
    public float FlightDuration => flightDuration;
    public Ease FlightEase => flightEase;
    public float Curvature => curvature;

    public float HeadCoreSize => headCoreSize;
    public float HeadGlowSize => headGlowSize;
    public float HeadHaloSize => headHaloSize;
    public float HeadPulseAmount => headPulseAmount;
    public float HeadPulseCycles => headPulseCycles;

    public float RibbonCoreWidth => ribbonCoreWidth;
    public float RibbonHotWidth => ribbonHotWidth;
    public float RibbonHotAlpha => ribbonHotAlpha;
    public float RibbonMiddleWidth => ribbonMiddleWidth;
    public float RibbonOuterWidth => ribbonOuterWidth;
    public float RibbonLifetime => ribbonLifetime;
    public float RibbonSampleInterval => ribbonSampleInterval;
    public float RibbonAlpha => ribbonAlpha;
    public float RibbonMiddleAlpha => ribbonMiddleAlpha;
    public float RibbonOuterAlpha => ribbonOuterAlpha;
    public float RibbonTailTaper => ribbonTailTaper;
    public float MiddleIceLift => middleIceLift;
    public float HaloSodaTint => haloSodaTint;
    public float RibbonOpacityHold => ribbonOpacityHold;

    public int StarCount => starCount;
    public Vector2Int StarCluster => starCluster;
    public float StarSpawnScatter => starSpawnScatter;
    public Vector2 StarLifetime => starLifetime;
    public Vector2 StarSize => starSize;

    /// <summary>
    /// One of the three star sizes, chosen by the configured weights.
    ///
    /// Discrete tiers rather than a continuous range: a range produces a smear of
    /// near-identical middling sizes, while three clearly different sizes read as a
    /// natural mix of large, medium and small the way the reference does.
    /// </summary>
    public float PickStarSize()
    {
        float large = Mathf.Max(starTierWeights.x, 0f);
        float medium = Mathf.Max(starTierWeights.y, 0f);
        float small = Mathf.Max(starTierWeights.z, 0f);

        float total = large + medium + small;
        if (total <= 0.0001f)
        {
            return starLargeSize;
        }

        float roll = Random.value * total;

        if (roll < large)
        {
            return starLargeSize;
        }

        return roll < large + medium
            ? starLargeSize * starMediumRatio
            : starLargeSize * starSmallRatio;
    }
    public Vector2 StarDrift => starDrift;
    public Vector2 StarSpin => starSpin;
    public float StarNoise => starNoise;

    public int DustCount => dustCount;
    public Vector2 DustLifetime => dustLifetime;
    public Vector2 DustSize => dustSize;
    public Vector2 DustDrift => dustDrift;
    public float DustAlpha => dustAlpha;

    public float ImpactFlashDuration => impactFlashDuration;
    public float ImpactFlashSize => impactFlashSize;
    public int ImpactStarCount => impactStarCount;
    public Vector2 ImpactStarSize => impactStarSize;
    public Vector2 ImpactStarRadius => impactStarRadius;
    public float ImpactStarLifetime => impactStarLifetime;
    public bool ImpactRingEnabled => impactRingEnabled;
    public float ImpactRingDuration => impactRingDuration;
    public float ImpactRingSize => impactRingSize;
    public Vector2 ImpactRingScale => impactRingScale;
    public float ImpactRingAlpha => impactRingAlpha;
    public float TrailLingerAfterImpact => trailLingerAfterImpact;

    public Color SlotGlowColor => slotGlowColor;
    public float IconPunchPeak => iconPunchPeak;
    public float IconPunchDip => iconPunchDip;
    public float IconPunchDuration => iconPunchDuration;

    public bool DrawAbovePanel => drawAbovePanel;

    private static OrderVfxSettings resolvedInstance;

    /// <summary>
    /// Finds the settings asset when a component has none assigned.
    ///
    /// Same shape as <see cref="SodaVisualLibrary.Resolve"/> and for the same
    /// reason: forgetting to drag the asset onto a component fails silently, and
    /// this feature exists in five scenes where that mistake is easy to make. The
    /// built-in default is returned as a last resort so the effect always plays.
    ///
    /// Order of attempts:
    ///   1. A previously resolved instance, cached for the session.
    ///   2. Resources/OrderVfxSettings, which also works in a player build.
    ///   3. In the editor only, the first asset of this type in the project.
    ///   4. The built-in default from <see cref="CreateDefault"/>.
    /// </summary>
    public static OrderVfxSettings Resolve()
    {
        if (resolvedInstance != null)
        {
            return resolvedInstance;
        }

        resolvedInstance = Resources.Load<OrderVfxSettings>("OrderVfxSettings");

#if UNITY_EDITOR
        if (resolvedInstance == null)
        {
            string[] found = UnityEditor.AssetDatabase.FindAssets("t:OrderVfxSettings");
            if (found.Length > 0)
            {
                resolvedInstance = UnityEditor.AssetDatabase.LoadAssetAtPath<OrderVfxSettings>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(found[0]));
            }
        }
#endif

        if (resolvedInstance == null)
        {
            resolvedInstance = CreateDefault();
        }

        return resolvedInstance;
    }

    /// <summary>
    /// The settings the effect uses when no asset has been assigned.
    ///
    /// Created in memory rather than loaded, so a scene missing the asset still
    /// plays a correct effect. It is marked HideAndDontSave so it can never be
    /// mistaken for a real project asset or leak into a build as a stray object.
    /// </summary>
    public static OrderVfxSettings CreateDefault()
    {
        OrderVfxSettings created = CreateInstance<OrderVfxSettings>();
        created.name = "OrderVfxSettings (built-in default)";
        created.hideFlags = HideFlags.HideAndDontSave;
        return created;
    }

    /// <summary>
    /// The trail colour for a soda, with the palette kept firmly in charge.
    ///
    /// <paramref name="paletteColor"/> is the fixed palette stop for this position
    /// along the trail. The soda's own colour is blended in at
    /// <see cref="SodaTintStrength"/>, but only after warm hues have been rotated
    /// away from gold, because a gold-dominant streak is exactly the look this
    /// effect exists to replace.
    /// </summary>
    public Color Tint(Color paletteColor, Color sodaColor, float extraStrength = 1f)
    {
        float strength = Mathf.Clamp01(sodaTintStrength * extraStrength);
        if (strength <= 0.001f)
        {
            return paletteColor;
        }

        Color usable = guardAgainstWarmTints ? CoolWarmHue(sodaColor) : sodaColor;

        // Only the hue is borrowed. Lerping raw RGB would also drag the palette's
        // brightness toward a dark soda and dull the whole trail.
        Color blended = Color.Lerp(paletteColor, usable, strength);
        return NormalizeBrightness(blended, paletteColor);
    }

    /// <summary>
    /// Rotates a red, orange or yellow hue up into the magenta/lavender band while
    /// leaving cyans, blues and purples alone. Saturation and value are untouched,
    /// so a vivid orange stays vivid, it just arrives as vivid pink.
    /// </summary>
    private static Color CoolWarmHue(Color color)
    {
        Color.RGBToHSV(color, out float hue, out float saturation, out float value);

        // Hue is normalised 0-1. Warm runs from red (0) through yellow (~0.17).
        // Anything above 0.17 and below the wrap point is already cool enough.
        //
        // The wrap sits at 0.96 rather than 0.92 so the pink drink (hue ~0.925)
        // passes through untouched. At 0.92 it was caught by the guard and
        // rotated down to ~0.75, which is violet - it made pink's trail almost
        // indistinguishable from the purple drink's. Deep reds above 0.96 are
        // still guarded, which is what the rule was actually for.
        const float warmEnd = 0.17f;
        const float warmWrap = 0.96f;

        bool isWarm = hue <= warmEnd || hue >= warmWrap;
        if (!isWarm)
        {
            return color;
        }

        // Map the warm band onto 0.75-0.88, which is violet through magenta.
        float distanceIntoBand = hue >= warmWrap ? hue - warmWrap : hue + (1f - warmWrap);
        float bandWidth = warmEnd + (1f - warmWrap);
        float normalized = bandWidth <= Mathf.Epsilon ? 0f : distanceIntoBand / bandWidth;

        float cooled = Mathf.Lerp(0.75f, 0.88f, normalized);
        Color result = Color.HSVToRGB(cooled, saturation, value);
        result.a = color.a;
        return result;
    }

    /// <summary>
    /// Restores the palette's own brightness after a blend, so tinting changes the
    /// hue of a trail without ever making it dimmer than the palette intended. The
    /// white core relies on this to stay the brightest thing on screen.
    /// </summary>
    private static Color NormalizeBrightness(Color blended, Color reference)
    {
        float blendedLuma = Luminance(blended);
        float referenceLuma = Luminance(reference);

        if (blendedLuma <= 0.001f)
        {
            return reference;
        }

        float scale = referenceLuma / blendedLuma;
        return new Color(
            Mathf.Clamp01(blended.r * scale),
            Mathf.Clamp01(blended.g * scale),
            Mathf.Clamp01(blended.b * scale),
            reference.a);
    }

    private static float Luminance(Color color)
    {
        return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
    }

    private static Color HexColor(byte r, byte g, byte b)
    {
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
