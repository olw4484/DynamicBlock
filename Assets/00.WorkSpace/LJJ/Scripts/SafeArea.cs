using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SafeArea : MonoBehaviour
{
    private RectTransform rectTransform;
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        Rect safeArea = Screen.safeArea;
        rectTransform.anchoredPosition = safeArea.center;
        rectTransform.sizeDelta = new Vector2(safeArea.width, safeArea.height);
    }
}
