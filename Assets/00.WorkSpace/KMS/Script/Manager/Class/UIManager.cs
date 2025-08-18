using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

// ================================
// Project : DynamicBlock
// Script  : UIManager.cs
// Desc    : HUD / 패널 관리
// ================================

[DisallowMultipleComponent]
[AddComponentMenu("Game/UIManager")]
public class UIManager : MonoBehaviour, IManager
{
    // =====================================
    // # Fields
    // =====================================
    [Header("Text")]
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _comboText;

    [Header("Hand_Preview")]
    [SerializeField] private Transform _handRoot;
    [SerializeField] private GameObject _piecePreviewPrefab;

    [Header("Panel")]
    [SerializeField] private GameObject _gameOverPanel;

    [SerializeField] private PieceManager _pieceManager;

    private readonly List<GameObject> _handPreviews = new();

    // 캐시된 델리게이트(해제 가능)
    private Action _onGameOverHandler;

    // =====================================
    // # Lifecycle
    // =====================================
    public void PreInit()
    {
        Debug.Assert(_handRoot != null, "[UIManager] _handRoot is null");
        Debug.Assert(_piecePreviewPrefab != null, "[UIManager] _piecePreviewPrefab is null");
        Debug.Assert(_scoreText != null, "[UIManager] _scoreText is null");
        Debug.Assert(_comboText != null, "[UIManager] _comboText is null");

        if (_pieceManager == null)
            _pieceManager = FindFirstObjectByType<PieceManager>(FindObjectsInactive.Exclude);
        Debug.Assert(_pieceManager != null, "[UIManager] _pieceManager is null");
    }
    public void Init()
    {
        if (_gameOverPanel) _gameOverPanel.SetActive(false);
        if (_scoreText) _scoreText.text = "0";
        if (_comboText) _comboText.text = "0";
        RefreshHand();
    }
    public void PostInit()
    {
        EventBus.OnScoreChanged += OnScoreChanged;
        EventBus.OnComboChanged += OnComboChanged;
        EventBus.OnHandRefilled += RefreshHand;
        EventBus.OnGameOver += () => _gameOverPanel?.SetActive(true);

        _onGameOverHandler = HandleGameOver;             
        EventBus.OnGameOver += _onGameOverHandler;        
    }

    // =====================================
    // # Event Handlers
    // =====================================
    private void OnScoreChanged(int score)
    {
        if (_scoreText) _scoreText.text = score.ToString();
    }

    private void OnComboChanged(int combo)
    {
        if (_comboText) _comboText.text = combo.ToString();
    }

    private void HandleGameOver()
    {
        if (_gameOverPanel) _gameOverPanel.SetActive(true);
    }

    private void RefreshHand()
    {
        if (_pieceManager == null || _handRoot == null || _piecePreviewPrefab == null)
            return;

        // 기존 프리뷰 정리
        for (int i = _handPreviews.Count - 1; i >= 0; i--)
        {
            Destroy(_handPreviews[i]);
        }
        _handPreviews.Clear();

        // 새로 생성
        var hand = _pieceManager.CurrentHand;
        for (int i = 0; i < hand.Count; i++)
        {
            var go = Instantiate(_piecePreviewPrefab, _handRoot);
            if (go.TryGetComponent<PiecePreviewView>(out var view))
                view.SetData(hand[i]);
            _handPreviews.Add(go);
        }
    }

    private void OnDestroy()
    {
        EventBus.OnScoreChanged -= OnScoreChanged;
        EventBus.OnComboChanged -= OnComboChanged;
        EventBus.OnHandRefilled -= RefreshHand;

        if (_onGameOverHandler != null)
            EventBus.OnGameOver -= _onGameOverHandler;
    }
}
