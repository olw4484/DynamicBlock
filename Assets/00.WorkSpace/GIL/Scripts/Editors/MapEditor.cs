#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using _00.WorkSpace.GIL.Scripts.Maps;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _00.WorkSpace.GIL.Scripts.Editors
{
    [CustomEditor(typeof(MapData))]
    public class MapEditor : Editor
    {
        private MapData _data;
        private int _brush; // 현재 브러시 ID
        private const int FruitCount = 5;
        private Dictionary<Sprite, int> _spriteToIndex; // sprite -> code
        private Dictionary<int, Sprite> _codeToSprite; // code -> sprite
        private Sprite _brushSprite; // 현재 선택된 블록의 스프라이트(지우개는 null)
        private Button _selectedPaletteButton;
        private bool _isDragging;
        private int _dragValue;
        private int _cellSize = 47; // 셀 한 변 픽셀 수
        private Action _rebuildGrid;
        private static readonly Regex s_CodeRegex =
            new(@"^\s*(\d+)(?=_)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        
        public override VisualElement CreateInspectorGUI()
        {
            _data = (MapData)target;
            BuildSpriteIndex();
            
            // 아이콘 로드/사이즈 보정
            MapEditorFunctions.EnsureIcons(_data);
            
            BuildSpriteIndex();
            
#if UNITY_EDITOR
            // 파일명 기준으로 id/index 동기화(메뉴 생성 직후 케이스 대응)
            var path = AssetDatabase.GetAssetPath(_data);
            if (!string.IsNullOrEmpty(path))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                const string prefix = "Stage_";
                if (name.StartsWith(prefix) && int.TryParse(name.Substring(prefix.Length), out var idx))
                {
                    if (_data.id != name || _data.mapIndex != idx)
                    {
                        _data.id = name;
                        _data.mapIndex = idx;
                        EditorUtility.SetDirty(_data);
                    }
                }
            }
#endif
            return BuildUI();
        }

        // ===== 메인 UI (인스펙터 전용) =====
        private VisualElement BuildUI()
        {
            var root = new VisualElement { name = "map-editor-root" };
            Pad(root, 6);
            
            // 레이아웃 컨테이너
            var container = new VisualElement();
            container.style.alignItems = Align.FlexStart; // 위쪽 정렬
            root.Add(container);

            // 패널들
            var left  = BuildGridPane();           // 그리드
            var right = BuildSettingPane();        // 셋팅

            // 기본 배치 (가로)
            container.style.flexDirection = FlexDirection.Row;
            left.style.marginRight = 8;
            container.Add(left);
            container.Add(right);
            left.style.flexGrow = 0;    
            left.style.flexShrink = 0;
            right.style.flexGrow = 1;   // 설정 패널은 남는 폭 사용
            right.style.minWidth = 280; // 설정 최소폭

            // 반응형 전환
            const float Breakpoint = 700f;
            bool isWide = true;

            void ApplyLayout(float width)
            {
                bool wide = width >= Breakpoint;
                if (wide == isWide) return;
                isWide = wide;

                if (wide)
                {
                    _cellSize = 60;
                    container.style.flexDirection = FlexDirection.Row;
                    container.Clear();
                    left.style.marginRight = 8;
                    left.style.marginBottom = 0;
                    container.Add(left);
                    container.Add(right);
                }
                else
                {
                    _cellSize = 47;
                    container.style.flexDirection = FlexDirection.Column;
                    container.Clear();
                    left.style.marginRight  = 0;
                    left.style.marginBottom = 8;
                    container.Add(right);
                    container.Add(left);
                }
                
                _rebuildGrid?.Invoke();
            }

            // 폭 변화를 감지해서 적용
            container.RegisterCallback<GeometryChangedEvent>(e =>
            {
                ApplyLayout(e.newRect.width);
            });

            // 첫 프레임 강제 적용
            root.schedule.Execute(() =>
            {
                float w = container.resolvedStyle.width > 0
                    ? container.resolvedStyle.width
                    : root.resolvedStyle.width;
                ApplyLayout(w);
            }).StartingIn(0);

            return root;
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
            VisualElement fruitBlocksRow = null;
            
            // 과일 카운트 참조(ApplyGoalUI에서 on/off)
            var fruitCounts  = new IntegerField[FruitCount + 1];

            // 중복 갱신 방지 플래그
            bool isUpdating = false;
            
            // TODO : 여기로 설정부분 옮기기
            // 스테이지 이름
            var idField = new TextField { value = _data.id };
            idField.style.unityTextAlign = TextAnchor.MiddleCenter;
            idField.style.width = 340;
            idField.style.fontSize = 25;
            idField.style.unityFontStyleAndWeight = FontStyle.Bold;
            idField.RegisterValueChangedCallback(e =>
            {
                MapEditorFunctions.MarkDirty(_data, "Rename Stage");
                _data.id = e.newValue;
            });
            panel.Add(idField);
            SpaceV(panel, 6);

            // 네비게이션 바
            var nav = Row();
            nav.Add(Button("<", () => { MapEditorFunctions.NavigateClamped(_data, -1); }));
            nav.Add(Button(">", () => { MapEditorFunctions.NavigateClamped(_data, +1); }));
            nav.Add(Button("Add", () => { MapEditorFunctions.AddAfterCurrent(_data); }));
            nav.Add(Button("Save", () => { MapEditorFunctions.Save(_data); }));
            nav.Add(Button("Delete", () =>
            {
                if (EditorUtility.DisplayDialog("Delete Stage", $"Delete '{_data?.id}'?\n(This cannot be undone)", "Delete", "Cancel"))
                {
                    MapEditorFunctions.DeleteCurrent(_data);
                }
            }));
            panel.Add(nav);
            SpaceV(panel, 6);
            
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
                ApplyGoalUI(_data, tutT, scoT, fruT, scoreDetailRow, fruitTable, fruitCounts, fruitBlocksRow);
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
                {
                    MapEditorFunctions.MarkDirty(_data, "Edit Score Goal");
                    _data.scoreGoal = Mathf.Max(0, e.newValue);
                });

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

                MapEditorFunctions.EnsureFruitArrays(_data);

                for (int i = 0; i < FruitCount; i++)
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
                    tile.style.marginRight = (i == FruitCount - 1) ? 0 : 8;
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

                    void SyncField() => fruitCount.SetValueWithoutNotify(_data.fruitGoals[idx]);

                    upBtn.clicked += () => { MapEditorFunctions.BumpFruitGoal(_data, idx, +1); SyncField(); };
                    dnBtn.clicked += () => { MapEditorFunctions.BumpFruitGoal(_data, idx, -1); SyncField(); };

                    // 타일 토글로 이벤트 전파 막기
                    upBtn.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                    dnBtn.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                    fruitCount.RegisterCallback<ClickEvent>(e => e.StopPropagation());

                    // (선택) 직접 입력/휠 증감
                    fruitCount.RegisterValueChangedCallback(e =>
                    {
                        MapEditorFunctions.SetFruitGoal(_data, idx, e.newValue);
                        SyncField();
                    });
                    fruitCount.RegisterCallback<WheelEvent>(e =>
                    {
                        if (!_data.fruitEnabled[idx]) return;
                        MapEditorFunctions.BumpFruitGoal(_data, idx, e.delta.y > 0 ? -1 : +1);
                        SyncField();
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
                        MapEditorFunctions.ToggleFruitEnable(_data, idx);
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

            var textWidth = 50;
            var textFontSize = 15;
            var sliderLength = 200;
            var floatWidth = 35;
            
            CreateAlphaRow(textWidth, textFontSize, sliderLength, floatWidth, panel);

            CreateCharlieRow(textWidth, textFontSize, sliderLength, floatWidth, panel);


            // ========== 블록 선택 ==========
            var selectBlockLbl = new Label("Select Block");
            selectBlockLbl.style.fontSize = 20;
            selectBlockLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(selectBlockLbl);
            SpaceV(panel, 4);

            var palette = new VisualElement();
            //palette.style.backgroundColor = new Color(1, 1, 1, 0.4f);
            palette.style.alignItems = Align.FlexStart;
            Pad(palette, 6);

            int blockSize = 50;
            int blockOffset = 5;

            var line2 = Row();
            line2.style.flexWrap = Wrap.NoWrap;
            line2.style.flexShrink = 0;
            line2.style.overflow = Overflow.Hidden;

            for (int i = 0; i < FruitCount; i++)
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
            line3.style.flexWrap = Wrap.NoWrap;
            line3.style.flexShrink = 0;
            line3.style.overflow = Overflow.Hidden;

            fruitBlocksRow = line3;

            for (int i = 0; i < FruitCount; i++)
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
            ApplyGoalUI(_data, tutT, scoT, fruT, scoreDetailRow, fruitTable, fruitCounts, fruitBlocksRow);
            return panel;
        }

        private void CreateCharlieRow(int textWidth, int textFontSize, int sliderLength, int floatWidth, VisualElement panel)
        {
            var charlieMinRow = Row();

            var charlieMinLbl = new Label("C Min");
            charlieMinLbl.style.width = textWidth;
            charlieMinLbl.style.fontSize = textFontSize;
            charlieMinLbl.style.unityFontStyleAndWeight = FontStyle.Bold;

            var charileMinSlider = new Slider(0f, 2f);
            charileMinSlider.value = _data.charlieMin;
            charileMinSlider.style.width = sliderLength;      
            
            var charlieMinField = new FloatField();
            charlieMinField.value = _data.charlieMin;
            charlieMinField.style.width = floatWidth;
            charlieMinField.formatString = "0.00";    // 2자리 소수 표시
            
            var charlieMaxRow = Row();
            
            var charlieMaxLbl = new Label("C Max");
            charlieMaxLbl.style.width = textWidth;
            charlieMaxLbl.style.fontSize = textFontSize;
            charlieMaxLbl.style.unityFontStyleAndWeight = FontStyle.Bold;

            var charlieMaxSlider = new Slider(0f, 2f);
            charlieMaxSlider.value = _data.charlieMax;
            charlieMaxSlider.style.width = sliderLength;     

            var charlieMaxField = new FloatField();
            charlieMaxField.value = _data.charlieMax;
            charlieMaxField.style.width = floatWidth;
            charlieMaxField.formatString = "0.00";    // 2자리 소수 표시

            bool charlieUpdating = false;          // 쌍방 이벤트 루프 방지

            // --- 로컬 헬퍼: Min을 설정 ---
            void SetCharlieMin(float v, bool withUndo = true)
            {
                if (charlieUpdating) return;
                charlieUpdating = true;

                float nv = Mathf.Clamp(v, 0f, 2f);
                if (withUndo) Undo.RecordObject(_data, "Change Charlie Min");
                _data.charlieMin = nv;

                // min이 max를 넘어섰으면 max를 따라올림
                if (_data.charlieMin > _data.charlieMax)
                {
                    _data.charlieMax = _data.charlieMin;
                    charlieMaxSlider.SetValueWithoutNotify(_data.charlieMax);
                    charlieMaxField.SetValueWithoutNotify(_data.charlieMax);
                }

                // 본인 UI 동기화
                charileMinSlider.SetValueWithoutNotify(_data.charlieMin);
                charlieMinField.SetValueWithoutNotify(_data.charlieMin);

                EditorUtility.SetDirty(_data);
                charlieUpdating = false;
            }

// --- 로컬 헬퍼: Max를 설정 ---
            void SetCharlieMax(float v, bool withUndo = true)
            {
                if (charlieUpdating) return;
                charlieUpdating = true;

                float nv = Mathf.Clamp(v, 0f, 2f);
                if (withUndo) Undo.RecordObject(_data, "Change Charlie Max");
                _data.charlieMax = nv;

                // max가 min보다 작아졌으면 min을 끌어내림
                if (_data.charlieMax < _data.charlieMin)
                {
                    _data.charlieMin = _data.charlieMax;
                    charileMinSlider.SetValueWithoutNotify(_data.charlieMin);
                    charlieMinField.SetValueWithoutNotify(_data.charlieMin);
                }

                // 본인 UI 동기화
                charlieMaxSlider.SetValueWithoutNotify(_data.charlieMax);
                charlieMaxField.SetValueWithoutNotify(_data.charlieMax);

                EditorUtility.SetDirty(_data);
                charlieUpdating = false;
            }

// --- 콜백: 헬퍼만 호출 ---
            charileMinSlider.RegisterValueChangedCallback(e => SetCharlieMin(e.newValue));
            charlieMinField .RegisterValueChangedCallback(e => SetCharlieMin(e.newValue));
            charlieMaxSlider.RegisterValueChangedCallback(e => SetCharlieMax(e.newValue));
            charlieMaxField .RegisterValueChangedCallback(e => SetCharlieMax(e.newValue));

// --- 초기값을 한 번 정규화(데이터가 어긋나 있었을 수 있으니) ---
            SetCharlieMin(_data.charlieMin, withUndo:false);
            SetCharlieMax(_data.charlieMax, withUndo:false);
            
            // 조립
            SpaceH(charlieMinRow, 6);
            charlieMinRow.Add(charlieMinLbl);
            SpaceH(charlieMinRow, 8);
            charlieMinRow.Add(charlieMinField);
            SpaceH(charlieMinRow, 8);
            charlieMinRow.Add(charileMinSlider);
            panel.Add(charlieMinRow);
            SpaceV(panel, 6);
            
            SpaceH(charlieMaxRow, 6);
            charlieMaxRow.Add(charlieMaxLbl);
            SpaceH(charlieMaxRow, 8);
            charlieMaxRow.Add(charlieMaxField);
            SpaceH(charlieMaxRow, 8);
            charlieMaxRow.Add(charlieMaxSlider);
            panel.Add(charlieMaxRow);
            SpaceV(panel, 6);
        }

        private void CreateAlphaRow(int textWidth, int textFontSize, int sliderLength, int floatWidth, VisualElement panel)
        {
            var alphaMinRow = Row();

            var alphaMinLbl = new Label("A Min");
            alphaMinLbl.style.width = textWidth;
            alphaMinLbl.style.fontSize = textFontSize;
            alphaMinLbl.style.unityFontStyleAndWeight = FontStyle.Bold;

            var alphaMinSlider = new Slider(0f, 2f);
            alphaMinSlider.value = _data.alphaMin;
            alphaMinSlider.style.width = sliderLength;      
            
            var alphaMinField = new FloatField();
            alphaMinField.value = _data.alphaMin;
            alphaMinField.style.width = floatWidth;
            alphaMinField.formatString = "0.00";    // 2자리 소수 표시
            
            var alphaMaxRow = Row();
            
            var alphaMaxLbl = new Label("A Max");
            alphaMaxLbl.style.width = textWidth;
            alphaMaxLbl.style.fontSize = textFontSize;
            alphaMaxLbl.style.unityFontStyleAndWeight = FontStyle.Bold;

            var alphaMaxSlider = new Slider(0f, 2f);
            alphaMaxSlider.value = _data.alphaMax;
            alphaMaxSlider.style.width = sliderLength;     

            var alphaMaxField = new FloatField();
            alphaMaxField.value = _data.alphaMax;
            alphaMaxField.style.width = floatWidth;
            alphaMaxField.formatString = "0.00";    // 2자리 소수 표시

            bool alphaUpdating = false;          // 쌍방 이벤트 루프 방지

            // --- 로컬 헬퍼: Min을 설정 ---
            void SetAlphaMin(float v, bool withUndo = true)
            {
                if (alphaUpdating) return;
                alphaUpdating = true;

                float nv = Mathf.Clamp(v, 0f, 2f);
                if (withUndo) Undo.RecordObject(_data, "Change Alpha Min");
                _data.alphaMin = nv;

                // min이 max를 넘어섰으면 max를 따라올림
                if (_data.alphaMin > _data.alphaMax)
                {
                    _data.alphaMax = _data.alphaMin;
                    alphaMaxSlider.SetValueWithoutNotify(_data.alphaMax);
                    alphaMaxField.SetValueWithoutNotify(_data.alphaMax);
                }

                // 본인 UI 동기화
                alphaMinSlider.SetValueWithoutNotify(_data.alphaMin);
                alphaMinField.SetValueWithoutNotify(_data.alphaMin);

                EditorUtility.SetDirty(_data);
                alphaUpdating = false;
            }

// --- 로컬 헬퍼: Max를 설정 ---
            void SetAlphaMax(float v, bool withUndo = true)
            {
                if (alphaUpdating) return;
                alphaUpdating = true;

                float nv = Mathf.Clamp(v, 0f, 2f);
                if (withUndo) Undo.RecordObject(_data, "Change Alpha Max");
                _data.alphaMax = nv;

                // max가 min보다 작아졌으면 min을 끌어내림
                if (_data.alphaMax < _data.alphaMin)
                {
                    _data.alphaMin = _data.alphaMax;
                    alphaMinSlider.SetValueWithoutNotify(_data.alphaMin);
                    alphaMinField.SetValueWithoutNotify(_data.alphaMin);
                }

                // 본인 UI 동기화
                alphaMaxSlider.SetValueWithoutNotify(_data.alphaMax);
                alphaMaxField.SetValueWithoutNotify(_data.alphaMax);

                EditorUtility.SetDirty(_data);
                alphaUpdating = false;
            }

// --- 콜백: 헬퍼만 호출 ---
            alphaMinSlider.RegisterValueChangedCallback(e => SetAlphaMin(e.newValue));
            alphaMinField .RegisterValueChangedCallback(e => SetAlphaMin(e.newValue));
            alphaMaxSlider.RegisterValueChangedCallback(e => SetAlphaMax(e.newValue));
            alphaMaxField .RegisterValueChangedCallback(e => SetAlphaMax(e.newValue));

// --- 초기값을 한 번 정규화(데이터가 어긋나 있었을 수 있으니) ---
            SetAlphaMin(_data.alphaMin, withUndo:false);
            SetAlphaMax(_data.alphaMax, withUndo:false);
            
            // 조립
            SpaceH(alphaMinRow, 6);
            alphaMinRow.Add(alphaMinLbl);
            SpaceH(alphaMinRow, 8);
            alphaMinRow.Add(alphaMinField);
            SpaceH(alphaMinRow, 8);
            alphaMinRow.Add(alphaMinSlider);
            panel.Add(alphaMinRow);
            SpaceV(panel, 6);
            
            SpaceH(alphaMaxRow, 6);
            alphaMaxRow.Add(alphaMaxLbl);
            SpaceH(alphaMaxRow, 8);
            alphaMaxRow.Add(alphaMaxField);
            SpaceH(alphaMaxRow, 8);
            alphaMaxRow.Add(alphaMaxSlider);
            panel.Add(alphaMaxRow);
            SpaceV(panel, 6);
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
            IntegerField[] fruitCounts,
            VisualElement fruitBlocksRow)
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
            
            if (fruitBlocksRow != null)
                //fruitBlocksRow.SetEnabled(isFruit);
                fruitBlocksRow.style.display = isFruit ? DisplayStyle.Flex : DisplayStyle.None;
            
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
        // 추후에 윈도우 패널화할 때 사용할 예정
        private VisualElement BuildGridPane()
        {
            var wrap = new VisualElement { name = "grid-wrap" };
            wrap.style.flexGrow = 0;
            wrap.style.flexShrink = 0;
            Pad(wrap, 6);

            var grid = new VisualElement { name = "grid" };
            wrap.Add(grid);
            SpaceV(wrap, 6);

            var tools = Row();
            tools.Add(Button("Clear", () =>
            {
                MapEditorFunctions.ClearLayout(_data);
                BuildGrid();                          // UI 새로 그림
            }));
            wrap.Add(tools);

            BuildGrid();
            _rebuildGrid = BuildGrid;                
            return wrap;

            void BuildGrid()
            {
                grid.Clear();
                grid.style.alignItems = Align.FlexStart;
                int rows = Mathf.Max(1, _data.rows);
                int cols = Mathf.Max(1, _data.cols);
                int cell = _cellSize;                 

                MapEditorFunctions.EnsureLayoutSize(_data);

                // PointerUp는 중복 등록되지 않게 먼저 Clear 후 다시 한 번만
                grid.RegisterCallback<PointerUpEvent>(e =>
                {
                    if (!_isDragging) return;
                    _isDragging = false;
                    grid.ReleasePointer(e.pointerId);
                    e.StopPropagation();
                });

                for (int r = 0; r < rows; r++)
                {
                    var line = Row();
                    line.style.marginBottom = (r == rows - 1) ? 0 : 2;

                    for (int c = 0; c < cols; c++)
                    {
                        int idx1D = r * cols + c;
                        var ve = new VisualElement();
                        ve.style.width = cell;
                        ve.style.height = cell;
                        ve.style.backgroundColor = new Color(0, 0, 0, 0.08f);
                        BorderThin(ve, new Color(0, 0, 0, 0.2f));
                        ve.style.marginRight = (c == cols - 1) ? 0 : 2;

                        var img = new Image { scaleMode = ScaleMode.ScaleToFit };
                        img.style.width  = cell - 2;
                        img.style.height = cell - 2;

                        int curVal = (idx1D < _data.layout.Count) ? _data.layout[idx1D] : 0;
                        img.sprite = IndexToSprite(curVal);
                        ve.Add(img);

                        ve.RegisterCallback<PointerDownEvent>(e =>
                        {
                            if (e.button == 1)
                            {
                                _isDragging = true;
                                _dragValue = 0;
                                PaintGridWithValue(idx1D, img, _dragValue);
                                grid.CapturePointer(e.pointerId);
                                e.StopPropagation();
                                return;
                            }
                            if (e.button != 0) return;
                            _isDragging = true;
                            int brushVal = SpriteToIndex(_brushSprite);
                            int oldVal   = _data.layout[idx1D];
                            _dragValue   = (brushVal == 0 || brushVal == oldVal) ? 0 : brushVal;
                            PaintGridWithValue(idx1D, img, _dragValue);
                            grid.CapturePointer(e.pointerId);
                            e.StopPropagation();
                        });

                        ve.RegisterCallback<PointerEnterEvent>(_ =>
                        {
                            if (!_isDragging) return;
                            PaintGridWithValue(idx1D, img, _dragValue);
                        });

                        line.Add(ve);
                    }

                    grid.Add(line);
                }
            }
        }
        private void PaintGridWithValue(int idx1D, Image img, int value)
        {
            _data.layout[idx1D] = value;
            img.sprite = IndexToSprite(value);
            MapEditorFunctions.MarkDirty(_data, "Paint Cell");
        }
        
        private void BuildSpriteIndex()
        {
            _spriteToIndex = new Dictionary<Sprite, int>();
            _codeToSprite  = new Dictionary<int, Sprite>();

            void AddAll(Sprite[] arr)
            {
                if (arr == null) return;
                foreach (var s in arr)
                {
                    if (!s) continue;
                    var m = s_CodeRegex.Match(s.name);
                    if (!m.Success) { Debug.LogWarning($"[MapEditor] 선행 숫자 없음: {s.name}"); continue; }

                    int code = int.Parse(m.Groups[1].Value);

                    // sprite -> code
                    _spriteToIndex[s] = code;

                    // code -> sprite (중복 코드면 최초 것 유지)
                    if (!_codeToSprite.ContainsKey(code))
                        _codeToSprite.Add(code, s);
                }
            }

            AddAll(_data.blockImages);
            AddAll(_data.blockWithFruitIcons);

            // 기존 코드 방식 재생성 
            MigrateLegacyLayout();
        }

        private int SpriteToIndex(Sprite s)
        {
            if (s == null) return 0;
            return (_spriteToIndex != null && _spriteToIndex.TryGetValue(s, out var code)) ? code : 0;
        }

        private Sprite IndexToSprite(int code)
        {
            if (code <= 0) return null;
            return (_codeToSprite != null && _codeToSprite.TryGetValue(code, out var s)) ? s : null;
        }
        
        private void SetBrushSprite(Sprite sprite, Button sourceBtn)
        {
            Highlight(_selectedPaletteButton, false);
            _brushSprite = sprite;
            _selectedPaletteButton = sourceBtn;
            Highlight(_selectedPaletteButton, true);
        }
        
        private void MigrateLegacyLayout()
        {
            if (_data?.layout == null || _data.layout.Count == 0) return;

            int baseCount  = _data.blockImages?.Length ?? 0;
            int fruitCount = _data.blockWithFruitIcons?.Length ?? 0;
            int maxLegacy  = baseCount + fruitCount;

            bool looksLegacy = false;
            foreach (var v in _data.layout)
            {
                if (v > 0 && v <= maxLegacy) { looksLegacy = true; break; }
            }
            if (!looksLegacy) return;

            // legacy -> code
            for (int i = 0; i < _data.layout.Count; i++)
            {
                int v = _data.layout[i];
                if (v <= 0) continue;

                Sprite s = null;
                if (v <= baseCount)
                {
                    // 1..baseCount -> blockImages[v-1]
                    s = _data.blockImages[v - 1];
                }
                else
                {
                    // baseCount+1.. -> blockWithFruitIcons[v - baseCount - 1]
                    int j = v - baseCount - 1;
                    if (_data.blockWithFruitIcons != null &&
                        j >= 0 && j < _data.blockWithFruitIcons.Length)
                        s = _data.blockWithFruitIcons[j];
                }

                int code = SpriteToIndex(s); // 이제 code는 101/201...
                _data.layout[i] = code;
            }

            MapEditorFunctions.MarkDirty(_data, "Migrate legacy layout -> code");
            Debug.Log("[MapEditor] 레거시 인덱스 레이아웃을 코드(파일명 앞 숫자)로 마이그레이션했습니다.");
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

            btn.style.backgroundColor = on ? new Color(1, 1, 1, 0.10f) : new Color(0, 0, 0, 0);
        }
        
        // ---- 스타일 & UI 생성 ----
        private static Button Button(string text, Action onClick)
        {
            var b = new Button(() => onClick?.Invoke()) { text = text };
            b.style.minWidth = 56;
            b.style.minHeight = 23;
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
            ve.style.paddingTop = all;  ve.style.paddingBottom = all;
        }
        private static void Border(VisualElement ve, Color c = default)
        {
            ve.style.borderLeftWidth = 1;  ve.style.borderLeftColor = c;
            ve.style.borderRightWidth = 1; ve.style.borderRightColor = c;
            ve.style.borderTopWidth = 1;   ve.style.borderTopColor = c;
            ve.style.borderBottomWidth = 1;ve.style.borderBottomColor = c;
        }
        private static void BorderThin(VisualElement ve, Color c) => Border(ve, c);
        
    }
    
    
}
#endif
