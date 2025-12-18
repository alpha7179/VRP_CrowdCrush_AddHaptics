using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameManager;
using static GameStepManager;

#region Enums
public enum SFXType
{
    UI_Click,
    Success_Feedback,
    Fail_Feedback,
    Pause_Feedback,
    Finish_Feedback,
    heartbeat,
    breath,
    EarRinging,

    Ambulance,
    Police,

    None
}

public enum AMBType
{
    Crowd,
    None
}
#endregion

#region Data Structures
[Serializable]
public struct SFXData
{
    public SFXType type;
    public List<AudioClip> clips; // 셔플을 위해 리스트로 변경
}

[Serializable]
public struct AMBData
{
    public AMBType type;
    public List<AudioClip> clips;
}

/// <summary>
/// 반복 재생되는 SFX 관리 컨테이너
/// </summary>
public class LoopingSFXContainer
{
    public AudioSource source;
    public float fadeFactor; // 0.0 ~ 1.0 (페이드)
    public float volumeScale; // 0.0 ~ 1.0 (게임 로직상 볼륨 크기)

    public LoopingSFXContainer(AudioSource src, float initialFade)
    {
        source = src;
        fadeFactor = initialFade;
        volumeScale = 1.0f;
    }
}
#endregion

public class AudioManager : MonoBehaviour
{
    #region Singleton
    public static AudioManager Instance { get; private set; }
    #endregion

    #region Inspector Fields

    [Header("Debug Settings")]
    [SerializeField] private bool isDebug = true;

    [Header("Audio Sources")]
    public AudioSource narSource;
    public AudioSource sfxSource; // 단발성

    [Header("AMB Sources (Cross-Fade)")]
    public AudioSource ambSourceA;
    public AudioSource ambSourceB;

    [Header("Clip Data")]
    public List<SFXData> sfxList = new List<SFXData>();
    public List<AMBData> ambList = new List<AMBData>();

    [Header("NAR Clips")]
    public AudioClip[] nar_tip;
    public AudioClip[] nar_Caution;
    public AudioClip[] nar_Tutorial;
    public AudioClip[] nar_Move;
    public AudioClip[] nar_ABCPose;
    public AudioClip[] nar_HoldPillar;
    public AudioClip[] nar_ClimbUp;
    public AudioClip[] nar_Escape;

    [Header("Settings")]
    [SerializeField] private float defaultFadeTime = 1.0f;

    // 데이터 관리
    private Dictionary<SFXType, List<AudioClip>> _sfxMap = new Dictionary<SFXType, List<AudioClip>>();
    private Dictionary<AMBType, List<AudioClip>> _ambMap = new Dictionary<AMBType, List<AudioClip>>();

    // 실행 중인 루프/셔플 SFX 관리
    private Dictionary<SFXType, LoopingSFXContainer> _activeLoopingSFX = new Dictionary<SFXType, LoopingSFXContainer>();
    private Dictionary<SFXType, Coroutine> _activeShuffleRoutines = new Dictionary<SFXType, Coroutine>();

    // AMB 제어 변수
    private Coroutine _ambCrossFadeCoroutine;
    private bool _isUsingSourceA = false;
    private float _fadeFactorA = 0f;
    private float _fadeFactorB = 0f;

    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
            InitializeDictionaries();

            if (ambSourceA != null) ambSourceA.loop = true;
            if (ambSourceB != null) ambSourceB.loop = true;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (DataManager.Instance == null) return;

        float masterVol = DataManager.Instance.GetMasterVolume();
        float narVol = DataManager.Instance.GetNARVolume();
        float sfxVol = DataManager.Instance.GetSFXVolume();
        float ambVol = DataManager.Instance.GetAMBVolume();

        narSource.volume = masterVol * narVol;
        sfxSource.volume = masterVol * sfxVol;

        // AMB Volume Update
        if (ambSourceA.isPlaying) ambSourceA.volume = masterVol * ambVol * _fadeFactorA;
        if (ambSourceB.isPlaying) ambSourceB.volume = masterVol * ambVol * _fadeFactorB;

        // Looping/Shuffle SFX Volume Update
        // * 중요: Scale 값을 곱해줘야 점점 커지는 효과 적용됨
        if (_activeLoopingSFX.Count > 0)
        {
            foreach (var kvp in _activeLoopingSFX)
            {
                LoopingSFXContainer container = kvp.Value;
                if (container != null && container.source != null)
                {
                    container.source.volume = masterVol * sfxVol * container.fadeFactor * container.volumeScale;
                }
            }
        }
    }
    #endregion

    #region Initialization
    private void InitializeDictionaries()
    {
        _sfxMap.Clear();
        foreach (var item in sfxList)
        {
            if (!_sfxMap.ContainsKey(item.type)) _sfxMap.Add(item.type, item.clips);
        }

        _ambMap.Clear();
        foreach (var item in ambList)
        {
            if (!_ambMap.ContainsKey(item.type)) _ambMap.Add(item.type, item.clips);
        }
    }
    #endregion

    #region NAR Logic
    public void PlayNAR(GameScene scene, GamePhase phase = GamePhase.Null, int num = 0, int tipPage = 0)
    {
        AudioClip clip = GetNarClip(scene, phase, num, tipPage);
        if (clip != null) PlayNAR(clip);
    }

    public void PlayNAR(AudioClip clip)
    {
        if (narSource.isPlaying) narSource.Stop();
        narSource.clip = clip;
        narSource.Play();
    }

    public void StopNAR() => narSource.Stop();

    private AudioClip GetNarClip(GameScene scene, GamePhase phase, int num, int tipPage)
    {
        switch (scene)
        {
            case GameScene.Menu: return GetSafeClip(nar_tip, tipPage);
            case GameScene.Simulator:
                switch (phase)
                {
                    case GamePhase.Caution: return GetSafeClip(nar_Caution, num);
                    case GamePhase.Tutorial: return GetSafeClip(nar_Tutorial, num);
                    case GamePhase.Move1: return GetSafeClip(nar_Move, num);
                    case GamePhase.ABCPose: return GetSafeClip(nar_ABCPose, num);
                    case GamePhase.HoldPillar: return GetSafeClip(nar_HoldPillar, num);
                    case GamePhase.Move2: return GetSafeClip(nar_Move, num);
                    case GamePhase.ClimbUp: return GetSafeClip(nar_ClimbUp, num);
                    case GamePhase.Escape: return GetSafeClip(nar_Escape, num);
                }
                break;
        }
        return null;
    }

    private AudioClip GetSafeClip(AudioClip[] arr, int idx)
    {
        if (arr != null && idx >= 0 && idx < arr.Length) return arr[idx];
        return null;
    }
    #endregion

    #region SFX Logic (OneShot, Loop, Shuffle)

    // 1. 단발성 또는 단일 루프 재생
    public void PlaySFX(SFXType type, bool isLoop = false, bool useFade = false)
    {
        if (!_sfxMap.ContainsKey(type)) return;
        List<AudioClip> clips = _sfxMap[type];
        if (clips == null || clips.Count == 0) return;

        AudioClip clip = clips[0]; // 첫 번째 클립 사용

        if (isLoop)
        {
            if (_activeLoopingSFX.ContainsKey(type)) return; // 이미 재생 중

            AudioSource loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.clip = clip;
            loopSource.loop = true;
            loopSource.spatialBlend = 0f;
            loopSource.playOnAwake = false;

            float startFactor = useFade ? 0f : 1f;
            LoopingSFXContainer container = new LoopingSFXContainer(loopSource, startFactor);
            _activeLoopingSFX.Add(type, container);

            loopSource.Play();

            if (useFade) StartCoroutine(FadeLoopingSFXRoutine(type, 0f, 1f, defaultFadeTime));
        }
        else
        {
            // OneShot
            if (useFade) StartCoroutine(PlayOneShotWithFadeRoutine(clip, defaultFadeTime));
            else sfxSource.PlayOneShot(clip, sfxSource.volume);
        }
    }

    // 2. [신규] 셔플 루프 재생 (여러 클립을 랜덤하게 섞어서 무한 재생) - 경찰차용
    public void PlayShuffleSFX(SFXType type, bool useFade = true)
    {
        if (!_sfxMap.ContainsKey(type)) return;
        if (_activeLoopingSFX.ContainsKey(type)) return; // 이미 재생 중

        List<AudioClip> clips = _sfxMap[type];
        if (clips == null || clips.Count == 0) return;

        // 소스 생성
        AudioSource loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.loop = false; // 직접 제어하므로 false
        loopSource.spatialBlend = 0f;

        float startFactor = useFade ? 0f : 1f;
        LoopingSFXContainer container = new LoopingSFXContainer(loopSource, startFactor);
        _activeLoopingSFX.Add(type, container);

        // 셔플 코루틴 시작
        Coroutine routine = StartCoroutine(ShuffleSFXLogic(type, loopSource, clips));
        _activeShuffleRoutines.Add(type, routine);

        if (useFade) StartCoroutine(FadeLoopingSFXRoutine(type, 0f, 1f, defaultFadeTime));
    }

    // 셔플 로직 코루틴
    private IEnumerator ShuffleSFXLogic(SFXType type, AudioSource source, List<AudioClip> clips)
    {
        while (true)
        {
            // 랜덤 클립 선택
            AudioClip nextClip = clips[UnityEngine.Random.Range(0, clips.Count)];
            source.clip = nextClip;
            source.Play();

            // 클립 길이만큼 대기 (끝나면 다음 것 재생)
            // 0.1초 정도 겹치게 하거나 딱 맞게 대기
            yield return new WaitForSeconds(nextClip.length);

            // 안전장치: 소스가 삭제되었으면 종료
            if (source == null) yield break;
        }
    }

    public void StopSFX(SFXType type, bool useFade = false)
    {
        // 셔플 루틴이 있다면 정지
        if (_activeShuffleRoutines.ContainsKey(type))
        {
            StopCoroutine(_activeShuffleRoutines[type]);
            _activeShuffleRoutines.Remove(type);
        }

        if (_activeLoopingSFX.ContainsKey(type))
        {
            if (useFade) StartCoroutine(FadeLoopingSFXRoutine(type, 1f, 0f, defaultFadeTime, true));
            else RemoveLoopingSource(type);
        }
    }

    private void RemoveLoopingSource(SFXType type)
    {
        if (_activeLoopingSFX.TryGetValue(type, out LoopingSFXContainer container))
        {
            if (container.source != null)
            {
                container.source.Stop();
                Destroy(container.source);
            }
            _activeLoopingSFX.Remove(type);
        }
    }

    public void StopAllSFX()
    {
        sfxSource.Stop();
        List<SFXType> keys = new List<SFXType>(_activeLoopingSFX.Keys);
        foreach (var key in keys) StopSFX(key, false);
    }

    // [중요] 볼륨 크기 조절 메서드 (점점 크게 하기 위해 필수)
    public void SetLoopingSFXScale(SFXType type, float scale)
    {
        if (_activeLoopingSFX.TryGetValue(type, out var container))
        {
            // 부드럽게 목표 스케일로 이동
            StartCoroutine(SmoothVolumeScaleRoutine(container, scale, 1.0f));
        }
    }

    private IEnumerator SmoothVolumeScaleRoutine(LoopingSFXContainer container, float targetScale, float duration)
    {
        float startScale = container.volumeScale;
        float timer = 0f;
        while (timer < duration)
        {
            if (container == null) yield break;
            timer += Time.deltaTime;
            container.volumeScale = Mathf.Lerp(startScale, targetScale, timer / duration);
            yield return null;
        }
        if (container != null) container.volumeScale = targetScale;
    }

    #endregion

    #region AMB Logic (Crowd Only)
    public void PlayAMB(AMBType type, int num = 0)
    {
        if (!_ambMap.ContainsKey(type)) return;
        List<AudioClip> clips = _ambMap[type];
        if (clips == null || clips.Count == 0) return;

        int idx = Mathf.Clamp(num, 0, clips.Count - 1);
        PlayAMB(clips[idx]);
    }

    public void PlayAMB(AudioClip nextClip)
    {
        if (ambSourceA.isPlaying && ambSourceA.clip == nextClip && _isUsingSourceA) return;
        if (ambSourceB.isPlaying && ambSourceB.clip == nextClip && !_isUsingSourceA) return;

        if (_ambCrossFadeCoroutine != null) StopCoroutine(_ambCrossFadeCoroutine);

        AudioSource incoming = _isUsingSourceA ? ambSourceB : ambSourceA;
        AudioSource outgoing = _isUsingSourceA ? ambSourceA : ambSourceB;
        _isUsingSourceA = !_isUsingSourceA;

        _ambCrossFadeCoroutine = StartCoroutine(CrossFadeAMBRoutine(incoming, outgoing, nextClip, defaultFadeTime));
    }

    public void StopAMB(float duration = 1.0f)
    {
        if (_ambCrossFadeCoroutine != null) StopCoroutine(_ambCrossFadeCoroutine);
        _ambCrossFadeCoroutine = StartCoroutine(FadeOutAllAMBRoutine(duration));
    }
    #endregion

    #region Coroutines (Audio Logic)
    private IEnumerator CrossFadeAMBRoutine(AudioSource inSource, AudioSource outSource, AudioClip nextClip, float duration)
    {
        inSource.clip = nextClip;
        inSource.loop = true;
        inSource.Play();

        float timer = 0f;
        float startOutFactor = (outSource == ambSourceA) ? _fadeFactorA : _fadeFactorB;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float ratio = timer / duration;
            if (inSource == ambSourceA) _fadeFactorA = Mathf.Lerp(0f, 1f, ratio); else _fadeFactorB = Mathf.Lerp(0f, 1f, ratio);
            if (outSource == ambSourceA) _fadeFactorA = Mathf.Lerp(startOutFactor, 0f, ratio); else _fadeFactorB = Mathf.Lerp(startOutFactor, 0f, ratio);
            yield return null;
        }
        if (inSource == ambSourceA) _fadeFactorA = 1f; else _fadeFactorB = 1f;
        if (outSource == ambSourceA) _fadeFactorA = 0f; else _fadeFactorB = 0f;
        outSource.Stop();
        _ambCrossFadeCoroutine = null;
    }

    private IEnumerator FadeOutAllAMBRoutine(float duration)
    {
        float timer = 0f;
        float startA = _fadeFactorA;
        float startB = _fadeFactorB;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float ratio = timer / duration;
            _fadeFactorA = Mathf.Lerp(startA, 0f, ratio);
            _fadeFactorB = Mathf.Lerp(startB, 0f, ratio);
            yield return null;
        }
        _fadeFactorA = 0f; _fadeFactorB = 0f;
        ambSourceA.Stop(); ambSourceB.Stop();
        _ambCrossFadeCoroutine = null;
    }

    private IEnumerator FadeLoopingSFXRoutine(SFXType type, float startFactor, float endFactor, float duration, bool isStopAfter = false)
    {
        if (!_activeLoopingSFX.ContainsKey(type)) yield break;
        LoopingSFXContainer container = _activeLoopingSFX[type];
        float timer = 0f;
        while (timer < duration)
        {
            if (container == null || container.source == null) yield break;
            timer += Time.deltaTime;
            container.fadeFactor = Mathf.Lerp(startFactor, endFactor, timer / duration);
            yield return null;
        }
        container.fadeFactor = endFactor;
        if (isStopAfter) RemoveLoopingSource(type);
    }

    private IEnumerator PlayOneShotWithFadeRoutine(AudioClip clip, float duration)
    {
        AudioSource tempSource = gameObject.AddComponent<AudioSource>();
        tempSource.clip = clip;
        tempSource.loop = false;
        tempSource.spatialBlend = 0f;
        tempSource.Play();
        float timer = 0f;
        float fadeDuration = Mathf.Min(duration, clip.length / 2);
        while (timer < fadeDuration) { timer += Time.deltaTime; tempSource.volume = Mathf.Lerp(0f, 1f, timer / fadeDuration); yield return null; }
        float sustainTime = clip.length - (fadeDuration * 2);
        if (sustainTime > 0) yield return new WaitForSeconds(sustainTime);
        timer = 0f;
        while (timer < fadeDuration) { timer += Time.deltaTime; tempSource.volume = Mathf.Lerp(1f, 0f, timer / fadeDuration); yield return null; }
        tempSource.Stop(); Destroy(tempSource);
    }
    #endregion

    #region Helpers
    public void StopAllAudio()
    {
        StopNAR();
        StopAllSFX();
        StopAMB(0.5f);
    }
    #endregion
}