# Materials

**Type:** the recipe joining a [model](README.md) to a [texture](Textures.md).
A material says *which shader* to use, *which texture* goes in the colour slot,
and how shiny/metallic the surface is.

**Location:** `Assets/GameAssets/NEW MODELS ASSETS -CONSISTING OF  ICE/Coke Pack/Materials/`

**None of these came from the artist.** FBX files rarely carry usable materials;
all six were built in Unity. This is normal, expected work.

---

## The six

All use shader **`Universal Render Pipeline/Lit`** — the same shader as the rest
of the project (`Assets/GameAssets/Materials/14025_Soda_Can_diff_*.mat`,
`Mat003.mat`). Settings are identical across all six: base colour white,
smoothness `0.35`, metallic `0`.

### Can — `Coke.fbx`

| Material | Texture | On |
|---|---|---|
| `Can_Body.mat` | `Coke-Map_Red.jpg` (1024×1024) | `Cylinder003`, the can body |
| `Material #45.mat` | `Coke-Map_Top_Red.jpg` (8×8) | `Line003`, the pull-tab |

### Tray — `Box+Ice.fbx`

| Material | Texture | On |
|---|---|---|
| `Box-Map_Body_Floor_Cyan.mat` | `Box-Map_Body_Floor_Cyan.jpg` | `Box002`, slot 1 |
| `Box-Map_Body_Inside-Walls_Blue.mat` | `Box-Map_Body_Inside-Walls_Blue.jpg` | `Box002`, slot 2 |
| `Box_Body_Light-Blue.mat` | `Box-Map_Body_Light-Blue.jpg` | `Box002`, slot 3 |
| `Box_Body_Off-Blue.mat` | `Box-Map_Top_Off-Blue.jpg` | `Box002`, slot 4 |

The 37 ice objects have no material of their own — they share the tray's.

---

## Why the names are odd

The names come from the artist's 3ds Max scene, and Unity uses them to bind each
material to the right slot on the model. **Renaming a material breaks that
binding** and the slot goes empty. So `Material #45` — Max's auto-generated name
for the pull-tab's material — has been left as it is, despite being meaningless.

The tray material names also do not match their texture names
(`Box_Body_Off-Blue` uses `Box-Map_**Top**_Off-Blue.jpg`). Pairing them by name
would map them wrongly.

---

## Shader choice, and what will probably change

`URP/Lit` reacts to scene lighting: it adds its own highlights and shading on top
of the texture.

That is right for the current **test** textures, which are flat colour. It will
be **wrong** once the artist delivers the final painted textures, because those
have shading and highlights *already baked into the image*. Lit would then apply
a second layer of lighting on top of painted lighting — highlights in two
directions at once, and a look that shifts with scene lighting the artist cannot
control.

**When the painted textures arrive, these should switch to
`Universal Render Pipeline/Unlit`,** which draws the texture exactly as painted.
That also matches the reference sheet's pipeline (3D → render → 2D overpaint) and
is cheaper on mobile.

Not done now, because the current textures are unshaded — Unlit would make them
look completely flat.

---

## Adding another colour

The model does not change. Duplicate the two can materials, point them at the
other colour's textures, and assign them to `Cylinder003` and `Line003`:

| New material | Texture |
|---|---|
| `Can_Body_Blue.mat` | `Coke-Map_Blue.jpg` |
| `Can_Tab_Blue.mat` | `Coke-Map_Top_Blue.jpg` |

Note this creates a **separate material per colour**, whereas the existing game
code in `Assets/Scripts/Main Scripts/Core Gameplay/Soda.cs` assigns a single
material per soda. That gap is a gameplay-integration problem and is deliberately
out of scope here — see the note at the end of [README.md](README.md).
