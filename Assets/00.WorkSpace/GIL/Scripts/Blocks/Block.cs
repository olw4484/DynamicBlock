using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.GameEvents;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _00.WorkSpace.GIL.Scripts.Blocks
{
    public class Block : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
    {
        [Header("Prefab & Data")]
        public GameObject shapePrefab;

        [Header("Pointer")] 
        public Vector3 shapeSelectedScale = Vector3.one * 1.2f;
        public Vector2 selectedOffset = new Vector2(0f, 700f);
        
        private Vector3 _shapeStartScale;
        private RectTransform _shapeTransform;
        private Canvas _canvas;
        private Vector3 _startPosition;
        
        private ShapeData _currentShapeData;

        private bool _isDragging;
        
        private void Awake()
        {
            _shapeStartScale = GetComponent<RectTransform>().localScale;
            _shapeTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            _startPosition = _shapeTransform.localPosition;
            _shapeStartScale = _shapeTransform.localScale;
        }
        
        public void GenerateBlock(ShapeData shapeData)
        {
            _shapeTransform.localPosition = _startPosition;
            _currentShapeData = shapeData;
            CreateBlock(shapeData);
        }
    
        public void CreateBlock(ShapeData shapeData)
        {
            if (shapePrefab == null || shapeData == null) return;
            RectTransform rectTransform = shapePrefab.GetComponent<RectTransform>();

            if (rectTransform == null) return;

            float width = rectTransform.sizeDelta.x;
            float height = rectTransform.sizeDelta.y;
        
            Vector2 offset = new Vector2((5 - 1) * 0.5f * width, -(5 - 1) * 0.5f * height);

            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    GameObject block = Instantiate(shapePrefab, transform);
                    RectTransform rt = block.GetComponent<RectTransform>();
                    rt.anchoredPosition = new Vector2(x * width, -y * height) - offset;
                    
                    block.SetActive(shapeData.rows[y].columns[x]);
                }
            }
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            _isDragging = false;
            
            _shapeTransform.localPosition = _startPosition + (Vector3)selectedOffset;
            _shapeTransform.localScale = shapeSelectedScale;
            
            MoveBlock(eventData);
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            _isDragging = true;
            MoveBlock(eventData);

            var shapeBlocks = new List<Transform>();
            foreach (Transform child in transform)
                if (child.gameObject.activeSelf) shapeBlocks.Add(child);

            GridManager.Instance.UpdateHoverPreview(shapeBlocks);
        }
        
        
        
        public void OnEndDrag(PointerEventData eventData)
        {
            List<Transform> shapeBlocks = new();
            foreach (Transform child in transform)
            {
                if (child.gameObject.activeSelf) 
                    shapeBlocks.Add(child);
            }
            
            bool placed = GridManager.Instance.CanPlaceShape(shapeBlocks);
            
            if (!placed)
            {
                _shapeTransform.localPosition = _startPosition;
                _shapeTransform.localScale = _shapeStartScale;
            }
            else
            {
                BlockStorage storage = FindObjectOfType<BlockStorage>();
                storage.OnBlockPlaced(this);

                int placedCount = 0;
                foreach (Transform child in shapeBlocks)
                {
                    if (child.gameObject.activeSelf)
                        placedCount++;
                }
                ScoreManager.Instance.AddScore(placedCount);
                Destroy(gameObject);
            }
            
            GridManager.Instance.ClearHoverPreview();
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isDragging)
            {
                _shapeTransform.localPosition = _startPosition; 
                _shapeTransform.localScale = _shapeStartScale; 
                GridManager.Instance.ClearHoverPreview();
            }
        }
        
        private void MoveBlock(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform,
                eventData.position,
                null,
                out Vector2 localPos);
            
            _shapeTransform.localPosition = localPos + selectedOffset;
        }
        
        public ShapeData GetShapeData() => _currentShapeData;
    }
}
