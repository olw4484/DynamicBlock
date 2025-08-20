using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(ShapeTemplate))]
public class ShapeEditor : UnityEditor.Editor
{
    private const int GridSize = 5;
    private VisualElement gridContainer;
    private ShapeTemplate[] shapes;
    private int currentIndex;
    private ShapeTemplate _target;

    public override VisualElement CreateInspectorGUI()
    {
        _target = (ShapeTemplate)target;

        var root = new VisualElement();
        root.style.flexDirection = FlexDirection.Column;
        root.style.paddingTop = 5;
        root.style.paddingBottom = 5;
        root.style.paddingLeft = 5;
        root.style.paddingRight = 5;

        root.Add(new Label(_target.name) { name = "title" });

        // Navigation + Add/Remove
        var navContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 5 } };
        navContainer.Add(CreateButton("<<", () => NavigateShapes(-1)));
        navContainer.Add(CreateButton(">>", () => NavigateShapes(1)));
        navContainer.Add(CreateButton("+", AddShape));
        navContainer.Add(CreateButton("-", RemoveShape));
        root.Add(navContainer);

        // ClearAll / Save
        var actionContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 5 } };
        actionContainer.Add(CreateButton("Clear All", ClearAll));
        actionContainer.Add(CreateButton("Save", Save));
        root.Add(actionContainer);

        root.Add(new Label("Click on a square to add/remove a block") { style = { marginBottom = 5 } });

        // Grid Container
        gridContainer = new VisualElement { style = { flexDirection = FlexDirection.Column, alignItems = Align.Center, marginBottom = 5 } };
        root.Add(gridContainer);

        // Classic Mode
        var classicContainer = new VisualElement { style = { flexDirection = FlexDirection.Column, marginBottom = 5 } };
        classicContainer.Add(new Label("Classic Mode Parameters"));

        // Score For Spawn
        var scoreField = new IntegerField("Score For Spawn") { value = _target.scoreForSpawn };
        scoreField.style.marginBottom = 2;
        scoreField.RegisterValueChangedCallback(evt =>
        {
            _target.scoreForSpawn = evt.newValue;
            EditorUtility.SetDirty(_target);
        });
        classicContainer.Add(scoreField);

        // Chance For Spawn (Label + Slider + Value)
        var chanceContainer = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 }
        };

        // Label
        var chanceLabel = new Label("Chance For Spawn");
        chanceLabel.style.width = 120;
        chanceLabel.style.marginRight = 5;

        // Slider Container (Slider만 담음)
        var sliderWrapper = new VisualElement { style = { flexGrow = 1, flexShrink = 1, flexDirection = FlexDirection.Row } };
        var chanceSlider = new Slider(0, 1) { value = _target.chanceForSpawn };
        chanceSlider.style.flexGrow = 1;
        sliderWrapper.Add(chanceSlider);

        // FloatField
        var chanceValue = new FloatField { value = _target.chanceForSpawn };
        chanceValue.style.width = 50;
        chanceValue.style.flexShrink = 0;
        chanceValue.style.marginLeft = 5;

        // 동기화
        chanceSlider.RegisterValueChangedCallback(evt =>
        {
            chanceValue.SetValueWithoutNotify(evt.newValue);
            _target.chanceForSpawn = evt.newValue;
            EditorUtility.SetDirty(_target);
        });
        chanceValue.RegisterValueChangedCallback(evt =>
        {
            chanceSlider.SetValueWithoutNotify(evt.newValue);
            _target.chanceForSpawn = evt.newValue;
            EditorUtility.SetDirty(_target);
        });

        // 추가
        chanceContainer.Add(chanceLabel);
        chanceContainer.Add(sliderWrapper);
        chanceContainer.Add(chanceValue);
        classicContainer.Add(chanceContainer);

        root.Add(classicContainer);

        // Adventure Mode
        var adventureContainer = new VisualElement { style = { flexDirection = FlexDirection.Column, marginBottom = 5 } };
        adventureContainer.Add(new Label("Adventure Mode Parameters"));
        var spawnLevelField = new IntegerField("Spawn From Level") { value = _target.spawnFromLevel };
        spawnLevelField.style.marginBottom = 2;
        spawnLevelField.RegisterValueChangedCallback(evt =>
        {
            _target.spawnFromLevel = evt.newValue;
            EditorUtility.SetDirty(_target);
        });
        adventureContainer.Add(spawnLevelField);
        root.Add(adventureContainer);

        LoadShapes();
        CreateGrid();

        return root;
    }

    private void LoadShapes()
    {
        shapes = Resources.LoadAll<ShapeTemplate>("Shapes");
        currentIndex = Array.IndexOf(shapes, _target);
        if (currentIndex == -1 && shapes.Length > 0)
        {
            currentIndex = 0;
            _target = shapes[currentIndex];
            Selection.activeObject = _target;
        }
    }

    private void NavigateShapes(int direction)
    {
        Save();
        if (shapes.Length == 0) return;

        currentIndex = (currentIndex + direction + shapes.Length) % shapes.Length;
        _target = shapes[currentIndex];
        Selection.activeObject = _target;
        CreateGrid();
    }

    private void AddShape()
    {
        Save();
        string path = AssetDatabase.GetAssetPath(_target);
        if (string.IsNullOrEmpty(path))
            path = "Assets/BlockPuzzleGameToolkit/ScriptableObjects/Shapes";
        else
            path = Path.GetDirectoryName(path);

        var newShape = CreateInstance<ShapeTemplate>();
        for (int i = 0; i < GridSize; i++)
            newShape.rows[i] = new ShapeRow();

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{path}/NewShape.asset");
        AssetDatabase.CreateAsset(newShape, assetPath);
        AssetDatabase.SaveAssets();

        LoadShapes();
        currentIndex = shapes.Length - 1;
        _target = shapes[currentIndex];
        Selection.activeObject = _target;
        CreateGrid();
    }

    private void RemoveShape()
    {
        if (shapes.Length <= 1) return;

        string path = AssetDatabase.GetAssetPath(_target);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();

        LoadShapes();
        currentIndex = Mathf.Clamp(currentIndex, 0, shapes.Length - 1);
        _target = shapes[currentIndex];
        Selection.activeObject = _target;
        CreateGrid();
    }

    private void CreateGrid()
    {
        if (gridContainer == null || _target == null) return;

        gridContainer.Clear();
        for (int i = 0; i < GridSize; i++)
        {
            if (_target.rows[i] == null)
                _target.rows[i] = new ShapeRow();

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 5 } };
            gridContainer.Add(row);

            for (int j = 0; j < GridSize; j++)
            {
                var toggle = new Toggle();
                toggle.value = _target.rows[i].cells[j];

                // --- 크기/스타일 설정 ---
                toggle.style.width = 60;
                toggle.style.height = 60;
                toggle.style.flexGrow = 0;
                toggle.style.flexShrink = 0;
                toggle.style.marginRight = 2;
                
                int x = i, y = j;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    _target.rows[x].cells[y] = evt.newValue;
                    UpdateToggleColor(toggle, evt.newValue);
                    EditorUtility.SetDirty(_target);
                });
                
                row.Add(toggle);
                
                // --- 체크박스 숨기기 ---
                var input = toggle.Q("unity-toggle__input");
                if (input != null)
                    input.style.display = DisplayStyle.None;
                
                var checkmark = toggle.Q("unity-checkmark");
                if (checkmark != null)
                    checkmark.style.display = DisplayStyle.None;
                
                // 색상 업데이트
                UpdateToggleColor(toggle, toggle.value);
            }
        }
    }

    private void UpdateToggleColor(Toggle toggle, bool active)
    {
        if (active)
            toggle.style.backgroundColor = new StyleColor(Color.white);
        else
            toggle.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
    }

    private Button CreateButton(string text, Action clickEvent)
    {
        var button = new Button(clickEvent) { text = text };
        button.style.flexGrow = 1;
        button.style.marginRight = 5;
        return button;
    }

    private void ClearAll()
    {
        if (_target == null) return;

        for (int i = 0; i < GridSize; i++)
            for (int j = 0; j < GridSize; j++)
                _target.rows[i].cells[j] = false;

        EditorUtility.SetDirty(_target);
        CreateGrid();
    }

    private void Save()
    {
        if (_target == null) return;

        EditorUtility.SetDirty(_target);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
