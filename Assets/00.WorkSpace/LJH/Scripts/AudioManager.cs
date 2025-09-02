using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AudioManager
/// - BGM/SE 풀링 관리
/// - 볼륨 저장
/// - 진동 연동
/// - Inspector에서 모든 AudioClip 관리
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM Clips")]
    public AudioClip BGM_Main;
    public AudioClip BGM_Adventure;

    [Header("Line Clear SE (1~6줄)")]
    public AudioClip[] SE_LineClear = new AudioClip[6];

    [Header("Block SE")]
    public AudioClip SE_BlockSelect;
    public AudioClip SE_BlockPlace;

    [Header("Stage SE")]
    public AudioClip SE_Adven_StageEnter;

    [Header("Button SE")]
    public AudioClip SE_Button;

    [Header("Audio Settings")]
    [SerializeField] private int sePoolSize = 10; // 동시에 재생 가능한 SE 수
    [Range(0f, 1f)] public float BGMVolume = 1f;
    [Range(0f, 1f)] public float SEVolume = 1f;

    private AudioSource bgmSource;
    private List<AudioSource> sePool = new List<AudioSource>();

    private void Awake()
    {
        // 싱글톤 보장
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // BGM Source 생성
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.volume = BGMVolume;

        // SE 풀 생성
        for (int i = 0; i < sePoolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.loop = false;
            src.volume = SEVolume;
            sePool.Add(src);
        }

        // PlayerPrefs에서 볼륨 불러오기
        BGMVolume = PlayerPrefs.GetFloat("BGMVolume", BGMVolume);
        SEVolume = PlayerPrefs.GetFloat("SEVolume", SEVolume);
        ApplyVolume();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) bgmSource.Pause();
        else bgmSource.UnPause();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) bgmSource.Pause();
        else bgmSource.UnPause();
    }

    #region BGM
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;
        bgmSource.clip = clip;
        bgmSource.volume = BGMVolume;
        bgmSource.Play();
    }

    public void StopBGM() => bgmSource.Stop();

    public void SetBGMVolume(float volume)
    {
        BGMVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("BGMVolume", BGMVolume);
        ApplyVolume();
    }
    #endregion

    #region SE
    public void PlaySE(AudioClip clip, bool vibrate = false)
    {
        if (clip == null) return;

        foreach (var src in sePool)
        {
            if (!src.isPlaying)
            {
                src.clip = clip;
                src.volume = SEVolume;
                src.Play();
                if (vibrate) Handheld.Vibrate();
                return;
            }
        }

        // 풀에 여유가 없으면 새로 생성
        var extra = gameObject.AddComponent<AudioSource>();
        extra.loop = false;
        extra.volume = SEVolume;
        extra.clip = clip;
        extra.Play();
        sePool.Add(extra);

        if (vibrate) Handheld.Vibrate();
    }

    public void SetSEVolume(float volume)
    {
        SEVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("SEVolume", SEVolume);
        ApplyVolume();
    }
    #endregion

    private void ApplyVolume()
    {
        if (bgmSource != null) bgmSource.volume = BGMVolume;
        foreach (var src in sePool)
            src.volume = SEVolume;
    }

    #region Helpers for Game Events
    public void PlayLineClearSE(int lineCount)
    {
        if (lineCount <= 0) return;
        if (lineCount > SE_LineClear.Length) lineCount = SE_LineClear.Length;
        PlaySE(SE_LineClear[lineCount - 1]);
    }

    public void PlayBlockSelectSE() => PlaySE(SE_BlockSelect);
    public void PlayBlockPlaceSE() => PlaySE(SE_BlockPlace, vibrate: true);
    public void PlayStageEnterSE() => PlaySE(SE_Adven_StageEnter);
    public void PlayButtonClickSE() => PlaySE(SE_Button, vibrate: true);
    #endregion
}