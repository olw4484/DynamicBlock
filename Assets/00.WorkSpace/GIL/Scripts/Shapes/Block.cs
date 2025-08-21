using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Block : MonoBehaviour, 
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Prefab & Data")]
    public GameObject shapePrefab;
    [HideInInspector]
    public ShapeData shapeData;

    [Header("Pointer")] 
    public Vector3 shapeSelectedScale = Vector3.one * 1.2f;
    public Vector2 selectedOffset = new Vector2(0f, 1000f);
        
    private Vector3 _shapeStartScale;
    private RectTransform _shapeTransform;
    private Canvas _canvas;
    private Vector3 _startPosition;
    private void Awake()
    {
        _shapeStartScale = GetComponent<RectTransform>().localScale;
        _shapeTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        _startPosition = _shapeTransform.localPosition;
    }

    
    public void GenerateBlock(ShapeData shapeData)
    {
        _shapeTransform.localPosition = _startPosition;
        CreateBlock(shapeData);
    }
    
    public void CreateBlock(ShapeData shapeData)
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

                bool isActive = shapeData.rows[y].columns[x];
                block.SetActive(isActive);
            }
        }
    }

    
    public void OnBeginDrag(PointerEventData eventData)
    {
        _shapeTransform.localScale = shapeSelectedScale;
        _startPosition = transform.position;
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
        List<Transform> shapeBlocks = new();

        foreach (Transform child in transform)
        {
            if (child.gameObject.activeSelf) 
                shapeBlocks.Add(child);
        }

        bool placed = false;
        if (GameEvents.CheckIfShapeCanBePlaced != null)
        {
            placed = GameEvents.CheckIfShapeCanBePlaced.Invoke(shapeBlocks);
        }
        
        if (!placed)
        {
            transform.position = _startPosition;
            transform.localScale = _shapeStartScale;
        }
        else
        {
            Destroy(gameObject); 
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("Block 전체 클릭");
    }
}
