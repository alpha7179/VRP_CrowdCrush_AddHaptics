using UnityEngine;

/// <summary>
/// 설정값(Settings)과 게임 세션 데이터(점수, 시간 등)를 영구 저장하는 매니저
/// </summary>
public class DataManager : MonoBehaviour
{
    public static DataManager Instance { get; private set; }

    [Header("User Settings")]
    [Range(0f, 1f)] public float MasterVolume = 1.0f;
    public bool IsAntiMotionSicknessMode = false;

    [Header("Session Data")]
    public int SuccessCount = 0;
    public float PlayTime = 0f;
    public string SelectedMap = "Street";

    // PlayerPrefs Keys
    private const string KEY_VOLUME = "MasterVolume";
    private const string KEY_MOTION_SICKNESS = "MotionSickness";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void InitializeSessionData()
    {
        SuccessCount = 0;
        PlayTime = 0f;
    }

    public void AddSuccessCount()
    {
        SuccessCount++;
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(KEY_VOLUME, MasterVolume);
        PlayerPrefs.SetInt(KEY_MOTION_SICKNESS, IsAntiMotionSicknessMode ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(KEY_VOLUME, 1.0f);
        IsAntiMotionSicknessMode = PlayerPrefs.GetInt(KEY_MOTION_SICKNESS, 0) == 1;
        AudioListener.volume = MasterVolume;
    }

    public void SetVolume(float volume)
    {
        MasterVolume = volume;
        AudioListener.volume = MasterVolume;
    }

    public void SetMotionSicknessMode(bool isEnabled)
    {
        IsAntiMotionSicknessMode = isEnabled;
    }
}