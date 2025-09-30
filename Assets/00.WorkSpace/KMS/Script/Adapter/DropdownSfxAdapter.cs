using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class DropdownSfxAdapter :
    MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    [SerializeField] bool playOnOpen = true;  // 드롭다운을 '탭/엔터'로 열 때
    [SerializeField] bool playOnSelect = true;  // 항목을 선택했을 때
    // 필요 시 특정 SFX ID로 바꾸고 싶으면 -1 → 기본 버튼음
    [SerializeField] int openSfxId = -1;
    [SerializeField] int selectSfxId = -1;

    TMP_Dropdown tmp;
    Dropdown ugui;

    void Awake()
    {
        TryGetComponent(out tmp);
        TryGetComponent(out ugui);

        if (playOnSelect)
        {
            if (tmp != null) tmp.onValueChanged.AddListener(_ => PlaySelect());
            if (ugui != null) ugui.onValueChanged.AddListener(_ => PlaySelect());
        }
    }

    public void OnPointerClick(PointerEventData _)
    {
        if (playOnOpen) PlayOpen();
    }

    public void OnSubmit(BaseEventData _)
    {
        if (playOnOpen) PlayOpen();
    }

    void PlayOpen()
    {
        if (openSfxId >= 0) Sfx.PlayId(openSfxId);
        else Sfx.Button();
    }

    void PlaySelect()
    {
        if (selectSfxId >= 0) Sfx.PlayId(selectSfxId);
        else Sfx.Button();
    }
}
