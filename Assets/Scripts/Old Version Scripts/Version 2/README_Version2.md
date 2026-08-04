# Coca Sorting — Version 2 setup

The Version 2 scripts are separate from the legacy implementation. No existing
script was modified.

## Scene setup

1. Work in a duplicate scene while migrating.
2. Disable/remove the old `Board` and `BoardControllerV2` components in that
   scene. Do not run an old board and `Board_version2` together.
3. Add `Board_version2` to the board GameObject.
   `SodaTransferResolver_version2` is added automatically.
4. Assign the existing node prefab and configure width, height, origin, and
   spacing. The defaults reproduce the current 4×5 board.
5. Duplicate the box prefab and add `Box_version2`.
6. Either fill `Soda Slots` explicitly or leave it empty. Empty means every
   child named `SodaPosition0`, `SodaPosition1`, ... is discovered
   automatically. The number of discovered positions is the box capacity.
7. The old `Box` component may remain on the duplicated prefab during
   migration. `Box_version2` disables it and mirrors `Sodas`, `IsDragged`,
   coordinates, and `IsOnBoard` for the existing rail list.
8. For capacities above four, use `RailSpawner_version2`, because the old
   `SpawnContoller` discovers only `SodaPosition0` through `SodaPosition3`.
   Never enable both spawners in the same scene.

The old tutorial, hammer, swap, and revive scripts call `Board.instance`
directly. They remain legacy-only until versioned replacements are connected.
The core placement, matching, packing, rewards, loss check, and rail spawning
are available in this Version 2 stack.

## Transfer rules

- Only orthogonally adjacent boxes are connected. Diagonals never transfer.
- A soda color can move only if that color already exists in both boxes.
- Candidate priority is lexicographic:
  1. complete a monochrome box;
  2. avoid creating a blocked full mixed box;
  3. empty a source box;
  4. remove one color from a source box;
  5. increase same-color concentration;
  6. prefer the newly placed source, then right, up, left, down.
- A full mixed box may move one soda as an unlock operation only when that
  vacancy immediately enables a real progress move.
- The resolver recalculates all direct edges after every move. It never uses a
  fixed scenario list or a fixed iteration count.
- Previously visited states are rejected, preventing ping-pong.

## Transfer safety

- Board interaction is locked for the whole resolution.
- Only one soda is animated at a time.
- A destination slot is reserved before the source inventory changes.
- Source removal and destination ownership are committed as one transaction.
- Soda colliders are disabled while in flight.
- Completed and empty boxes leave the logical grid before their visual removal,
  so they cannot be processed or rewarded twice.

## Verification

Use:

`Tools > Coca Sorting > Run Version 2 Algorithm Tests`

The diagnostics cover non-adjacent rejection, six-slot capacity, deterministic
direction priority, full mixed-box unlocking, chain resolution, repeated-state
protection, and termination.
