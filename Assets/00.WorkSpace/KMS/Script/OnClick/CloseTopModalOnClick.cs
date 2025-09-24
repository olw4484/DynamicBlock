using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CloseTopModalOnClick : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData _)
    {
        if (!Game.IsBound || Game.UI == null) return;
        Sfx.Button();
        Game.UI.CloseTopModal();
    }
}
