using System;
using System.Collections.Generic;
using System.Linq;
using _00.WorkSpace.GIL.Scripts.Maps;
using TMPro;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct StageLayout
{
    public int stageCount;             // 이 층에 몇 개?
    public TextAnchor alignment;       // 왼/가운데/오른쪽 정렬
}

[Serializable]
public struct IndexRangeSprite
{
    [Min(0)] public int stageStart;  // 색칠 시작 시점, 0부터 시작
    public int stageEnd;    // 색칠 완료 시점
    public Sprite sprite;   // 적용할 이미지 
}
public class StageList : MonoBehaviour
{
    [Header("References")]
    [Tooltip("VerticalLayoutGroup 붙은 부모(콘텐츠)")]
    [SerializeField] private RectTransform verticalRoot;
    [Tooltip("HorizontalLayoutGroup 붙은 Floor 프리팹")]
    [SerializeField] private GameObject floorPrefab;         // 
    [Tooltip("각 층마다 들어갈 Prefab : 스테이지 진입 버튼")]
    [SerializeField] private GameObject stageButtonPrefab;   // 각 아이템(버튼/이미지) 프리팹
    [SerializeField] private List<MapData> maps; // Resources/Maps 폴더에서 직접 가져오기
    [SerializeField] private string mapsResourcesPath = "Maps"; // Resources/Maps 디렉토리 명
    
    [Header("Layout Settings")]

    [Tooltip("층 사이 간격, horizontalSpacing과 동일하게 하나 커스텀 가능")]
    [SerializeField, Min(0)] private float verticalSpacing = 8f;
    [Tooltip("층 내부 간격, verticalSpacing과 동일하게 하나 커스텀 가능")]
    [SerializeField, Min(0)] private float horizontalSpacing = 8f;

    [Tooltip("각 스테이지 입장 버튼의 크기, 0일 경우 프리팹 크기 사용")]
    [SerializeField, Min(0)] private float buttonSize;

    [Header("Stage Layout Info")]
    [Tooltip("각 층마다 레이아웃 위치 선정")]
    [SerializeField] private List<StageLayout> stageLayouts = new();
    [SerializeField] private List<IndexRangeSprite> indexRangeSprites = new();
    public List<GameObject> stageButtons = new();


    public void Rebuild()
    {
        // 0) 현재 maps에 MapData가 들어가있는지 확인, 없을 경우 바로 추가
        if (maps == null || maps.Count == 0)
        {
            maps = Resources.LoadAll<MapData>(mapsResourcesPath)
                    .OrderBy(m => m.mapIndex)
                    .ToList();
        }

        if (!verticalRoot || floorPrefab == null || stageButtonPrefab == null)
        {
            Debug.LogError("[StageListGenerator] Assign references first.");
            return;
        }

        // 1) 기존 자식 제거, 기존 stageButtons 목록 초기화
        for (int i = verticalRoot.childCount - 1; i >= 0; i--)
            DestroyImmediate(verticalRoot.GetChild(i).gameObject);

        stageButtons.Clear();

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
            if (!rowLE) rowLE = row.AddComponent<LayoutElement>();
            // 추가 설정이 없을 경우 stageButtonPrefab의 크기를 따라감
            rowLE.preferredHeight = buttonSize < 0 ? stageButtonPrefab.GetComponent<RectTransform>().rect.height : buttonSize;

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
                // 추가 설정이 없을 경우 stageButtonPrefab의 크기를 따라감
                itemLE.preferredWidth = buttonSize < 0 ? stageButtonPrefab.GetComponent<RectTransform>().rect.width : buttonSize;
                itemLE.preferredHeight = buttonSize < 0 ? stageButtonPrefab.GetComponent<RectTransform>().rect.height : buttonSize;
                // 5) 생성된 아이템들은 전체 리스트에 넣기
                stageButtons.Add(item);
            }
        }

        // 6) 생성된 스테이지 진입 버튼들의 이름 재정렬
        var count = GetStageCount();
        RenameStageButtons(stageButtons, count);

        // 7) 생성된 스테이지 진입 버튼들의 진입 스테이지 재정렬
        SetStageButtonEnterNumber(stageButtons, count);

        // 8) 각각 블럭을 startIndex~endIndex까지 sprite로 색칠하기
        ApplyClearSpritesByIndex(stageButtons, indexRangeSprites);

        // 9) 1번 스테이지는 대해 플레이 가능하게, 250919_GIL : 지금은 StageManager에서 처리
        // stageButtons[0].GetComponent<EnterStageButton>().SetButtonState(ButtonState.Playable);

        // 10) 각각 블록의 맵 데이터를 받아 PanelSwitchOnClick의 targetPanel을 교체
        ApplyTargetPanelsByMapGoal();

        // End) 완료 로그 출력
        Debug.Log("[StageListGenerator] 스테이지 레이아웃 생성 완료!");
    }
    // 6) 전체 스테이지 갯수 반환
    private int GetStageCount()
    {
        var result = 0;
        foreach (StageLayout stageLayout in stageLayouts)
        {
            result += stageLayout.stageCount;
        }
        return result;
    }
    // 6) 각각 스테이지 번호를 Stage_번호 로 변환
    private void RenameStageButtons(List<GameObject> list, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            list[i - 1].name = $"Stage_{i}";
            // 옵션) 숫자 텍스트가 있다면 표시, 없으니 지움
            // 250919_GIL : 디버그용 스테이지 번호 표시 기능이 있는게 좋을 듯
            // 스테이지 번호가 플레이 가능, 잠긴 스테이지에 대해 출력되게 함
            var txt = list[i - 1].GetComponentInChildren<TextMeshProUGUI>();
            txt.text = $"{i}";
            // 지금은 활성화, 필요 시 StageManager에서 변경
            // SetState에 의해 자동으로 바뀌게 될 것
            txt.enabled = true;
        }
    }
    // 7) 각 스테이지 진입 번호를 설정
    private void SetStageButtonEnterNumber(List<GameObject> list, int count)
    {
        for (int i = 0; i < count; i++)
            list[i].GetComponent<EnterStageButton>().SetStageNumber(i + 1);
    }
    // 8) 스테이지 클리어 시 버튼을 리스트 안의 정보에 따라 색칠함
    private void ApplyClearSpritesByIndex(List<GameObject> stageButtons, List<IndexRangeSprite> list)
    {
        foreach (var item in list)
        {
            for (int i = item.stageStart; i <= item.stageEnd; i++)
            {
                stageButtons[i].GetComponent<EnterStageButton>().SetClearSprite(item.sprite);
            }
        }
    }
    /// <summary>
    /// MapData의 과일 정보에 따른 해당 버튼의 targetPanel 변경
    /// </summary>
    private void ApplyTargetPanelsByMapGoal()
    {
        // 1) stageButtons 리스트에 할당된 스테이지 번호와 맵 goalKind를 매칭
        for (int i = 0; i < stageButtons.Count; i++)
        {
            var btn = stageButtons[i].GetComponent<EnterStageButton>();
            if (!btn) continue;

            int stageNumber = btn.GetStageNumber();        // SetStageButtonEnterNumber에서 1-based로 셋팅됨
            int mapArrayIndex = stageNumber;          // MapManager.EnterStage에서 _mapList[stageNumber]를 사용 중
                                                      // (0번은 튜토리얼/디폴트로 쓰는 구조라 1부터 스테이지로 보는 패턴)

            // 방어: 인덱스 범위 체크
            if (mapArrayIndex < 0 || mapArrayIndex >= maps.Count) continue;

            var map = maps[mapArrayIndex];
            var goal = map.goalKind; // Score or Fruit

            // 2) 버튼의 타겟 패널 설정
            // PanelSwitchOnClick 사용하기
            stageButtons[i].GetComponent<PanelSwitchOnClick>()
                .SetTargetPanel(goal == MapGoalKind.Score ? "Score" : "Fruit");
            // PanelToggleOnClick 사용하기, 토글을 위에 추가하는 방식으로 우선 구현
            //stageButtons[i].GetComponent<PanelToggleOnClick>().SetKey(goal == MapGoalKind.Score ? "Score" : "Fruit");
        }
    }
}

/// <summary>
/// Stage Layout을 재생성하는 Editor의 Tool
/// </summary>
[CustomEditor(typeof(StageList))]
public class StageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        StageList stageList = (StageList)target;

        GUILayout.Space(10);
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Create Stage"))
        {
            stageList.Rebuild();
        }
        GUILayout.EndHorizontal();
    }
}
