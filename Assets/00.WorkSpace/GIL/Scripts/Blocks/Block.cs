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
        
        [HideInInspector] public int SpawnSlotIndex = -1; // 블록 위치 ( 0 ~ 2 )
        
        [Header("Pointer")] 
        public Vector3 shapeSelectedScale = Vector3.one * 1.2f;
        public Vector2 selectedOffset = new Vector2(0f, 500f);
        public float editorOffset = -200f;
        private Vector3 _shapeStartScale;
        private RectTransform _shapeTransform;
        private Canvas _canvas;
        private Vector3 _startPosition;
        
        private ShapeData _currentShapeData;
        private Sprite _currentSprite;
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
        
        public ShapeData GetShapeData() => _currentShapeData;
        public Sprite GetSpriteData() => _currentSprite;
        public Sprite SetSpriteData(Sprite sprite) => _currentSprite = sprite;
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

            // 1) 자식 정리: 템플릿이 이 객체의 자식이면 지우지 말 것!
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (child == shapePrefab) continue; // 템플릿 보존
                Destroy(child);
            }

            // 2) 치수는 '템플릿(프리팹 또는 템플릿 오브젝트)'에서 가져옴
            var rectTransform = shapePrefab.GetComponent<RectTransform>();
            if (rectTransform == null) { Debug.LogError("[Block] shapePrefab has no RectTransform"); return; }

            float width = rectTransform.sizeDelta.x;
            float height = rectTransform.sizeDelta.y;
            Vector2 offset = new Vector2((5 - 1) * 0.5f * width, -(5 - 1) * 0.5f * height);

            // 3) _currentSprite 방어: 없으면 템플릿의 Image라도 복사
            if (_currentSprite == null)
            {
                var tmplImg = shapePrefab.GetComponent<UnityEngine.UI.Image>();
                if (tmplImg && tmplImg.sprite) _currentSprite = tmplImg.sprite;
            }

            // 4) 셀 생성
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    var cell = Instantiate(shapePrefab, transform);
                    if (cell == null) { Debug.LogError("[Block] Instantiate returned null (template destroyed?)"); continue; }

                    var rt = cell.GetComponent<RectTransform>();
                    if (rt != null) rt.anchoredPosition = new Vector2(x * width, -y * height) - offset;

                    var img = cell.GetComponent<UnityEngine.UI.Image>();
                    if (img != null && _currentSprite != null) img.sprite = _currentSprite;

                    cell.SetActive(shapeData.rows[y].columns[x]);
                }
            }
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            if(TouchGate.GetTouchID() == int.MinValue) TouchGate.SetTouchID(eventData.pointerId);
            
            if(TouchGate.GetTouchID() != eventData.pointerId) return;
            
            BlockSpawnManager.Instance?.ClearPreview();
            
            _shapeTransform.localScale = shapeSelectedScale;

            Sfx.BlockSelect();

            MoveBlock(eventData);
        }
        
        public void OnDrag(PointerEventData eventData)
        {
            if(TouchGate.GetTouchID() != eventData.pointerId) return;
            
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
    }
}
