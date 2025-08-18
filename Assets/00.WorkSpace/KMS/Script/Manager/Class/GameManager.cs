using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ================================
// Project : DynamicBlock
// Script  : GameManager.cs
// Desc    : ���� ����(FSM) ����
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
        // �ʱ�ȭ ���� �� ����
        // �ʿ� �� ���� Ŭ����, ���� ���� ��
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
        // �� ���ε� �Ǵ� �ý��� �ʱ�ȭ ��ƾ ȣ��
        // �������� ����/����/���� ���ʱ�ȭ �Լ� �ۼ��ؼ� �����
        SetState(GameState.Ready);
        StartGame();
    }

    private void SetState(GameState next)
    {
        State = next;
        // ���º� �߰� �� �ʿ� �� �б�
        // Debug.Log($"[GameManager] State -> {next}");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
