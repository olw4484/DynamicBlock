using _00.WorkSpace.GIL.Scripts.Managers;
using UnityEngine;

public sealed class RestartOnClick : MonoBehaviour
{
    public enum RestartMode { SoftReset, ReloadSceneViaEvent, ReloadSceneDirect }

    [SerializeField] RestartMode mode = RestartMode.SoftReset;
    [SerializeField] string[] closePanels = { "Options", "GameOver" }; // �ʿ��
    [SerializeField] string openPanelAfter = "Game";    // SoftReset�� �� �� �г�
    [SerializeField] string gameplayScene = "Gameplay"; // �ϵ� ���� ��� ��
    [SerializeField] float cooldown = 0.12f;
    
    [SerializeField] bool clearRunStateOnSoftReset = false;
    
    float _cool;

    void Update() { if (_cool > 0f) _cool -= Time.unscaledDeltaTime; }

    public void Invoke()
    {
        if (_cool > 0f || !Game.IsBound) return;
        _cool = cooldown;

        Sfx.Button();

        switch (mode)
        {
            case RestartMode.SoftReset:
                RestartFlow.SoftReset(openPanelAfter, closePanels);
                // GIL_Add
                // 지금은 소프트 리셋 시작이라 여기에 클래식 맵 생성 알고리즘 적용
                if (clearRunStateOnSoftReset)
                {
                    // 홈 버튼: 저장된 러닝 상태 완전 삭제
                    MapManager.Instance?.saveManager?.ClearRunState(true);
                    // 점수 UI 초기화가 필요하면(메인으로 가더라도)
                    ScoreManager.Instance?.ResetAll();
                    // 게임 화면을 열지 않으므로 새 판 강제 진입은 하지 않음
                }
                else
                {
                    // 재시작 버튼: 바로 새 클래식 판 시작
                    MapManager.Instance?.EnterClassic(MapManager.ClassicEnterPolicy.ForceNew);
                }
                break;
            case RestartMode.ReloadSceneViaEvent:
                RestartFlow.ReloadViaEvent(gameplayScene);
                break;
            case RestartMode.ReloadSceneDirect:
                RestartFlow.ReloadDirect(gameplayScene);
                break;
        }
    }
}