using System; // Action 사용을 위해 추가
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
            transform.parent = null; // 최상위 계층으로 분리
            DontDestroyOnLoad(gameObject);

            // 초기화 시 저장된 설정을 불러옵니다.
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
    private const string KEY_MOTION_SICKNESS = "MotionSickness";

    #endregion

    #region Events (Observer Pattern)

    // 볼륨이 변경될 때 구독자들에게 알리기 위한 이벤트 정의
    public event Action<float> OnMasterVolumeChanged;
    public event Action<float> OnNARVolumeChanged;
    public event Action<float> OnSFXVolumeChanged;
    public event Action<float> OnAMBVolumeChanged;

    // 멀미 모드 변경 이벤트
    public event Action<bool> OnMotionSicknessChanged;

    #endregion

    #region User Settings Fields

    [Header("User Settings")]
    [Tooltip("전체 마스터 볼륨 (0.0 ~ 1.0)")]
    [SerializeField][Range(0f, 1f)] private float MasterVolume = 1.0f;
    [SerializeField][Range(0f, 1f)] private float NARVolume = 1.0f;
    [SerializeField][Range(0f, 1f)] private float SFXVolume = 1.0f;
    [SerializeField][Range(0f, 1f)] private float AMBVolume = 1.0f;

    [Tooltip("멀미 방지 모드 활성화 여부")]
    public bool IsAntiMotionSicknessMode = false;

    #endregion

    #region Session Data Fields

    [Header("Session Data")]
    [Tooltip("미션 성공 횟수")]
    [SerializeField] private int SuccessCount = 0;

    [Tooltip("실수 횟수")]
    [SerializeField] private int MistakeCount = 0;

    [Tooltip("총 플레이 시간 (초 단위)")]
    [SerializeField] private float PlayTime = 0f;

    [Tooltip("현재 선택된 맵 이름")]
    [SerializeField] private string SelectedMap = null;

    #endregion

    #region Session Management API

    public void InitializeSessionData()
    {
        SuccessCount = 0;
        MistakeCount = 0;
        PlayTime = 0f;
        // SelectedMap은 유지
        Debug.Log("[DataManager] Session Data Initialized.");
    }

    public void AddSuccessCount() => SuccessCount++;
    public int GetSuccessCount() => SuccessCount;

    public void AddMistakeCount() => MistakeCount++;
    public int GetMistakeCount() => MistakeCount;

    public void AddPlayTime(float timeToAdd)
    {
        PlayTime += timeToAdd;
    }
    public float GetPlayTime() => PlayTime;

    public void SetSelectedMap(string value) => SelectedMap = value;
    public string GetSelectedMap() => SelectedMap;

    #endregion

    #region Settings Management API (Event-Driven)

    // Setter 내에서 값이 변경될 때만 Event를 Invoke 하도록 수정

    public void SetMasterVolume(float volume)
    {
        float newValue = Mathf.Clamp01(volume);
        if (Mathf.Approximately(MasterVolume, newValue)) return; // 값의 변화가 없으면 리턴

        MasterVolume = newValue;
        OnMasterVolumeChanged?.Invoke(MasterVolume); // 구독자들에게 알림
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


    public void SetMotionSicknessMode(bool isEnabled)
    {
        if (IsAntiMotionSicknessMode == isEnabled) return;

        IsAntiMotionSicknessMode = isEnabled;
        OnMotionSicknessChanged?.Invoke(IsAntiMotionSicknessMode);
    }

    /// <summary>
    /// 현재 설정값(볼륨, 멀미 모드)을 PlayerPrefs에 저장합니다.
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(KEY_MASTERVOLUME, MasterVolume);

        // [버그 수정 완료] 기존 코드에서 MasterVolume을 저장하던 오류 수정
        PlayerPrefs.SetFloat(KEY_NARVOLUME, NARVolume);
        PlayerPrefs.SetFloat(KEY_SFXVOLUME, SFXVolume);
        PlayerPrefs.SetFloat(KEY_AMBVOLUME, AMBVolume);

        PlayerPrefs.SetInt(KEY_MOTION_SICKNESS, IsAntiMotionSicknessMode ? 1 : 0);

        PlayerPrefs.Save();
        Debug.Log("[DataManager] Settings Saved.");
    }

    /// <summary>
    /// 저장된 설정값을 불러와 적용합니다.
    /// </summary>
    public void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(KEY_MASTERVOLUME, 1.0f);
        NARVolume = PlayerPrefs.GetFloat(KEY_NARVOLUME, 1.0f);
        SFXVolume = PlayerPrefs.GetFloat(KEY_SFXVOLUME, 1.0f);
        AMBVolume = PlayerPrefs.GetFloat(KEY_AMBVOLUME, 1.0f);
        IsAntiMotionSicknessMode = PlayerPrefs.GetInt(KEY_MOTION_SICKNESS, 0) == 1;

        Debug.Log($"[DataManager] Settings Loaded - Master: {MasterVolume}, NAR: {NARVolume}, SFX: {SFXVolume}, AMB: {AMBVolume}");

        // 로드 직후 초기값을 반영하고 싶다면 여기서 Invoke를 호출할 수도 있지만, 
        // 보통 리스너(AudioManager 등)가 Start에서 Get함수를 한 번 호출하여 초기화하는 것이 안전합니다.
    }

    #endregion
}