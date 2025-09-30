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

        var baseMat = text.fontSharedMaterial;                 // (Instance �ƴ�!)
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
                // ������ ��Ʋ��/���̴��� �����ϴ� �� ��Ƽ����
                var m = new Material(fa.material);
                // ���� ��Ʋ�� ���
                var fallbackTex = fa.material.GetTexture(ShaderUtilities.ID_MainTex);

                // �ʿ��� ��Ÿ�� �Ӽ��� ���� (��Ʋ��/�׶���Ʈ������ ���� �ǵ帮�� �ʱ�)
                CopySdfStyle(baseMat, m);

                // ���� ��Ʋ�� ����
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
        // �ʿ��� �͸� ��� ����
        dst.SetFloat(ShaderUtilities.ID_FaceDilate, src.GetFloat(ShaderUtilities.ID_FaceDilate));
        dst.SetColor(ShaderUtilities.ID_FaceColor, src.GetColor(ShaderUtilities.ID_FaceColor));
        dst.SetFloat(ShaderUtilities.ID_OutlineWidth, src.GetFloat(ShaderUtilities.ID_OutlineWidth));
        dst.SetFloat(ShaderUtilities.ID_OutlineSoftness, src.GetFloat(ShaderUtilities.ID_OutlineSoftness));
        dst.SetColor(ShaderUtilities.ID_OutlineColor, src.GetColor(ShaderUtilities.ID_OutlineColor));
        // �ʿ� �� Underlay �� �߰�
    }
}
#endif
