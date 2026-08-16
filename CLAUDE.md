# Coca Sorting — project log

Working notes for Claude across sessions. Read this first.

Unity **6000.3.21f1**, URP, portrait mobile. Unity Editor MCP is connected —
verify in-editor (`Unity_RunCommand`, `Unity_GetConsoleLogs`, Play Mode captures),
not by inspection.

---

## Branches

| Ref | What |
|---|---|
| `main` | untouched original |
| `archive/pre-level-redesign` | archive branch |
| `pre-level-redesign-v1` | baseline tag |
| `feature/level-progression-v2` | **all work happens here** |

`origin` exists. **Nothing has been pushed.** Do not push without asking.

---

## Status

### Done

- **Phase 0** — `LevelNaming` resolver; six files had each re-implemented the same
  `StartsWith("Level") + int.TryParse`. `TransferAlgorithm.LastSelectionSource`
  made `[ThreadStatic]` for the parallel solver.
- **Phase 1** — `removedCells` split into `Playable / Removed / Blocker / Frozen`
  (`BoardCellKind`). Frozen blockers = crack on hit 1, open on hit 2.
  `Tools > Coca Sorting > Tests > Run Blocker Rules Tests` — 24 checks.
- **Phase 2** — `LevelDefinition` assets + `LevelSceneGenerator`, deterministic
  authored rail queues, 25 levels, Build Settings synced.
- **Feedback pass** — Yellow→Pink, drag cell detection, rail colour ramp,
  weighted spawning.
- **Solver** — `Levels/Simulation/`. **25/25 levels solvable**, replay-verified.
- **Orders card redesign** — procedural sprites + runtime skin.
- **HUD rework** — progress bar hidden, card + level label repositioned.
- **Block orders** — `OrderKind.Blocks`: "open N locked blocks" as a goal
  alongside drink orders, on levels 11+ (not 13, which is the frozen tutorial).
- **All boards are 4x5.** Anything wider fell outside the camera. Do not widen
  a board without checking the camera framing first.
- **Holes are drawn.** `#` cells now get a `Hole Cover` — see below. Before this
  they were invisible and read as free cells.
- **Difficulty ramp pass** — all six drinks on the rail from L11; ordered-colour
  arrival bias fell from a flat 2.0 to 1.30→1.05 across L10–25; mixed rail boxes
  55%→90% and three-colour boxes 40%→75% from L18/L20; L14–16 gained a second
  drink order, so every level from 14 up is a three-slot card.

- **Second difficulty pass** — the campaign was still finishing too fast.
  Every drink order from **L4 up asks for 4 packed boxes, not 3** (≈+33%), via
  `GetOrderCountBonus`. The rail widens far earlier: only L1 is pure, L2–3 carry
  3 colours, L4–5 four, L6–9 five, L10+ all six. Rail queues grew with it —
  L5 19→25 boxes, L20 26→30.

- **Hidden Bombs — built, played and tuned.** Bombs, level-start preview,
  rail-borne Defusers, Scanner, both failure modes, per-level config,
  solver-verified layout pools, layered explosion VFX. See the section below.

- **Android build works.** `Builds/Android/CocaSorting.apk`, ~57 MB. Recipe and
  its two mandatory workarounds are at the bottom of this file.

### Next, in order

1. **Play 10–25 again** now that orders are a third longer. Bomb counts, defuser
   economy, preview length, and whether Immediate mode is fair at all.
2. **All three tutorials together** (X-blocker L6, bombs L10, frozen L13) — they
   share `TutorialManager`, so batch them. The bomb tutorial is the only part of
   the bomb brief still unbuilt.
3. **Remaining bomb polish**: wire-cut defuse animation, scanner beam art, and a
   real Defuser model — it is still primitives.

Plan file: `C:\Users\Arash\.claude\plans\unity-mobile-puzzle-breezy-wind.md`

---

## Architecture facts that matter

**Levels are scenes; `LevelDefinition` assets are baked into them.** `Board`
never reads a definition at runtime. Edit `CampaignAuthoring.cs`, then
re-author + re-bake. `Tools > Coca Sorting > Levels > Level Designer`.

**Board shapes are text pictures** in `CampaignAuthoring`, top row first:
`.` playable, `#` hole, `X` blocker, `F` frozen. Soda letters:
`R B O K G P` — **K is pink**, because P is purple.

**The blue cell tiles are painted into the `Platform` mesh**, not spawned per
cell — `Node.prefab`'s own MeshRenderer is *disabled*, and in edit mode the whole
scene holds five renderers. So the platform always draws a full 4x5 grid whatever
shape the level has, and a `#` hole used to show a tile identical to a free cell
while silently refusing every drop. `Board.GenerateHoleVisual` +
`BlockedCellVisuals.CreateHoleCover` now cover them. Its colours are **constants,
not serialized fields**, so it reached all 25 baked scenes without a re-bake.

**A palette colour with no order gets no rail supply** unless it also starts on
the board or is listed in `Distractors`. `ResolveDistractors` now promotes such
colours automatically; before that, level 25 named green in its palette and
shipped a five-colour rail. Adding green back made the finale unsolvable at four
blocks, so **L25 is three blocks now** — twelve playable cells will not carry
eight packs.

**The transfer rule:** a soda may only move into a box that *already contains
that colour* and has a free slot; neither box may be packed. Boxes are colour
seeds. This is the game's core and is deeply underexploited.

**A Blocks order still costs sodas.** Blockers are only opened by packing a box
in an adjacent cell, and a frozen blocker needs two. `ComputeRailNeeds` adds two
extra boxes of supply per requested block — without that, trading a drink order
for a block order silently starved the level and the solver could finish every
drink with nothing left to break anything.

**Anything that changes an order's size must change `ComputeRailNeeds` too.**
`GetOrderCountBonus` is applied in *both* `ApplySpec` (what the order asks for)
and `ComputeRailNeeds` (what the rail delivers). Applying it in only one place
leaves every affected level exactly one box short of each ordered colour, and the
solver reports that as **unsolvable** — which reads as a broken level design
rather than as a supply bug.

**A cracked frozen blocker has not opened.** Only `Board.UnlockBlockedCell`
reports to a Blocks order, so half-breaking one cannot advance it. The simulator
mirrors this.

**Blocks orders raise no flying VFX.** There is no packed box to deliver — the
block is destroyed in place — so `OrderPanelUI` bypasses the impact presenter for
those slots and the slot just hops. Its icon is drawn by `OrderPanelTextures`
because the board's blockers are plain cubes with tape generated at runtime.

**`Soda.SodaColor.Pink` is ordinal 3** (renamed from `Yellow`). The ordinal is
load-bearing — every scene and asset stores it as an int.

**Sodas need a corrective rotation.** `SpawnContoller.SpawnSodaAtSlot` uses
`Quaternion.Euler(-90,0,0)`; the model is authored lying down. Anything
rendering a soda outside the normal path must apply it (this is why order icons
were sideways).

**The game uses `Bottle.prefab`, not `Soda.prefab`.** `Soda.prefab` is stale and
its material list has drifted.

---

## Hidden Bombs

**The trigger is dropping a Box on the bomb's cell.** There is no timer that
runs on its own. `Countdown` mode arms a fuse measured in placements and a blast
clears the bomb's cell plus its four neighbours; `Immediate` mode ends the level.
Levels 22–25 are Immediate.

**Bombs go only on plain empty playable cells** — `Board.CanHostBomb`. Blocker,
frozen and hole cells have no `Node`, so a bomb there could never be dropped on
or defused, and a starting-box cell would hide it under something immovable.

**Bombs never touch `breakingBlockedCells`.** That set gates `ResolvePlacement`'s
`while (count > 0)` spin and a bomb has no guaranteed `UnlockBlockedCell`. Bombs
use `bombs` + `liveBombLookup`.

**A detonation is queued during placement and fired AFTER resolution.**
`HandleBombsAfterPlacement` runs synchronously from `UpdateBoxPosition` and only
arms bombs / adds them to `pendingDetonations`; `ResolvePlacement` drains
`breakingBlockedCells` and *then* runs `ProcessPendingDetonations`.

This ordering is not cosmetic. The first version blew the boxes up during
placement, and `Box.AnimateSodaToSlot` ends with
`sodaTransform.SetParent(transform)` — so destroying a destination box while a
soda was flying into it threw `MissingReferenceException`, killed the resolve
coroutine, and **the board could never match anything again**. That bug looked
like "matching is broken", not like a bomb bug. `AnimateSodaToSlot` now also
checks `this != null` before the re-parent, and destroys the orphaned soda.

Already-burning fuses tick *before* the new bomb arms, so a bomb does not lose a
move to its own arrival.

**The explosion plays, finishes, and only then is the level lost.**
`ProcessPendingDetonations` waits out the blast before calling
`GameManager.LoseLevel()`. Calling it first put the revive panel on screen while
the explosion played behind it, which read as the panel causing the loss.
`LoseLevel()` was added as the single entry point into the lose flow, mirroring
`BeginWinSequence` — a bomb ends the level with the board half empty, so
`CheckLoseCondition(true)` would have been asserting something untrue about the
board.

**Layouts are picked from a solver-verified pool,** `pool[attempt % count]`.
`Tools > Coca Sorting > Levels > Generate Bomb Layouts` draws candidates and
solves each with the bomb cells modelled as holes — conservative, because it
assumes the player never defuses. `GameDataManager.LevelAttempt` increments in
`BeginLevel`, so Play Again re-rolls; a revive does not reload the scene, so it
keeps its layout.

**Bomb count is capped at 45% of a level's legal cells.** The late boards are
the most blocked, so the highest number on the curve lands on the level with the
least room — level 24 has 8 legal cells and the curve wanted 6 bombs, and every
layout was unwinnable.

**Defusers ride the rail, in place of a box.** `BombDirector` sets
`SpawnContoller.RailSlotClaim` / `RailSlotFactory`; the claim runs once per batch
and returns which first-row slots become Defusers — weighted roughly 40% none,
40% one, 19% two, and **never the whole batch** (a batch with no boxes cannot
advance the board). A claimed slot does **not** draw from the authored queue, so
the box that would have filled it is *delayed, not lost*, and the queue is still
consumed in authored order — which is what lets the solver model the rail as a
flat index.

This is only safe because `RemoveSpawnerList` prunes by `IRailItem.IsConsumed`
(null-guarded) instead of `GetComponent<Box>() != null && box.IsOnBoard`. With
the old test a Defuser could never be pruned, and since the rail only refills
when `spawnedBoxes` is empty, one unused Defuser pinned the rail forever — and
masked `CheckRailExhausted`, which needs the same list empty.

An earlier version docked them beside the board at a hand-written offset from the
board origin. It was off-screen on the portrait camera: the item existed and was
draggable and nobody could see it.

**Dragging a Defuser highlights nothing, ever.** Highlighting the valid target
lit up every bomb the moment the player picked one up, so dragging it slowly
across the board was a free unlimited scan and the scanner charges were
worthless.

**A Defuser dropped on the board never returns to the rail.** It defuses, or it
is spent where it landed. Flying home on a miss let one charge sweep the whole
board a cell at a time. Released *off* the board it does return — that is not a
decision. `wrongDefuserIsConsumed` now controls whether the *charge* is refunded
(a fresh one rides down later), not whether the object comes back.

**Nothing but the bomb involved may ever become visible.** No reveal of
neighbours on defuse, none on detonation. `Board.HideConcealableBombs` is called
at the end of every blast as a hard reset. An early version revealed adjacent
bombs after a defuse as a reward — on a 4x5 board nearly every cell touches
another, so two defuses mapped the level. **Armed** bombs are excluded from
hiding: their fuse is running and concealing a timer the player is judged on is
not fair.

**The bomb HUD and director are added at runtime** by `BombHud`, off
`sceneLoaded`, only when `Board.BombSettings.IsActive`. Nothing was added to the
25 baked scenes.

**Input freezes use the owner-scoped constraints** — `TrySetPlacementConstraint`
plus `TrySetDragConstraint` on every rail item. Not `IsResolving` (also gates
win/lose), not `gameEnded` (kills them permanently), not `timeScale = 0` (stops
our own coroutines and not `OnMouseDown`).

**The bomb body is black in every state.** State is carried by the *fuse*
colour — red revealed, orange armed, green defused — so the silhouette never
changes. A bomb that changed colour stopped reading as a bomb, and black is the
highest-contrast thing available against the pale blue tiles.

**The explosion is six layers with staggered timing** (`BombExplosionVfx`):
flash, two-part fireball, a flat shockwave ring on the board plane, stretched
sparks, tumbling debris, drifting smoke. Two things bit while building it, both
invisible in code review and obvious the moment it was rendered:

- **Transparency must be configured for the alpha-blended path too.** URP's
  particle shaders default to opaque, and setting blend factors alone is not
  enough — `_Surface`, `_SrcBlend`, `_DstBlend`, `_ZWrite`, the
  `_SURFACE_TYPE_TRANSPARENT` keyword and the render queue all move together.
  Configuring only the additive layers rendered the smoke as **solid grey slabs
  covering the whole board**.
- **Size every layer in cells, not in arbitrary multiples.** The flash was
  originally 3.6× the cell size, which is wider than the entire 4x5 board, so it
  whited out the screen.

**Camera shake goes through `CameraShaker`**, which creates a
`CinemachineImpulseSource` and adds a `CinemachineImpulseListener` to the live
vcam at runtime. None of the 25 baked scenes has a listener, and without one an
impulse moves nothing.

---

## Traps that have already bitten

**Serialized fields do not update in already-generated scenes.** Changing a C#
default changes nothing in the 25 baked scenes. This caused the frozen frost bug.
`LevelSceneGenerator.ApplyBlockerStyle` now propagates blocker style; anything
similar needs the same treatment, then a re-bake.

**`Destroy()` is deferred to end of frame.** `OrderPanelUI` rebuilds during
start-up, so walking its children counts outgoing slots too — that produced a
card exactly twice as wide as it should be. Count from `OrderPanelUI.Slots`.

**`breakingBlockedCells` gates `ResolvePlacement`**, which spins on
`while (count > 0)`. Anything added there without a guaranteed `UnlockBlockedCell`
hangs resolution forever. Bombs must use their own set.

**Blocker/hole cells have no `Node`,** so `GetDropTargetNode` returns null there.
Bombs must sit on real playable cells.

**Never set `node.isOccupied = true`** for a non-box occupant — it feeds
`CheckBoardFill` and fires a false board-full loss.

**Drag reference position must be `transform.position`.** The collider centre is
offset +0.32 local Y which, under the box's −90° rotation, is a **1.15 cell**
error. See `Box.GetPlacementReferencePosition`.

**Post-processing is force-disabled** every scene load by
`VisualStyleRuntime.ConfigureSceneRendering()`. Nothing may rely on bloom;
glow comes from blend mode (`UIGlow.shader`).

**The Main Camera is driven by a `CinemachineBrain`** — `DOShakePosition` on it
does nothing. Use `CinemachineImpulseSource` (Cinemachine 2.10.7 installed).

**Haptics are hard-disabled.** `VibrationSettings.Awake` forces
`IsVibrationEnabled = false` and never persists; the CandyCoded calls in
`ButtonVibrationHandler` are commented out though the package is installed.

**A blanket `AssetDatabase.SaveAssets()` fails** on read-only AdMob package
assets and buries real output. Use `SaveAssetIfDirty` per asset. The
`Saving Prefab to immutable folder` console error on scene save is pre-existing
and harmless.

**MCP `Unity_RunCommand` sandbox:** no `System.Reflection`; its wrapper namespace
collides with `Image`, so fully-qualify `UnityEngine.UI.Image`. Its logger does
not honour format specifiers — concatenate. Reflection-based tests must live in
an in-project editor script instead.

**A manual `Camera.Render()` outside Play Mode does not draw screen-space
canvases.** `OrderPanelPreview` uses a world-space canvas for this reason.

---

## UI is skinned at runtime, not per scene

The HUD is identical in all 25 scenes, so appearance is applied in code and
reaches every scene at once:

- `OrderPanelSkin` — added by `OrderPanelUI.Awake`. Card look and layout.
  All colours/sizes are inspector fields.
- `LevelHudLayout` — added by `UIManager.Awake`. Hides the progress bar
  (deactivated, never destroyed) and moves the level label.
- `OrderPanelTextures` — procedural rounded rects, pills, shadow.
- `BombHud` — installs itself off `sceneLoaded` when `Board.BombSettings.IsActive`,
  and adds `BombDirector` with it. Preview banner, dim, scanner beam, scanner
  button, defuser counter. Sprites from `BombHudTextures` (rounded plate, pill,
  radar glyph).

**The scanner button lives on the RIGHT edge.** The left column already carries
settings, no-ads and two SWAP powerups; anchoring there put it on top of them.
It greys out and stops responding when `Board.instance.LiveBombCount == 0` —
repainted off `Board.BombStateChanged`, so it dims the instant the last bomb is
defused rather than on the next director event.

**Large translucent gradient overlays do not work** on a rounded card — the
sprite has square corners and paints over them. Use thin rounded highlight
lines. A soft shadow's shape must be inset by its own softness, or 9-slicing
stretches an opaque edge into a hard rectangle.

`Tools > Coca Sorting > Preview Orders Panel` renders the card to
`Builds/Previews/` (gitignored) without Play Mode. **Use it — do not tune UI blind.**

---

## Verification

- `Tools > Coca Sorting > Levels > Verify All Levels Are Solvable` — must stay 25/25.
- `Tools > Coca Sorting > Levels > Level Designer` → Validate All — 0 errors.
- `Tools > Coca Sorting > Tests > Run Blocker Rules Tests` — 24 checks.
- Bomb pools — **120 layouts across 15 levels, 0 unsolvable**
  (`LevelVerification.VerifyWithBombs` per stored layout).
- Build Settings — **28 scenes** in campaign order.
- Console clean after every change.

`LevelSimulator` mirrors `UniversalSodaTransferSystem.Resolve` statement for
statement and calls the **real** `TransferAlgorithm`. If resolution behaviour
changes, the simulator must change with it or every solvability claim goes stale.

**Changing orders or board shape invalidates the bomb pools.** They were solved
against the *old* orders. Re-run `Tools > Coca Sorting > Levels > Generate Bomb
Layouts`, re-bake, and re-verify — otherwise the pools are a stale promise that
the level is still winnable.

---

## Android build

Works today; ~57 MB APK, ~13 min. Settings already correct in the project:
IL2CPP + ARM64, Portrait, `optimizedFramePacing` off, APK not AAB, debug signing.

Two workarounds are **load-bearing** and must not be "cleaned up":

**1. `Assets/Plugins/Android/mainTemplate.gradle` carries three hand-added
`implementation` lines** — `play-services-ads`, `constraintlayout`,
`lifecycle-process`. They are declared in the plugin's own
`GoogleMobileAdsDependencies.xml` and *should* be resolved automatically, but
EDM4U cannot run: the machine's system `JAVA_HOME` points at a stale Unity
**2021.3.42f1** JDK. Every build prints `JAVA_HOME is set to an invalid
directory` and `Resolution Failed` — **and then succeeds**, because Gradle uses
Unity's own bundled JDK and those manual lines supply what EDM4U didn't. Without
them the build dies at AAPT resource linking.

**2. `PlayerSettings.Android.minifyRelease` is off.** The same AAR bundles
"nextgen" wrapper classes referencing an ads namespace declared nowhere, and R8
treats them as fatal. Before a Play release, re-enable it *and* add
`-dontwarn com.google.android.libraries.ads.mobile.sdk.**`.

There is also a `mainTemplate.gradle.backup` in that folder. It is not used;
do not restore it over the live file.

---

## Open / unverified

- Orders card **in motion** (impact punch, glow, tick pop) not yet seen in play.
- The frozen **cracked** state has not been seen in play — only the intact state.
- Level *difficulty* after the second pass (4-box orders) is unvalidated by a
  human. Solvable ≠ fun.
- The explosion has only been seen frame-by-frame in slow motion, never at speed.
- **Countdown mode has never been played.** Only Immediate (22–25) has been
  exercised; the fuse, the armed pulse and the neighbour-clearing blast are
  verified by code path and unit-style checks, not by playing them.
- The APK has **not been installed on a device** this session — it built, and
  its contents were checked, but nobody has run it.
- `.cursor/mcp.json`, `ProjectSettings/EditorSettings.asset` and
  `ProjectSettings/TimeManager.asset` show as dirty — Unity/tool churn,
  deliberately not committed.

---

## Working agreement

- Phased, with a checkpoint after each phase.
- Grey-box first on big features, polish after the user has played it.
- Commit at every checkpoint; never push.
- The user is fine with direct disagreement — say when a request has a problem,
  then do the work.
