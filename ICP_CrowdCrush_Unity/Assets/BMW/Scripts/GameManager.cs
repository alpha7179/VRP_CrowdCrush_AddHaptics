using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

/// <summary>
/// 게임의 전체 생명주기, 씬 전환, 전역 상태(일시정지 등)를 관리하는 최상위 매니저원
/// </summary>
public class GameManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    public static GameManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool isDebug = true;

    [Header("Game State Info")]
    public bool IsPaused = false;       // 현재 일시정지 상태 여부
    public string CurrentSceneName;     // 현재 활성화된 씬 이름

    // --- 전역 이벤트 (다른 매니저들이 구독) ---
    public event Action<bool> OnPauseStateChanged;      // 일시정지 상태 변경 시 발생
    public event Action<string> OnSceneLoaded;          // 씬 로드 완료 시 발생 (초기화용)
    public event Action OnGameClear;                    // 게임 클리어 (탈출 성공) 시 발생
    public event Action OnGameOver;                     // 게임 오버 (실패) 시 발생

    private void Awake()
    {
        // 싱글톤 패턴: 중복 생성 방지 및 씬 전환 시 파괴 방지
        if (Instance == null)
        {
            Instance = this;
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 시작 시 현재 씬 이름 저장
        CurrentSceneName = SceneManager.GetActiveScene().name;
    }

    // [추가] 유니티 내장 씬 로드 이벤트를 구독하여, 누가 로드하든 확실하게 상태를 리셋함
    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    // 게임 일시정지/재개 토글
    public void TogglePause()
    {
        // 인트로 씬 등 일시정지가 필요 없는 씬 예외 처리
        if (CurrentSceneName.Equals("IntroScene", StringComparison.OrdinalIgnoreCase)) return;

        IsPaused = !IsPaused;

        // 물리 연산 및 시간 정지/재개 (TimeScale 조절)
        Time.timeScale = IsPaused ? 0f : 1f;

        // 이벤트 발생
        OnPauseStateChanged?.Invoke(IsPaused);

        if (isDebug) Debug.Log($"[GameManager] Pause State: {IsPaused}");
    }

    // 씬 전환 요청 (SceneTransitionManager에 위임)
    public void LoadScene(string sceneName)
    {
        // 페이드 매니저가 있으면 페이드 효과와 함께 전환
        if (SceneTransitionManager.Instance != null)
        {
            if (isDebug) Debug.Log($"[GameManager] Requesting Fade Transition to: {sceneName}");
            SceneTransitionManager.Instance.LoadScene(sceneName);
        }
        else
        {
            // 없으면 기존 방식대로 직접 전환 (비상용 Fallback)
            if (isDebug) Debug.LogWarning("[GameManager] SceneTransitionManager not found. Loading directly.");
            StartCoroutine(LoadSceneRoutine(sceneName));
        }
    }

    // 비상용 직접 로드 루틴 (SceneTransitionManager가 없을 때만 사용됨)
    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        // 로드 완료 처리는 HandleSceneLoaded 이벤트에서 일괄 처리됨
    }

    // 씬 로드가 완료되면 자동으로 호출되는 콜백 함수
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CurrentSceneName = scene.name;

        // 씬 전환 시 무조건 게임 상태 초기화
        Time.timeScale = 1f;
        IsPaused = false;

        // 씬 로드 완료 이벤트 전파
        OnSceneLoaded?.Invoke(scene.name);

        if (isDebug) Debug.Log($"[GameManager] Scene Loaded & State Reset: {scene.name}");
    }

    // 게임 클리어 처리
    public void TriggerGameClear()
    {
        if (isDebug) Debug.Log("[GameManager] Mission Clear!");
        OnGameClear?.Invoke();
    }

    // 게임 오버 처리
    public void TriggerGameOver()
    {
        if (isDebug) Debug.Log("[GameManager] Game Over!");
        OnGameOver?.Invoke();
    }

    // 애플리케이션 종료
    public void QuitGame()
    {
        if (isDebug) Debug.Log("[GameManager] Quitting Application...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}