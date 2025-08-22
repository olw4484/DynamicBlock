#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _00.WorkSpace.GIL.Scripts.Editors
{
    [CustomEditor(typeof(ShapeData))]
    public class ShapeEditor : Editor
    {
        private const int GridSize = 5;
        private VisualElement gridContainer;
        private ShapeData[] shapes;
        private int currentIndex;
        private ShapeData _target;

        public override VisualElement CreateInspectorGUI()
        {
            _target = (ShapeData)target;

            var root = new VisualElement { style = { flexDirection = FlexDirection.Column, paddingTop = 5, paddingBottom = 5, paddingLeft = 5, paddingRight = 5 } };

            var nameField = new TextField("Name") { value = _target.name };
            nameField.RegisterValueChangedCallback(evt => { ChangeName(evt); });
            root.Add(nameField);

            var idField = new TextField("ID") { value = _target.Id };
            idField.RegisterValueChangedCallback(evt => { ChangeName(evt); });
            root.Add(nameField);
            
            // Navigation buttons
            var navContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 5 } };
            navContainer.Add(CreateButton("<", () => NavigateShapes(-1), 0.5f));
            navContainer.Add(CreateButton(">", () => NavigateShapes(1), 0.5f));
            navContainer.Add(CreateButton("Add Shape", AddShape));
            navContainer.Add(CreateButton("Remove Shape", RemoveShape));
            root.Add(navContainer);

            // Action buttons
            var actionContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 5 } };
            actionContainer.Add(CreateButton("Clear All", ClearAll));
            actionContainer.Add(CreateButton("Save", Save));
            root.Add(actionContainer);

            root.Add(new Label("Click on a square to add/remove a block") { style = { marginBottom = 5 } });

            // Grid container
            gridContainer = new VisualElement { style = { flexDirection = FlexDirection.Column, alignItems = Align.Center, marginBottom = 5 } };
            root.Add(gridContainer);
            
            var classicContainer = new VisualElement { style = { flexDirection = FlexDirection.Column, marginBottom = 5 } };
            classicContainer.Add(new Label("\nClassic Mode Parameters"));

            var scoreField = new IntegerField("Score For Spawn") { value = _target.scoreForSpawn };
            scoreField.style.width = 200;
            scoreField.RegisterValueChangedCallback(evt => { _target.scoreForSpawn = evt.newValue; });
            classicContainer.Add(scoreField);
            
            var sliderContainer = new VisualElement { name = "slider-container" };
            sliderContainer.style.flexDirection = FlexDirection.Row;
            var chanceField = new Slider("Chance for Spawn", 0, 1) { value = _target.chanceForSpawn };
            var chanceValue = new FloatField { value = _target.chanceForSpawn };
            chanceValue.style.marginLeft = 10;
            chanceValue.RegisterValueChangedCallback(evt => { chanceField.value = evt.newValue; });
            chanceField.RegisterValueChangedCallback(evt =>
            {
                chanceValue.value = evt.newValue;
                _target.chanceForSpawn = evt.newValue;
            });
            
            chanceField.style.width = 200;
            chanceField.RegisterValueChangedCallback(evt =>
            {
                _target.chanceForSpawn = evt.newValue;
                EditorUtility.SetDirty(_target);
            });
            sliderContainer.Add(chanceField);
            sliderContainer.Add(chanceValue);
            root.Add(sliderContainer);
            
            root.Add(classicContainer);

            LoadShapes();
            CreateGrid();

            return root;
        }

        private void ChangeName(ChangeEvent<string> evt)
        {
            if (evt.newValue == _target.name) return;
            string path = AssetDatabase.GetAssetPath(_target);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.RenameAsset(path, evt.newValue);
                AssetDatabase.SaveAssets();
            }
            _target.name = evt.newValue;
            EditorUtility.SetDirty(_target);
        }

        private void LoadShapes()
        {
            shapes = Resources.LoadAll<ShapeData>("Shapes");
            currentIndex = shapes.ToList().IndexOf(_target);
            if (currentIndex == -1 && shapes.Length > 0)
            {
                currentIndex = 0;
                Selection.activeObject = shapes[currentIndex];
            }
        }

        private void NavigateShapes(int direction)
        {
            Save();
            currentIndex = (currentIndex + direction + shapes.Length) % shapes.Length;
            Selection.activeObject = shapes[currentIndex];
            _target = shapes[currentIndex];
            CreateGrid();
        }

        private void AddShape()
        {
            Save();
            var path = AssetDatabase.GetAssetPath(_target);
            if (string.IsNullOrEmpty(path))
            {
                path = "Assets/00.WorkSpace/GIL/Resources/Shapes";
            }
            else
            {
                path = Path.GetDirectoryName(path);
            }

            var newShapeTemplate = CreateInstance<ShapeData>();
            for (var i = 0; i < GridSize; i++)
            {
                newShapeTemplate.rows[i] = new ShapeRow();
            }

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{path}/00.0_block_N_R.asset");
            AssetDatabase.CreateAsset(newShapeTemplate, assetPath);
            AssetDatabase.SaveAssets();

            LoadShapes();
            currentIndex = shapes.Length - 1;
            Selection.activeObject = newShapeTemplate;
            _target = newShapeTemplate;
            CreateGrid();
        }

        private void RemoveShape()
        {
            if (shapes.Length <= 1) return;

            if (!EditorUtility.DisplayDialog("Delete Shape", "Are you sure you want to delete this shape?", "Yes", "No")) 
                return;
            
            var path = AssetDatabase.GetAssetPath(_target);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();

            LoadShapes();
            currentIndex = Mathf.Clamp(currentIndex, 0, shapes.Length - 1);
            Selection.activeObject = shapes[currentIndex];
            _target = shapes[currentIndex];
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
                    var toggle = new Toggle {style = { width = 50, height = 50 , flexGrow = 0, flexShrink = 0, marginRight = 2}};
                    toggle.value = _target.rows[i].columns[j];

                    int x = i, y = j;
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        _target.rows[x].columns[y] = evt.newValue;
                        UpdateToggleColor(toggle, evt.newValue);
                    });

                    row.Add(toggle);

                    var input = toggle.Q("unity-toggle__input");
                    if (input != null) input.style.display = DisplayStyle.None;
                    var checkmark = toggle.Q("unity-checkmark");
                    if (checkmark != null) checkmark.style.display = DisplayStyle.None;

                    UpdateToggleColor(toggle, toggle.value);
                }
            }
        }

        private void UpdateToggleColor(Toggle toggle, bool active)
        {
            toggle.style.backgroundColor = active ? new StyleColor(Color.white) : new StyleColor(new Color(0.2f, 0.2f, 0.2f));
        }

        private Button CreateButton(string text, Action clickEvent, float flex = 1f)
        {
            var button = new Button(clickEvent) { text = text , style = { flexGrow = flex, marginRight = 5 }};
            return button;
        }

        private void ClearAll()
        {
            if (_target == null) return;

            for (int i = 0; i < GridSize; i++)
                for (int j = 0; j < GridSize; j++)
                    _target.rows[i].columns[j] = false;
            
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
}
#endif