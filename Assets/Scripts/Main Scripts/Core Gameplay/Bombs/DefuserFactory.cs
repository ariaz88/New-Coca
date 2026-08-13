using UnityEngine;

/// <summary>
/// Builds the Defuser item.
///
/// Generated rather than authored as a prefab for the same reason the blocker
/// and bomb art is: adding a prefab reference would mean a new serialized field
/// on something in all 25 already-baked scenes, and a serialized field added
/// after a bake keeps its authored-day default in every one of them. Generating
/// it means the mechanic works in every level the moment the code exists.
///
/// Grey-box: a dark grip with two bright jaws, readable as pliers at cell size.
/// </summary>
public static class DefuserFactory
{
    private static readonly Color GripColor = new Color(0.16f, 0.17f, 0.22f, 1f);
    private static readonly Color JawColor = new Color(0.20f, 0.82f, 0.45f, 1f);

    public static Defuser Create(Vector3 worldPosition, float scale = 0.20f)
    {
        GameObject root = new GameObject("Defuser");
        root.transform.position = worldPosition;

        // One collider on the root, sized to the whole tool. OnMouseDown needs a
        // collider, and putting it here rather than on the parts means the drag
        // reference stays the root transform - which is what placement uses.
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(scale * 1.5f, scale * 1.2f, scale * 1.5f);
        collider.center = new Vector3(0f, scale * 0.25f, 0f);

        CreatePart(root.transform, PrimitiveType.Cube, GripColor,
            new Vector3(0f, 0f, 0f),
            new Vector3(scale * 0.34f, scale * 0.22f, scale * 0.9f));

        // The two jaws splay forward so the silhouette is not just a bar.
        CreateJaw(root.transform, scale, -1f);
        CreateJaw(root.transform, scale, 1f);

        return root.AddComponent<Defuser>();
    }

    private static void CreateJaw(Transform parent, float scale, float side)
    {
        GameObject jaw = CreatePart(parent, PrimitiveType.Cube, JawColor,
            new Vector3(side * scale * 0.16f, 0f, scale * 0.62f),
            new Vector3(scale * 0.16f, scale * 0.18f, scale * 0.55f));
        jaw.transform.localRotation = Quaternion.Euler(0f, side * 14f, 0f);
    }

    private static GameObject CreatePart(
        Transform parent, PrimitiveType type, Color color, Vector3 localPosition, Vector3 localScale)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = type.ToString();

        // Generated art must never intercept a drop raycast, and the root's own
        // collider is the only one this item wants.
        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null)
        {
            Object.Destroy(partCollider);
        }

        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = "DefuserPart" };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return part;
    }
}
