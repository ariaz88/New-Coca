# New art delivery — "Coke Pack"

Documentation for the models, textures and materials the artist delivered.
Everything here describes what is **actually in the files**, verified in Unity,
not what was intended.

---

## First: the three things, and how they differ

This is the part that is easy to confuse. All three are needed to see a coloured
object on screen, and they are not interchangeable.

| | What it is | In this delivery | File type |
|---|---|---|---|
| **Model** | The *shape*. Points, faces and a UV layout — a UV layout is a flat "sewing pattern" saying which part of an image lands on which part of the surface. A model has **no colour of its own**. | `Coke.fbx`, `Box+Ice.fbx` | `.fbx` |
| **Texture** | A flat *image*. Just a picture — a `.jpg` you could open in any photo viewer. A texture on its own cannot be attached to anything. | the 14 `.jpg` files | `.jpg`, `.png` |
| **Material** | The *recipe* that joins the two: "use this shader, put this texture in the colour slot, make it this shiny." A model **wears** materials; a material **holds** textures. | the 6 `.mat` files in `Coke Pack/Materials/` | `.mat` |

The chain is always the same:

```
Model  ──wears──▶  Material  ──holds──▶  Texture
Coke.fbx           Can_Body.mat          Coke-Map_Red.jpg
```

**Why this matters here:** the artist sent models and textures but **no
materials**. That middle link was missing, which is why the box arrived with
broken references and showed no colour. FBX exports rarely carry usable
materials — building them in Unity is normal and expected work, not a mistake by
the artist.

### Material *slots*

One model can wear **several** materials at once. Each separately-paintable
region is a "slot". The can has two: one for the body, one for the pull-tab.
That is what lets the tab be a different colour from the can without being a
separate file.

---

## Index

| Document | Covers |
|---|---|
| [Coke.fbx.md](Coke.fbx.md) | The drink can model |
| [Box+Ice.fbx.md](Box+Ice.fbx.md) | The tray + ice model |
| [Textures.md](Textures.md) | All 14 texture images |
| [Materials.md](Materials.md) | All 6 materials |

---

## Current status

Done:

- All 14 textures imported, with correct settings.
- 6 materials built on **URP/Lit**, matching the shader the rest of the project
  uses.
- Both models fully textured — **every material slot resolves, none are empty**.
- Both staged in the open scene as `NEW_Coke` and `NEW_Box+Ice`.

Not done, and deliberately so:

- **No connection to gameplay.** These are display objects sitting in a scene.
  Nothing in the game uses them, and no existing prefab, scene or script was
  touched.
- **Red only.** The other colours exist as loose texture files with no materials.
- **No animation.** There is none in either file — see below.

---

## Open questions for the artist

1. **The ice has no animation.** Both FBX files contain only 3ds Max's default
   empty `Take 001` — no curves, no bones, no deformers. Whatever animation
   exists in Max did not survive the export.
2. **The pull-tab question is answered: nothing to change.** It already imports
   as its own object with its own material. He does not need to re-export.
3. **`Box+Ice.fbx` contains leftovers from the working scene** — a second empty
   tray, a duplicate can, and a large oval spline, all sitting several metres
   from the model. See [Box+Ice.fbx.md](Box+Ice.fbx.md).
4. **Colour coverage.** Blue, green, purple, red and yellow were delivered. The
   reference sheet shows orange and pink, which are missing here.
5. **Save as `.png` or `.jpg`, never `.jfif`.** Unity's importer ignores `.jfif`
   entirely — such a file is not a broken texture, it is an absent one. The
   reference image arrived in that format.

## Note on lighting

The reference sheet states its own pipeline: **3D model → offline render → 2D
overpaint in Photoshop**. That look is painted, not lit by an engine. So the
artist's plan to bake shading into the texture is the correct one — real-time
lighting cannot reach it.

The catch is specific to this game and not to the reference: **baked shading only
holds while an object is seen from roughly one angle**, because the highlight is
painted pointing one way permanently. The reference is a static 2D match-3 where
nothing rotates. Here, sodas move and are re-oriented on spawn. The fixed
portrait camera makes this workable, but any tumbling during an animation will
show the highlights as fake.
