using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

/// <summary>
/// 게임의 전체 생명주기, 씬 전환, 전역 상태(일시정지 등)를 관리하는 최상위 매니저
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
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 이미 인스턴스가 존재하면 중복된 객체는 파괴
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 시작 시 현재 씬 이름 저장
        CurrentSceneName = SceneManager.GetActiveScene().name;
    }

    // 게임 일시정지/재개 토글 (ControllerInputManager의 메뉴 버튼에서 호출)
    public void TogglePause()
    {
        // 인트로 씬 등 일시정지가 필요 없는 씬 예외 처리
        if (CurrentSceneName.Equals("IntroScene", StringComparison.OrdinalIgnoreCase)) return;

        IsPaused = !IsPaused;

        // 물리 연산 및 시간 정지/재개 (TimeScale 조절)
        Time.timeScale = IsPaused ? 0f : 1f;

        // 이벤트 발생 -> GameUIManager 등이 구독하여 팝업 처리
        OnPauseStateChanged?.Invoke(IsPaused);

        if(isDebug) Debug.Log($"[GameManager3] Pause State: {IsPaused}");
    }

    // 특정 씬으로 비동기 전환 (로딩 화면 처리 가능)
    // <param name="sceneName">이동할 씬의 정확한 이름</param>
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        if(isDebug) Debug.Log($"[GameManager3] Loading Scene: {sceneName}...");

        // 비동기 로딩 시작
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        // 로딩이 끝날 때까지 대기
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 씬 전환 후 상태 초기화
        CurrentSceneName = sceneName;
        Time.timeScale = 1f; // 시간 정상화
        IsPaused = false;

        // 씬 로드 완료 이벤트 전파 (PlayerManager, UIManager 초기화)
        OnSceneLoaded?.Invoke(sceneName);

        if(isDebug) Debug.Log($"[GameManager3] Scene Loaded Complete: {sceneName}");
    }

    // 게임 클리어 (탈출 성공) 처리 -> OuttroUIManager 호출용
    public void TriggerGameClear()
    {
        if(isDebug) Debug.Log("[GameManager3] Mission Clear!");
        OnGameClear?.Invoke();
    }

    // 게임 오버 (실패) 처리
    public void TriggerGameOver()
    {
        if(isDebug) Debug.Log("[GameManager3] Game Over!");
        OnGameOver?.Invoke();
    }

    // 애플리케이션 종료
    public void QuitGame()
    {
        if(isDebug) Debug.Log("[GameManager3] Quitting Application...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}