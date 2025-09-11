using UnityEngine;

public class AudioUIButton : MonoBehaviour
{
    // ��ư�� ���
    public void FlipBgm() => AudioUI.FlipBgm();
    public void FlipSe() => AudioUI.FlipSe();
    public void FlipVibration() => AudioUI.FlipVibration();

    // ����ġ/��۽� (OnValueChanged(bool))
    public void SetBgm(bool on) => AudioUI.SetBgm(on);
    public void SetSe(bool on) => AudioUI.SetSe(on);
    public void SetVibration(bool on) => AudioUI.SetVibration(on);

    public void OnToggleBgm(bool isOn) => AudioUI.SetBgm(isOn);
    public void OnToggleSe(bool isOn) => AudioUI.SetSe(isOn);
    public void OnToggleVibration(bool isOn) => AudioUI.SetVibration(isOn);
}
