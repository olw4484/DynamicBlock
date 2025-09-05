#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _00.WorkSpace.GIL.Scripts.Maps;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _00.WorkSpace.GIL.Scripts.Editors
{
    [CustomEditor(typeof(MapData))]
    public class MapEditor : Editor
    {
        private MapData _data;
        private int _brush; // 현재 브러시 ID
        private const int FRUIT_COUNT = 5;
        
        public override VisualElement CreateInspectorGUI()
        {
            _data = (MapData)target;
            return BuildUI();
        }

        // ===== 메인 UI (인스펙터 전용) =====
        private VisualElement BuildUI()
        {
            var root = new VisualElement { name = "map-editor-root" };
            Pad(root, 6);

            // 스테이지 이름
            var idField = new TextField { value = _data.id };
            idField.style.unityTextAlign = TextAnchor.MiddleCenter;
            idField.style.fontSize = 25;
            idField.style.unityFontStyleAndWeight = FontStyle.Bold;
            idField.RegisterValueChangedCallback(e => { });
            root.Add(idField);
            SpaceV(root, 6);

            // 네비게이션 바
            var nav = Row();
            nav.Add(Button("<", () => { }));
            nav.Add(Button(">", () => { }));
            nav.Add(Button("Add", AddNew));
            nav.Add(Button("Save", Save));
            nav.Add(Button("Delete", Delete));
            root.Add(nav);
            SpaceV(root, 6);

            // 본문(세로 배치): 우측패널 → 그리드
            var container = new VisualElement();
            root.Add(container);

            var right = BuildSettingPane();   // 목표/팔레트 등
            var left  = BuildGridPane();    // 맵 칠하기

            container.Add(right);
            SpaceV(container, 8);
            container.Add(left);

            return root;

            void Save()
            {
                EditorUtility.SetDirty(_data);
                AssetDatabase.SaveAssets();
            }

            void AddNew() { }

            void Delete()
            {
                if (EditorUtility.DisplayDialog("Delete Stage",
                    $"Delete '{_data.id}'?\n(This cannot be undone)", "Delete", "Cancel"))
                { }
            }
        }

        private VisualElement BuildSettingPane()
        {
            var panel = new VisualElement { name = "settings-pane" };
            panel.style.minWidth = 280;

            // ----- 참조들 미리 선언 -----
            Toggle tutT = null, scoT = null, fruT = null;

            // 점수 상세(목표 점수)와 과일 표를 모아두는 컨테이너
            VisualElement scoreDetailRow = null;
            VisualElement fruitTable     = null;

            // 과일 토글/카운트 배열
            var fruitToggles = new Toggle[FRUIT_COUNT + 1];       // 1..N
            var fruitCounts  = new IntegerField[FRUIT_COUNT + 1]; // 1..N

            // 중복 갱신 방지 플래그
            bool isUpdating = false;

            void EnsureFruitArrays()
            {
                if (_data.fruitEnabled == null || _data.fruitEnabled.Length < FRUIT_COUNT + 1)
                {
                    var arr = new bool[FRUIT_COUNT + 1];
                    if (_data.fruitEnabled != null)
                        Array.Copy(_data.fruitEnabled, arr, Math.Min(_data.fruitEnabled.Length, arr.Length));
                    _data.fruitEnabled = arr;
                }
                if (_data.fruitGoals == null || _data.fruitGoals.Length < FRUIT_COUNT + 1)
                {
                    var arr = new int[FRUIT_COUNT + 1];
                    if (_data.fruitGoals != null)
                        Array.Copy(_data.fruitGoals, arr, Math.Min(_data.fruitGoals.Length, arr.Length));
                    _data.fruitGoals = arr;
                }
            }
            
            panel.Add(new Label("Select Game Mode") { style = { fontSize = 20, unityFontStyleAndWeight = FontStyle.Bold } });
            
            void SetGoal(MapGoalKind kind)
            {
                if (isUpdating) return;
                isUpdating = true;
                Undo.RecordObject(_data, "Change Goal");
                _data.goalKind = kind;
                EditorUtility.SetDirty(_data);
                ApplyGoalUI(_data, tutT, scoT, fruT, scoreDetailRow, fruitTable, fruitToggles, fruitCounts);
                isUpdating = false;
            }

            // ========== 튜토리얼 ==========
            {
                var row = Row();
                tutT = new Toggle(); // 라벨 없는 체크박스
                var lbl = new Label("Tutorial") {style = { width = 50,unityFontStyleAndWeight = FontStyle.Bold }};
                SpaceH(row, 6); 
                row.Add(tutT);
                SpaceH(row, 8); 
                row.Add(lbl); 
                panel.Add(row);
                SpaceV(panel, 6);
            }

            // ========== 점수 - 목표 점수 ==========
            {
                var row = Row();
                scoT = new Toggle();
                var lbl = new Label("Score") {style = { width = 50, unityFontStyleAndWeight = FontStyle.Bold }};
                row.Add(scoT);
                row.Add(lbl); 
                panel.Add(row);

                // 토글 ON일 때만 보일 상세(오른쪽에 붙이기)
                scoreDetailRow = Row();
                scoreDetailRow.style.alignItems = Align.Center;
                scoreDetailRow.style.flexGrow = 1;                         // 남은 가로영역 차지
                scoreDetailRow.style.display = _data.goalKind == MapGoalKind.Score ? DisplayStyle.Flex : DisplayStyle.None;

                var scoreLabel = new Label("Target Score") { style = { marginLeft = 12 }};

                var scoreField = new IntegerField("") { value = _data.scoreGoal, style = { width = 75 } };
                scoreField.label = string.Empty;
                scoreField.labelElement.style.display = DisplayStyle.None;

                scoreField.RegisterValueChangedCallback(e =>
                { });

                scoreDetailRow.Add(scoreLabel);
                SpaceH(scoreDetailRow, 6);
                scoreDetailRow.Add(scoreField);

                SpaceH(row, 6);
                row.Add(lbl);
                SpaceH(row, 8);
                row.Add(scoT);
                SpaceH(row, 6);
                row.Add(scoreDetailRow);

                panel.Add(row);
                SpaceV(panel, 6);
            }

            // ========== 과일 (타일 레이아웃) ==========
            {
                var row = Row();
                fruT = new Toggle();
                var lbl = new Label("Fruit") {style = { width = 50, unityFontStyleAndWeight = FontStyle.Bold }};
                SpaceH(row, 6); 
                row.Add(fruT);
                SpaceH(row, 8); 
                row.Add(lbl); 
                panel.Add(row);

                // 활성화 시에만 보일: 타일 리스트
                fruitTable = new VisualElement(); // 기존 fruitTable 변수 재사용 (섹션 컨테이너)
                SpaceV(fruitTable, 4);

                var tilesWrap = RowWrap(); // 가로 배치 + 줄바꿈
                tilesWrap.style.marginLeft = 8;
                fruitTable.Add(tilesWrap);

                EnsureFruitArrays();

                for (int i = 0; i < FRUIT_COUNT; i++)
                {
                    // 타일 컨테이너(네모)
                    var tile = new VisualElement();
                    tile.style.width  = 70;
                    tile.style.minHeight = 70;
                    Pad(tile, 8);
                    Border(tile, new Color(0,0,0,0.25f));
                    tile.style.alignItems = Align.Center;     // 세로 정렬 시 자식 가로 중앙
                    tile.style.flexDirection = FlexDirection.Column; // 세로로 쌓기
                    tile.style.marginRight = 8;
                    tile.style.marginBottom = 8;
                    // 둥그렇게 만들기
                    tile.style.borderTopLeftRadius = 6; 
                    tile.style.borderTopRightRadius = 6;
                    tile.style.borderBottomLeftRadius = 6; 
                    tile.style.borderBottomRightRadius = 6;

                    var fruitIcon = new Image { scaleMode = ScaleMode.ScaleToFit };
                    fruitIcon.style.width  = 36;
                    fruitIcon.style.height = 36;
                    Sprite spr = null;
                    if (_data.fruitImages != null && i < _data.fruitImages.Length)
                        spr = _data.fruitImages[i];
                    fruitIcon.sprite = spr;
                    SpaceV(tile, 4);

                    var fruitToggle = new Toggle();
                    fruitToggle.SetValueWithoutNotify(_data.fruitEnabled[i]);

                    var fruitCount = new IntegerField() { style = { width = 50 } };
                    fruitCount.SetValueWithoutNotify(_data.fruitGoals[i]);
                    fruitCount.SetEnabled(_data.fruitEnabled[i]); // 활성일 때만 입력 가능

                    int idx = i;
                    fruitToggle.RegisterValueChangedCallback(e =>
                    {
                        Undo.RecordObject(_data, "Toggle Fruit Enable");
                        EnsureFruitArrays();
                        _data.fruitEnabled[idx] = e.newValue;
                        EditorUtility.SetDirty(_data);

                        fruitCount.SetEnabled(e.newValue); // 즉시 반영
                        ApplyGoalUI(_data, tutT, scoT, fruT, scoreDetailRow, fruitTable, fruitToggles, fruitCounts);
                    });

                    fruitCount.RegisterValueChangedCallback(e =>
                    {
                        Undo.RecordObject(_data, "Edit Fruit Goal");
                        EnsureFruitArrays();
                        _data.fruitGoals[idx] = Mathf.Max(0, e.newValue);
                        EditorUtility.SetDirty(_data);
                    });

                    fruitToggles[i] = fruitToggle;
                    fruitCounts[i]  = fruitCount;

                    // 타일에 추가(세로 중앙 정렬)
                    tile.Add(fruitIcon);
                    SpaceV(tile, 4);
                    tile.Add(fruitToggle);
                    SpaceV(tile, 4);
                    tile.Add(fruitCount);

                    tilesWrap.Add(tile);
                }

                panel.Add(fruitTable);
                SpaceV(panel, 10);
            }

            // ========== 블록 선택 ==========
            panel.Add(new Label("Select Block") { style = { fontSize = 20, unityFontStyleAndWeight = FontStyle.Bold } });
            SpaceV(panel, 4);

            var palette = new VisualElement();
            palette.style.backgroundColor = new Color(1, 1, 1, 0.4f);
            palette.style.alignItems = Align.Center;
            Pad(palette, 6);

            int blockSize = 60;
            var line2 = Row();
            for (int i = 0; i < FRUIT_COUNT; i++)
            {
                var button = new Button(() => { }) { style =
                {
                    width = blockSize, 
                    height = blockSize, 
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }};
                Pad(button, 0f);
                button.text = string.Empty;

                var image = new Image { scaleMode = ScaleMode.ScaleToFit };
                image.style.width = blockSize;
                image.style.height = blockSize;
                image.sprite = _data.blockImages?[i];
                button.Add(image);
                line2.Add(button);
            }
            var line3 = Row();
            for (int i = 0; i < FRUIT_COUNT; i++)
            {
                var button = new Button(() => { }) { style =
                {
                    width = blockSize, 
                    height = blockSize,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }};
                Pad(button, 0f);
                button.text = string.Empty;

                var image = new Image { scaleMode = ScaleMode.ScaleToFit };
                image.style.width = blockSize;
                image.style.height = blockSize;
                image.sprite = _data.blockWithFruitIcons?[i];
                button.Add(image);
                line3.Add(button);
            }
            palette.Add(line2);
            palette.Add(line3);
            panel.Add(palette);

            // ---- 라디오 토글처럼 동작하도록 초기화 + 콜백 ----
            tutT.SetValueWithoutNotify(_data.goalKind == MapGoalKind.Tutorial);
            scoT.SetValueWithoutNotify(_data.goalKind == MapGoalKind.Score);
            fruT.SetValueWithoutNotify(_data.goalKind == MapGoalKind.Fruit);

            tutT.RegisterValueChangedCallback(e =>
            {
                if (isUpdating) return;
                if (e.newValue) SetGoal(MapGoalKind.Tutorial);
                else if (_data.goalKind == MapGoalKind.Tutorial) tutT.SetValueWithoutNotify(true);
            });
            scoT.RegisterValueChangedCallback(e =>
            {
                if (isUpdating) return;
                if (e.newValue) SetGoal(MapGoalKind.Score);
                else if (_data.goalKind == MapGoalKind.Score) scoT.SetValueWithoutNotify(true);
            });
            fruT.RegisterValueChangedCallback(e =>
            {
                if (isUpdating) return;
                if (e.newValue) SetGoal(MapGoalKind.Fruit);
                else if (_data.goalKind == MapGoalKind.Fruit) fruT.SetValueWithoutNotify(true);
            });

            // ---- 최초 1회 상태 적용 ----
            ApplyGoalUI(_data, tutT, scoT, fruT, scoreDetailRow, fruitTable, fruitToggles, fruitCounts);
            return panel;
        }

        private static void ApplyGoalUI(
            MapData data,
            Toggle tutT, Toggle scoT, Toggle fruT,
            VisualElement scoreDetailRow,
            VisualElement fruitTable,
            Toggle[] fruitToggles,
            IntegerField[] fruitCounts)
        {
            bool isTut   = data.goalKind == MapGoalKind.Tutorial;
            bool isScore = data.goalKind == MapGoalKind.Score;
            bool isFruit = data.goalKind == MapGoalKind.Fruit;

            // 라디오 상태 반영(알림 없이)
            tutT?.SetValueWithoutNotify(isTut);
            scoT?.SetValueWithoutNotify(isScore);
            fruT?.SetValueWithoutNotify(isFruit);

            // 표시/비표시
            if (scoreDetailRow != null)
                scoreDetailRow.style.display = isScore ? DisplayStyle.Flex : DisplayStyle.None;

            if (fruitTable != null)
                fruitTable.style.display = isFruit ? DisplayStyle.Flex : DisplayStyle.None;

            // 과일 표 내부 활성/비활성
            int n = (fruitToggles?.Length ?? 1) - 1;
            for (int i = 1; i <= n; i++)
            {
                var toggle = fruitToggles?[i];
                var intFields = fruitCounts[i];
                // Fruit 모드일 때만 편집 가능
                toggle?.SetEnabled(isFruit && !isTut);
                intFields?.SetEnabled(isFruit && !isTut && i < data.fruitEnabled.Length && data.fruitEnabled[i]);
            }
        }


        // ===== 왼쪽 패널: 맵 칠하기 공간 (단순 그리드) =====
        private VisualElement BuildGridPane()
        {
            var wrap = new VisualElement { name = "grid-wrap" };
            wrap.style.flexGrow = 1;
            Border(wrap, new Color(0,0,0,0.25f)); // 이미 있는 헬퍼
            Pad(wrap, 6);

            var grid = new VisualElement { name = "grid" };
            wrap.Add(grid);

            // (선택) 아래 여백
            SpaceV(wrap, 6);

            // (선택) 리빌드 버튼
            var tools = Row();
            wrap.Add(tools);

            // 처음 한 번 생성
            BuildGrid();
            return wrap;

            // ---- 내부: rows×cols 네모칸 그리기 ----
            void BuildGrid()
            {
                grid.Clear();

                int rows = Mathf.Max(1, _data.rows);
                int cols = Mathf.Max(1, _data.cols);
                const int cell = 22; // 셀 한 변 픽셀 수 (원하면 24~28로 키워도 됨)

                for (int r = 0; r < rows; r++)
                {
                    var line = Row();
                    line.style.marginBottom = (r == rows - 1) ? 0 : 2;

                    for (int c = 0; c < cols; c++)
                    {
                        var ve = new VisualElement();
                        ve.style.width  = cell;
                        ve.style.height = cell;
                        ve.style.backgroundColor = new Color(0, 0, 0, 0.08f); // 연한 회색
                        BorderThin(ve, new Color(0, 0, 0, 0.2f));            // 테두리
                        ve.style.marginRight = (c == cols - 1) ? 0 : 2;

                        line.Add(ve);
                    }

                    grid.Add(line);
                }
            }
        }


        // ---- 스타일 & UI 생성 ----
        private static Button Button(string text, Action onClick)
        {
            var b = new Button(() => onClick?.Invoke()) { text = text };
            b.style.minWidth = 56;
            return b;
        }
        private static VisualElement Row()
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            return r;
        }
        private static VisualElement RowWrap()
        {
            var r = Row();
            r.style.flexWrap = Wrap.Wrap;
            return r;
        }
        private static void SpaceV(VisualElement parent, float h) => parent.Add(new VisualElement { style = { height = h } });
        private static void SpaceH(VisualElement parent, float w) => parent.Add(new VisualElement { style = { width = w } });
        private static void Pad(VisualElement ve, float all)
        {
            ve.style.paddingLeft = all; ve.style.paddingRight = all;
            ve.style.paddingTop  = all; ve.style.paddingBottom = all;
        }
        private static void Border(VisualElement ve, Color c)
        {
            ve.style.borderLeftWidth   = 1; ve.style.borderLeftColor   = c;
            ve.style.borderRightWidth  = 1; ve.style.borderRightColor  = c;
            ve.style.borderTopWidth    = 1; ve.style.borderTopColor    = c;
            ve.style.borderBottomWidth = 1; ve.style.borderBottomColor = c;
        }
        private static void BorderThin(VisualElement ve, Color c) => Border(ve, c);
    }
}
#endif
