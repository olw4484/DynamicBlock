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
            var left  = BuildGridPane();      // 맵 칠하기

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

            var selectModeLbl = new Label("Select Game Mode");
            selectModeLbl.style.fontSize = 20;
            selectModeLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(selectModeLbl);

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

                var lbl = new Label("Tutorial");
                lbl.style.width = 50;
                lbl.style.unityFontStyleAndWeight = FontStyle.Bold;

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

                var lbl = new Label("Score");
                lbl.style.width = 50;
                lbl.style.unityFontStyleAndWeight = FontStyle.Bold;

                panel.Add(row);

                // 토글 ON일 때만 보일 상세(오른쪽에 붙이기)
                scoreDetailRow = Row();
                scoreDetailRow.style.alignItems = Align.Center;
                scoreDetailRow.style.flexGrow = 1;
                scoreDetailRow.style.display = _data.goalKind == MapGoalKind.Score ? DisplayStyle.Flex : DisplayStyle.None;

                var scoreLabel = new Label("Target Score");
                scoreLabel.style.marginLeft = 12;

                var scoreField = new IntegerField("");
                scoreField.value = _data.scoreGoal;
                scoreField.style.width = 75;
                scoreField.label = string.Empty;
                scoreField.labelElement.style.display = DisplayStyle.None;

                scoreField.RegisterValueChangedCallback(e =>
                { });

                scoreDetailRow.Add(scoreLabel);
                SpaceH(scoreDetailRow, 6);
                scoreDetailRow.Add(scoreField);

                SpaceH(row, 6);
                row.Add(scoT);
                SpaceH(row, 8);
                row.Add(lbl);
                SpaceH(row, 6);
                row.Add(scoreDetailRow);

                panel.Add(row);
                SpaceV(panel, 6);
            }

            // ========== 과일 (타일 레이아웃) ==========
            {
                var row = Row();
                fruT = new Toggle();

                var lbl = new Label("Fruit");
                lbl.style.width = 50;
                lbl.style.unityFontStyleAndWeight = FontStyle.Bold;

                SpaceH(row, 6);
                row.Add(fruT);
                SpaceH(row, 8);
                row.Add(lbl);
                panel.Add(row);

                // 활성화 시에만 보일: 타일 리스트
                fruitTable = new VisualElement();
                SpaceV(fruitTable, 4);

                var tilesWrap = Row();
                tilesWrap.style.marginLeft = 8;
                tilesWrap.style.flexShrink = 0;
                tilesWrap.style.overflow = Overflow.Hidden;
                fruitTable.Add(tilesWrap);

                EnsureFruitArrays();

                for (int i = 0; i < FRUIT_COUNT; i++)
                {
                    int idx = i;

                    // 타일 = 버튼
                    var tile = new Button();
                    tile.text = string.Empty;
                    tile.style.flexWrap = Wrap.NoWrap;
                    tile.style.flexShrink = 0;
                    tile.style.overflow = Overflow.Hidden;
                    tile.style.width = 70;
                    tile.style.minHeight = 70;
                    Pad(tile, 8);
                    Border(tile, new Color(0, 0, 0, 0.25f));
                    tile.style.alignItems = Align.Center;
                    tile.style.justifyContent = Justify.Center;
                    tile.style.flexDirection = FlexDirection.Column;
                    tile.style.marginRight = (i == FRUIT_COUNT - 1) ? 0 : 8;
                    tile.style.marginBottom = 8;
                    tile.style.borderTopLeftRadius = 6;
                    tile.style.borderTopRightRadius = 6;
                    tile.style.borderBottomLeftRadius = 6;
                    tile.style.borderBottomRightRadius = 6;

                    // 아이콘
                    var icon = new Image { scaleMode = ScaleMode.ScaleToFit };
                    icon.style.width = 36;
                    icon.style.height = 36;
                    if (_data.fruitImages != null && idx < _data.fruitImages.Length)
                        icon.sprite = _data.fruitImages[idx];
                    tile.Add(icon);
                    SpaceV(tile, 4);

                    // === 목표 갯수 + 스핀버튼 ===
                    var countWrap = Row();
                    countWrap.style.alignItems = Align.Center;

                    var fruitCount = new IntegerField();
                    fruitCount.style.width = 30;
                    fruitCount.isDelayed = true;
                    fruitCount.SetValueWithoutNotify(_data.fruitGoals[idx]);

                    // 스핀 버튼(위/아래)
                    var spin = new VisualElement();
                    spin.style.flexDirection = FlexDirection.Column;
                    spin.style.marginLeft = 4;

                    var upBtn = new Button { text = "▲" };
                    upBtn.style.width = 18; upBtn.style.height = 14;
                    upBtn.style.paddingLeft = upBtn.style.paddingRight =
                    upBtn.style.paddingTop = upBtn.style.paddingBottom = 0;

                    var dnBtn = new Button { text = "▼" };
                    dnBtn.style.width = 18; dnBtn.style.height = 14;
                    dnBtn.style.paddingLeft = dnBtn.style.paddingRight =
                    dnBtn.style.paddingTop = dnBtn.style.paddingBottom = 0;

                    void Bump(int delta)
                    {
                        Undo.RecordObject(_data, "Change Fruit Goal");
                        EnsureFruitArrays();
                        var v = Mathf.Max(0, fruitCount.value + delta);
                        fruitCount.SetValueWithoutNotify(v);
                        _data.fruitGoals[idx] = v;
                        EditorUtility.SetDirty(_data);
                    }

                    upBtn.clicked += () => Bump(+1);
                    dnBtn.clicked += () => Bump(-1);

                    // 타일 토글로 이벤트 전파 막기
                    upBtn.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                    dnBtn.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                    fruitCount.RegisterCallback<ClickEvent>(e => e.StopPropagation());

                    // (선택) 휠로 증감
                    fruitCount.RegisterCallback<WheelEvent>(e =>
                    {
                        if (!_data.fruitEnabled[idx]) return;
                        Bump(e.delta.y > 0 ? -1 : +1);
                        e.StopPropagation();
                    });

                    spin.Add(upBtn);
                    spin.Add(dnBtn);
                    countWrap.Add(fruitCount);
                    countWrap.Add(spin);

                    // 활성 상태 반영
                    bool enabledNow = _data.fruitEnabled[idx];
                    fruitCount.SetEnabled(enabledNow);
                    upBtn.SetEnabled(enabledNow);
                    dnBtn.SetEnabled(enabledNow);

                    tile.Add(countWrap);

                    // 타일 클릭 = 활성/비활성 토글
                    tile.clicked += () =>
                    {
                        Undo.RecordObject(_data, "Toggle Fruit Enable");
                        EnsureFruitArrays();
                        _data.fruitEnabled[idx] = !_data.fruitEnabled[idx];
                        EditorUtility.SetDirty(_data);

                        bool on = _data.fruitEnabled[idx];
                        fruitCount.SetEnabled(on);
                        upBtn.SetEnabled(on);
                        dnBtn.SetEnabled(on);
                        SetFruitTileVisual(tile, on);
                    };

                    // 처음 비주얼
                    SetFruitTileVisual(tile, _data.fruitEnabled[idx]);

                    // ApplyGoalUI에서 on/off 제어용 참조
                    fruitCounts[idx] = fruitCount;

                    tilesWrap.Add(tile);
                }


                panel.Add(fruitTable);
                SpaceV(panel, 7);
            }

            // ========== 블록 선택 ==========
            var selectBlockLbl = new Label("Select Block");
            selectBlockLbl.style.fontSize = 20;
            selectBlockLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(selectBlockLbl);
            SpaceV(panel, 4);

            var palette = new VisualElement();
            palette.style.backgroundColor = new Color(1, 1, 1, 0.4f);
            palette.style.alignItems = Align.FlexStart;
            Pad(palette, 6);

            int blockSize = 50;
            int blockOffset = 5;

            var line2 = Row();
            line2.style.flexWrap = Wrap.NoWrap;
            line2.style.flexShrink = 0;
            line2.style.overflow = Overflow.Hidden;

            for (int i = 0; i < FRUIT_COUNT; i++)
            {
                int index = i;

                var button = new Button();
                button.style.width = blockSize;
                button.style.height = blockSize;
                button.style.alignItems = Align.Center;
                button.style.justifyContent = Justify.Center;
                Pad(button, 0f);
                button.text = string.Empty;

                var image = new Image { scaleMode = ScaleMode.ScaleToFit };
                image.style.width = blockSize - blockOffset;
                image.style.height = blockSize - blockOffset;
                image.sprite = _data.blockImages?[i];
                button.Add(image);

                button.clicked += () => SetBrushSprite(_data.blockImages?[index], button);

                // 기본 테두리(원복용 기준)
                Highlight(button, false);

                // 이미 선택된 브러시가 있다면, 재생성 시 하이라이트 복구
                if (_brushSprite != null && _brushSprite == _data.blockImages?[index])
                    Highlight(button, true);

                line2.Add(button);
            }

            var line3 = Row();
            line3.style.flexWrap = Wrap.NoWrap;      // <-- 오타 수정(line2 -> line3)
            line3.style.flexShrink = 0;
            line3.style.overflow = Overflow.Hidden;

            for (int i = 0; i < FRUIT_COUNT; i++)
            {
                int index = i;

                var button = new Button();
                button.style.width = blockSize;
                button.style.height = blockSize;
                button.style.alignItems = Align.Center;
                button.style.justifyContent = Justify.Center;
                Pad(button, 0f);
                button.text = string.Empty;

                var image = new Image { scaleMode = ScaleMode.ScaleToFit };
                image.style.width = blockSize - blockOffset;
                image.style.height = blockSize - blockOffset;
                image.sprite = _data.blockWithFruitIcons?[i];
                button.Add(image);

                button.clicked += () => SetBrushSprite(_data.blockWithFruitIcons?[index], button);

                Highlight(button, false);
                if (_brushSprite != null && _brushSprite == _data.blockWithFruitIcons?[index])
                    Highlight(button, true);

                line3.Add(button);
            }
            palette.Add(line2);
            palette.Add(line3);
            panel.Add(palette);
            SpaceV(panel, 8f);

            var paintGridLbl = new Label("Paint Grid");
            paintGridLbl.style.fontSize = 20;
            paintGridLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(paintGridLbl);

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
        
        private static void SetFruitTileVisual(Button tile, bool enabled)
        {
            if (tile == null) return;

            var cBorder = enabled ? new Color(0.21f, 0.52f, 0.96f, 1f) : new Color(0, 0, 0, 0.25f);
            var bw = enabled ? 3 : 1;

            tile.style.borderLeftWidth   = bw;
            tile.style.borderRightWidth  = bw;
            tile.style.borderTopWidth    = bw;
            tile.style.borderBottomWidth = bw;

            tile.style.borderLeftColor   = cBorder;
            tile.style.borderRightColor  = cBorder;
            tile.style.borderTopColor    = cBorder;
            tile.style.borderBottomColor = cBorder;

            tile.style.backgroundColor = enabled ? new Color(1, 1, 1, 0.10f) : new Color(0, 0, 0, 0);
            tile.style.opacity = enabled ? 1f : 0.6f; // 비활성 느낌
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
            int n = Math.Min(data.fruitEnabled?.Length ?? 0, fruitCounts?.Length ?? 0);
            for (int i = 0; i < n; i++)
            {
                var intField = fruitCounts[i];
                intField?.SetEnabled(
                    data.goalKind == MapGoalKind.Fruit &&
                    i < data.fruitEnabled.Length &&
                    data.fruitEnabled[i]
                );
            }
        }

        // ===== 왼쪽 패널: 맵 칠하기 공간 (단순 그리드) =====
        private VisualElement BuildGridPane()
        {
            var wrap = new VisualElement { name = "grid-wrap" };
            wrap.style.flexGrow = 0;
            wrap.style.flexShrink = 0;
            Border(wrap, new Color(0, 0, 0, 0.25f)); // 이미 있는 헬퍼
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
                grid.style.alignItems = Align.FlexStart;
                int rows = Mathf.Max(1, _data.rows);
                int cols = Mathf.Max(1, _data.cols);
                const int cell = 40; // 셀 한 변 픽셀 수

                for (int r = 0; r < rows; r++)
                {
                    var line = Row();
                    line.style.marginBottom = (r == rows - 1) ? 0 : 2;

                    for (int c = 0; c < cols; c++)
                    {
                        var ve = new VisualElement();
                        ve.style.width = cell;
                        ve.style.height = cell;
                        ve.style.backgroundColor = new Color(0, 0, 0, 0.08f);
                        BorderThin(ve, new Color(0, 0, 0, 0.2f));
                        ve.style.marginRight = (c == cols - 1) ? 0 : 2;

                        // 셀 내부 이미지 (처음엔 비어 있음)
                        var img = new Image { scaleMode = ScaleMode.ScaleToFit };
                        img.style.width = cell - 2;
                        img.style.height = cell - 2;
                        ve.Add(img);

                        // 클릭하면 현재 브러시 스프라이트로 변경 (지우개는 null)
                        ve.RegisterCallback<ClickEvent>(_ =>
                        {
                            // _brushSprite == null 이면 지우개 동작
                            if (_brushSprite == null || img.sprite == _brushSprite)
                            {
                                img.sprite = null;           // 지우기
                            }
                            else
                            {
                                img.sprite = _brushSprite;   // 현재 선택한 블록으로 칠하기
                            }
                        });

                        line.Add(ve);
                    }

                    grid.Add(line);
                }
            }
        }

        private Sprite _brushSprite; // 현재 선택된 블록의 스프라이트(지우개는 null)
        private Button _selectedPaletteButton;

        private void SetBrushSprite(Sprite sprite, Button sourceBtn)
        {
            // 이전 선택 원복
            Highlight(_selectedPaletteButton, false);

            _brushSprite = sprite;
            _selectedPaletteButton = sourceBtn;

            // 새 선택 강조
            Highlight(_selectedPaletteButton, true);
        }

        private void Highlight(Button btn, bool on)
        {
            if (btn == null) return;

            var normal = new Color(0, 0, 0, 0.25f);
            var active = new Color(0.21f, 0.52f, 0.96f, 1f); // 파란 테두리

            var bw = on ? 3 : 1;
            var bc = on ? active : normal;

            btn.style.borderLeftWidth = bw;
            btn.style.borderRightWidth = bw;
            btn.style.borderTopWidth = bw;
            btn.style.borderBottomWidth = bw;

            btn.style.borderLeftColor = bc;
            btn.style.borderRightColor = bc;
            btn.style.borderTopColor = bc;
            btn.style.borderBottomColor = bc;

            // 살짝 배경 강조 (원하면 제거)
            btn.style.backgroundColor = on ? new Color(1, 1, 1, 0.10f) : new Color(0, 0, 0, 0);
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
        /// <summary>
        /// 세로로 일정 높이 여백 부여
        /// </summary>
        /// <param name="parent">추가할 위치</param>
        /// <param name="h">세로 높이</param>
        private static void SpaceV(VisualElement parent, float h) => parent.Add(new VisualElement { style = { height = h } });
        /// <summary>
        /// 가로로 일정 너비 여백 부여
        /// </summary>
        /// <param name="parent">추가할 위치</param>
        /// <param name="w">가로 너비</param>
        private static void SpaceH(VisualElement parent, float w) => parent.Add(new VisualElement { style = { width = w } });
        private static void Pad(VisualElement ve, float all)
        {
            ve.style.paddingLeft = all; ve.style.paddingRight = all;
            ve.style.paddingTop = all; ve.style.paddingBottom = all;
        }
        private static void Border(VisualElement ve, Color c)
        {
            ve.style.borderLeftWidth = 1; ve.style.borderLeftColor = c;
            ve.style.borderRightWidth = 1; ve.style.borderRightColor = c;
            ve.style.borderTopWidth = 1; ve.style.borderTopColor = c;
            ve.style.borderBottomWidth = 1; ve.style.borderBottomColor = c;
        }
        private static void BorderThin(VisualElement ve, Color c) => Border(ve, c);
    }
}
#endif
