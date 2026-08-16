# `Box+Ice.fbx` — the tray and the ice

**Type:** Model (3D shape). Authored in **3ds Max**, exported as binary FBX.
**Path:** `Assets/GameAssets/NEW MODELS ASSETS -CONSISTING OF  ICE/Coke Pack/Box+Ice.fbx`

---

## What is inside

**42 separate objects, 38,276 triangles**, spread over 9.03 × 0.53 × 3.53 world
units. That width is misleading — the actual tray is small; the file also
contains leftovers scattered several metres away.

### The tray and its ice — clustered near the origin

| Objects | Count | Role |
|---|---|---|
| `Box002` | 1 | **the tray itself** — the only object with 4 material slots |
| `Gengon001`–`Gengon031` | 30 | ice chunks (Max "gengon" = a prism primitive) |
| `Ice_Cube_001`, `Ice_Cube_002` | 2 | ice chunks, named |
| `Box003`, `Box005`–`Box008` | 5 | cube-shaped ice chunks, resting at y ≈ 0.11–0.18 |

So the ice is **37 loose meshes**, which is where most of the triangle count goes.

### Leftovers from the artist's working scene

These sit far from the tray and are almost certainly not intended:

| Object | Position | What it appears to be |
|---|---|---|
| `Box009` | `(-3.79, 0, 0.38)` | a second, empty tray |
| `Cylinder004` | `(-2.70, 0, 0.02)` | a duplicate can body |
| `Line004` | `(-2.82, 0.47, 0.03)` | the duplicate can's pull-tab |
| `Line002` | `(-7.05, 0, -1.78)` | a large flat oval spline |

`Cylinder004` + `Line004` are the same pair that makes up `Coke.fbx` — a copy of
the can left in the scene. **Worth asking the artist to clean these out and
re-export**, since they inflate the file and make the model's bounds four times
wider than the tray.

---

## Materials

Four slots, all on `Box002`, all built on **URP/Lit**:

| Material | Texture | Size |
|---|---|---|
| `Box-Map_Body_Floor_Cyan.mat` | `Box-Map_Body_Floor_Cyan.jpg` | 8×8 |
| `Box-Map_Body_Inside-Walls_Blue.mat` | `Box-Map_Body_Inside-Walls_Blue.jpg` | 8×8 |
| `Box_Body_Light-Blue.mat` | `Box-Map_Body_Light-Blue.jpg` | 8×8 |
| `Box_Body_Off-Blue.mat` | `Box-Map_Top_Off-Blue.jpg` | 8×8 |

**Note the names do not line up.** `Box_Body_Light-Blue` takes
`Box-Map_Body_Light-Blue.jpg`, and `Box_Body_Off-Blue` takes
`Box-Map_**Top**_Off-Blue.jpg`. Matching material to texture by name would pair
them wrongly — they were mapped explicitly.

### What was wrong on arrival

The FBX arrived with **four dangling material references**: it named four
materials and pointed at asset IDs that **did not exist anywhere in the project**.
Every one of the 42 renderers had an empty material slot, so the whole model drew
untextured. The four materials have been created and the references now resolve.

**The ice has no material of its own.** It shares the tray's materials, so it
currently renders as flat blue — not the translucent, glassy ice in the reference
sheet. Giving ice its own translucent material is an open art decision, not
something to guess at.

---

## Animation

**There is none.** The file contains only 3ds Max's default empty `Take 001` — no
animation curves, no bones, no deformers.

This matters because the artist said the ice "must be animated" and that a lot of
work had gone into it. Whatever exists in his Max scene **did not survive the
export**. Nothing can be wired up until an FBX arrives with actual animation in
it, or the movement is built in Unity instead.

---

## Performance concern

38,276 triangles is heavy for a mobile puzzle game, and 42 objects means 42
draw calls before any batching. If a tray like this ends up on every occupied
board cell plus the rail, this becomes the most expensive thing on screen by a
wide margin. Worth resolving before the model is used in gameplay:

- strip the leftovers (above),
- reduce the ice from 37 separate meshes,
- or combine the ice into a single mesh per tray.

---

## In the scene

Staged as **`NEW_Box+Ice`** at the origin. Display only — nothing in the game
references it.
