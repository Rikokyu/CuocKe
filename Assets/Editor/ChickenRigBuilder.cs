#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-shot Editor tool that rigs and animates the "feature" chicken models:
///   ostrich, dino (bird template) - chicken_family (one bird rig per animal) - chicken_smart, chicken_cook (humanoid template)
/// and adds Idle / Walk / Gesture to chicken_jocky (already rigged) plus Attack / Hit / Die (+ Walk / Eat wiring) to the normal chicken controller.
/// Bones, skin weights, Avatars, clips and controllers are all generated from code; nothing is bought or imported.
/// </summary>
public static class ChickenRigBuilder
{
    const string AnimDir = "Assets/Animations/Chicken/Feature";
    const string MeshDir = "Assets/Art/Chicken/Rigged";
    const string PrefabDir = "Assets/Prefabs/feature-Character";

    static StringBuilder log;
    static void L(string s) { log.AppendLine(s); }

    // ------------------------------------------------------------------ math / curve helpers
    static float SS(float e0, float e1, float x)
    {
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    /// +1 / -1: does rotating 'point' about 'axis' (positive angle, around 'pivot') move it toward 'want'?
    static float Sgn(Vector3 pivot, Vector3 point, Vector3 axis, Vector3 want)
    {
        Vector3 r = point - pivot;
        Vector3 d = Quaternion.AngleAxis(10f, axis) * r - r;
        return Vector3.Dot(d, want) >= 0f ? 1f : -1f;
    }

    static Func<float, float> Sin(float amp, float len, float phase, float cycles)
    {
        return t => amp * Mathf.Sin(Mathf.PI * 2f * (cycles * t / len + phase));
    }

    static Func<float, float> Const(float v) { return t => v; }

    /// piecewise smooth curve from (time,value) pairs
    static Func<float, float> K(params float[] tv)
    {
        return t =>
        {
            if (t <= tv[0]) return tv[1];
            for (int i = 2; i < tv.Length; i += 2)
                if (t <= tv[i]) return Mathf.Lerp(tv[i - 1], tv[i + 1], SS(tv[i - 2], tv[i], t));
            return tv[tv.Length - 1];
        };
    }

    static Func<float, float> Add(Func<float, float> a, Func<float, float> b) { return t => a(t) + b(t); }

    static Func<float, float> Window(Func<float, float> f, float t0, float t1)
    {
        return t => f(t) * SS(t0, t0 + 0.25f, t) * (1f - SS(t1 - 0.25f, t1, t));
    }

    struct Ax
    {
        public Vector3 axis; public Func<float, float> deg;
        public Ax(Vector3 a, Func<float, float> d) { axis = a; deg = d; }
    }

    // ------------------------------------------------------------------ clip authoring
    class Author
    {
        public readonly AnimationClip clip;
        readonly Transform root;
        readonly float len;
        readonly bool loop;
        const int Fps = 30;

        public Author(string name, Transform root, float len, bool loop)
        {
            clip = new AnimationClip();
            clip.name = name;
            clip.frameRate = Fps;
            this.root = root; this.len = len; this.loop = loop;
        }

        /// world-axis rotations (applied in the given order, first = outermost) on top of the bone's rest rotation
        public void Rot(Transform bone, params Ax[] axes)
        {
            string path = AnimationUtility.CalculateTransformPath(bone, root);
            Transform parent = bone.parent;
            Quaternion rest = bone.localRotation;
            Vector3[] la = new Vector3[axes.Length];
            for (int i = 0; i < axes.Length; i++) la[i] = parent.InverseTransformDirection(axes[i].axis).normalized;

            int n = Mathf.Max(2, Mathf.RoundToInt(len * Fps));
            AnimationCurve cx = new AnimationCurve(), cy = new AnimationCurve(), cz = new AnimationCurve(), cw = new AnimationCurve();
            Quaternion prev = rest;
            for (int k = 0; k <= n; k++)
            {
                float t = (float)k / n * len;
                Quaternion q = Quaternion.identity;
                for (int i = 0; i < axes.Length; i++) q = q * Quaternion.AngleAxis(axes[i].deg(t), la[i]);
                q = q * rest;
                if (Quaternion.Dot(q, prev) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                prev = q;
                cx.AddKey(t, q.x); cy.AddKey(t, q.y); cz.AddKey(t, q.z); cw.AddKey(t, q.w);
            }
            Smooth(cx); Smooth(cy); Smooth(cz); Smooth(cw);
            clip.SetCurve(path, typeof(Transform), "localRotation.x", cx);
            clip.SetCurve(path, typeof(Transform), "localRotation.y", cy);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", cz);
            clip.SetCurve(path, typeof(Transform), "localRotation.w", cw);
        }

        /// displacement along a WORLD direction (metres) added to the bone's rest local position
        public void Pos(Transform bone, Vector3 worldDir, Func<float, float> meters)
        {
            string path = AnimationUtility.CalculateTransformPath(bone, root);
            Vector3 rest = bone.localPosition;
            Vector3 dl = bone.parent.InverseTransformVector(worldDir.normalized);
            int n = Mathf.Max(2, Mathf.RoundToInt(len * Fps));
            AnimationCurve cx = new AnimationCurve(), cy = new AnimationCurve(), cz = new AnimationCurve();
            for (int k = 0; k <= n; k++)
            {
                float t = (float)k / n * len;
                Vector3 p = rest + dl * meters(t);
                cx.AddKey(t, p.x); cy.AddKey(t, p.y); cz.AddKey(t, p.z);
            }
            Smooth(cx); Smooth(cy); Smooth(cz);
            clip.SetCurve(path, typeof(Transform), "localPosition.x", cx);
            clip.SetCurve(path, typeof(Transform), "localPosition.y", cy);
            clip.SetCurve(path, typeof(Transform), "localPosition.z", cz);
        }

        static void Smooth(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        }

        public AnimationClip Save(string path)
        {
            clip.EnsureQuaternionContinuity();
            AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = loop; s.loopBlendOrientation = loop; s.loopBlendPositionY = loop; s.loopBlendPositionXZ = loop;
            s.startTime = 0f; s.stopTime = len;
            AnimationUtility.SetAnimationClipSettings(clip, s);

            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }
    }

    // ------------------------------------------------------------------ geometry
    class RInfo
    {
        public Renderer r; public Mesh src;
        public Vector3[] lv, ln, wv;
    }

    class Basis
    {
        public Vector3 c, f, l;
        public float minY, maxY, H, halfF, halfL;
    }

    class BirdP { public float legTop = 0.45f, kneeH = 0.22f, headH = 0.85f, z0 = 0.55f, z1 = 0.7f; }

    class Unit
    {
        public string prefix = "";
        public Basis b;
        public List<RInfo> rends = new List<RInfo>();
        public Transform[] bones;
        public Dictionary<string, Transform> map = new Dictionary<string, Transform>();
        public Vector3 TiltFwd, LegFwd, ArmFwdL, ArmFwdR, RollDownL, RollDownR;
        public bool armsOut; public float armDown;
        public BirdP bp = new BirdP();
        public Transform T(string n) { return map[n]; }
    }

    static RInfo Gather(Renderer r)
    {
        RInfo info = new RInfo(); info.r = r;
        SkinnedMeshRenderer smr = r as SkinnedMeshRenderer;
        Matrix4x4 l2w = r.transform.localToWorldMatrix;
        if (smr != null)
        {
            Mesh baked = new Mesh();
            smr.BakeMesh(baked, true); // world scale, relative to the renderer's position/rotation
            info.src = smr.sharedMesh;
            Vector3[] bv = baked.vertices;
            Matrix4x4 trs = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
            Matrix4x4 inv = l2w.inverse;
            info.wv = new Vector3[bv.Length]; info.lv = new Vector3[bv.Length];
            for (int i = 0; i < bv.Length; i++) { info.wv[i] = trs.MultiplyPoint3x4(bv[i]); info.lv[i] = inv.MultiplyPoint3x4(info.wv[i]); }
            info.ln = baked.normals;
            UnityEngine.Object.DestroyImmediate(baked);
        }
        else
        {
            info.src = r.GetComponent<MeshFilter>().sharedMesh;
            info.lv = info.src.vertices; info.ln = info.src.normals;
            info.wv = new Vector3[info.lv.Length];
            for (int i = 0; i < info.lv.Length; i++) info.wv[i] = l2w.MultiplyPoint3x4(info.lv[i]);
        }
        return info;
    }

    static Basis MakeBasis(List<Vector3> pts, bool bird, Vector3? forceF)
    {
        Basis b = new Basis();
        Vector3 mn = pts[0], mx = pts[0], sum = Vector3.zero;
        foreach (Vector3 p in pts) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); sum += p; }
        b.minY = mn.y; b.maxY = mx.y; b.H = Mathf.Max(0.01f, mx.y - mn.y);
        b.c = new Vector3((mn.x + mx.x) * 0.5f, 0f, (mn.z + mx.z) * 0.5f);
        Vector3 allC = sum / pts.Count; allC.y = 0f;

        if (forceF.HasValue) b.f = forceF.Value;
        else if (bird)
        {
            Vector3 top = Vector3.zero; int n = 0;
            foreach (Vector3 p in pts) if ((p.y - b.minY) / b.H > 0.8f) { top += new Vector3(p.x, 0f, p.z); n++; }
            Vector3 d = n > 0 ? top / n - allC : Vector3.forward; d.y = 0f;
            b.f = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, d.z >= 0f ? 1f : -1f);
        }
        else
        {
            float extX = mx.x - mn.x, extZ = mx.z - mn.z;
            Vector3 f0 = extX > extZ ? Vector3.forward : Vector3.right; // arms spread along the longer axis
            float maxF = -1e9f, minF = 1e9f; Vector3 feet = Vector3.zero; int fn = 0;
            foreach (Vector3 p in pts)
            {
                float h = (p.y - b.minY) / b.H;
                float d = Vector3.Dot(p - b.c, f0);
                if (h > 0.8f) { maxF = Mathf.Max(maxF, d); minF = Mathf.Min(minF, d); }
                if (h < 0.08f) { feet += p; fn++; }
            }
            float footOff = fn > 0 ? Vector3.Dot(feet / fn - b.c, f0) : 0f;
            float score = (maxF + minF) + footOff;
            b.f = score >= 0f ? f0 : -f0;
        }
        b.l = Vector3.Cross(Vector3.up, b.f).normalized;

        foreach (Vector3 p in pts)
        {
            float h = (p.y - b.minY) / b.H;
            b.halfF = Mathf.Max(b.halfF, Mathf.Abs(Vector3.Dot(p - b.c, b.f)));
            float s = Mathf.Abs(Vector3.Dot(p - b.c, b.l));
            if (bird || (h > 0.55f && h < 0.75f)) b.halfL = Mathf.Max(b.halfL, s);
        }
        b.halfF = Mathf.Max(b.halfF, 0.01f); b.halfL = Mathf.Max(b.halfL, 0.01f);
        return b;
    }

    // ------------------------------------------------------------------ bones
    static Transform Bone(Transform parent, string name, Vector3 worldPos)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        return g.transform;
    }

    static void CreateBirdBones(Unit u, Transform armature, List<Vector3> pts)
    {
        Basis b = u.b; BirdP bp = u.bp; Vector3 up = Vector3.up; string px = u.prefix;
        Vector3 hipsP = new Vector3(b.c.x, b.minY + bp.legTop * b.H, b.c.z);
        Vector3 hc = Vector3.zero; int n = 0;
        foreach (Vector3 p in pts) if ((p.y - b.minY) / b.H > bp.headH + 0.03f) { hc += p; n++; }
        hc = n > 0 ? hc / n : hipsP + b.f * 0.6f * b.halfF;
        Vector3 neckP = hipsP + b.f * (0.45f * b.halfF) + up * (0.10f * b.H);
        Vector3 headP = new Vector3(hc.x, b.minY + (bp.headH - 0.05f) * b.H, hc.z);
        Vector3 tailP = hipsP - b.f * (0.5f * b.halfF) + up * (0.05f * b.H);
        float spread = 0.25f * b.halfL;
        Vector3 thL = hipsP - b.l * spread, thR = hipsP + b.l * spread;
        Vector3 shL = new Vector3(thL.x, b.minY + bp.kneeH * b.H, thL.z), shR = new Vector3(thR.x, shL.y, thR.z);

        Transform hips = Bone(armature, px + "Hips", hipsP);
        Transform neck = Bone(hips, px + "Neck", neckP);
        Transform head = Bone(neck, px + "Head", headP);
        Transform tail = Bone(hips, px + "Tail", tailP);
        Transform tL = Bone(hips, px + "ThighL", thL), tR = Bone(hips, px + "ThighR", thR);
        Transform sL = Bone(tL, px + "ShinL", shL), sR = Bone(tR, px + "ShinR", shR);
        u.bones = new[] { hips, neck, head, tail, tL, tR, sL, sR };
        string[] names = { "Hips", "Neck", "Head", "Tail", "ThighL", "ThighR", "ShinL", "ShinR" };
        for (int i = 0; i < names.Length; i++) u.map[names[i]] = u.bones[i];

        u.TiltFwd = b.l * Sgn(hipsP, hipsP + up, b.l, b.f);
        u.LegFwd = b.l * Sgn(hipsP, hipsP - up, b.l, b.f);
    }

    static float[] BirdWeights(Unit u, Vector3 p)
    {
        Basis b = u.b; BirdP bp = u.bp;
        float uu = Vector3.Dot(p - b.c, b.f) / b.halfF;
        float s = Vector3.Dot(p - b.c, b.l);
        float h = (p.y - b.minY) / b.H;

        float wLeg = 1f - SS(bp.legTop - 0.05f, bp.legTop + 0.05f, h);
        float wShin = 1f - SS(bp.kneeH - 0.04f, bp.kneeH + 0.04f, h);
        float side = SS(-0.08f * b.halfL, 0.08f * b.halfL, s); // 1 = +lateral (R)
        float wUp = 1f - wLeg;
        float zone = SS(0.35f, 0.7f, uu) * SS(bp.z0, bp.z1, h);
        float head = SS(bp.headH - 0.1f, bp.headH, h);
        float wNeck = wUp * zone * (1f - head), wHead = wUp * zone * head;
        float wTail = wUp * (1f - zone) * SS(-0.4f, -0.8f, uu);
        float wHips = Mathf.Max(0f, wUp - wNeck - wHead - wTail);
        float thigh = wLeg * (1f - wShin), shin = wLeg * wShin;
        // order: Hips, Neck, Head, Tail, ThighL, ThighR, ShinL, ShinR
        return new[] { wHips, wNeck, wHead, wTail, thigh * (1f - side), thigh * side, shin * (1f - side), shin * side };
    }

    static void CreateHumBones(Unit u, Transform armature, List<Vector3> pts)
    {
        Basis b = u.b; Vector3 up = Vector3.up; string px = u.prefix;
        Vector3 hipsP = new Vector3(b.c.x, b.minY + 0.42f * b.H, b.c.z);
        Vector3 spineP = new Vector3(b.c.x, b.minY + 0.55f * b.H, b.c.z);
        Vector3 headP = new Vector3(b.c.x, b.minY + 0.80f * b.H, b.c.z);
        float shoulderY = b.minY + 0.72f * b.H;
        Vector3 aL = new Vector3(b.c.x, shoulderY, b.c.z) - b.l * 0.30f * b.halfL;
        Vector3 aR = new Vector3(b.c.x, shoulderY, b.c.z) + b.l * 0.30f * b.halfL;
        Vector3 lL = new Vector3(b.c.x, b.minY + 0.40f * b.H, b.c.z) - b.l * 0.10f * b.halfL;
        Vector3 lR = new Vector3(b.c.x, b.minY + 0.40f * b.H, b.c.z) + b.l * 0.10f * b.halfL;

        Transform hips = Bone(armature, px + "Hips", hipsP);
        Transform spine = Bone(hips, px + "Spine", spineP);
        Transform head = Bone(spine, px + "Head", headP);
        Transform armL = Bone(spine, px + "ArmL", aL), armR = Bone(spine, px + "ArmR", aR);
        Transform legL = Bone(hips, px + "LegL", lL), legR = Bone(hips, px + "LegR", lR);
        u.bones = new[] { hips, spine, head, armL, armR, legL, legR };
        string[] names = { "Hips", "Spine", "Head", "ArmL", "ArmR", "LegL", "LegR" };
        for (int i = 0; i < names.Length; i++) u.map[names[i]] = u.bones[i];

        u.TiltFwd = b.l * Sgn(hipsP, hipsP + up, b.l, b.f);
        u.LegFwd = b.l * Sgn(lL, lL - up, b.l, b.f);
        u.ArmFwdL = b.l * Sgn(aL, aL - up, b.l, b.f);
        u.ArmFwdR = b.l * Sgn(aR, aR - up, b.l, b.f);
        u.RollDownL = b.f * Sgn(aL, aL - b.l, b.f, -up);
        u.RollDownR = b.f * Sgn(aR, aR + b.l, b.f, -up);

        // are the arms stretched out sideways (T-pose)? then idle/walk lower them
        int armCount = 0; float armH = 0f;
        foreach (Vector3 p in pts)
        {
            float h = (p.y - b.minY) / b.H;
            float s = Mathf.Abs(Vector3.Dot(p - b.c, b.l)) / b.halfL;
            if (s > 0.7f && h > 0.3f && h < 0.95f) { armCount++; armH += h; }
        }
        float meanH = armCount > 0 ? armH / armCount : 0f;
        u.armsOut = armCount > pts.Count * 0.01f && meanH > 0.55f && (2f * b.halfL / b.H) > 0.5f;
        u.armDown = u.armsOut ? 55f : 0f;
    }

    static float[] HumWeights(Unit u, Vector3 p)
    {
        Basis b = u.b;
        float s = Vector3.Dot(p - b.c, b.l);
        float h = (p.y - b.minY) / b.H;
        float wLeg = 1f - SS(0.34f, 0.42f, h);
        float rest = 1f - wLeg;
        float armZone = SS(0.45f, 0.7f, Mathf.Abs(s) / b.halfL) * SS(0.28f, 0.38f, h) * (1f - SS(0.85f, 0.95f, h));
        float head = SS(0.78f, 0.86f, h);
        float arm = rest * armZone * (1f - head);
        float wHead = rest * head;
        float r2 = Mathf.Max(0f, rest - arm - wHead);
        float spine = r2 * SS(0.38f, 0.52f, h);
        float hips = r2 - spine;
        float side = SS(-0.08f * b.halfL, 0.08f * b.halfL, s); // 1 = R
        // order: Hips, Spine, Head, ArmL, ArmR, LegL, LegR
        return new[] { hips, spine, wHead, arm * (1f - side), arm * side, wLeg * (1f - side), wLeg * side };
    }

    static BoneWeight Top4(float[] w)
    {
        List<int> idx = new List<int>();
        for (int i = 0; i < w.Length; i++) idx.Add(i);
        idx.Sort((a, c) => w[c].CompareTo(w[a]));
        float sum = 0f;
        for (int i = 0; i < 4 && i < idx.Count; i++) sum += w[idx[i]];
        BoneWeight bw = new BoneWeight();
        if (sum <= 1e-5f) { bw.boneIndex0 = 0; bw.weight0 = 1f; return bw; }
        bw.boneIndex0 = idx[0]; bw.weight0 = w[idx[0]] / sum;
        bw.boneIndex1 = idx[1]; bw.weight1 = w[idx[1]] / sum;
        bw.boneIndex2 = idx[2]; bw.weight2 = w[idx[2]] / sum;
        bw.boneIndex3 = idx[3]; bw.weight3 = w[idx[3]] / sum;
        return bw;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static string Safe(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    static void SkinUnit(Unit u, string modelName, bool bird)
    {
        Transform rootBone = u.bones[0];
        foreach (RInfo ri in u.rends)
        {
            int n = ri.lv.Length;
            BoneWeight[] bw = new BoneWeight[n];
            for (int i = 0; i < n; i++) bw[i] = Top4(bird ? BirdWeights(u, ri.wv[i]) : HumWeights(u, ri.wv[i]));

            GameObject go = ri.r.gameObject;
            Matrix4x4 l2w = go.transform.localToWorldMatrix;
            Matrix4x4[] bind = new Matrix4x4[u.bones.Length];
            for (int k = 0; k < bind.Length; k++) bind[k] = u.bones[k].worldToLocalMatrix * l2w;

            Mesh nm = UnityEngine.Object.Instantiate(ri.src);
            nm.name = Safe(modelName + "_" + u.prefix + go.name) + "_rigged";
            nm.vertices = ri.lv;
            if (ri.ln != null && ri.ln.Length == n) nm.normals = ri.ln;
            nm.boneWeights = bw;
            nm.bindposes = bind;
            nm.RecalculateBounds();
            string path = MeshDir + "/" + nm.name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(nm, path);

            SkinnedMeshRenderer smr = ri.r as SkinnedMeshRenderer;
            if (smr == null)
            {
                Material[] mats = ri.r.sharedMaterials;
                var shadow = ri.r.shadowCastingMode; bool recv = ri.r.receiveShadows;
                MeshFilter mf = go.GetComponent<MeshFilter>();
                UnityEngine.Object.DestroyImmediate(ri.r);
                if (mf != null) UnityEngine.Object.DestroyImmediate(mf);
                smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMaterials = mats; smr.shadowCastingMode = shadow; smr.receiveShadows = recv;
            }
            smr.sharedMesh = nm;
            smr.bones = u.bones;
            smr.rootBone = rootBone;
            smr.updateWhenOffscreen = true;
        }
    }

    // ------------------------------------------------------------------ clips for the generated rigs
    static void BirdIdle(Author a, Unit u, float len, float ph)
    {
        Basis b = u.b; Vector3 up = Vector3.up;
        a.Pos(u.T("Hips"), up, Sin(0.004f * b.H, len, ph, 1));
        a.Rot(u.T("Hips"), new Ax(b.f, Sin(1.5f, len, ph, 1)));
        a.Rot(u.T("Neck"), new Ax(up, Sin(9f, len, ph + 0.2f, 1)), new Ax(u.TiltFwd, Sin(3f, len, ph, 2)));
        a.Rot(u.T("Head"), new Ax(up, Sin(12f, len, ph + 0.5f, 1)), new Ax(u.TiltFwd, Sin(4f, len, ph + 0.1f, 2)));
        a.Rot(u.T("Tail"), new Ax(up, Sin(7f, len, ph + 0.3f, 2)));
    }

    static void BirdWalk(Author a, Unit u, float len, float ph, float swing, float lift)
    {
        Basis b = u.b; Vector3 up = Vector3.up;
        a.Pos(u.T("Hips"), up, Sin(0.015f * b.H, len, ph, 2));
        a.Rot(u.T("Hips"), new Ax(b.f, Sin(2.5f, len, ph, 1)), new Ax(up, Sin(3f, len, ph + 0.25f, 1)));
        a.Rot(u.T("ThighL"), new Ax(u.LegFwd, Sin(swing, len, ph, 1)));
        a.Rot(u.T("ThighR"), new Ax(u.LegFwd, Sin(swing, len, ph + 0.5f, 1)));
        float two = Mathf.PI * 2f;
        a.Rot(u.T("ShinL"), new Ax(-u.LegFwd, t => lift * Mathf.Max(0f, Mathf.Cos(two * (t / len + ph)))));
        a.Rot(u.T("ShinR"), new Ax(-u.LegFwd, t => lift * Mathf.Max(0f, Mathf.Cos(two * (t / len + ph + 0.5f)))));
        a.Rot(u.T("Neck"), new Ax(u.TiltFwd, Sin(7f, len, ph + 0.2f, 2)));
        a.Rot(u.T("Head"), new Ax(u.TiltFwd, Sin(-5f, len, ph + 0.2f, 2)));
        a.Rot(u.T("Tail"), new Ax(up, Sin(10f, len, ph + 0.25f, 1)));
    }

    static void BirdGesture(Author a, Unit u, float len, bool peck, float peckDeg)
    {
        Vector3 up = Vector3.up;
        if (peck)
        {
            a.Rot(u.T("Neck"), new Ax(u.TiltFwd, K(0, 0, .35f, 0, .85f, peckDeg, 1.05f, peckDeg * 0.8f, 1.35f, peckDeg, 1.55f, peckDeg * 0.8f, 2.1f, 0, len, 0)));
            a.Rot(u.T("Head"), new Ax(u.TiltFwd, K(0, 0, .35f, 0, .85f, 25f, 1.05f, 15f, 1.35f, 25f, 1.55f, 15f, 2.1f, 0, len, 0)));
            a.Rot(u.T("Hips"), new Ax(u.TiltFwd, K(0, 0, .35f, 0, .85f, 8f, 1.55f, 8f, 2.1f, 0, len, 0)));
            a.Rot(u.T("Tail"), new Ax(u.TiltFwd, K(0, 0, .35f, 0, .85f, -12f, 1.55f, -12f, 2.1f, 0, len, 0)));
        }
        else
        {
            a.Rot(u.T("Neck"), new Ax(up, K(0, 0, .5f, 35f, 1.2f, 35f, 1.8f, -30f, 2.4f, -30f, len, 0)), new Ax(u.TiltFwd, K(0, 0, .5f, -8f, 2.4f, -8f, len, 0)));
            a.Rot(u.T("Head"), new Ax(up, K(0, 0, .5f, 20f, 1.2f, 20f, 1.8f, -20f, 2.4f, -20f, len, 0)));
            a.Rot(u.T("Tail"), new Ax(up, Sin(18f, len, 0f, 3)));
            a.Rot(u.T("Hips"), new Ax(up, K(0, 0, .5f, 6f, 1.2f, 6f, 1.8f, -6f, 2.4f, -6f, len, 0)));
        }
    }

    static void HumIdle(Author a, Unit u, float len)
    {
        Basis b = u.b; Vector3 up = Vector3.up;
        a.Pos(u.T("Hips"), up, Sin(0.004f * b.H, len, 0f, 1));
        a.Rot(u.T("Spine"), new Ax(u.TiltFwd, Sin(1.5f, len, 0f, 1)), new Ax(up, Sin(2f, len, 0.25f, 1)));
        a.Rot(u.T("Head"), new Ax(up, Sin(9f, len, 0.3f, 1)), new Ax(u.TiltFwd, Sin(3f, len, 0f, 2)));
        a.Rot(u.T("ArmL"), new Ax(u.ArmFwdL, Sin(3f, len, 0.1f, 1)), new Ax(u.RollDownL, Const(u.armDown)));
        a.Rot(u.T("ArmR"), new Ax(u.ArmFwdR, Sin(3f, len, 0.6f, 1)), new Ax(u.RollDownR, Const(u.armDown)));
    }

    static void HumWalk(Author a, Unit u, float len)
    {
        Basis b = u.b; Vector3 up = Vector3.up;
        a.Pos(u.T("Hips"), up, Sin(0.012f * b.H, len, 0f, 2));
        a.Rot(u.T("Hips"), new Ax(b.f, Sin(3f, len, 0f, 1)));
        a.Rot(u.T("Spine"), new Ax(up, Sin(5f, len, 0.25f, 1)), new Ax(u.TiltFwd, Const(3f)));
        a.Rot(u.T("Head"), new Ax(up, Sin(-4f, len, 0.25f, 1)), new Ax(u.TiltFwd, Sin(2f, len, 0f, 2)));
        a.Rot(u.T("LegL"), new Ax(u.LegFwd, Sin(26f, len, 0f, 1)));
        a.Rot(u.T("LegR"), new Ax(u.LegFwd, Sin(26f, len, 0.5f, 1)));
        a.Rot(u.T("ArmL"), new Ax(u.ArmFwdL, Sin(18f, len, 0.5f, 1)), new Ax(u.RollDownL, Const(u.armDown)));
        a.Rot(u.T("ArmR"), new Ax(u.ArmFwdR, Sin(18f, len, 0f, 1)), new Ax(u.RollDownR, Const(u.armDown)));
    }

    static void HumGesture(Author a, Unit u, float len, bool wave)
    {
        Vector3 up = Vector3.up;
        float down = u.armDown;
        // base pose identical to idle for the arm that does not gesture
        a.Rot(u.T("ArmL"), new Ax(u.ArmFwdL, Sin(3f, len, 0.1f, 1)), new Ax(u.RollDownL, Const(down)));
        if (wave)
        {
            float raise = u.armsOut ? -30f : -115f;
            Func<float, float> baseRoll = K(0, down, .5f, down, 1.0f, raise, 2.4f, raise, 2.9f, down, len, down);
            Func<float, float> wag = Window(Sin(12f, len, 0f, 4), 1.0f, 2.4f);
            a.Rot(u.T("ArmR"), new Ax(u.RollDownR, Add(baseRoll, wag)));
            a.Rot(u.T("Head"), new Ax(up, K(0, 0, .5f, 0, 1f, -12f, 2.4f, -12f, 2.9f, 0, len, 0)), new Ax(u.TiltFwd, Window(Sin(5f, len, 0f, 3), 1.0f, 2.4f)));
            a.Rot(u.T("Spine"), new Ax(u.TiltFwd, K(0, 0, .5f, 0, 1f, -4f, 2.4f, -4f, 2.9f, 0, len, 0)));
        }
        else
        {
            Func<float, float> fwd = K(0, 0, .5f, 55f, 2.4f, 55f, 2.9f, 0, len, 0);
            Func<float, float> circleA = Window(Sin(14f, len, 0f, 5), 0.6f, 2.5f);
            Func<float, float> circleB = Window(Sin(14f, len, 0.25f, 5), 0.6f, 2.5f);
            a.Rot(u.T("ArmR"), new Ax(u.ArmFwdR, Add(fwd, circleA)), new Ax(up, circleB), new Ax(u.RollDownR, Const(down)));
            a.Rot(u.T("Spine"), new Ax(u.TiltFwd, K(0, 0, .5f, 6f, 2.4f, 6f, 2.9f, 0, len, 0)), new Ax(up, Window(Sin(5f, len, 0f, 5), 0.6f, 2.5f)));
            a.Rot(u.T("Head"), new Ax(u.TiltFwd, K(0, 0, .5f, 10f, 2.4f, 10f, 2.9f, 0, len, 0)));
        }
        a.Pos(u.T("Hips"), up, Sin(0.004f * u.b.H, len, 0f, 1));
    }

    static AnimatorController MakeFeatureController(string name, AnimationClip idle, AnimationClip walk, AnimationClip gesture)
    {
        string path = AnimDir + "/" + name + ".controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
        AnimatorController c = AnimatorController.CreateAnimatorControllerAtPath(path);
        c.AddParameter("isWalking", AnimatorControllerParameterType.Bool);
        c.AddParameter("Gesture", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine sm = c.layers[0].stateMachine;
        AnimatorState sIdle = sm.AddState("Idle"); sIdle.motion = idle;
        AnimatorState sWalk = sm.AddState("Walk"); sWalk.motion = walk;
        AnimatorState sGest = sm.AddState("Gesture"); sGest.motion = gesture;
        sm.defaultState = sIdle;

        AnimatorStateTransition t = sIdle.AddTransition(sWalk);
        t.hasExitTime = false; t.duration = 0.2f; t.AddCondition(AnimatorConditionMode.If, 0, "isWalking");
        t = sWalk.AddTransition(sIdle);
        t.hasExitTime = false; t.duration = 0.2f; t.AddCondition(AnimatorConditionMode.IfNot, 0, "isWalking");
        t = sIdle.AddTransition(sGest);
        t.hasExitTime = false; t.duration = 0.2f; t.AddCondition(AnimatorConditionMode.If, 0, "Gesture");
        t = sGest.AddTransition(sIdle);
        t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0.25f;
        return c;
    }

    // ------------------------------------------------------------------ feature models (ostrich, dino, family, smart, cook)
    public static string BuildFeatureModels()
    {
        log = new StringBuilder();
        EnsureFolder(AnimDir); EnsureFolder(MeshDir);
        string[][] models =
        {
            new[] { "ostrich", "Assets/Art/Chicken/ostrich.fbx" },
            new[] { "dino", "Assets/Art/Chicken/dino.fbx" },
            new[] { "chicken_family", "Assets/Art/Chicken/chicken_family.fbx" },
            new[] { "chicken_smart", "Assets/Art/Chicken/chicken_smart.fbx" },
            new[] { "chicken_cook", "Assets/Art/Chicken/chicken_cook.fbx" },
        };
        foreach (string[] m in models)
        {
            try { BuildModel(m[0], m[1]); }
            catch (Exception e) { L("ERROR " + m[0] + ": " + e.Message + "\n" + e.StackTrace); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return log.ToString();
    }

/// <summary>Facing (world space, as placed in the scene) for models whose forward axis the auto-detection gets wrong.
    /// dino: eyes/horns/mouth sit on the +Z end, the tall rump feathers on the other end - the "highest points" test picked the feathers, so the rig faced backwards.
    /// chicken_family: the grey Icosphere helpers made the detection random; all four birds face -Z.</summary>
    static Vector3? ForwardOverride(string name)
    {
        if (name == "dino") return Vector3.forward;
        if (name == "chicken_family") return Vector3.back;
        return null;
    }

    /// <summary>Re-rigs dino and chicken_family that are already in the scene: rig, skin weights, clips and prefab are regenerated with the right facing.</summary>
    public static string RebuildFacing()
    {
        log = new StringBuilder();
        EnsureFolder(AnimDir); EnsureFolder(MeshDir);
        try { RebuildOne("dino", "Assets/Art/Chicken/dino.fbx", 50f, 0f); }
        catch (Exception e) { L("ERROR dino: " + e.Message + "\n" + e.StackTrace); }
        try { RebuildOne("chicken_family", "Assets/Art/Chicken/chicken_family.fbx", 80f, 0.619f); }
        catch (Exception e) { L("ERROR chicken_family: " + e.Message + "\n" + e.StackTrace); }
        foreach (string stale in new[] { MeshDir + "/chicken_family_A0_Icosphere_rigged.asset", MeshDir + "/chicken_family_A1_Icosphere.001_rigged.asset" })
            if (AssetDatabase.LoadAssetAtPath<Mesh>(stale) != null) AssetDatabase.DeleteAsset(stale);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkAllScenesDirty();
        return log.ToString();
    }

    static void RebuildOne(string name, string fbxPath, float visualScale, float pivotY)
    {
        GameObject cur = GameObject.Find(name);
        if (cur == null) { L(name + ": not in scene"); return; }
        Vector3 pos = cur.transform.position;
        int sib = cur.transform.GetSiblingIndex();
        ChickenPatrol oldPat = cur.GetComponent<ChickenPatrol>();
        float radius = oldPat != null ? oldPat.radius : -1f, speed = oldPat != null ? oldPat.walkSpeed : -1f;

        cur.name = name + "__old";
        GameObject tmp = new GameObject(name);   // stands in for the un-rigged FBX placement that BuildModel expects
        tmp.transform.position = new Vector3(pos.x, pivotY > 0f ? pivotY : pos.y, pos.z);
        tmp.transform.rotation = Quaternion.identity;
        tmp.transform.localScale = Vector3.one * visualScale;

        BuildModel(name, fbxPath);

        UnityEngine.Object.DestroyImmediate(cur);
        GameObject neu = GameObject.Find(name);
        if (neu == null) { L(name + ": rebuild produced no object"); return; }
        neu.transform.SetSiblingIndex(sib);
        if (name == "chicken_family")
        {
            // the family meshes are ~1 cm at import scale and the baked-vertex path ignores the visual scale, so enlarge the rig instead (x80 = ~1.2 m hen); pivot height 0.619 keeps the feet on y=0
            string pp = PrefabDir + "/" + name + ".prefab";
            GameObject pc = PrefabUtility.LoadPrefabContents(pp);
            pc.transform.Find("Armature").localScale = Vector3.one * 80f;
            PrefabUtility.SaveAsPrefabAsset(pc, pp);
            PrefabUtility.UnloadPrefabContents(pc);
        }
        ChickenPatrol np = neu.GetComponent<ChickenPatrol>();
        if (np != null && radius > 0f) { np.radius = radius; np.walkSpeed = speed; }
        L(name + ": rebuilt, root yaw=" + neu.transform.eulerAngles.y);
    }


    static void BuildModel(string name, string fbxPath)
    {
        GameObject old = GameObject.Find(name);
        if (old == null) { L(name + ": scene object not found - skipped"); return; }
        if (old.GetComponent<Animator>() != null) { L(name + ": already has an Animator - skipped (already rigged)"); return; }

        bool bird = name == "ostrich" || name == "dino";
        bool family = name == "chicken_family";
        Vector3 pos = old.transform.position;
        Quaternion rot = old.transform.rotation;
        Vector3 scale = old.transform.localScale;
        int siblingIndex = old.transform.GetSiblingIndex();

        GameObject root = new GameObject("__new_" + name);
        root.transform.position = pos;
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        visual.name = "Model";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.rotation = rot;
        visual.transform.localScale = scale;

        List<Renderer> rends = new List<Renderer>();
        foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
            if (r is MeshRenderer || r is SkinnedMeshRenderer) rends.Add(r);

        // chicken_family: the FBX also holds two stray 2 m grey "Icosphere" helper meshes (the real chickens are ~1 cm at import scale). Drop them.
        List<Renderer> allRends = new List<Renderer>(rends);
        if (family)
        {
            for (int k = rends.Count - 1; k >= 0; k--)
                if (rends[k].name.StartsWith("Icosphere")) { UnityEngine.Object.DestroyImmediate(rends[k].gameObject); rends.RemoveAt(k); }
        }

        // ---- units
        List<Unit> units = new List<Unit>();
        if (family)
        {
            for (int i = 0; i < rends.Count; i++)
            {
                Unit u = new Unit(); u.prefix = "A" + allRends.IndexOf(rends[i]) + "_"; u.rends.Add(Gather(rends[i]));
                u.bp.legTop = 0.34f; u.bp.kneeH = 0.17f; u.bp.headH = 0.72f; u.bp.z0 = 0.5f; u.bp.z1 = 0.62f;
                units.Add(u);
            }
        }
        else
        {
            Unit u = new Unit();
            foreach (Renderer r in rends) u.rends.Add(Gather(r));
            if (name == "ostrich") { u.bp.legTop = 0.46f; u.bp.kneeH = 0.23f; u.bp.headH = 0.86f; u.bp.z0 = 0.58f; u.bp.z1 = 0.72f; }
            if (name == "dino") { u.bp.legTop = 0.40f; u.bp.kneeH = 0.20f; u.bp.headH = 0.82f; u.bp.z0 = 0.50f; u.bp.z1 = 0.65f; }
            units.Add(u);
        }

        // ---- bases (family: every animal uses the facing of the biggest one)
        Vector3? sharedF = null;
        int biggest = 0; float bigH = -1f;
        List<List<Vector3>> unitPts = new List<List<Vector3>>();
        for (int i = 0; i < units.Count; i++)
        {
            List<Vector3> pts = new List<Vector3>();
            foreach (RInfo ri in units[i].rends) pts.AddRange(ri.wv);
            unitPts.Add(pts);
            float maxY = float.MinValue, minY = float.MaxValue;
            foreach (Vector3 p in pts) { maxY = Mathf.Max(maxY, p.y); minY = Mathf.Min(minY, p.y); }
            if (maxY - minY > bigH) { bigH = maxY - minY; biggest = i; }
        }
        Vector3? fixedF = ForwardOverride(name);
        units[biggest].b = MakeBasis(unitPts[biggest], bird || family, fixedF);
        sharedF = units[biggest].b.f;
        for (int i = 0; i < units.Count; i++) if (i != biggest) units[i].b = MakeBasis(unitPts[i], bird || family, fixedF.HasValue ? fixedF : (family ? sharedF : (Vector3?)null));

        Vector3 fwd = units[biggest].b.f;
        float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        visual.transform.rotation = rot;     // keep the original look
        visual.transform.position = pos;

        Transform armature = new GameObject("Armature").transform;
        armature.SetParent(root.transform, false);

        for (int i = 0; i < units.Count; i++)
        {
            Unit u = units[i];
            if (bird || family) CreateBirdBones(u, armature, unitPts[i]); else CreateHumBones(u, armature, unitPts[i]);
            SkinUnit(u, name, bird || family);
        }

        // ---- clips
        float idleLen = family ? 3.2f : (bird ? 3.2f : 3.6f);
        float walkLen = name == "ostrich" ? 1.2f : (name == "dino" ? 1.0f : (family ? 0.7f : 1.0f));
        float gestLen = family ? 2.6f : (bird ? 2.6f : 3.2f);
        Author idle = new Author(name + "_Idle", root.transform, idleLen, true);
        Author walk = new Author(name + "_Walk", root.transform, walkLen, true);
        Author gest = new Author(name + "_Gesture", root.transform, gestLen, false);
        for (int i = 0; i < units.Count; i++)
        {
            Unit u = units[i];
            float ph = family ? (i * 0.27f) % 1f : 0f;
            if (bird || family)
            {
                float swing = name == "ostrich" ? 24f : (name == "dino" ? 26f : 30f);
                float lift = name == "ostrich" ? 38f : (name == "dino" ? 40f : 35f);
                BirdIdle(idle, u, idleLen, ph);
                BirdWalk(walk, u, walkLen, ph, swing, lift);
                BirdGesture(gest, u, gestLen, name != "dino", name == "ostrich" ? 70f : 55f);
            }
            else
            {
                HumIdle(idle, u, idleLen);
                HumWalk(walk, u, walkLen);
                HumGesture(gest, u, gestLen, name == "chicken_smart");
                L(name + ": armsOut=" + u.armsOut + " armDown=" + u.armDown);
            }
        }
        AnimationClip cIdle = idle.Save(AnimDir + "/" + name + "_Idle.anim");
        AnimationClip cWalk = walk.Save(AnimDir + "/" + name + "_Walk.anim");
        AnimationClip cGest = gest.Save(AnimDir + "/" + name + "_Gesture.anim");
        AnimatorController ctrl = MakeFeatureController(name, cIdle, cWalk, cGest);

        // ---- Animator + Avatar
        Avatar av = AvatarBuilder.BuildGenericAvatar(root, "");
        av.name = name + "Avatar";
        string avPath = AnimDir + "/" + name + "Avatar.asset";
        if (AssetDatabase.LoadAssetAtPath<Avatar>(avPath) != null) AssetDatabase.DeleteAsset(avPath);
        AssetDatabase.CreateAsset(av, avPath);
        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = ctrl;
        animator.avatar = av;
        animator.applyRootMotion = false;

        // ---- limited-area patrol (small radius, the models stand in a crowded spot)
        ChickenPatrol pat = root.AddComponent<ChickenPatrol>();
        pat.animator = animator;
        pat.gestureTrigger = "Gesture";
        switch (name)
        {
            case "ostrich": pat.radius = 0.7f; pat.walkSpeed = 0.5f; break;
            case "dino": pat.radius = 0.6f; pat.walkSpeed = 0.5f; break;
            case "chicken_family": pat.radius = 0.3f; pat.walkSpeed = 0.3f; break;
            case "chicken_smart": pat.radius = 0.4f; pat.walkSpeed = 0.4f; break;
            default: pat.radius = 0.3f; pat.walkSpeed = 0.4f; break;
        }

        // ---- save prefab (overwrites the unused featureCharacter prefab, same path) and swap the scene object
        string prefabPath = PrefabDir + "/" + name + ".prefab";
        // existing prefab is overwritten in place below (keeps its GUID)
        GameObject saved = PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
        UnityEngine.Object.DestroyImmediate(old);
        root.name = name;
        root.transform.SetSiblingIndex(siblingIndex);
        L(name + ": rigged. units=" + units.Count + " bones/unit=" + units[0].bones.Length + " forward=" + fwd + " clips=Idle/Walk/Gesture prefab=" + prefabPath);
    }
}
#endif
