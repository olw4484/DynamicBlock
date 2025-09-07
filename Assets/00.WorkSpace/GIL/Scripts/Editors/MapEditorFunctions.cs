#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using _00.WorkSpace.GIL.Scripts.Maps;

namespace _00.WorkSpace.GIL.Scripts.Editors
{
    /// <summary>
    /// MapEditor의 비-UI 기능 모음집
    /// </summary>
    public static class MapEditorFunctions
    {
        private const string MapsFolder = "Assets/00.WorkSpace/GIL/Resources/Maps";
        private const int FruitCount = 5;
        
        private const int StartIndex  = 0;
        
        private const string StagePrefix = "Stage_";
        private static string StageName(int index) => $"{StagePrefix}{index}";


        // 공통
        public static void MarkDirty(UnityEngine.Object obj, string undoName = null)
        {
            if (!string.IsNullOrEmpty(undoName)) Undo.RecordObject(obj, undoName);
            EditorUtility.SetDirty(obj);
        }

        /// <summary> "Assets/00.WorkSpace/GIL/Resources/Maps" 까지 단계별 폴더 생성 </summary>
        public static void EnsureFolderChain(string fullPath)
        {
            var parts = fullPath.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{cur}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        public static void EnsureMapsFolder() => EnsureFolderChain(MapsFolder);
        public static string GetMapsFolderPath() => MapsFolder;

        public static void EnsureLayoutSize(MapData data)
        {
            int rows = Mathf.Max(1, data.rows);
            int cols = Mathf.Max(1, data.cols);
            int total = rows * cols;

            if (data.layout == null) data.layout = new List<int>(total);

            if (data.layout.Count != total)
            {
                if (data.layout.Count < total) data.layout.AddRange(new int[total - data.layout.Count]);
                else data.layout.RemoveRange(total, data.layout.Count - total);
            }
        }

        public static void ClearLayout(MapData data)
        {
            EnsureLayoutSize(data);
            MarkDirty(data, "Clear Layout");
            for (int i = 0; i < data.layout.Count; i++) data.layout[i] = 0;
        }

        public static void EnsureFruitArrays(MapData data)
        {
            if (data.fruitEnabled == null || data.fruitEnabled.Length < FruitCount)
            {
                var arr = new bool[FruitCount];
                if (data.fruitEnabled != null)
                    Array.Copy(data.fruitEnabled, arr, Math.Min(data.fruitEnabled.Length, arr.Length));
                data.fruitEnabled = arr;
            }
            if (data.fruitGoals == null || data.fruitGoals.Length < FruitCount)
            {
                var arr = new int[FruitCount];
                if (data.fruitGoals != null)
                    Array.Copy(data.fruitGoals, arr, Math.Min(data.fruitGoals.Length, arr.Length));
                data.fruitGoals = arr;
            }
        }

        // Query/Load
        public static List<MapData> FindAllMapsSortedInFolder()
        {
            EnsureMapsFolder();
            var guids = AssetDatabase.FindAssets("t:MapData", new[] { MapsFolder });
            return guids
                .Select(g => AssetDatabase.LoadAssetAtPath<MapData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m != null)
                .OrderBy(m => m.mapIndex).ThenBy(m => m.name)
                .ToList();
        }

        // Reindex (Stage 0..N), 0번은 튜토리얼
        /// <summary>
        /// 모든 맵을 0..N 순번으로 재배열하고, 파일명/MapData.id 를 "Stage_{N}"으로 동기화
        /// TMP 임시 이동 후 최종 경로로 MoveAsset 하므로 충돌/잔여를 방지.
        /// </summary>
        public static void ReindexAllSequential(List<MapData> mapsArg = null)
        {
            EnsureMapsFolder();

            var maps = (mapsArg != null && mapsArg.Count > 0)
                ? mapsArg.Where(m => m != null).ToList()
                : FindAllMapsSortedInFolder();

            // 최신화
            foreach (var m in maps)
            {
                var p = AssetDatabase.GetAssetPath(m);
                if (!string.IsNullOrEmpty(p))
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 모두 TMP 이름으로 일시 변경(충돌 회피)
            foreach (var m in maps)
            {
                string path = AssetDatabase.GetAssetPath(m);
                if (string.IsNullOrEmpty(path)) continue;

                string tmpBase = $"TMP{Guid.NewGuid():N}";
                string err = AssetDatabase.RenameAsset(path, tmpBase);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 최종 이름: Stage_0, Stage_1, ...
            for (int i = 0; i < maps.Count; i++)
            {
                var m = maps[i];
                if (m == null) continue;
                
                int newIndex    = StartIndex + i;         
                string curPath  = AssetDatabase.GetAssetPath(m);
                string folder   = System.IO.Path.GetDirectoryName(curPath)?.Replace('\\','/');
                string finalName= StageName(newIndex);
                string finalPath= $"{folder}/{finalName}.asset";

                // 같은 이름 잔여 제거
                var existing = AssetDatabase.LoadAssetAtPath<MapData>(finalPath);
                if (existing != null && existing != m)
                    AssetDatabase.DeleteAsset(finalPath);

                // 파일명 변경
                string err2 = AssetDatabase.RenameAsset(curPath, finalName);
                if (!string.IsNullOrEmpty(err2))
                    Debug.LogError($"Final rename failed: {curPath} -> {finalName}\n{err2}");

                // 데이터 동기화
                m.mapIndex = newIndex;
                m.id       = finalName;
                MarkDirty(m);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }




        // Navigation 
        public static MapData NavigateClamped(MapData current, int delta, bool setSelection = true)
        {
            var maps = FindAllMapsSortedInFolder();
            if (maps.Count == 0) return null;

            int i = maps.IndexOf(current);
            if (i < 0) i = 0;

            int ni = Mathf.Clamp(i + delta, 0, maps.Count - 1);
            var target = maps[ni];

            if (setSelection && target != null)
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }
            return target;
        }

        // Add
        public static MapData AddAfterCurrent(MapData current)
        {
            EnsureMapsFolder();

            // 새 에셋 임시 생성
            var asset = ScriptableObject.CreateInstance<MapData>();
            if (current != null) { asset.rows = current.rows; asset.cols = current.cols; }
            EnsureLayoutSize(asset);

            string tempPath = AssetDatabase.GenerateUniqueAssetPath(
                System.IO.Path.Combine(GetMapsFolderPath(), "NEW.asset"));
            AssetDatabase.CreateAsset(asset, tempPath);
            AssetDatabase.SaveAssets(); // 생성만

            // 현 목록 취득 후 '새 자산'은 일단 제외
            var maps = FindAllMapsSortedInFolder();
            maps.Remove(asset);

            // 현재 뒤에 끼워넣을 위치 계산
            int insertPos = 0;
            if (current != null)
            {
                int curIdx = maps.IndexOf(current);
                insertPos = (curIdx < 0) ? maps.Count : curIdx + 1;
            }
            if (insertPos > maps.Count) insertPos = maps.Count;

            // 뒤쪽만 한 칸씩 밀기
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int j = maps.Count - 1; j >= insertPos; j--)
                {
                    var m = maps[j];
                    if (m == null) continue;

                    int newIndex = StartIndex + j + 1;
                    string newName = StageName(newIndex);

                    string path = AssetDatabase.GetAssetPath(m);
                    AssetDatabase.RenameAsset(path, newName);

                    m.mapIndex = newIndex;
                    m.id = newName;
                    EditorUtility.SetDirty(m);
                }

                // 새 에셋 최종 이름 부여
                int finalIndex = StartIndex + insertPos;
                string finalName = StageName(finalIndex);
                AssetDatabase.RenameAsset(tempPath, finalName);

                asset.mapIndex = finalIndex;
                asset.id = finalName;
                EditorUtility.SetDirty(asset);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return asset;
        }
        
        public static void EnsureIcons(MapData data)
        {
            // 비어있으면 Resources에서 로드
            if (data.fruitImages == null || data.fruitImages.Length == 0)
                data.fruitImages = Resources.LoadAll<Sprite>("FruitIcons");
            if (data.blockImages == null || data.blockImages.Length == 0)
                data.blockImages = Resources.LoadAll<Sprite>("BlockImages");
            if (data.blockWithFruitIcons == null || data.blockWithFruitIcons.Length == 0)
                data.blockWithFruitIcons = Resources.LoadAll<Sprite>("BlockWithFruitImages");
        }
        
        // Delete
        public static void DeleteCurrent(MapData current)
        {
            if (current == null) return;

            var maps = FindAllMapsSortedInFolder();
            int i = maps.IndexOf(current);
            if (i < 0) return;

            // 삭제
            string path = AssetDatabase.GetAssetPath(current);
            AssetDatabase.DeleteAsset(path);

            // 뒤에 것들만 앞으로 한 칸씩 당기기
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int j = i + 1; j < maps.Count; j++)
                {
                    var m = maps[j];
                    if (m == null) continue;

                    int newIndex = StartIndex + (j - 1);
                    string newName = StageName(newIndex);

                    string p = AssetDatabase.GetAssetPath(m);
                    AssetDatabase.RenameAsset(p, newName);

                    m.mapIndex = newIndex;
                    m.id = newName;
                    EditorUtility.SetDirty(m);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // 선택 이동
            var newList = FindAllMapsSortedInFolder();
            if (newList.Count > 0)
            {
                int sel = Mathf.Clamp(i, 0, newList.Count - 1);
                Selection.activeObject = newList[sel];
                EditorGUIUtility.PingObject(newList[sel]);
            }
            else
            {
                Selection.activeObject = null;
            }
        }

        // Save 
        public static void Save(MapData data)
        {
            MarkDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(data);
        }

        // Fruit helpers
        public static void BumpFruitGoal(MapData data, int idx, int delta)
        {
            EnsureFruitArrays(data);
            if (idx < 0 || idx >= data?.fruitGoals?.Length) return;
            data.fruitGoals[idx] = Mathf.Max(0, data.fruitGoals[idx] + delta);
            MarkDirty(data, "Change Fruit Goal");
        }

        public static void SetFruitGoal(MapData data, int idx, int value)
        {
            EnsureFruitArrays(data);
            if (idx < 0 || idx >= data?.fruitGoals?.Length) return;
            data.fruitGoals[idx] = Mathf.Max(0, value);
            MarkDirty(data, "Edit Fruit Goal");
        }

        public static void ToggleFruitEnable(MapData data, int idx)
        {
            EnsureFruitArrays(data);
            if (idx < 0 || idx >= data?.fruitEnabled?.Length) return;
            data.fruitEnabled[idx] = !data.fruitEnabled[idx];
            MarkDirty(data, "Toggle Fruit Enable");
        }

        // Paint helpers 
        /// <summary> 같은 스프라이트를 다시 칠하면 null로 지우기(토글 효과). </summary>
        public static Sprite PaintToggle(Sprite brush, Sprite current) =>
            (brush == null || brush == current) ? null : brush;

        // Menu: 재정렬/잔여 정리
        [MenuItem("Tools/Maps/Reindex (Fix TMP and Order)")]
        public static void Menu_ReindexFix()
        {
            ReindexAllSequential();
            Debug.Log("Maps reindexed and cleaned.");
        }
    }
}
#endif
