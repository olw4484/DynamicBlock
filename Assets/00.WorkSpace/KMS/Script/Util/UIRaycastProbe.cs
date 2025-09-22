using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIRaycastProbe : MonoBehaviour
{
    GraphicRaycaster[] _raycasters;

    void Awake()
    {
        _raycasters = FindObjectsOfType<GraphicRaycaster>(includeInactive: true);
        Debug.Log($"[Probe] raycasters={_raycasters.Length}");
    }

    void Update()
    {
        // ����� ��ġ �켱, ������ ���콺 �ٿ�
        bool pressed = false;
        Vector2 pos = default;

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            pressed = true;
            pos = Input.GetTouch(0).position;
        }
        else if (Input.GetMouseButtonDown(0))
        {
            pressed = true;
            pos = Input.mousePosition;
        }

        if (!pressed) return;

        var es = EventSystem.current;
        if (!es) { Debug.Log("[Probe] EventSystem ����"); return; }

        var ped = new PointerEventData(es) { position = pos };
        var results = new List<RaycastResult>();

        Debug.Log($"[Probe] === tap {pos} ===");
        foreach (var rc in _raycasters)
        {
            if (!rc || !rc.isActiveAndEnabled) continue;
            results.Clear();
            rc.Raycast(ped, results);
            foreach (var r in results)
            {
                // ���� ������ ������� ���� (���� ���� �� ����)
                var cg = r.gameObject.GetComponentInParent<CanvasGroup>();
                var ray = r.gameObject.TryGetComponent<Graphic>(out var g) ? g.raycastTarget : (bool?)null;
                Debug.Log($"[Probe] hit={r.gameObject.name} sort={r.sortingOrder} " +
                          $"alpha={(cg ? cg.alpha : 1f)} inter={(cg ? cg.interactable : true)} ray={(cg ? cg.blocksRaycasts : true)} " +
                          $"graphicRT={(ray?.ToString() ?? "n/a")}");
            }
        }
    }
}
