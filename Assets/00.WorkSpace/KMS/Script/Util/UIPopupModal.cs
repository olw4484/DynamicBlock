using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public sealed class UIPopupModal : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RectTransform modalRoot;       // 모달 내용 루트(없으면 자기 자신)
    [SerializeField] RectTransform blockerParent;   // 보통 모달과 같은 Canvas/같은 SafeArea
    [SerializeField] Color dimColor = new Color(0, 0, 0, 0.5f);
    [SerializeField] bool closeOnBlockerClick = true;

    [Header("Background Canvases to lock (optional)")]
    [SerializeField] List<CanvasGroup> backgroundGroups;

    Image _blockerImg;

    void OnEnable()
    {
        EnsureBlocker();
        SetBackgroundInteractable(false);
    }

    void OnDisable()
    {
        RemoveBlocker();
        SetBackgroundInteractable(true);
    }

    void EnsureBlocker()
    {
        if (modalRoot == null) modalRoot = (RectTransform)transform;
        if (blockerParent == null) blockerParent = (RectTransform)transform.parent;

        // 실수 방지: Blocker Parent가 자기 자신이면 부모로 교체
        if (blockerParent == modalRoot && modalRoot.parent is RectTransform p)
            blockerParent = p;

        if (_blockerImg != null) return;

        var go = new GameObject("ModalBlocker",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;

        _blockerImg = go.GetComponent<Image>();
        _blockerImg.color = dimColor.a <= 0f ? new Color(0, 0, 0, 0.001f) : dimColor; // 완전투명 레이캐스트 이슈 예방
        _blockerImg.raycastTarget = true;

        var rt = (RectTransform)go.transform;
        rt.SetParent(blockerParent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // Blocker는 모달 바로 뒤, 모달은 그 위
        int modalIdx = modalRoot.GetSiblingIndex();
        rt.SetSiblingIndex(modalIdx);
        modalRoot.SetSiblingIndex(modalIdx + 1);

        if (closeOnBlockerClick)
        {
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => gameObject.SetActive(false));
        }
    }

    void RemoveBlocker()
    {
        if (_blockerImg != null)
        {
            Destroy(_blockerImg.gameObject);
            _blockerImg = null;
        }
    }

    void SetBackgroundInteractable(bool on)
    {
        if (backgroundGroups == null) return;
        foreach (var g in backgroundGroups)
        {
            if (g == null) continue;
            // 자기 자신/자식이면 스킵
            if (modalRoot && (g.transform == modalRoot || g.transform.IsChildOf(modalRoot)))
                continue;

            g.interactable = on;
            g.blocksRaycasts = on;  // 포인터 이벤트 완전 차단
        }
    }
}
