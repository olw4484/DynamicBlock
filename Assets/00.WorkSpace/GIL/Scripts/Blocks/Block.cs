using System.Collections.Generic;
using _00.WorkSpace.GIL.Scripts.GameEvents;
using _00.WorkSpace.GIL.Scripts.Grids;
using _00.WorkSpace.GIL.Scripts.Managers;
using _00.WorkSpace.GIL.Scripts.Shapes;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _00.WorkSpace.GIL.Scripts.Blocks
{
    public class Block : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Prefab & Data")]
        public GameObject shapePrefab;

        [Header("Pointer")] 
        public Vector3 shapeSelectedScale = Vector3.one * 1.2f;
        public Vector2 selectedOffset = new Vector2(0f, 500f);
        public float editorOffset = -200f;
        private Vector3 _shapeStartScale;
        private RectTransform _shapeTransform;
        private Canvas _canvas;
        private Vector3 _startPosition;
        
        private ShapeData _currentShapeData;

        private bool _isDragging;
        private bool _startReady;

        private void Awake()
        {
            _shapeStartScale = GetComponent<RectTransform>().localScale;
            _shapeTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            _startPosition = _shapeTransform.localPosition;
            _shapeStartScale = _shapeTransform.localScale;
#if UNITY_EDITOR
            selectedOffset.y += editorOffset;
#endif
        }

        public ShapeData GetCurrentShapeData() => _currentShapeData;
        
        public void GenerateBlock(ShapeData shapeData)
        {
            if (shapeData == null)
            {
                Debug.LogError("[Block] shapeData is null");
                return;
            }

            if (_shapeTransform == null)
            {
                if (shapePrefab != null)
                    _shapeTransform = shapePrefab.GetComponent<RectTransform>();
                if (_shapeTransform == null)
                    _shapeTransform = GetComponentInChildren<RectTransform>(includeInactive: true);

                if (_shapeTransform == null)
                {
                    Debug.LogError($"[Block] _shapeTransform not found on '{name}'. " +
                                   $"Assign shapePrefab or _shapeTransform in prefab.");
                    return;
                }
            }

            if (!_startReady)
            {
                _startPosition = _shapeTransform.localPosition;
                _startReady = true;
            }

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
            if(TouchGate.GetTouchID() == int.MinValue) TouchGate.SetTouchID(eventData.pointerId);
            
            if(TouchGate.GetTouchID() != eventData.pointerId) return;
            
            BlockSpawnManager.Instance?.ClearPreview();
            
            _isDragging = false;
            
            _shapeTransform.localScale = shapeSelectedScale;

            Sfx.BlockSelect();

            MoveBlock(eventData);
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            if(TouchGate.GetTouchID() != eventData.pointerId) return;
            
            _isDragging = true;
            MoveBlock(eventData);

            var shapeBlocks = new List<Transform>();
            foreach (Transform child in transform)
                if (child.gameObject.activeSelf) shapeBlocks.Add(child);

            GridManager.Instance.UpdateHoverPreview(shapeBlocks);
        }
        
        
        
        public void OnPointerUp(PointerEventData eventData)
        {
            if(TouchGate.GetTouchID() != eventData.pointerId) return;
            
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
                Sfx.BlockPlace();

                BlockStorage storage = FindObjectOfType<BlockStorage>();
                storage.OnBlockPlaced(this);
                
                // TODO : 적절한 튜토리얼 시작 위치 옮기기
                if (MapManager.Instance.GameMode == GameMode.Tutorial)
                {
                    MapManager.Instance.OnTutorialCompleted();
                }
                
                Destroy(gameObject);
            }
            
            GridManager.Instance.ClearHoverPreview();
        }
        
        private void MoveBlock(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _shapeTransform.parent as RectTransform, 
                eventData.position,
                 _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera,
                out Vector2 localPos);
            
            _shapeTransform.anchoredPosition = localPos + (selectedOffset / _canvas.scaleFactor);
        }
        
        public ShapeData GetShapeData() => _currentShapeData;
    }
}
