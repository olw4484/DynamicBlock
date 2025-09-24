using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PopupBinder : MonoBehaviour
{
    [SerializeField] GameObject target; // Info-Chiron ∑Á∆Æ

    public void Show() { if (target) target.SetActive(true); }
    public void Hide() { if (target) target.SetActive(false); }
    public void Toggle() { if (target) target.SetActive(!target.activeSelf); }
}
