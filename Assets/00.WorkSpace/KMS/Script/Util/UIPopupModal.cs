using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public sealed class UIPopupModal : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] RectTransform modalRoot;       // ��� ���� ��Ʈ(������ �ڱ� �ڽ�)
    [SerializeField] RectTransform blockerParent;   // ���� ��ް� ���� Canvas/���� SafeArea
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

        // �Ǽ� ����: Blocker Parent�� �ڱ� �ڽ��̸� �θ�� ��ü
        if (blockerParent == modalRoot && modalRoot.parent is RectTransform p)
            blockerParent = p;

        if (_blockerImg != null) return;

        var go = new GameObject("ModalBlocker",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;

        _blockerImg = go.GetComponent<Image>();
        _blockerImg.color = dimColor.a <= 0f ? new Color(0, 0, 0, 0.001f) : dimColor; // �������� ����ĳ��Ʈ �̽� ����
        _blockerImg.raycastTarget = true;

        var rt = (RectTransform)go.transform;
        rt.SetParent(blockerParent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // Blocker�� ��� �ٷ� ��, ����� �� ��
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
            // �ڱ� �ڽ�/�ڽ��̸� ��ŵ
            if (modalRoot && (g.transform == modalRoot || g.transform.IsChildOf(modalRoot)))
                continue;

            g.interactable = on;
            g.blocksRaycasts = on;  // ������ �̺�Ʈ ���� ����
        }
    }
}
