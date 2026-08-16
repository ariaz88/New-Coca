# `Coke.fbx` — the drink can

**Type:** Model (3D shape). Authored in **3ds Max**, exported as binary FBX.
**Path:** `Assets/GameAssets/NEW MODELS ASSETS -CONSISTING OF  ICE/Coke Pack/Coke.fbx`

---

## What is inside

| Object | Role | Verts | Tris | Material slot |
|---|---|---|---|---|
| `Cylinder003` | the can body | 1,996 | 3,816 | `Can_Body` |
| `Line003` | **the pull-tab** | 2,600 | 5,200 | `Material #45` |

Plus one empty root transform. Total **9,016 triangles**.

**Size:** 0.52 × 0.52 × 0.52 world units.

---

## The pull-tab — the artist's question, answered

He asked whether Unity could select the tab separately and give it its own
material, because he exported everything as "one object". **Yes, and it already
works. He does not need to change or re-export anything.**

The mechanism is not quite what either of us assumed. The tab is not a second
submesh on the can — it is **its own GameObject**, `Line003`, with its own
renderer and its own material. In 3ds Max it is a *renderable spline*: a curve
told to generate geometry. Max exports that as a normal mesh, so Unity treats it
like any other object.

Practical consequence: the tab can be recoloured, hidden, or animated completely
independently of the can body.

**One thing worth raising with him:** the tab is **5,200 triangles — more than
the entire can body at 3,816.** A pull-tab is a detail a few pixels across on a
phone. Renderable splines are expensive because Max sweeps a full tube along the
curve. Asking him to lower the spline's interpolation steps would likely cut this
by 80% with no visible difference.

---

## Materials

Two slots, both built on **URP/Lit**:

| Slot | Material | Texture | Size |
|---|---|---|---|
| body | `Can_Body.mat` | `Coke-Map_Red.jpg` | 1024×1024 |
| tab | `Material #45.mat` | `Coke-Map_Top_Red.jpg` | 8×8 |

`Material #45` is the auto-generated name 3ds Max gave the spline's material. It
is ugly but harmless — renaming it would break the link Unity uses to bind the
material to the slot, so it has been left alone.

The 8×8 texture is the one the artist described: a tiny image of flat colour
blocks, where colour comes from the picture rather than from a Unity setting.
See [Textures.md](Textures.md) for why its import settings had to be changed.

---

## Colours

Only **red** is set up. The FBX itself references `Coke-Map_Red.jpg` and
`Coke-Map_Top_Red.jpg`, and materials were built for those.

Blue, green, purple and yellow exist as loose texture files with no materials.
Adding a colour means duplicating the two materials and swapping their textures —
the model does not change.

---

## Import notes

- **Animation: none.** The file contains only 3ds Max's default empty `Take 001` —
  no animation curves, no bones, no deformers.
- Materials are stored **externally**, in `Coke Pack/Materials/`, so they can be
  edited without touching the FBX.
- Original materials used a `Shader Graphs/PhysicalMaterial3DsMax` shader that
  Unity auto-generates from Max's Physical Material. They were switched to
  **URP/Lit** to match the rest of the project.

---

## In the scene

Staged as **`NEW_Coke`** at position `(2, 0, 0)`. It is a display object only —
nothing in the game references it.
