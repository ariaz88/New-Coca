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
  drink order, so every level from 14 up is a three-slot card. Counts stay at 3
  packed boxes per drink.

- **Hidden Bombs — built and playable (grey-box).** Bombs, level-start preview,
  Defuser, Scanner, both failure modes, per-level config, solver-verified layout
  pools. See the section below.

### Next, in order

1. **Tune** with the user playing 10–25. Bomb counts, defuser economy, preview
   length, and whether Immediate mode is fair at all.
2. **Polish + all three tutorials together** (X-blocker L6, bombs L10,
   frozen L13) — they share `TutorialManager`, so batch them. The bomb tutorial
   is the only part of the bomb brief not yet built.
3. **Bomb VFX**: wire-cut defuse, sparks, smoke, scanner beam art. The grey-box
   shapes are primitives.

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

**`HandleBombsAfterPlacement` runs before `ResolvePlacement`,** synchronously.
A blast firing mid-resolution would destroy boxes the transfer system is moving
sodas into. Already-burning fuses tick *before* the new bomb arms, so a bomb
does not lose a move to its own arrival.

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

**Defusers are not rail items.** They dock beside the board. `spawnedBoxes` must
be empty before the rail refills, so an unused Defuser in that list would stop
every further box arriving. `IRailItem` still exists and `RemoveSpawnerList`
prunes by `IsConsumed` (null-guarded), which is what removes the Box coupling.

**The bomb HUD and director are added at runtime** by `BombHud`, off
`sceneLoaded`, only when `Board.BombSettings.IsActive`. Nothing was added to the
25 baked scenes.

**Input freezes use the owner-scoped constraints** — `TrySetPlacementConstraint`
plus `TrySetDragConstraint` on every rail item. Not `IsResolving` (also gates
win/lose), not `gameEnded` (kills them permanently), not `timeScale = 0` (stops
our own coroutines and not `OnMouseDown`).

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
- Console clean after every change.

`LevelSimulator` mirrors `UniversalSodaTransferSystem.Resolve` statement for
statement and calls the **real** `TransferAlgorithm`. If resolution behaviour
changes, the simulator must change with it or every solvability claim goes stale.

---

## Open / unverified

- Orders card **in motion** (impact punch, glow, tick pop) not yet seen in play.
- The frozen **cracked** state has not been seen in play — only the intact state.
- Level *difficulty* is unvalidated. Solvable ≠ fun; needs the user to play.
- `.cursor/mcp.json` and `ProjectSettings/EditorSettings.asset` show as dirty —
  Unity/tool churn, deliberately not committed.

---

## Working agreement

- Phased, with a checkpoint after each phase.
- Grey-box first on big features, polish after the user has played it.
- Commit at every checkpoint; never push.
- The user is fine with direct disagreement — say when a request has a problem,
  then do the work.
