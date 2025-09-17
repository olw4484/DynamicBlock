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
    const string KeyBgmVol = "BGMVolume";
    const string KeySeVol = "SEVolume";
    const string KeyBgmLastOn = "LastOn_BGMVolume";
    const string KeySeLastOn = "LastOn_SEVolume";
    const string KeyVib = "Vibration";
    public static AudioManager Instance { get; private set; }
    [Header("Audio Settings")]
    [SerializeField] private int sePoolSize = 10; // 동시에 재생 가능한 SE 수
    [Range(0f, 1f)] public float BGMVolume = 1f;
    [Range(0f, 1f)] public float SEVolume = 1f;
    public bool VibrateEnabled = true;

    [Header("1001 Click Button")]
    public AudioClip SE_Button;

    [Header("1002 Classic Start")]
    public AudioClip SE_Classic_StageEnter;

    [Header("1003 ")]

    [Header("1004 Classic Game Over")]
    public AudioClip SE_Classic_GameOver;

    [Header("1005 Classic New record")]
    public AudioClip SE_Classic_NewRecord;

    [Header("1006 Adven Start")]
    public AudioClip SE_Adven_StageEnter;

    [Header("1007 Adven Fail")]
    public AudioClip SE_Adven_Fail;

    [Header("1008 Adven Clear")]
    public AudioClip SE_Adven_Clear;

    [Header("1009 Pick Block")]
    public AudioClip SE_BlockSelect;

    [Header("1010 Place Block")]
    public AudioClip SE_BlockPlace;

    [Header("1011~1018 Clear Combo (1~8 over)")]
    public AudioClip[] SE_ClearCombo = new AudioClip[8];

    [Header("1019 Continue Time Check")]
    public AudioClip SE_ContinueTimeCheck;

    [Header("1020~1023 Line Clear SE (1~6Line)")]
    public AudioClip[] SE_LineClear = new AudioClip[6];

    [Header("1024 Clear All Block")]
    public AudioClip SE_ClearAllBlock;

    [Header("BGM Clips")]
    public AudioClip BGM_Main;
    public AudioClip BGM_Adventure;
    private AudioSource continueTickSrc;
    private AudioSource bgmSource;
    private List<AudioSource> sePool = new List<AudioSource>();

    // 초기화
    private void Awake()
    {
        Debug.Log($"[AudioManager] Awake: BGMVol={BGMVolume}, SEVol={SEVolume}");
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
        VibrateEnabled = PlayerPrefs.GetInt(KeyVib, 1) == 1;
        ApplyVolume();
    }
    // 앱 포커스/일시정지 시 BGM 일시정지
    private void OnApplicationPause(bool pause)
    {
        if (pause) bgmSource.Pause();
        else bgmSource.UnPause();
    }
    // 앱 포커스/일시정지 시 BGM 일시정지
    private void OnApplicationFocus(bool focus)
    {
        if (!focus) bgmSource.Pause();
        else bgmSource.UnPause();
    }

    #region BGM
    // 동일 BGM이 재생 중이면 무시
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) { Debug.LogWarning("[BGM] null clip"); return; }
        Debug.Log($"[BGM] play {clip.name} vol={BGMVolume}");
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;
        bgmSource.clip = clip;
        bgmSource.volume = BGMVolume;
        bgmSource.loop = true;
        bgmSource.mute = false;
        bgmSource.Play();
    }
    // BGM 정지
    public void StopBGM() => bgmSource.Stop();
    // BGM 볼륨 설정 (0~1)
    public void SetBGMVolume(float volume)
    {
        float prev = BGMVolume;
        BGMVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("BGMVolume", BGMVolume);
        if (BGMVolume > 0f) PlayerPrefs.SetFloat("LastOn_BGMVolume", BGMVolume);
        ApplyVolume();
        Debug.Log($"[BGM] SetBGMVolume {prev} -> {BGMVolume}\n{System.Environment.StackTrace}");
    }
    #endregion

    #region SE
    // 사용 가능한 AudioSource가 있으면 재생, 없으면 새로 생성
    public void PlaySE(AudioClip clip, bool vibrate = false)
    {
        if (clip == null) return;

        foreach (var src in sePool)
        {
            if (!src.isPlaying)
            {
                src.clip = clip;
                src.volume = SEVolume;
                src.mute = false;                
                src.spatialBlend = 0f;            
                src.Play();
                Debug.Log($"[SE] play {clip.name} vol={src.volume}");
                if (vibrate) TryVibrate();
                return;
            }
        }
        Debug.LogWarning("[SE] no free source in pool");

        // 풀에 여유가 없으면 새로 생성
        var extra = gameObject.AddComponent<AudioSource>();
        extra.loop = false;
        extra.volume = SEVolume;
        extra.clip = clip;
        extra.Play();
        sePool.Add(extra);

        if (vibrate) TryVibrate();
    }
    // SE 볼륨 설정 (0~1)
    public void SetSEVolume(float volume)
    {
        float prev = SEVolume;
        SEVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("SEVolume", SEVolume);
        if (SEVolume > 0f) PlayerPrefs.SetFloat("LastOn_SEVolume", SEVolume);
        ApplyVolume();
        Debug.Log($"[SE ] SetSEVolume {prev} -> {SEVolume}\n{System.Environment.StackTrace}");
    }
    // ON/OFF 토글 전용
    public void SetBgmOn(bool on)
    {
        if (on)
        {
            float last = PlayerPrefs.GetFloat(KeyBgmLastOn, 1f);
            SetBGMVolume(last);
        }
        else
        {
            SetBGMVolume(0f);
        }
    }
    public void SetSeOn(bool on)
    {
        if (on) 
        { 
            SetSEVolume(PlayerPrefs.GetFloat(KeySeLastOn, 1f));
        }
        else
        {
            SetSEVolume(0f);
        }
    }

    #endregion
    // 볼륨 적용
    private void ApplyVolume()
    {
        if (bgmSource) bgmSource.volume = BGMVolume;
        foreach (var src in sePool) src.volume = SEVolume;

        if (continueTickSrc)
        {
            continueTickSrc.volume = SEVolume;
            continueTickSrc.mute = SEVolume <= 0f;
        }
    }
    public void SetVibrateEnabled(bool on) 
    {
        VibrateEnabled = on;
        PlayerPrefs.SetInt(KeyVib, on ? 1 : 0);
    }
    #region Helpers for Game Events
    // 줄 클리어 SE 재생 (1~6줄)
    public void PlayLineClearSE(int lineCount)
    {
        if (lineCount <= 0) return;
        if (lineCount > SE_LineClear.Length) lineCount = SE_LineClear.Length;
        PlaySE(SE_LineClear[lineCount - 1]);
    }
    // 클리어 콤보 SE 재생 (1~8콤보)
    public void PlayClearComboSE(int comboCount)
    {
        if (comboCount <= 0) return;
        if (comboCount > SE_ClearCombo.Length) comboCount = SE_ClearCombo.Length;
        PlaySE(SE_ClearCombo[comboCount - 1], vibrate: true);
    }
    // 기타 게임 이벤트용 SE 재생 헬퍼
    public void PlayClassicStageEnterSE() => PlaySE(SE_Classic_StageEnter);
    // Game Over SE
    public void PlayClassicGameOverSE() => PlaySE(SE_Classic_GameOver, vibrate: true);
    // New Record SE
    public void PlayClassicNewRecordSE() => PlaySE(SE_Classic_NewRecord, vibrate: true);
    // Adventure Fail SE
    public void PlayAdvenFailSE() => PlaySE(SE_Adven_Fail, vibrate: true);
    // Adventure Clear SE
    public void PlayAdvenClearSE() => PlaySE(SE_Adven_Clear, vibrate: true);
    // Clear All Block SE
    public void PlayClearAllBlockSE() => PlaySE(SE_ClearAllBlock, vibrate: true);
    // Continue Time Check SE
    public void PlayContinueTimeCheckSE()
    {
        if (!SE_ContinueTimeCheck)
        {
            Debug.LogWarning("[Audio] SE_ContinueTimeCheck clip is null");
            return;
        }
        var src = EnsureContinueTickSource();

        // 이미 같은 클립이 재생 중이면 스킵
        if (src.isPlaying && src.clip == SE_ContinueTimeCheck) return;

        src.clip = SE_ContinueTimeCheck;
        src.loop = true;
        src.mute = SEVolume <= 0f;
        src.Play();
        Debug.Log("[Audio] ContinueTimeCheck start");
    }
    // Stop Time Check SE
    public void StopContinueTimeCheckSE()
    {
        if (!continueTickSrc) return;
        if (continueTickSrc.isPlaying) continueTickSrc.Stop();
        continueTickSrc.clip = null;
        Debug.Log("[Audio] ContinueTimeCheck stop");
    }
    // Block Select SE
    public void PlayBlockSelectSE() => PlaySE(SE_BlockSelect);
    // Block Place SE
    public void PlayBlockPlaceSE() => PlaySE(SE_BlockPlace, vibrate: true);
    // Adventure Stage Enter SE
    public void PlayStageEnterSE() => PlaySE(SE_Adven_StageEnter);
    // Button Click SE
    public void PlayButtonClickSE() => PlaySE(SE_Button, vibrate: true);
    #endregion
    static void TryVibrate()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    if (Instance != null && !Instance.VibrateEnabled) return;
        UnityEngine.Handheld.Vibrate();
#else
        // 모바일이 아니면 무시 (필요시 로그)
        // Debug.Log("[Vibrate] skipped (not mobile build)");
#endif
    }
    public bool IsBgmOn => BGMVolume > 0.0001f;
    public bool IsSeOn => SEVolume > 0.0001f;
    public void StopAllSe()
    {
        AudioListener.pause = false;
        if (sePool != null) foreach (var s in sePool) if (s) s.Stop();
        if (continueTickSrc) continueTickSrc.Stop();
    }
    public void PauseAll()
    {
        AudioListener.pause = true;
        if (bgmSource) bgmSource.Pause();
        if (sePool != null) foreach (var s in sePool) if (s) s.Pause();
        if (continueTickSrc) continueTickSrc.Pause();
    }
    public void ResumeAll()
    {
        AudioListener.pause = false;
        if (bgmSource) bgmSource.UnPause();
        if (sePool != null) foreach (var s in sePool) if (s) s.UnPause();
        if (continueTickSrc) continueTickSrc.UnPause();
    }
    private AudioSource EnsureContinueTickSource()
    {
        if (!continueTickSrc)
        {
            continueTickSrc = gameObject.AddComponent<AudioSource>();
            continueTickSrc.loop = true;
            continueTickSrc.playOnAwake = false;
            continueTickSrc.spatialBlend = 0f;
        }
        // 볼륨/뮤트는 현재 SE 설정에 추종
        continueTickSrc.volume = SEVolume;
        continueTickSrc.mute = SEVolume <= 0f;
        return continueTickSrc;
    }
}