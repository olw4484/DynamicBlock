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
        if (!text || !text.font || !text.fontSharedMaterial)
        { Debug.LogWarning("Select a TMP_Text with a shared material (no Instance)."); return; }

        var baseMat = text.fontSharedMaterial;                 // (Instance 아님!)
        string presetName = baseMat.name;

        var q = new Queue<TMP_FontAsset>();
        var seen = new HashSet<TMP_FontAsset>();
        q.Enqueue(text.font);

        while (q.Count > 0)
        {
            var fa = q.Dequeue();
            if (!fa || !seen.Add(fa)) continue;

            string path = AssetDatabase.GetAssetPath(fa);
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            Material found = null;
            foreach (var a in assets)
                if (a is Material m && m.name == presetName) { found = m; break; }

            if (!found)
            {
                // 폴백의 아틀라스/셰이더를 유지하는 새 머티리얼
                var m = new Material(fa.material);
                // 폴백 아틀라스 백업
                var fallbackTex = fa.material.GetTexture(ShaderUtilities.ID_MainTex);

                // 필요한 스타일 속성만 복사 (아틀라스/그라디언트스케일 등은 건드리지 않기)
                CopySdfStyle(baseMat, m);

                // 폴백 아틀라스 복구
                m.SetTexture(ShaderUtilities.ID_MainTex, fallbackTex);

                m.name = presetName;
                AssetDatabase.AddObjectToAsset(m, path);
                EditorUtility.SetDirty(m);
                Debug.Log($"[TMP] Created preset '{presetName}' on {fa.name}");
            }

            var list = fa.fallbackFontAssetTable;
            if (list != null) foreach (var f in list) q.Enqueue(f);
        }
        AssetDatabase.SaveAssets();
    }

    static void CopySdfStyle(Material src, Material dst)
    {
        // 필요한 것만 골라 복사
        dst.SetFloat(ShaderUtilities.ID_FaceDilate, src.GetFloat(ShaderUtilities.ID_FaceDilate));
        dst.SetColor(ShaderUtilities.ID_FaceColor, src.GetColor(ShaderUtilities.ID_FaceColor));
        dst.SetFloat(ShaderUtilities.ID_OutlineWidth, src.GetFloat(ShaderUtilities.ID_OutlineWidth));
        dst.SetFloat(ShaderUtilities.ID_OutlineSoftness, src.GetFloat(ShaderUtilities.ID_OutlineSoftness));
        dst.SetColor(ShaderUtilities.ID_OutlineColor, src.GetColor(ShaderUtilities.ID_OutlineColor));
        // 필요 시 Underlay 등 추가
    }
}
#endif
