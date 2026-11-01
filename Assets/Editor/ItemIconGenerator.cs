#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders a 2D icon (transparent PNG) from the 3D model of each pickup item and assigns it to
/// ItemDefinition.icon, so the hotbar shows the real object instead of a coloured square.
///
/// Run: menu  Chicken Training > Items > Generate Hotbar Icons From Models
///
/// To add another item: add one line to Entries (item asset + the model/prefab to photograph).
/// If 'childName' is set, only that child of the prefab is rendered (used for ItemPickups.prefab, which
/// holds several pickups). Icons already set by hand (outside OutputFolder) are never overwritten.
/// </summary>
public static class ItemIconGenerator
{
    const string OutputFolder = "Assets/Art/Items/ItemIcons";
    const int IconSize = 256;
    const string PickupsPrefab = "Assets/Prefabs/feature-Character/ItemPickups.prefab";

    sealed class Entry
    {
        public readonly string itemAsset;
        public readonly string modelAsset;
        public readonly string childName;

        public Entry(string itemAsset, string modelAsset, string childName = null)
        {
            this.itemAsset = itemAsset;
            this.modelAsset = modelAsset;
            this.childName = childName;
        }
    }

    static readonly Entry[] Entries =
    {
        // Items that exist as pickups in the world: photograph the pickup itself.
        new Entry("Assets/Data/Items/Item_VeChai.asset",      PickupsPrefab, "Pickup_VeChai"),
        new Entry("Assets/Data/Items/Item_BinhTuoiCay.asset", PickupsPrefab, "Pickup_BinhTuoiCay"),
        new Entry("Assets/Data/Items/Item_ThungHang.asset",   PickupsPrefab, "Pickup_ThungHang"),

        // Items that have a model in the project but no pickup in the scene.
        new Entry("Assets/Data/Items/Item_ThamTap.asset",        "Assets/Art/Items/mat.fbx"),
        new Entry("Assets/Data/Items/Item_PhanBonProtein.asset", "Assets/Art/Items/Monster Protein Powder.fbx"),
    };

    [MenuItem("Chicken Training/Items/Generate Hotbar Icons From Models")]
    public static void GenerateAllMenu()
    {
        Debug.Log(GenerateAll());
    }

    public static string GenerateAll()
    {
        var log = new StringBuilder();
        EnsureFolder(OutputFolder);

        foreach (Entry e in Entries)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(e.itemAsset);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(e.modelAsset);
            if (item == null) { log.AppendLine("SKIP  " + e.itemAsset + " : item not found"); continue; }
            if (model == null) { log.AppendLine("SKIP  " + item.name + " : model not found " + e.modelAsset); continue; }

            if (item.icon != null && !AssetDatabase.GetAssetPath(item.icon).StartsWith(OutputFolder))
            {
                log.AppendLine("SKIP  " + item.name + " : already has a hand-made icon");
                continue;
            }

            Texture2D tex = Render(model, e.childName);
            if (tex == null) { log.AppendLine("FAIL  " + item.name + " : nothing to render"); continue; }

            string pngPath = OutputFolder + "/Icon_" + item.name.Replace("Item_", "") + ".png";
            File.WriteAllBytes(ToDiskPath(pngPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = IconSize;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            EditorUtility.SetDirty(item);
            log.AppendLine("OK    " + item.name + " <- " + pngPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return log.ToString();
    }

    // ------------------------------------------------------------------------------------------

    static Texture2D Render(GameObject source, string childName)
    {
        GameObject root = null, camGo = null, lightGo = null;
        RenderTexture rt = null;
        RenderTexture previous = RenderTexture.active;

        try
        {
            int layer = FindFreeLayer();

            root = Object.Instantiate(source);
            root.hideFlags = HideFlags.HideAndDontSave; // never saved, never dirties the open scene
            root.transform.position = new Vector3(0f, -300f, 0f);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            Transform target = root.transform;
            if (!string.IsNullOrEmpty(childName))
            {
                target = FindDeep(root.transform, childName);
                if (target == null) return null;
            }

            // Hide every renderer that is not part of the thing we photograph.
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (!r.transform.IsChildOf(target)) r.enabled = false;

            var renderers = new List<Renderer>();
            foreach (Renderer r in target.GetComponentsInChildren<Renderer>(false))
                if (r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)) renderers.Add(r);
            if (renderers.Count == 0) return null;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++) b.Encapsulate(renderers[i].bounds);

            // Flat objects (a mat) are photographed more from above so they do not collapse into a line.
            float horizontal = Mathf.Max(b.size.x, b.size.z);
            float pitch = b.size.y < horizontal * 0.3f ? 55f : 24f;
            Quaternion rot = Quaternion.Euler(pitch, 150f, 0f);
            Quaternion inv = Quaternion.Inverse(rot);

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(
                    (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = inv * (corner - b.center);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            float radius = b.extents.magnitude;
            float half = Mathf.Max(max.x - min.x, max.y - min.y) * 0.5f * 1.12f;
            Vector3 centerLocal = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
            float dist = radius * 2f + 0.01f;

            camGo = new GameObject("IconCamera");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            Camera cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(half, 0.0001f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << layer;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            cam.nearClipPlane = Mathf.Max(0.0005f, dist - radius * 1.5f);
            cam.farClipPlane = dist + radius * 1.5f;
            cam.transform.rotation = rot;
            cam.transform.position = b.center + rot * centerLocal - rot * Vector3.forward * dist;

            lightGo = new GameObject("IconLight");
            lightGo.hideFlags = HideFlags.HideAndDontSave;
            lightGo.layer = layer;
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.None;
            light.transform.rotation = rot * Quaternion.Euler(25f, 25f, 0f);

            rt = new RenderTexture(IconSize, IconSize, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
            tex.Apply();
            return tex;
        }
        finally
        {
            RenderTexture.active = previous;
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            if (camGo != null) Object.DestroyImmediate(camGo);
            if (lightGo != null) Object.DestroyImmediate(lightGo);
            if (root != null) Object.DestroyImmediate(root);
        }
    }

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    static int FindFreeLayer()
    {
        for (int i = 31; i >= 8; i--)
            if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
        return 31;
    }

    static string ToDiskPath(string assetPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
    }

    static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder)) return;
        string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
    }
}
#endif
