using UnityEngine;

/// <summary>
/// 사용자의 설정값(볼륨, 편의 기능)과 게임 세션 데이터(점수, 시간 등)를 관리하는 매니저입니다.
/// <para>
/// 1. PlayerPrefs를 사용하여 설정을 기기에 영구 저장하거나 불러옵니다.<br/>
/// 2. 게임 플레이 중 발생하는 통계 데이터(성공/실패 횟수, 플레이 시간)를 추적합니다.<br/>
/// 3. 씬이 변경되어도 데이터가 유지되도록 Singleton으로 동작합니다.
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

    // PlayerPrefs 저장 키
    private const string KEY_VOLUME = "MasterVolume";
    private const string KEY_MOTION_SICKNESS = "MotionSickness";

    #endregion

    #region User Settings

    [Header("User Settings")]
    [Tooltip("전체 마스터 볼륨 (0.0 ~ 1.0)")]
    [Range(0f, 1f)] public float MasterVolume = 1.0f;

    [Tooltip("멀미 방지 모드 활성화 여부 (FOV 축소, 비네팅 등 적용용)")]
    public bool IsAntiMotionSicknessMode = false;

    #endregion

    #region Session Data

    [Header("Session Data")]
    [Tooltip("미션 성공 횟수")]
    public int SuccessCount = 0;

    [Tooltip("실수 횟수")]
    public int MistakeCount = 0;

    [Tooltip("총 플레이 시간 (초 단위)")]
    public float PlayTime = 0f;

    [Tooltip("현재 선택된 맵 이름")]
    public string SelectedMap = "Street";

    #endregion

    #region Session Management API

    /// <summary>
    /// 새로운 게임 세션을 시작할 때 데이터를 초기화합니다.
    /// </summary>
    public void InitializeSessionData()
    {
        SuccessCount = 0;
        MistakeCount = 0;
        PlayTime = 0f;
        // SelectedMap은 유지 (로비에서 선택하고 들어왔을 수 있으므로)
        Debug.Log("[DataManager] Session Data Initialized.");
    }

    /// <summary>
    /// 성공 횟수를 1 증가시킵니다.
    /// </summary>
    public void AddSuccessCount()
    {
        SuccessCount++;
    }

    /// <summary>
    /// 실수 횟수를 1 증가시킵니다.
    /// </summary>
    public void AddMistakeCount()
    {
        MistakeCount++;
    }

    /// <summary>
    /// 플레이 시간을 누적합니다.
    /// </summary>
    /// <param name="timeToAdd">추가할 시간(초)</param>
    public void AddPlayTime(float timeToAdd)
    {
        PlayTime += timeToAdd;
        // 너무 자주 로그가 찍히는 것을 방지하려면 아래 줄은 주석 처리 가능
        // Debug.Log($"[DataManager] PlayTime updated: {PlayTime:F2} seconds");
    }

    #endregion

    #region Settings Management API

    /// <summary>
    /// 마스터 볼륨을 설정하고 즉시 적용합니다. (AudioListener 전역 볼륨 제어)
    /// </summary>
    /// <param name="volume">0.0 ~ 1.0 사이의 볼륨 값</param>
    public void SetVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        AudioListener.volume = MasterVolume;
    }

    /// <summary>
    /// 멀미 방지 모드(Anti-Motion Sickness)를 설정합니다.
    /// </summary>
    public void SetMotionSicknessMode(bool isEnabled)
    {
        IsAntiMotionSicknessMode = isEnabled;
    }

    /// <summary>
    /// 현재 설정값(볼륨, 멀미 모드)을 PlayerPrefs에 저장합니다.
    /// </summary>
    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(KEY_VOLUME, MasterVolume);
        PlayerPrefs.SetInt(KEY_MOTION_SICKNESS, IsAntiMotionSicknessMode ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log("[DataManager] Settings Saved.");
    }

    /// <summary>
    /// 저장된 설정값을 불러와 적용합니다. 저장된 값이 없으면 기본값을 사용합니다.
    /// </summary>
    public void LoadSettings()
    {
        // 기본값: 볼륨 1.0, 멀미모드 OFF(0)
        MasterVolume = PlayerPrefs.GetFloat(KEY_VOLUME, 1.0f);
        IsAntiMotionSicknessMode = PlayerPrefs.GetInt(KEY_MOTION_SICKNESS, 0) == 1;

        // 불러온 값 즉시 적용
        AudioListener.volume = MasterVolume;

        Debug.Log($"[DataManager] Settings Loaded - Volume: {MasterVolume}, Anti-Motion: {IsAntiMotionSicknessMode}");
    }

    #endregion
}