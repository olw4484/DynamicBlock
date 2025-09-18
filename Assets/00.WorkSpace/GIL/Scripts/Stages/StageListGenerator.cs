using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct StageLayout
{
    public int stageCount;             // 이 층에 몇 개?
    public TextAnchor alignment;       // 왼/가운데/오른쪽 정렬
}

public class StageListGenerator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("VerticalLayoutGroup 붙은 부모(콘텐츠)")]
    [SerializeField] private RectTransform verticalRoot;
    [Tooltip("HorizontalLayoutGroup 붙은 Floor 프리팹")]
    [SerializeField] private GameObject floorPrefab;         // 
    [Tooltip("각 층마다 들어갈 Prefab : 스테이지 진입 버튼")]
    [SerializeField] private GameObject stageButtonPrefab;   // 각 아이템(버튼/이미지) 프리팹

    [Header("Layout Settings")]
    
    [Tooltip("층 사이 간격, horizontalSpacing과 동일하게 하나 커스텀 가능")]
    [SerializeField] private float verticalSpacing = 8f;
    [Tooltip("층 내부 간격, verticalSpacing과 동일하게 하나 커스텀 가능")]
    [SerializeField] private float horizontalSpacing = 8f;

    [Header("Stage Layout Info")]
    [Tooltip("각 층마다 레이아웃 위치 선정")]
    [SerializeField] private List<StageLayout> stageLayouts = new(); 
    public readonly List<GameObject> StageButtons = new();
    
    public void Rebuild()
    {
        if (!verticalRoot || floorPrefab == null || stageButtonPrefab == null)
        {
            Debug.LogError("[StageListGenerator] Assign references first.");
            return;
        }
        
        // 1) 기존 자식 제거, 기존 stageButtons 목록 초기화
        for (int i = verticalRoot.childCount - 1; i >= 0; i--)
            DestroyImmediate(verticalRoot.GetChild(i).gameObject);
        
        StageButtons.Clear();
        
        // 2) VerticalGroup Settings 동기화
        var vlgSetting = verticalRoot.GetComponent<VerticalLayoutGroup>();
        if (vlgSetting)
        {
            vlgSetting.spacing = verticalSpacing;
            vlgSetting.childAlignment = TextAnchor.UpperCenter;
            vlgSetting.reverseArrangement = true;
            
            vlgSetting.childControlWidth = true;
            vlgSetting.childControlHeight = true;
            
            vlgSetting.childForceExpandWidth = true;
            vlgSetting.childForceExpandHeight = false;
        }
        
        // 3) 층마다 Row 생성 + 정렬 반영 + 아이템 생성
        for (int floor = 0; floor < stageLayouts.Count; floor++)
        {
            var layout = stageLayouts[floor];

            // Row 생성
            var row = Instantiate(floorPrefab, verticalRoot);
            row.name = $"Floor_{floor + 1}";
            var rowRT = row.transform as RectTransform;
            
            // Row 높이 ( Preferred Height ) 보장
            var rowLE = row.GetComponent<LayoutElement>();
            if(!rowLE) rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = stageButtonPrefab.GetComponent<RectTransform>().rect.height;
            
            // Row의 HorizontalLayoutGroup 세팅
            var hlgSetting = row.GetComponent<HorizontalLayoutGroup>();
            if (hlgSetting)
            {
                hlgSetting.spacing = horizontalSpacing;
                hlgSetting.childAlignment = layout.alignment; // 왼/가운데/오른쪽
                
                hlgSetting.childControlWidth = true;
                hlgSetting.childControlHeight = true;
                
                hlgSetting.childForceExpandWidth = false;     // 아이템 너비를 퍼뜨릴지 여부(원하면 true)
                hlgSetting.childForceExpandHeight = false;
            }
            // 4) 이 층에 StageItem 생성
            for (int i = 0; i < Mathf.Max(0, layout.stageCount); i++)
            {
                var item = Instantiate(stageButtonPrefab, rowRT);
                
                // 버튼 크기 보장
                var itemLE = item.GetComponent<LayoutElement>();
                if (!itemLE) itemLE = item.AddComponent<LayoutElement>();
                itemLE.preferredHeight = stageButtonPrefab.GetComponent<RectTransform>().rect.height;
                
                // 5) 생성된 아이템들은 전체 리스트에 넣기
                StageButtons.Add(item);
            }
        }
        
        // 6) 생성된 스테이지 진입 버튼들의 이름 재정렬
        var count = GetStageCount();
        RenameStageButtons(StageButtons, count);
        
        // 7) 생성된 스테이지 진입 버튼들의 진입 스테이지 재정렬
        SetStageButtonEnterNumber(StageButtons, count);
        
        // 8) 완료 로그 출력
        Debug.Log("[StageListGenerator] 스테이지 레이아웃 생성 완료!");
    }
    
    private int GetStageCount()
    {
        var result = 0;
        foreach (StageLayout stageLayout in stageLayouts)
        {
            result += stageLayout.stageCount;
        }
        return result;
    }

    private void RenameStageButtons(List<GameObject> list, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            list[i-1].name = $"Stage_{i}";
            // 옵션) 숫자 텍스트가 있다면 표시
            var txt = list[i-1].GetComponentInChildren<TextMeshProUGUI>();
            txt.text = $"{i}";
        }
    }
    
    private void SetStageButtonEnterNumber(List<GameObject> list, int count)
    {
        for (int i = 0; i < count; i++)
            list[i].GetComponent<EnterStageButton>().SetStageNumber(i+1);
    }
    
}

/// <summary>
/// Stage Layout을 재생성하는 Editor의 Tool
/// </summary>
[CustomEditor(typeof(StageListGenerator))]
public class StageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        StageListGenerator stageListGenerator = (StageListGenerator)target;

        GUILayout.Space(10);
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Create Stage"))
        {
            stageListGenerator.Rebuild();
        }
        GUILayout.EndHorizontal();
    }
}
