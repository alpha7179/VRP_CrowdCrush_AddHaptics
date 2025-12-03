using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

/// <summary>
/// 게임의 전체 생명주기(Lifecycle), 씬 전환, 전역 상태(일시정지 등)를 관리하는 최상위 매니저입니다.
/// <para>
/// 1. 게임의 일시정지 및 재개 기능을 제어하고 이벤트를 발행합니다.<br/>
/// 2. SceneTransitionManager를 통해 씬 전환을 요청합니다.<br/>
/// 3. 게임 클리어 및 게임 오버 상태를 관리하고 이벤트를 전파합니다.
/// </para>
/// </summary>
public class GameManager : MonoBehaviour
{
    #region Singleton

    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        // 싱글톤 패턴: 중복 생성 방지 및 씬 전환 시 파괴 방지
        if (Instance == null)
        {
            Instance = this;
            transform.parent = null; // 최상위 계층으로 분리
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Inspector Settings

    [Header("Debug Settings")]
    [Tooltip("디버그 로그 출력 여부를 설정합니다.")]
    [SerializeField] private bool isDebug = true;

    #endregion

    #region Public State

    [Header("Game State Info")]
    /// <summary>
    /// 현재 게임이 일시정지 상태인지 여부입니다.
    /// </summary>
    public bool IsPaused = false;

    /// <summary>
    /// 현재 활성화된 씬의 이름입니다.
    /// </summary>
    public string CurrentSceneName;

    #endregion

    #region Events

    /// <summary>
    /// 일시정지 상태가 변경될 때 발생하는 이벤트입니다. (bool: isPaused)
    /// </summary>
    public event Action<bool> OnPauseStateChanged;

    /// <summary>
    /// 씬 로드가 완료되었을 때 발생하는 이벤트입니다. (string: sceneName)
    /// </summary>
    public event Action<string> OnSceneLoaded;

    /// <summary>
    /// 게임 클리어(목표 달성) 시 발생하는 이벤트입니다.
    /// </summary>
    public event Action OnGameClear;

    /// <summary>
    /// 게임 오버(실패) 시 발생하는 이벤트입니다.
    /// </summary>
    public event Action OnGameOver;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        // 유니티 내장 씬 로드 이벤트를 구독하여 상태 리셋 로직을 수행합니다.
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        // 시작 시 현재 씬 이름 저장
        CurrentSceneName = SceneManager.GetActiveScene().name;
    }

    #endregion

    #region Public API

    /// <summary>
    /// 게임의 일시정지 상태를 토글(Toggle)합니다.
    /// <para>Time.timeScale을 조절하여 물리 연산 및 시간을 멈추거나 재개합니다.</para>
    /// </summary>
    public void TogglePause()
    {
        // 인트로 씬 등 일시정지가 불필요한 씬 예외 처리
        if (CurrentSceneName.Equals("Main_Intro ", StringComparison.OrdinalIgnoreCase)) return;

        IsPaused = !IsPaused;

        // 시간 정지/재개 적용
        Time.timeScale = IsPaused ? 0f : 1f;

        // 상태 변경 이벤트 전파
        OnPauseStateChanged?.Invoke(IsPaused);

        if (isDebug) Debug.Log($"[GameManager] Pause State Changed: {IsPaused}");
    }

    /// <summary>
    /// 지정된 이름의 씬을 로드합니다.
    /// <para>SceneTransitionManager가 존재하면 페이드 효과를 사용하고, 없으면 즉시 로드합니다.</para>
    /// </summary>
    /// <param name="sceneName">이동할 씬의 이름</param>
    public void LoadScene(string sceneName)
    {
        if (SceneTransitionManager.Instance != null)
        {
            if (isDebug) Debug.Log($"[GameManager] Requesting Fade Transition to: {sceneName}");
            SceneTransitionManager.Instance.LoadScene(sceneName);
        }
        else
        {
            // 비상용 Fallback: 매니저가 없을 경우 직접 로드
            if (isDebug) Debug.LogWarning("[GameManager] SceneTransitionManager not found. Loading directly.");
            StartCoroutine(LoadSceneRoutine(sceneName));
        }
    }

    /// <summary>
    /// 게임 클리어(미션 성공) 이벤트를 발생시킵니다.
    /// </summary>
    public void TriggerGameClear()
    {
        if (isDebug) Debug.Log("[GameManager] Mission Clear!");
        OnGameClear?.Invoke();
    }

    /// <summary>
    /// 게임 오버(미션 실패) 이벤트를 발생시킵니다.
    /// </summary>
    public void TriggerGameOver()
    {
        if (isDebug) Debug.Log("[GameManager] Game Over!");
        OnGameOver?.Invoke();
    }

    /// <summary>
    /// 애플리케이션을 종료합니다. (에디터에서는 플레이 모드 중단)
    /// </summary>
    public void QuitGame()
    {
        if (isDebug) Debug.Log("[GameManager] Quitting Application...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Internal Logic

    /// <summary>
    /// 씬 로드가 완료되면 호출되는 콜백입니다. 게임 상태를 초기화합니다.
    /// </summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CurrentSceneName = scene.name;

        // 씬이 바뀌면 일시정지 해제 및 시간 정상화
        Time.timeScale = 1f;
        IsPaused = false;

        // 씬 로드 완료 이벤트 전파 (UI 갱신 등)
        OnSceneLoaded?.Invoke(scene.name);

        if (isDebug) Debug.Log($"[GameManager] Scene Loaded & State Reset: {scene.name}");
    }

    /// <summary>
    /// SceneTransitionManager가 없을 때 사용하는 비상용 씬 로드 코루틴입니다.
    /// </summary>
    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        // 로드 완료 후 처리는 HandleSceneLoaded에서 수행됩니다.
    }

    #endregion
}