using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : GameManager.cs
// Desc    : 게임 상태(FSM) 관리
// ================================

public enum GameState { Ready, Playing, Paused, GameOver }

[DisallowMultipleComponent]
[AddComponentMenu("Game/GameManager")]
public class GameManager : MonoBehaviour, IManager
{
    // =====================================
    // # Fields
    // =====================================
    public static GameManager Instance { get; private set; }
    public GameState State { get; private set; }

    [SerializeField] private BoardManager _board;
    [SerializeField] private PieceManager _pieceManager;

    // =====================================
    // # Lifecycle
    // =====================================
    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PreInit() { }
    public void Init()
    {
        SetState(GameState.Ready);
    }
    public void PostInit()
    {
        EventBus.OnGameOver += () => SetState(GameState.GameOver);
    }

    // =====================================
    // # State Control
    // =====================================
    public void StartGame()
    {
        // 초기화 보장 후 시작
        // 필요 시 보드 클리어, 손패 리필 등
        SetState(GameState.Playing);
    }

    public void PauseGame()
    {
        if (State != GameState.Playing) return;
        Time.timeScale = 0f;
        SetState(GameState.Paused);
    }

    public void ResumeGame()
    {
        if (State != GameState.Paused) return;
        Time.timeScale = 1f;
        SetState(GameState.Playing);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        // 씬 리로드 또는 시스템 초기화 루틴 호출
        // 간단히는 보드/점수/손패 재초기화 함수 작성해서 재시작
        SetState(GameState.Ready);
        StartGame();
    }

    private void SetState(GameState next)
    {
        State = next;
        // 상태별 추가 훅 필요 시 분기
        // Debug.Log($"[GameManager] State -> {next}");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
