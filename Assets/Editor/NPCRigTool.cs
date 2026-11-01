#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// One-off editor helper used to finish the NPC model swap inside Unity (no external DCC):
///  - BuildMrBeast: merges the 3 duplicated skeletons of MrBeast.fbx into one and builds a valid Humanoid avatar.
///  - BuildUncleGrandpa: Generic rig (already has bones) + procedural clips on a copy of the NPC_UAL controller.
///  - BuildStatic: gives a skeleton-less mesh model (Yubaba, masha) a simple 3-bone skinned rig + procedural clips.
/// Everything is written to Assets/Art/Characters/NPC/Rigged. Controllers keep the NPC_UAL parameters/states so the
/// existing NPCRoleAnimator works unchanged.
/// </summary>
public static class NPCRigTool
{
    const string Out = "Assets/Art/Characters/NPC/Rigged";
    const string Ual = "Assets/Animations/NPC/Shared/NPC_UAL.controller";

    class Roles { public string hips, spine, head, armL, armR, legL, legR; }

    static void EnsureDir(string p)
    {
        if (AssetDatabase.IsValidFolder(p)) return;
        string parent = System.IO.Path.GetDirectoryName(p).Replace('\\', '/');
        EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(p));
    }

    static Bounds WorldBounds(GameObject go)
    {
        Bounds b = new Bounds(); bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // ------------------------------------------------------------------ MrBeast (Humanoid)
    public static string BuildMrBeast()
    {
        var sb = new StringBuilder();
        string fbx = "Assets/Art/Characters/NPC/MrBeast.fbx";
        EnsureDir(Out + "/MrBeast");

        // silence the permanent "Rig Error" of the source file: our own avatar is built below
        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        var oldHd = imp.humanDescription;
        if (imp.animationType != ModelImporterAnimationType.Generic)
        {
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.SaveAndReimport();
        }

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var go = (GameObject)Object.Instantiate(asset);
        go.name = "MrBeast";
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        foreach (var n in new[] { "Camera", "Light" })
        {
            var t = go.transform.Find(n);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        Transform body = null; int bestCount = -1;
        foreach (Transform ch in go.transform) if (ch.name.EndsWith(".ao")) { int cnt = ch.GetComponentsInChildren<Transform>(true).Length; if (cnt > bestCount) { bestCount = cnt; body = ch; } }
        if (body == null) { Object.DestroyImmediate(go); return "ERROR: no .ao armature found"; }

        var map = new Dictionary<string, Transform>();
        foreach (var t in body.GetComponentsInChildren<Transform>(true)) if (!map.ContainsKey(t.name)) map[t.name] = t;

        // graft branches that exist only in the other armatures (face/hair/limbs) onto the base skeleton
        int grafted = 0;
        foreach (Transform other in new List<Transform>(go.transform.GetComponentsInChildren<Transform>(true)))
        {
            if (!other.name.EndsWith(".ao") || other == body) continue;
            foreach (Transform t in new List<Transform>(other.GetComponentsInChildren<Transform>(true)))
            {
                if (t == other || map.ContainsKey(t.name)) continue;
                Transform tp;
                if (t.parent == null || !map.TryGetValue(t.parent.name, out tp)) continue;
                t.SetParent(tp, true); grafted++;
                foreach (var d in t.GetComponentsInChildren<Transform>(true)) if (!map.ContainsKey(d.name)) map[d.name] = d;
            }
        }
        sb.AppendLine("grafted branches=" + grafted);

        int missing = 0, rebound = 0; float maxDiff = 0f;
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var bones = smr.bones; var nb = new Transform[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null) { nb[i] = null; continue; }
                Transform m;
                if (map.TryGetValue(b.name, out m))
                {
                    maxDiff = Mathf.Max(maxDiff, (m.position - b.position).magnitude);
                    nb[i] = m; if (m != b) rebound++;
                }
                else { nb[i] = b; missing++; sb.AppendLine("  bone not in body skeleton: " + b.name); }
            }
            smr.bones = nb;
            Transform rb;
            if (smr.rootBone != null && map.TryGetValue(smr.rootBone.name, out rb)) smr.rootBone = rb;
        }
        sb.AppendLine("rebound bones=" + rebound + " missing=" + missing + " maxPositionDiff=" + maxDiff.ToString("F5"));
        if (missing > 0 || maxDiff > 0.01f)
        {
            Object.DestroyImmediate(go);
            return sb.ToString() + "ERROR: skeleton copies are not identical, aborted";
        }

        foreach (Transform c in new List<Transform>(go.GetComponentsInChildren<Transform>(true)))
        {
            if (c != null && c.name.EndsWith(".ao") && c != body) Object.DestroyImmediate(c.gameObject);
        }

        // unique-name check
        var seen = new HashSet<string>(); var dups = new List<string>();
        var all = go.GetComponentsInChildren<Transform>(true);
        foreach (var t in all) if (!seen.Add(t.name)) dups.Add(t.name);
        sb.AppendLine("transforms=" + all.Length + " duplicateNames=" + dups.Count + (dups.Count > 0 ? " e.g. " + dups[0] : ""));

        var hbs = new List<HumanBone>();
        foreach (var h in oldHd.human)
        {
            var hb = h;
            if (hb.humanName == "Head") hb.boneName = "head";
            else if (hb.humanName == "Chest") hb.boneName = "spine_03";
            else if (hb.humanName == "UpperChest") hb.boneName = "spine_05";
            if (!seen.Contains(hb.boneName)) { sb.AppendLine("  skipped " + hb.humanName + " -> " + hb.boneName + " (not found)"); continue; }
            hb.limit.useDefaultValues = true;
            hbs.Add(hb);
        }
        if (hbs.Count == 0)
        {
            var pairs = new List<string[]>
            {
                new[]{"Hips","pelvis"}, new[]{"Spine","spine_01"}, new[]{"Chest","spine_03"}, new[]{"UpperChest","spine_05"},
                new[]{"Neck","neck_01"}, new[]{"Head","head"}
            };
            foreach (var side in new[] { new[]{"Left","_l"}, new[]{"Right","_r"} })
            {
                string S = side[0], x = side[1];
                pairs.Add(new[]{ S + "UpperLeg", "thigh" + x }); pairs.Add(new[]{ S + "LowerLeg", "calf" + x });
                pairs.Add(new[]{ S + "Foot", "foot" + x }); pairs.Add(new[]{ S + "Toes", "ball" + x });
                pairs.Add(new[]{ S + "Shoulder", "clavicle" + x }); pairs.Add(new[]{ S + "UpperArm", "upperarm" + x });
                pairs.Add(new[]{ S + "LowerArm", "lowerarm" + x }); pairs.Add(new[]{ S + "Hand", "hand" + x });
                pairs.Add(new[]{ S + " Thumb Proximal", "thumb_01" + x }); pairs.Add(new[]{ S + " Thumb Intermediate", "thumb_02" + x }); pairs.Add(new[]{ S + " Thumb Distal", "thumb_03" + x });
                string[,] fing = { {"Index","index"}, {"Middle","middle"}, {"Ring","ring"}, {"Little","pinky"} };
                for (int f = 0; f < 4; f++)
                {
                    pairs.Add(new[]{ S + " " + fing[f,0] + " Proximal", fing[f,1] + "_metacarpal" + x });
                    pairs.Add(new[]{ S + " " + fing[f,0] + " Intermediate", fing[f,1] + "_01" + x });
                    pairs.Add(new[]{ S + " " + fing[f,0] + " Distal", fing[f,1] + "_02" + x });
                }
            }
            foreach (var pr in pairs)
            {
                if (!seen.Contains(pr[1])) { sb.AppendLine("  skipped " + pr[0] + " -> " + pr[1] + " (not found)"); continue; }
                var hb = new HumanBone { humanName = pr[0], boneName = pr[1] };
                hb.limit.useDefaultValues = true;
                hbs.Add(hb);
            }
        }
        sb.AppendLine("human bones mapped=" + hbs.Count);
        var sk = new List<SkeletonBone>();
        foreach (var t in all)
            sk.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
        var hd = new HumanDescription
        {
            human = hbs.ToArray(), skeleton = sk.ToArray(),
            upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
            armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false
        };
        Avatar avatar = AvatarBuilder.BuildHumanAvatar(go, hd);
        sb.AppendLine("avatar valid=" + avatar.isValid + " human=" + avatar.isHuman);
        if (!avatar.isValid || !avatar.isHuman) { Object.DestroyImmediate(go); return sb.ToString() + "ERROR: humanoid avatar not valid"; }

        string avPath = Out + "/MrBeast/MrBeast_HumanAvatar.asset";
        AssetDatabase.DeleteAsset(avPath);
        avatar.name = "MrBeast_HumanAvatar";
        AssetDatabase.CreateAsset(avatar, avPath);

        var an = go.GetComponent<Animator>();
        if (an == null) an = go.AddComponent<Animator>();
        an.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(avPath);
        an.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Ual);
        an.applyRootMotion = false;

        string prefab = Out + "/MrBeast/MrBeast_Rigged.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefab);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        return sb.ToString() + "OK prefab=" + prefab;
    }

    // ------------------------------------------------------------------ Uncle Grandpa (Generic, existing bones)
    public static string BuildUncleGrandpa()
    {
        var sb = new StringBuilder();
        string fbx = "Assets/Art/Characters/NPC/Uncle Grandpa.fbx";
        string name = "UncleGrandpa";
        EnsureDir(Out + "/" + name);

        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        if (imp.animationType != ModelImporterAnimationType.Generic)
        {
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();
        }
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var go = (GameObject)Object.Instantiate(asset);
        go.name = "Uncle Grandpa";
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        string p = "uncle grandpa.qc_skeleton/Pelvis";
        var r = new Roles
        {
            hips = p, spine = null, head = p + "/Head",
            armL = p + "/Arm1_left", armR = p + "/Arm1_right",
            legL = p + "/Leg1_left", legR = p + "/Leg1_right"
        };
        foreach (var path in new[] { r.hips, r.head, r.armL, r.armR, r.legL, r.legR })
            if (go.transform.Find(path) == null) { Object.DestroyImmediate(go); return "ERROR: bone path not found: " + path; }

        float h = WorldBounds(go).size.y;
        sb.AppendLine("height=" + h.ToString("F3"));
        sb.AppendLine(BuildClipsAndController(name, go.transform, r, h));

        var an = go.GetComponent<Animator>();
        if (an == null) an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Out + "/" + name + "/" + name + ".controller");
        an.applyRootMotion = false;

        PrefabUtility.SaveAsPrefabAsset(go, Out + "/" + name + "/" + name + "_Rigged.prefab");
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        return sb.ToString() + "OK";
    }

    // ------------------------------------------------------------------ Static mesh -> simple skinned rig
    public static string BuildStatic(string name, string fbx)
    {
        var sb = new StringBuilder();
        EnsureDir(Out + "/" + name);
        EnsureDir(Out + "/" + name + "/Meshes");

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (asset == null) return "ERROR: model not found " + fbx;
        var go = (GameObject)Object.Instantiate(asset);
        go.name = name;
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;

        var mrs = go.GetComponentsInChildren<MeshRenderer>(true);
        if (mrs.Length == 0) { Object.DestroyImmediate(go); return "ERROR: no MeshRenderer in " + fbx; }
        Bounds wb = WorldBounds(go);
        float minY = wb.min.y, H = wb.size.y;
        Vector3 c = wb.center;
        sb.AppendLine("bounds size=" + wb.size.ToString("F3") + " minY=" + minY.ToString("F3") + " meshes=" + mrs.Length);

        var armature = new GameObject("Armature").transform; armature.SetParent(go.transform, false);
        armature.position = new Vector3(c.x, minY, c.z);
        var hips = new GameObject("Hips").transform; hips.SetParent(armature, false); hips.position = new Vector3(c.x, minY + 0.38f * H, c.z);
        var spine = new GameObject("Spine").transform; spine.SetParent(hips, false); spine.position = new Vector3(c.x, minY + 0.62f * H, c.z);
        var head = new GameObject("Head").transform; head.SetParent(spine, false); head.position = new Vector3(c.x, minY + 0.84f * H, c.z);
        var bones = new[] { hips, spine, head };

        int totalVerts = 0;
        foreach (var mr in mrs)
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Mesh src = mf.sharedMesh;
            Mesh m = Object.Instantiate(src);
            m.name = src.name + "_skinned";
            Vector3[] verts = m.vertices;
            if (verts.Length == 0) { sb.AppendLine("  WARNING mesh not readable: " + src.name); continue; }
            Matrix4x4 toRoot = go.transform.worldToLocalMatrix * mr.transform.localToWorldMatrix;
            var weights = new BoneWeight[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                float y = toRoot.MultiplyPoint3x4(verts[i]).y;
                float hh = Mathf.Clamp01((y - minY) / H);
                BoneWeight bw = new BoneWeight();
                if (hh <= 0.40f) { bw.boneIndex0 = 0; bw.weight0 = 1f; }
                else if (hh < 0.55f) { float t = Mathf.SmoothStep(0, 1, (hh - 0.40f) / 0.15f); bw.boneIndex0 = 0; bw.weight0 = 1 - t; bw.boneIndex1 = 1; bw.weight1 = t; }
                else if (hh <= 0.70f) { bw.boneIndex0 = 1; bw.weight0 = 1f; }
                else if (hh < 0.80f) { float t = Mathf.SmoothStep(0, 1, (hh - 0.70f) / 0.10f); bw.boneIndex0 = 1; bw.weight0 = 1 - t; bw.boneIndex1 = 2; bw.weight1 = t; }
                else { bw.boneIndex0 = 2; bw.weight0 = 1f; }
                weights[i] = bw;
            }
            m.boneWeights = weights;
            var bind = new Matrix4x4[3];
            for (int i = 0; i < 3; i++) bind[i] = bones[i].worldToLocalMatrix * mr.transform.localToWorldMatrix;
            m.bindposes = bind;
            m.RecalculateBounds();

            var mats = mr.sharedMaterials;
            var shadow = mr.shadowCastingMode; bool recv = mr.receiveShadows;
            var host = mr.gameObject;
            Object.DestroyImmediate(mr);
            Object.DestroyImmediate(mf);
            var smr = host.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = m;
            smr.sharedMaterials = mats;
            smr.bones = bones;
            smr.rootBone = hips;
            smr.shadowCastingMode = shadow; smr.receiveShadows = recv;
            Bounds lb = m.bounds; lb.Expand(lb.size * 0.25f); smr.localBounds = lb;

            string mp = Out + "/" + name + "/Meshes/" + host.name + "_skinned.asset";
            AssetDatabase.DeleteAsset(mp);
            AssetDatabase.CreateAsset(m, mp);
            totalVerts += verts.Length;
        }
        sb.AppendLine("skinned vertices=" + totalVerts);

        var r = new Roles { hips = "Armature/Hips", spine = "Armature/Hips/Spine", head = "Armature/Hips/Spine/Head" };
        sb.AppendLine(BuildClipsAndController(name, go.transform, r, H));

        var an = go.GetComponent<Animator>();
        if (an == null) an = go.AddComponent<Animator>();
        an.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Out + "/" + name + "/" + name + ".controller");
        an.applyRootMotion = false;

        PrefabUtility.SaveAsPrefabAsset(go, Out + "/" + name + "/" + name + "_Rigged.prefab");
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        return sb.ToString() + "OK";
    }

    // ------------------------------------------------------------------ procedural clips
    class CB
    {
        public Dictionary<string, Func<float, Quaternion>> rot = new Dictionary<string, Func<float, Quaternion>>();
        public Dictionary<string, Func<float, Vector3>> pos = new Dictionary<string, Func<float, Vector3>>();
        public void R(string path, Func<float, Quaternion> f)
        {
            if (path == null) return;
            Func<float, Quaternion> prev;
            if (rot.TryGetValue(path, out prev)) rot[path] = t => f(t) * prev(t); else rot[path] = f;
        }
        public void P(string path, Func<float, Vector3> f)
        {
            if (path == null) return;
            Func<float, Vector3> prev;
            if (pos.TryGetValue(path, out prev)) pos[path] = t => f(t) + prev(t); else pos[path] = f;
        }
    }

    static float Env(float u, float a, float b, float c)
    {
        if (u < a) return Mathf.SmoothStep(0, 1, u / a);
        if (u < b) return 1f;
        if (u < c) return 1f - Mathf.SmoothStep(0, 1, (u - b) / (c - b));
        return 0f;
    }

    static Vector3 LimbDir(Transform b)
    {
        if (b == null || b.childCount == 0) return Vector3.down;
        Vector3 d = b.GetChild(0).position - b.position;
        return d.sqrMagnitude < 1e-12f ? Vector3.down : d.normalized;
    }

    static Quaternion Swing(Vector3 d, float deg)
    {
        Vector3 ax = Vector3.Cross(d, Vector3.forward);
        if (ax.sqrMagnitude < 1e-4f) ax = Vector3.left;
        return Quaternion.AngleAxis(deg, ax.normalized);
    }

    static void Bake(AnimationClip clip, Transform root, CB cb, float len)
    {
        int n = Mathf.Max(4, Mathf.RoundToInt(len * 20f));
        foreach (var kv in cb.rot)
        {
            Transform b = root.Find(kv.Key); if (b == null) continue;
            Quaternion pw = b.parent.rotation, q0 = b.localRotation, prev = q0;
            var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
            for (int i = 0; i <= n; i++)
            {
                float t = len * i / n;
                Quaternion q = Quaternion.Inverse(pw) * kv.Value(t) * pw * q0;
                if (Quaternion.Dot(q, prev) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                prev = q;
                cx.AddKey(t, q.x); cy.AddKey(t, q.y); cz.AddKey(t, q.z); cw.AddKey(t, q.w);
            }
            Set(clip, kv.Key, "m_LocalRotation.x", cx); Set(clip, kv.Key, "m_LocalRotation.y", cy);
            Set(clip, kv.Key, "m_LocalRotation.z", cz); Set(clip, kv.Key, "m_LocalRotation.w", cw);
        }
        foreach (var kv in cb.pos)
        {
            Transform b = root.Find(kv.Key); if (b == null) continue;
            Vector3 p0 = b.localPosition;
            var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve();
            for (int i = 0; i <= n; i++)
            {
                float t = len * i / n;
                Vector3 v = p0 + b.parent.InverseTransformVector(kv.Value(t));
                cx.AddKey(t, v.x); cy.AddKey(t, v.y); cz.AddKey(t, v.z);
            }
            Set(clip, kv.Key, "m_LocalPosition.x", cx); Set(clip, kv.Key, "m_LocalPosition.y", cy); Set(clip, kv.Key, "m_LocalPosition.z", cz);
        }
    }

    static void Set(AnimationClip clip, string path, string prop, AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop), curve);
    }

    static string BuildClipsAndController(string name, Transform root, Roles r, float h)
    {
        var sb = new StringBuilder();
        Vector3 dAL = r.armL != null ? LimbDir(root.Find(r.armL)) : Vector3.down;
        Vector3 dAR = r.armR != null ? LimbDir(root.Find(r.armR)) : Vector3.down;
        Vector3 dLL = r.legL != null ? LimbDir(root.Find(r.legL)) : Vector3.down;
        Vector3 dLR = r.legR != null ? LimbDir(root.Find(r.legR)) : Vector3.down;
        string spineP = r.spine ?? r.hips;

        // state name -> (length, loop, builder)
        var defs = new List<KeyValuePair<string, object[]>>();
        Action<string, float, bool, Action<CB, float>> add = (state, len, loop, body) =>
            defs.Add(new KeyValuePair<string, object[]>(state, new object[] { len, loop, body }));

        const float TwoPi = Mathf.PI * 2f;

        add("Idle", 2.4f, true, (cb, L) =>
        {
            cb.P(r.hips, t => Vector3.up * (0.004f * h * Mathf.Sin(TwoPi * t / L)));
            cb.R(spineP, t => Quaternion.Euler(1.2f * Mathf.Sin(TwoPi * t / L), 0, 0));
            cb.R(r.head, t => Quaternion.Euler(-0.8f * Mathf.Sin(TwoPi * t / L + 0.6f), 1.5f * Mathf.Sin(TwoPi * t / L), 0));
            cb.R(r.armL, t => Swing(dAL, 2f * Mathf.Sin(TwoPi * t / L)));
            cb.R(r.armR, t => Swing(dAR, -2f * Mathf.Sin(TwoPi * t / L)));
        });
        add("Walk", 0.8f, true, (cb, L) =>
        {
            cb.P(r.hips, t => Vector3.up * (0.012f * h * (1f - Mathf.Cos(2f * TwoPi * t / L)) * 0.5f));
            cb.R(r.hips, t => Quaternion.Euler(0, 0, 3f * Mathf.Sin(TwoPi * t / L)));
            cb.R(spineP, t => Quaternion.Euler(2f, 4f * Mathf.Sin(TwoPi * t / L), 0));
            cb.R(r.legL, t => Swing(dLL, 25f * Mathf.Sin(TwoPi * t / L)));
            cb.R(r.legR, t => Swing(dLR, -25f * Mathf.Sin(TwoPi * t / L)));
            cb.R(r.armL, t => Swing(dAL, -20f * Mathf.Sin(TwoPi * t / L)));
            cb.R(r.armR, t => Swing(dAR, 20f * Mathf.Sin(TwoPi * t / L)));
        });
        add("Talk", 1.4f, true, (cb, L) =>
        {
            cb.R(r.head, t => Quaternion.Euler(5f * Mathf.Sin(2f * TwoPi * t / L), 4f * Mathf.Sin(TwoPi * t / L), 0));
            cb.R(spineP, t => Quaternion.Euler(1.5f * Mathf.Sin(2f * TwoPi * t / L), 0, 0));
            cb.R(r.armR, t => Swing(dAR, 12f + 10f * Mathf.Sin(2f * TwoPi * t / L)));
            cb.R(r.armL, t => Swing(dAL, 4f * Mathf.Sin(TwoPi * t / L)));
        });
        add("Gesture_PickUp", 2.2f, false, (cb, L) =>
        {
            cb.P(r.hips, t => Vector3.down * (0.04f * h * Env(t / L, 0.3f, 0.6f, 0.9f)));
            cb.R(spineP, t => Quaternion.Euler(35f * Env(t / L, 0.3f, 0.6f, 0.9f), 0, 0));
            cb.R(r.head, t => Quaternion.Euler(10f * Env(t / L, 0.3f, 0.6f, 0.9f), 0, 0));
            cb.R(r.armR, t => Swing(dAR, 50f * Env(t / L, 0.3f, 0.6f, 0.9f)));
        });
        add("Gesture_Interact", 1.8f, false, (cb, L) =>
        {
            cb.R(r.armR, t => Swing(dAR, 70f * Env(t / L, 0.25f, 0.65f, 0.9f)));
            cb.R(r.head, t => Quaternion.Euler(0, 8f * Env(t / L, 0.25f, 0.65f, 0.9f), 0));
            cb.R(spineP, t => Quaternion.Euler(5f * Env(t / L, 0.25f, 0.65f, 0.9f), 0, 0));
        });
        add("Gesture_Jab", 0.8f, false, (cb, L) =>
        {
            cb.R(spineP, t => Quaternion.Euler(0, -15f * Env(t / L, 0.2f, 0.4f, 0.85f), 0));
            cb.R(r.armR, t => Swing(dAR, 85f * Env(t / L, 0.2f, 0.4f, 0.85f)));
            cb.P(r.hips, t => Vector3.forward * (0.03f * h * Env(t / L, 0.2f, 0.4f, 0.85f)));
        });
        add("Gesture_Cross", 0.9f, false, (cb, L) =>
        {
            cb.R(spineP, t => Quaternion.Euler(0, 20f * Env(t / L, 0.25f, 0.45f, 0.85f), 0));
            cb.R(r.armL, t => Swing(dAL, 85f * Env(t / L, 0.25f, 0.45f, 0.85f)));
            cb.P(r.hips, t => Vector3.forward * (0.03f * h * Env(t / L, 0.25f, 0.45f, 0.85f)));
        });
        add("Gesture_Fixing", 3.2f, false, (cb, L) =>
        {
            cb.P(r.hips, t => Vector3.down * (0.12f * h * Env(t / L, 0.2f, 0.8f, 0.95f)));
            cb.R(spineP, t => Quaternion.Euler(30f * Env(t / L, 0.2f, 0.8f, 0.95f), 0, 0));
            cb.R(r.head, t => Quaternion.Euler(15f * Env(t / L, 0.2f, 0.8f, 0.95f), 6f * Mathf.Sin(TwoPi * t / L * 2f) * Env(t / L, 0.2f, 0.8f, 0.95f), 0));
            cb.R(r.armR, t => Swing(dAR, (35f + 10f * Mathf.Sin(TwoPi * t / L * 3f)) * Env(t / L, 0.2f, 0.8f, 0.95f)));
        });
        add("Gesture_Torch", 2.4f, true, (cb, L) =>
        {
            cb.R(r.armR, t => Swing(dAR, 100f + 3f * Mathf.Sin(TwoPi * t / L)));
            cb.R(r.head, t => Quaternion.Euler(0, 0, 4f * Mathf.Sin(TwoPi * t / L)));
            cb.R(spineP, t => Quaternion.Euler(2f, 0, 0));
        });
        add("Gesture_Dance", 1.2f, true, (cb, L) =>
        {
            cb.P(r.hips, t => Vector3.up * (0.02f * h * (1f - Mathf.Cos(2f * TwoPi * t / L)) * 0.5f));
            cb.R(r.hips, t => Quaternion.Euler(0, 0, 8f * Mathf.Sin(TwoPi * t / L)));
            cb.R(spineP, t => Quaternion.Euler(0, 0, -8f * Mathf.Sin(TwoPi * t / L)));
            cb.R(r.head, t => Quaternion.Euler(0, 0, 10f * Mathf.Sin(2f * TwoPi * t / L)));
            cb.R(r.armL, t => Swing(dAL, 40f * Mathf.Sin(2f * TwoPi * t / L)));
            cb.R(r.armR, t => Swing(dAR, -40f * Mathf.Sin(2f * TwoPi * t / L)));
        });

        var clips = new Dictionary<string, AnimationClip>();
        foreach (var d in defs)
        {
            float len = (float)d.Value[0]; bool loop = (bool)d.Value[1];
            var body = (Action<CB, float>)d.Value[2];
            var cb = new CB(); body(cb, len);
            var clip = new AnimationClip { name = name + "_" + d.Key, frameRate = 30f };
            Bake(clip, root, cb, len);
            var s = AnimationUtility.GetAnimationClipSettings(clip); s.loopTime = loop; s.loopBlendPositionY = false;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            string cp = Out + "/" + name + "/" + name + "_" + d.Key + ".anim";
            AssetDatabase.DeleteAsset(cp);
            AssetDatabase.CreateAsset(clip, cp);
            clips[d.Key] = AssetDatabase.LoadAssetAtPath<AnimationClip>(cp);
        }

        string dst = Out + "/" + name + "/" + name + ".controller";
        AssetDatabase.DeleteAsset(dst);
        if (!AssetDatabase.CopyAsset(Ual, dst)) return "ERROR: could not copy NPC_UAL controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(dst);
        int assigned = 0;
        foreach (var layer in ctrl.layers)
            foreach (var st in layer.stateMachine.states)
            {
                AnimationClip c;
                if (clips.TryGetValue(st.state.name, out c)) { st.state.motion = c; assigned++; }
                else sb.AppendLine("  state without clip: " + st.state.name);
            }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        sb.AppendLine("controller=" + dst + " clips=" + clips.Count + " statesAssigned=" + assigned);
        return sb.ToString();
    }
}
#endif
