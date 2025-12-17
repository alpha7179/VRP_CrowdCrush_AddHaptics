using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // 딕셔너리 안전한 순회를 위해 사용
using Unity.Tutorials.Core.Editor;
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
    None
}

public enum AMBType
{
    Crowd,
    Ambulance,
    Police,
    None
}
#endregion

#region Data Structures
[Serializable]
public struct SFXData
{
    public SFXType type;
    public AudioClip clip;
}

[Serializable]
public struct AMBData
{
    public AMBType type;
    public List<AudioClip> clips;
}

/// <summary>
/// 반복 재생되는 SFX의 상태를 관리하는 내부 클래스
/// </summary>
public class LoopingSFXContainer
{
    public AudioSource source;
    public float fadeFactor; // 0.0 ~ 1.0 (페이드 진행률)

    public LoopingSFXContainer(AudioSource src, float initialFade)
    {
        source = src;
        fadeFactor = initialFade;
    }
}
#endregion

public class AudioManager : MonoBehaviour
{
    #region Singleton
    public static AudioManager Instance { get; private set; }
    #endregion

    #region Inspector Fields
    [Header("Audio Sources")]
    [Tooltip("볼륨 참조용 (실제 소리 출력용 아님)")]
    public AudioSource volSource;
    public AudioSource narSource;
    public AudioSource sfxSource; // 단발성(OneShot) 전용
    public AudioSource ambSource;

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

    // 딕셔너리 (데이터 검색용)
    private Dictionary<SFXType, AudioClip> _sfxMap = new Dictionary<SFXType, AudioClip>();
    private Dictionary<AMBType, List<AudioClip>> _ambMap = new Dictionary<AMBType, List<AudioClip>>();

    // 활성화된 반복 SFX 관리 (Key: Type, Value: Container)
    private Dictionary<SFXType, LoopingSFXContainer> _activeLoopingSFX = new Dictionary<SFXType, LoopingSFXContainer>();

    // AMB 페이드 제어용 변수
    private Coroutine _ambCoroutine;
    private float _ambFadeFactor = 1.0f; // AMB의 현재 페이드 상태 (0~1)

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
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (DataManager.Instance == null) return;

        // 1. 현재 설정된 볼륨 값 가져오기 (0.0 ~ 1.0)
        float masterVol = (float)DataManager.Instance.GetMasterVolume() / 100f;
        float narVol = (float)DataManager.Instance.GetNARVolume() / 100f;
        float sfxVol = (float)DataManager.Instance.GetSFXVolume() / 100f;
        float ambVol = (float)DataManager.Instance.GetAMBVolume() / 100f;

        // 2. NAR 볼륨 적용 (Master * Category)
        narSource.volume = masterVol * narVol;

        // 3. SFX (OneShot) 볼륨 적용
        // 주의: PlayOneShot은 발사될 때의 볼륨을 따르지만, Source 자체 볼륨을 바꿔두면 다음 발사 시 적용됨.
        // 이미 발사된 OneShot 소리는 개별 제어가 불가능하므로, Source 볼륨을 계속 갱신해줌.
        sfxSource.volume = masterVol * sfxVol;

        // 4. AMB 볼륨 적용 (Master * Category * FadeFactor)
        // 코루틴은 _ambFadeFactor만 건드리고, 실제 볼륨 적용은 여기서 함.
        if (ambSource.isPlaying)
        {
            ambSource.volume = masterVol * ambVol * _ambFadeFactor;
        }

        // 5. Looping SFX 볼륨 적용 (Master * SFX * FadeFactor)
        if (_activeLoopingSFX.Count > 0)
        {
            // 딕셔너리 순회 중 수정 오류 방지를 위해 ToList 사용 권장되나, Update에서는 값만 바꾸므로 foreach 가능
            // 단, Coroutine에서 Remove가 발생할 수 있으므로 null 체크 필수
            foreach (var kvp in _activeLoopingSFX)
            {
                LoopingSFXContainer container = kvp.Value;
                if (container != null && container.source != null)
                {
                    container.source.volume = masterVol * sfxVol * container.fadeFactor;
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
            if (!_sfxMap.ContainsKey(item.type)) _sfxMap.Add(item.type, item.clip);
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
        // 중첩 방지: 기존 나레이션 정지
        if (narSource.isPlaying) narSource.Stop();

        narSource.clip = clip;
        narSource.Play();
        // 볼륨은 Update에서 자동 적용됨
    }

    public void StopNAR() => narSource.Stop();

    private AudioClip GetNarClip(GameScene scene, GamePhase phase, int num, int tipPage)
    {
        // (기존 분기 로직과 동일, 생략 없이 사용 가능)
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

    #region SFX Logic

    public void PlaySFX(SFXType type, bool isLoop = false, bool useFade = false)
    {
        if (!_sfxMap.ContainsKey(type)) return;
        AudioClip clip = _sfxMap[type];

        // 1. 반복 재생 (Loop)
        if (isLoop)
        {
            if (_activeLoopingSFX.ContainsKey(type)) return; // 이미 재생 중

            // 새 AudioSource 생성
            AudioSource loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.clip = clip;
            loopSource.loop = true;
            loopSource.spatialBlend = 0f; // 2D Sound
            loopSource.playOnAwake = false;

            // 컨테이너 생성 (초기 FadeFactor 설정)
            float startFactor = useFade ? 0f : 1f;
            LoopingSFXContainer container = new LoopingSFXContainer(loopSource, startFactor);

            _activeLoopingSFX.Add(type, container);
            loopSource.Play(); // 볼륨은 Update에서 0으로 시작해서 올라감

            if (useFade)
            {
                StartCoroutine(FadeLoopingSFXRoutine(type, 0f, 1f, defaultFadeTime));
            }
        }
        // 2. 단발성 재생 (OneShot)
        else
        {
            if (useFade)
            {
                // 페이드가 있는 OneShot은 제어를 위해 임시 소스 필요
                StartCoroutine(PlayOneShotWithFadeRoutine(clip, defaultFadeTime));
            }
            else
            {
                // 일반 OneShot은 Update의 볼륨 제어를 받기 위해 현재 볼륨으로 재생
                // *주의: PlayOneShot은 호출 시점의 볼륨으로 고정됨. 
                // 즉, 재생 중에 마스터 볼륨을 줄여도 이미 나간 소리는 안 줄어듦 (유니티 특징).
                // 완벽한 실시간 제어를 원하면 모든 SFX를 개별 AudioSource로 만들어야 하지만, 성능상 타협함.
                sfxSource.PlayOneShot(clip, sfxSource.volume);
            }
        }
    }

    public void StopSFX(SFXType type, bool useFade = false)
    {
        if (_activeLoopingSFX.ContainsKey(type))
        {
            if (useFade)
            {
                // Fade Out -> End
                StartCoroutine(FadeLoopingSFXRoutine(type, 1f, 0f, defaultFadeTime, true));
            }
            else
            {
                // 즉시 정지 및 제거
                RemoveLoopingSource(type);
            }
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
        // 반복 재생 중인 것들 모두 정리
        List<SFXType> keys = new List<SFXType>(_activeLoopingSFX.Keys);
        foreach (var key in keys) RemoveLoopingSource(key);
    }
    #endregion

    #region AMB Logic
    public void PlayAMB(AMBType type, int num = 0)
    {
        if (!_ambMap.ContainsKey(type)) return;
        List<AudioClip> clips = _ambMap[type];
        if (clips == null || clips.Count == 0) return;

        int idx = Mathf.Clamp(num, 0, clips.Count - 1);
        PlayAMB(clips[idx]);
    }

    public void PlayAMB(AudioClip clip)
    {
        if (ambSource.clip == clip && ambSource.isPlaying && _ambCoroutine == null) return;

        if (_ambCoroutine != null) StopCoroutine(_ambCoroutine);
        _ambCoroutine = StartCoroutine(ChangeAMBRoutine(clip, defaultFadeTime));
    }

    public void StopAMB(float duration = 1.0f)
    {
        if (_ambCoroutine != null) StopCoroutine(_ambCoroutine);
        _ambCoroutine = StartCoroutine(FadeOutAMBRoutine(duration));
    }
    #endregion

    #region Coroutines (Fade Logic - Factor Control Only)

    // AMB 교체: Fade Out Factor -> Clip Change -> Fade In Factor
    private IEnumerator ChangeAMBRoutine(AudioClip nextClip, float duration)
    {
        float halfDuration = duration * 0.5f;

        // 1. Fade Out (Factor 1 -> 0)
        if (ambSource.isPlaying)
        {
            float timer = 0f;
            float startFactor = _ambFadeFactor;
            while (timer < halfDuration)
            {
                timer += Time.deltaTime;
                _ambFadeFactor = Mathf.Lerp(startFactor, 0f, timer / halfDuration);
                yield return null;
            }
            _ambFadeFactor = 0f;
            ambSource.Stop();
        }

        // 2. Change Clip
        ambSource.clip = nextClip;
        ambSource.Play();

        // 3. Fade In (Factor 0 -> 1)
        float inTimer = 0f;
        while (inTimer < halfDuration)
        {
            inTimer += Time.deltaTime;
            _ambFadeFactor = Mathf.Lerp(0f, 1f, inTimer / halfDuration);
            yield return null;
        }
        _ambFadeFactor = 1f;
        _ambCoroutine = null;
    }

    private IEnumerator FadeOutAMBRoutine(float duration)
    {
        if (!ambSource.isPlaying) yield break;

        float timer = 0f;
        float startFactor = _ambFadeFactor;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            _ambFadeFactor = Mathf.Lerp(startFactor, 0f, timer / duration);
            yield return null;
        }

        _ambFadeFactor = 0f;
        ambSource.Stop();
        _ambCoroutine = null;
    }

    // Looping SFX Fade (Factor 조절)
    // isStopAfter: 페이드 아웃 후 파괴할지 여부
    private IEnumerator FadeLoopingSFXRoutine(SFXType type, float startFactor, float endFactor, float duration, bool isStopAfter = false)
    {
        if (!_activeLoopingSFX.ContainsKey(type)) yield break;

        LoopingSFXContainer container = _activeLoopingSFX[type];
        float timer = 0f;

        while (timer < duration)
        {
            // 도중에 소스가 사라졌으면 중단
            if (container == null || container.source == null) yield break;

            timer += Time.deltaTime;
            container.fadeFactor = Mathf.Lerp(startFactor, endFactor, timer / duration);
            yield return null;
        }

        container.fadeFactor = endFactor;

        if (isStopAfter)
        {
            RemoveLoopingSource(type);
        }
    }

    // OneShot with Fade (임시 소스 사용, 실시간 볼륨 계산 포함)
    private IEnumerator PlayOneShotWithFadeRoutine(AudioClip clip, float duration)
    {
        AudioSource tempSource = gameObject.AddComponent<AudioSource>();
        tempSource.clip = clip;
        tempSource.loop = false;
        tempSource.spatialBlend = 0f;
        tempSource.Play();

        float timer = 0f;
        float fadeDuration = Mathf.Min(duration, clip.length / 2);

        // Fade In
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float fadeFactor = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            // 실시간 볼륨 계산 (Master * SFX * Fade)
            UpdateTempSourceVolume(tempSource, fadeFactor);
            yield return null;
        }

        // Sustain
        float sustainTime = clip.length - (fadeDuration * 2);
        if (sustainTime > 0)
        {
            float sustainTimer = 0f;
            while (sustainTimer < sustainTime)
            {
                sustainTimer += Time.deltaTime;
                UpdateTempSourceVolume(tempSource, 1f); // Fade 1.0 유지
                yield return null;
            }
        }

        // Fade Out
        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float fadeFactor = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            UpdateTempSourceVolume(tempSource, fadeFactor);
            yield return null;
        }

        tempSource.Stop();
        Destroy(tempSource);
    }

    private void UpdateTempSourceVolume(AudioSource src, float fadeFactor)
    {
        if (DataManager.Instance == null) return;
        float master = (float)DataManager.Instance.GetMasterVolume() / 100f;
        float sfx = (float)DataManager.Instance.GetSFXVolume() / 100f;
        src.volume = master * sfx * fadeFactor;
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