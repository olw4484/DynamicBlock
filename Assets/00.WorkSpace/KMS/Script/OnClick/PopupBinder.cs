using UnityEngine;

public class PopupBinder : MonoBehaviour
{
    [SerializeField] GameObject target; // Info-Chiron ∑Á∆Æ
    [SerializeField] bool playSfx = true;

    void ClickSfx() { if (playSfx) Sfx.Button(); }

    public void Show() { ClickSfx(); if (target) target.SetActive(true); }
    public void Hide() { ClickSfx(); if (target) target.SetActive(false); }
    public void Toggle() { ClickSfx(); if (target) target.SetActive(!target.activeSelf); }
}
