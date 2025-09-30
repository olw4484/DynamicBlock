#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TMPFontBatchReplacer : EditorWindow
{
    TMP_FontAsset targetFont;

    [MenuItem("Tools/TMP/Replace All TMP Fonts...")]
    static void Open() => GetWindow<TMPFontBatchReplacer>("TMP Font Replacer");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Replace every TMP_Text.font in Prefabs + Scenes", EditorStyles.boldLabel);
        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Target TMP Font", targetFont, typeof(TMP_FontAsset), false);

        if (GUILayout.Button("Replace in Project"))
        {
            if (!targetFont)
            {
                EditorUtility.DisplayDialog("Assign Font", "Target TMP_FontAsset is missing.", "OK");
                return;
            }
            ReplaceEverywhere(targetFont);
        }
    }

    static void ReplaceEverywhere(TMP_FontAsset font)
    {
        int changed = 0;

        // 1) Prefabs
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            bool dirty = false;

            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t && t.font != font)
                {
                    t.font = font;
                    EditorUtility.SetDirty(t);
                    dirty = true; changed++;
                }
            }
            if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 2) Scenes
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
        string currentPath = SceneManager.GetActiveScene().path;

        foreach (var guid in sceneGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            // 씬 내부 오브젝트만 (프로젝트 자산 제외)
            foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>()
                     .Where(x => x && !AssetDatabase.Contains(x) && x.gameObject.scene == scene))
            {
                if (t.font != font)
                {
                    t.font = font;
                    EditorUtility.SetDirty(t);
                    changed++;
                }
            }
            EditorSceneManager.SaveScene(scene);
        }

        //// 3) TMP Settings 기본 폰트도 맞춰주기(선택)
        //var settings = TMP_Settings.instance;
        //if (settings != null)
        //{
        //    if (settings.defaultFontAsset != font)
        //    {
        //        settings.defaultFontAsset = font;
        //        EditorUtility.SetDirty(settings);
        //    }
        //    if (settings.fallbackFontAssets == null ||
        //        !settings.fallbackFontAssets.Contains(font))
        //    {
        //        settings.fallbackFontAssets.Insert(0, font);
        //        EditorUtility.SetDirty(settings);
        //    }
        //}

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Done",
            $"Replaced {changed} TMP_Text components.\nDefault/Fallback updated.", "OK");
        Debug.Log($"[TMPFontBatchReplacer] Replaced {changed} components to {font.name}");
    }
}
#endif
