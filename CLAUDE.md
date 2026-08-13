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

### Next, in order

1. **Hidden Bombs — core, grey-box.** User's own design; full spec is in the plan
   file. Build it playable with placeholder art *before* any VFX.
2. **Tune** with the user playing 10–25.
3. **Polish + all three tutorials together** (X-blocker L6, bombs L10,
   frozen L13) — they share `TutorialManager`, so batch them.

Plan file: `C:\Users\Arash\.claude\plans\unity-mobile-puzzle-breezy-wind.md`

---

## Architecture facts that matter

**Levels are scenes; `LevelDefinition` assets are baked into them.** `Board`
never reads a definition at runtime. Edit `CampaignAuthoring.cs`, then
re-author + re-bake. `Tools > Coca Sorting > Levels > Level Designer`.

**Board shapes are text pictures** in `CampaignAuthoring`, top row first:
`.` playable, `#` hole, `X` blocker, `F` frozen. Soda letters:
`R B O K G P` — **K is pink**, because P is purple.

**The transfer rule:** a soda may only move into a box that *already contains
that colour* and has a free slot; neither box may be packed. Boxes are colour
seeds. This is the game's core and is deeply underexploited.

**`Soda.SodaColor.Pink` is ordinal 3** (renamed from `Yellow`). The ordinal is
load-bearing — every scene and asset stores it as an int.

**Sodas need a corrective rotation.** `SpawnContoller.SpawnSodaAtSlot` uses
`Quaternion.Euler(-90,0,0)`; the model is authored lying down. Anything
rendering a soda outside the normal path must apply it (this is why order icons
were sideways).

**The game uses `Bottle.prefab`, not `Soda.prefab`.** `Soda.prefab` is stale and
its material list has drifted.

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
