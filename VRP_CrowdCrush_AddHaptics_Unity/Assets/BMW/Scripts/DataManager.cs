using System;
using UnityEngine;

/// <summary>
/// 사용자의 설정값(볼륨, 편의 기능)과 게임 세션 데이터(점수, 시간 등)를 관리하는 매니저입니다.
/// <para>
/// 1. PlayerPrefs를 사용하여 설정을 기기에 영구 저장하거나 불러옵니다.<br/>
/// 2. 게임 플레이 중 발생하는 통계 데이터(성공/실패 횟수, 플레이 시간)를 추적합니다.<br/>
/// 3. Observer 패턴(Action)을 적용하여 볼륨 변경 시에만 이벤트를 호출합니다.
/// </para>
/// </summary>

public class DataManager : MonoBehaviour
{
    #region Singleton

    public static DataManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Constants (Keys)

    private const string KEY_MASTERVOLUME = "MasterVolume";
    private const string KEY_NARVOLUME = "NARVolume";
    private const string KEY_SFXVOLUME = "SFXVolume";
    private const string KEY_AMBVOLUME = "AMBVolume";
    private const string KEY_HAPTIC_INTENSITY = "HapticIntensity";
    private const string KEY_MOTION_SICKNESS = "MotionSickness";

    #endregion

    #region Events (Observer Pattern)

    public event Action<float> OnMasterVolumeChanged;
    public event Action<float> OnNARVolumeChanged;
    public event Action<float> OnSFXVolumeChanged;
    public event Action<float> OnAMBVolumeChanged;
    public event Action<float> OnHapticIntensityChanged;

    // 멀미 모드 변경 이벤트 (켜짐/꺼짐)
    public event Action<bool> OnMotionSicknessChanged;

    #endregion

    #region User Settings Fields

    [Header("Audio Settings")]
    [SerializeField][Range(0f, 1f)] private float MasterVolume = 1.0f;
    [SerializeField][Range(0f, 1f)] private float NARVolume = 1.0f;
    [SerializeField][Range(0f, 1f)] private float SFXVolume = 1.0f;
    [SerializeField][Range(0f, 1f)] private float AMBVolume = 1.0f;

    [Header("Haptic Settings (Vibration)")]
    [Tooltip("유저가 설정하는 진동 세기 (마스터)")]
    [SerializeField][Range(0f, 1f)] private float HapticIntensity = 1.0f;

    [Tooltip("하드웨어 진동의 최소 임계값")]
    [SerializeField][Range(0f, 1f)] private float MinHapticLimit = 0.0f;

    [Tooltip("하드웨어 진동의 최대 한계값")]
    [SerializeField][Range(0f, 1f)] private float MaxHapticLimit = 1.0f;

    [Header("Game Settings")]
    [Tooltip("멀미 방지 모드 활성화 여부")]
    public bool IsAntiMotionSicknessMode = false;

    #endregion

    #region Session Data Fields
    [Header("Session Data")]
    [SerializeField] private int SuccessCount = 0;
    [SerializeField] private int MistakeCount = 0;
    [SerializeField] private float PlayTime = 0f;
    [SerializeField] private string SelectedMap = null;
    #endregion

    #region Session Management API
    public void InitializeSessionData() { SuccessCount = 0; MistakeCount = 0; PlayTime = 0f; Debug.Log("[DataManager] Session Data Initialized."); }
    public void AddSuccessCount() => SuccessCount++;
    public int GetSuccessCount() => SuccessCount;
    public void AddMistakeCount() => MistakeCount++;
    public int GetMistakeCount() => MistakeCount;
    public void AddPlayTime(float timeToAdd) => PlayTime += timeToAdd;
    public float GetPlayTime() => PlayTime;
    public void SetSelectedMap(string value) => SelectedMap = value;
    public string GetSelectedMap() => SelectedMap;
    #endregion

    #region Settings Management API

    // --- Audio Setters ---
    public void SetMasterVolume(float volume)
    {
        float newValue = Mathf.Clamp01(volume);
        if (Mathf.Approximately(MasterVolume, newValue)) return;
        MasterVolume = newValue;
        OnMasterVolumeChanged?.Invoke(MasterVolume);
    }
    public float GetMasterVolume() => MasterVolume;

    public void SetNARVolume(float volume)
    {
        float newValue = Mathf.Clamp01(volume);
        if (Mathf.Approximately(NARVolume, newValue)) return;
        NARVolume = newValue;
        OnNARVolumeChanged?.Invoke(NARVolume);
    }
    public float GetNARVolume() => NARVolume;

    public void SetSFXVolume(float volume)
    {
        float newValue = Mathf.Clamp01(volume);
        if (Mathf.Approximately(SFXVolume, newValue)) return;
        SFXVolume = newValue;
        OnSFXVolumeChanged?.Invoke(SFXVolume);
    }
    public float GetSFXVolume() => SFXVolume;

    public void SetAMBVolume(float volume)
    {
        float newValue = Mathf.Clamp01(volume);
        if (Mathf.Approximately(AMBVolume, newValue)) return;
        AMBVolume = newValue;
        OnAMBVolumeChanged?.Invoke(AMBVolume);
    }
    public float GetAMBVolume() => AMBVolume;

    // --- Haptic Setters & Logic ---
    public void SetHapticIntensity(float intensity)
    {
        float newValue = Mathf.Clamp01(intensity);
        if (Mathf.Approximately(HapticIntensity, newValue)) return;
        HapticIntensity = newValue;
        OnHapticIntensityChanged?.Invoke(HapticIntensity);
    }
    public float GetHapticIntensity() => HapticIntensity;

    public float GetAdjustedHapticStrength(float rawInputStrength)
    {
        float input = Mathf.Clamp01(rawInputStrength);
        float effectiveMax = MaxHapticLimit * HapticIntensity;
        float effectiveMin = (HapticIntensity > 0.01f) ? MinHapticLimit : 0f;
        return Mathf.Lerp(effectiveMin, effectiveMax, input);
    }

    // --- Other Settings (멀미 모드) ---
    public void SetMotionSicknessMode(bool isEnabled)
    {
        if (IsAntiMotionSicknessMode == isEnabled) return;
        IsAntiMotionSicknessMode = isEnabled;
        OnMotionSicknessChanged?.Invoke(IsAntiMotionSicknessMode);
    }

    // --- Save & Load ---
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(KEY_MASTERVOLUME, MasterVolume);
        PlayerPrefs.SetFloat(KEY_NARVOLUME, NARVolume);
        PlayerPrefs.SetFloat(KEY_SFXVOLUME, SFXVolume);
        PlayerPrefs.SetFloat(KEY_AMBVOLUME, AMBVolume);
        PlayerPrefs.SetFloat(KEY_HAPTIC_INTENSITY, HapticIntensity);
        PlayerPrefs.SetInt(KEY_MOTION_SICKNESS, IsAntiMotionSicknessMode ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log("[DataManager] Settings Saved.");
    }

    public void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(KEY_MASTERVOLUME, 1.0f);
        NARVolume = PlayerPrefs.GetFloat(KEY_NARVOLUME, 1.0f);
        SFXVolume = PlayerPrefs.GetFloat(KEY_SFXVOLUME, 1.0f);
        AMBVolume = PlayerPrefs.GetFloat(KEY_AMBVOLUME, 1.0f);
        HapticIntensity = PlayerPrefs.GetFloat(KEY_HAPTIC_INTENSITY, 1.0f);
        IsAntiMotionSicknessMode = PlayerPrefs.GetInt(KEY_MOTION_SICKNESS, 0) == 1;

        Debug.Log($"[DataManager] Settings Loaded. Haptic: {HapticIntensity}, AntiMotion: {IsAntiMotionSicknessMode}");
    }

    #endregion
}