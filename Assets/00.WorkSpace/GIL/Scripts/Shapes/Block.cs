using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Block : MonoBehaviour, 
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Prefab & Data")]
    public GameObject shapePrefab; // 생성할 블록 모양 Prefab
    [HideInInspector]
    public ShapeData shapeData; // Shape 데이터

    [Header("Pointer")] 
    public Vector3 shapeSelectedScale = Vector3.one * 1.2f;
    public Vector2 selectedOffset = new Vector2(0f, 1000f);
        
    private Vector3 _shapeStartScale;
    private RectTransform _shapeTransform;
    private Canvas _canvas;
    private RectTransform[,] _blocks = new RectTransform[5, 5];

    private void Awake()
    {
        _shapeStartScale = GetComponent<RectTransform>().localScale;
        _shapeTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
    }
    
    // 생성 메서드
    public void GenerateBlock(ShapeData shapeData)
    {
        if (shapePrefab == null || shapeData == null)
        {
            Debug.LogWarning("Prefab 또는 ShapeTemplate이 할당되지 않았습니다.");
            return;
        }

        RectTransform rectTransform = shapePrefab.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogWarning("ShapePrefab에 RectTransform이 없습니다.");
            return;
        }

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

                // 활성화 여부 설정
                bool isActive = shapeData.rows[y].columns[x];
                block.SetActive(isActive);
                
                ShapeBlock sb = block.AddComponent<ShapeBlock>();
                sb.x = x;
                sb.y = y;
                sb.parentBlock = this;
                
                _blocks[x, y] = rt; // 나중에 터치 처리나 색상 변경 등용
            }
        }
    }
    
    public RectTransform GetBlock(int x, int y)
    {
        return _blocks[x, y];
    }

    
    public void OnBeginDrag(PointerEventData eventData)
    {
        _shapeTransform.localScale = shapeSelectedScale;
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out pos);
        _shapeTransform.localPosition = pos + selectedOffset;
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        _shapeTransform.localScale = _shapeStartScale;

        GameEvents.CheckIfShapeCanBePlaced();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("Block 전체 클릭");
    }
}
