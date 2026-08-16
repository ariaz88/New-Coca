# Textures

**Type:** flat image files. A texture is *only a picture* — it does nothing until
a [material](Materials.md) puts it in a slot. All 14 live in
`Assets/GameAssets/NEW MODELS ASSETS -CONSISTING OF  ICE/Coke Pack/`.

All of these are **test/placeholder art.** The artist has said the final version
will be the whole object hand-painted with shading and highlights baked in.

---

## The can — body maps (1024×1024)

The large maps. These carry the real artwork: the swirl, the logo shapes, the
light bands down the side.

| File | Colour | Used by |
|---|---|---|
| `Coke-Map_Red.jpg` | red | **`Can_Body.mat`** |
| `Coke-Map_Blue.jpg` | blue | *unused* |
| `Coke-Map_Green.jpg` | green | *unused* |
| `Coke-Map_Purple.jpg` | purple | *unused* |
| `Coke-Map_Yellow.jpg` | yellow | *unused* |

## The can — tab maps (8×8)

| File | Colour | Used by |
|---|---|---|
| `Coke-Map_Top_Red.jpg` | red | **`Material #45.mat`** (the pull-tab) |
| `Coke-Map_Top_Blue.jpg` | blue | *unused* |
| `Coke-Map_Top_Green.jpg` | green | *unused* |
| `Coke-Map_Top_Purple.jpg` | purple | *unused* |
| `Coke-Map_Top_Yellow.jpg` | yellow | *unused* |

## The tray (8×8)

| File | Used by |
|---|---|
| `Box-Map_Body_Floor_Cyan.jpg` | `Box-Map_Body_Floor_Cyan.mat` |
| `Box-Map_Body_Inside-Walls_Blue.jpg` | `Box-Map_Body_Inside-Walls_Blue.mat` |
| `Box-Map_Body_Light-Blue.jpg` | `Box_Body_Light-Blue.mat` |
| `Box-Map_Top_Off-Blue.jpg` | `Box_Body_Off-Blue.mat` |

---

## Why nine of these are 8×8, and what had to change

The artist described giving the pull-tab "an 8-pixel texture, coloured by the
texture map rather than by colour parameters inside Unity." That approach is used
for **every tray map too** — nine of the fourteen files are 8×8 images of flat
colour blocks. The model's UV layout points each region at one block.

It is a sound technique: it keeps colour under the artist's control instead of
scattered across Unity settings, and the files are almost free.

**But Unity's default import settings destroy it.** All nine arrived set to:

- `Bilinear` filtering — blends neighbouring pixels. On a 1024px map that is
  invisible smoothing; on an 8px map, **every colour block bleeds into its
  neighbours**, because a single pixel is an eighth of the whole image.
- `Compressed` (DXT) — block compression works on 4×4 pixel tiles. On an 8×8
  image that is four tiles for the entire texture, and the colours shift visibly.
- `Mipmaps` on — pointless at this size, and generates a 1×1 average that can be
  sampled at distance, turning the whole texture into one muddy colour.

All nine have been changed to **Point filtering, Uncompressed, no mipmaps,
Clamp wrapping**. The colours are now exactly what the artist painted.

**This must be repeated for any future 8-pixel texture he sends.** It is not
automatic — Unity will apply the same damaging defaults every time.

---

## Colour coverage

Delivered: blue, green, purple, red, yellow.

The reference sheet shows **orange** and **pink** cans, neither of which is in
this set — and yellow, which is in the set, does not appear on the reference's
can row in the same way. Reconciling the palette is an open question for the
artist.

---

## File format warning

Save textures as **`.png` or `.jpg`**. Unity's importer does not recognise
**`.jfif`** — a `.jfif` file dropped into the project is not a broken texture, it
is an *absent* one, invisible to Unity entirely. `.jfif` is the same data as
`.jpg`, so renaming the extension is enough to fix one; no re-export is needed.
