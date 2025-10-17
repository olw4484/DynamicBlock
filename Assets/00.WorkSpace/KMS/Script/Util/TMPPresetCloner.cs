#if UNITY_EDITOR
using UnityEditor;
using TMPro;
using UnityEngine;
using System.Collections.Generic;

public static class TMPPresetCloner
{
    [MenuItem("Tools/TMP/Clone Current Material Preset To Fallbacks (Selected TMP)")]
    static void CloneToFallbacks()
    {
        var text = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<TMP_Text>() : null;
        if (!text || !text.font) { Debug.LogWarning("Select a TMP_Text with a valid TMP_FontAsset."); return; }

        // ���� ������ ����, ������ �ν��Ͻ� ���
        var baseMat = text.fontSharedMaterial != null ? text.fontSharedMaterial : text.fontMaterial;
        if (!baseMat) { Debug.LogWarning("No material on selected TMP_Text."); return; }

        string presetName = baseMat.name.Replace(" (Instance)", string.Empty);

        var q = new Queue<TMP_FontAsset>();
        var seen = new HashSet<TMP_FontAsset>();
        q.Enqueue(text.font);

        var boundMap = new Dictionary<TMP_FontAsset, Material>(); // ��Ʈ���� ������ �⺻���� ����� ���

        while (q.Count > 0)
        {
            var fa = q.Dequeue();
            if (!fa || !seen.Add(fa)) continue;

            string path = AssetDatabase.GetAssetPath(fa);
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);

            Material preset = null;
            foreach (var a in assets)
                if (a is Material m && m.name == presetName) { preset = m; break; }

            if (!preset)
            {
                var m = new Material(fa.material); // ������ ���̴�/��Ʋ�� ����
                var atlas = fa.material.GetTexture(ShaderUtilities.ID_MainTex);

                CopySdfStyleWide(baseMat, m);
                m.SetTexture(ShaderUtilities.ID_MainTex, atlas);
                m.name = presetName;

                AssetDatabase.AddObjectToAsset(m, path);
                EditorUtility.SetDirty(m);
                preset = m;
                Debug.Log($"[TMP] Created preset '{presetName}' on {fa.name}");
            }

            // �ٽ�: ���� ��Ʈ�� �⺻ ��Ƽ������ ���������� �ٲ��ش�
            if (fa.material != preset)
            {
                fa.material = preset;
                EditorUtility.SetDirty(fa);
                boundMap[fa] = preset;
            }

            // ���� ü�� ��ȸ
            var list = fa.fallbackFontAssetTable;
            if (list != null) foreach (var f in list) q.Enqueue(f);
        }

        AssetDatabase.SaveAssets();

        // ��/�����տ� �̹� �����ϴ� ����޽��� ��Ƽ���� ���������� ����ȭ
        RebindSubmeshMaterials(boundMap);

        // ���� ������Ʈ ȭ�� ��� �ݿ�
        TMPOutlineUtil.ApplyOutlineToAllMaterials(text, baseMat);
    }

    static void RebindSubmeshMaterials(Dictionary<TMP_FontAsset, Material> bound)
    {
        if (bound == null || bound.Count == 0) return;

        // �� �� ��� TMP_SubMeshUI / TMP_SubMesh �Ⱦ, �ش� FontAsset�� �⺻ ��Ƽ����� ��ü
        foreach (var sm in Object.FindObjectsOfType<TMP_SubMeshUI>(true))
        {
            if (sm.fontAsset && bound.TryGetValue(sm.fontAsset, out var mat))
            {
                if (sm.sharedMaterial != mat) { sm.sharedMaterial = mat; EditorUtility.SetDirty(sm); }
            }
        }
        foreach (var sm in Object.FindObjectsOfType<TMP_SubMesh>(true))
        {
            if (sm.fontAsset && bound.TryGetValue(sm.fontAsset, out var mat))
            {
                if (sm.sharedMaterial != mat) { sm.sharedMaterial = mat; EditorUtility.SetDirty(sm); }
            }
        }
    }

    static void CopySdfStyleWide(Material src, Material dst)
    {
        void CInt(int idF, int idC)
        {
            if (dst.HasProperty(idF) && src.HasProperty(idF)) dst.SetFloat(idF, src.GetFloat(idF));
            if (dst.HasProperty(idC) && src.HasProperty(idC)) dst.SetColor(idC, src.GetColor(idC));
        }
        if (dst.HasProperty(ShaderUtilities.ID_FaceDilate) && src.HasProperty(ShaderUtilities.ID_FaceDilate))
            dst.SetFloat(ShaderUtilities.ID_FaceDilate, src.GetFloat(ShaderUtilities.ID_FaceDilate));
        if (dst.HasProperty(ShaderUtilities.ID_FaceColor) && src.HasProperty(ShaderUtilities.ID_FaceColor))
            dst.SetColor(ShaderUtilities.ID_FaceColor, src.GetColor(ShaderUtilities.ID_FaceColor));
        if (dst.HasProperty(ShaderUtilities.ID_OutlineWidth) && src.HasProperty(ShaderUtilities.ID_OutlineWidth))
            dst.SetFloat(ShaderUtilities.ID_OutlineWidth, src.GetFloat(ShaderUtilities.ID_OutlineWidth));
        if (dst.HasProperty(ShaderUtilities.ID_OutlineSoftness) && src.HasProperty(ShaderUtilities.ID_OutlineSoftness))
            dst.SetFloat(ShaderUtilities.ID_OutlineSoftness, src.GetFloat(ShaderUtilities.ID_OutlineSoftness));
        if (dst.HasProperty(ShaderUtilities.ID_OutlineColor) && src.HasProperty(ShaderUtilities.ID_OutlineColor))
            dst.SetColor(ShaderUtilities.ID_OutlineColor, src.GetColor(ShaderUtilities.ID_OutlineColor));

        // Underlay / Glow
        CInt(ShaderUtilities.ID_UnderlayOffsetX, 0);
        CInt(ShaderUtilities.ID_UnderlayOffsetY, 0);
        CInt(ShaderUtilities.ID_UnderlayDilate, 0);
        CInt(ShaderUtilities.ID_UnderlaySoftness, 0);
        if (dst.HasProperty(ShaderUtilities.ID_UnderlayColor) && src.HasProperty(ShaderUtilities.ID_UnderlayColor))
            dst.SetColor(ShaderUtilities.ID_UnderlayColor, src.GetColor(ShaderUtilities.ID_UnderlayColor));
        if (dst.HasProperty(ShaderUtilities.ID_GlowColor) && src.HasProperty(ShaderUtilities.ID_GlowColor))
            dst.SetColor(ShaderUtilities.ID_GlowColor, src.GetColor(ShaderUtilities.ID_GlowColor));
        if (dst.HasProperty(ShaderUtilities.ID_GlowPower) && src.HasProperty(ShaderUtilities.ID_GlowPower))
            dst.SetFloat(ShaderUtilities.ID_GlowPower, src.GetFloat(ShaderUtilities.ID_GlowPower));
    }
}
#endif
