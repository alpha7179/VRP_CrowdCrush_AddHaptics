using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 씬 전환 시 페이드 효과(Fade In/Out)를 관리하고, 씬 로드 중 카메라 연결 끊김을 방지하는 매니저입니다.
/// <para>
/// 1. 비동기 씬 로딩과 페이드 효과 동기화<br/>
/// 2. Camera.onPreCull을 이용한 캔버스-카메라 강제 동기화 (화면 깜빡임 방지)<br/>
/// 3. 인트로 복귀 시 싱글톤 매니저 초기화
/// </para>
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    #region Singleton

    public static SceneTransitionManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Inspector Settings

    [Header("Components")]
    [Tooltip("페이드 효과의 투명도를 조절할 CanvasGroup입니다.")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Tooltip("페이드 이미지가 그려질 캔버스입니다. (WorldSpace 모드 권장)")]
    [SerializeField] private Canvas fadeCanvas;

    [Header("Settings")]
    [Tooltip("페이드 인/아웃에 걸리는 시간(초)입니다.")]
    [SerializeField] private float fadeDuration = 1.0f;

    #endregion

    #region Internal State

    /// <summary>
    /// 현재 페이드 효과나 씬 전환이 진행 중인지 여부입니다.
    /// </summary>
    private bool isFading = false;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // 렌더링 파이프라인: 카메라가 컬링(Culling)을 하기 직전에 호출되는 이벤트 구독
        Camera.onPreCull += HandleCameraPreCull;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Camera.onPreCull -= HandleCameraPreCull;
    }

    private void Start()
    {
        // 초기화: 시작 시 화면을 밝게 켭니다.
        ForceAssignCamera();
        StartCoroutine(FadeRoutine(1f, 0f));
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// 새로운 씬이 로드되었을 때 호출됩니다.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ForceAssignCamera();
    }

    /// <summary>
    /// Unity 렌더링 루프의 PreCull 단계에서 호출됩니다.
    /// 씬 전환 직후 프레임 드랍이나 캔버스 소실로 인한 깜빡임을 방지하기 위해,
    /// 카메라가 렌더링하기 직전에 캔버스를 강제로 할당합니다.
    /// </summary>
    /// <param name="cam">현재 렌더링을 준비 중인 카메라</param>
    private void HandleCameraPreCull(Camera cam)
    {
        // 페이드 중이거나 화면이 어두운 상태(알파값 존재)일 때만 작동하여 성능 절약
        if (isFading || (fadeCanvasGroup != null && fadeCanvasGroup.alpha > 0.01f))
        {
            // 메인 카메라이고, 캔버스에 아직 연결되지 않았다면 즉시 연결
            if (cam.CompareTag("MainCamera") && fadeCanvas.worldCamera != cam)
            {
                fadeCanvas.worldCamera = cam;
                fadeCanvas.planeDistance = 0.1f; // 카메라 바로 앞에 캔버스 배치 (클리핑 방지)
            }
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// 페이드 효과와 함께 지정된 씬으로 비동기 전환합니다.
    /// </summary>
    /// <param name="sceneName">로드할 씬의 이름</param>
    public void LoadScene(string sceneName)
    {
        if (isFading) return;

        // 인트로(타이틀)로 돌아갈 경우, 게임 상태를 리셋합니다.
        if (sceneName == "IntroScene")
        {
            ResetGameManagers();
        }

        StartCoroutine(TransitionRoutine(sceneName));
    }

    #endregion

    #region Core Logic & Coroutines

    /// <summary>
    /// 페이드 아웃 -> 씬 로드 -> 페이드 인 과정을 처리하는 메인 코루틴입니다.
    /// </summary>
    private IEnumerator TransitionRoutine(string sceneName)
    {
        isFading = true;

        // 1. 페이드 아웃 (투명 -> 검정)
        yield return StartCoroutine(FadeRoutine(0f, 1f));

        // 2. 비동기 씬 로딩 시작
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false; // 로딩이 끝나도 즉시 화면을 띄우지 않음

        // 로딩 진행률 대기 (0.9f가 로딩 완료 시점)
        while (op.progress < 0.9f)
        {
            yield return null;
        }

        // 3. 씬 전환 승인
        op.allowSceneActivation = true;

        // 실제 씬 변경이 완료될 때까지 대기 (이 사이의 깜빡임을 HandleCameraPreCull이 방어)
        while (!op.isDone)
        {
            yield return null;
        }

        // 안전장치: 새 씬의 카메라 재연결
        ForceAssignCamera();

        // 4. 페이드 인 (검정 -> 투명)
        yield return StartCoroutine(FadeRoutine(1f, 0f));

        isFading = false;
    }

    /// <summary>
    /// CanvasGroup의 Alpha 값을 시간에 따라 변경합니다.
    /// </summary>
    private IEnumerator FadeRoutine(float startAlpha, float endAlpha)
    {
        float elapsedTime = 0f;

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true; // 페이드 중 터치 방지
            fadeCanvasGroup.alpha = startAlpha;
        }

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / fadeDuration);

            if (fadeCanvasGroup != null)
                fadeCanvasGroup.alpha = newAlpha;

            yield return null;
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = endAlpha;

            // 페이드 인(화면이 밝아짐)이 완전히 끝났을 때만 터치 허용 (endAlpha == 0)
            // 페이드 아웃(화면이 어두워짐) 상태라면 터치 차단 (endAlpha == 1)
            fadeCanvasGroup.blocksRaycasts = (endAlpha > 0.9f);
        }
    }

    /// <summary>
    /// 현재 활성화된 메인 카메라를 찾아 페이드 캔버스에 할당합니다.
    /// (HandleCameraPreCull이 실패했을 경우를 대비한 백업 메서드)
    /// </summary>
    private void ForceAssignCamera()
    {
        if (fadeCanvas != null && (fadeCanvas.worldCamera == null || !fadeCanvas.worldCamera.gameObject.activeInHierarchy))
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                fadeCanvas.worldCamera = mainCam;
                fadeCanvas.planeDistance = 0.1f;
            }
        }
    }

    /// <summary>
    /// 게임 재시작 시 기존에 남아있는 싱글톤 매니저들을 정리합니다.
    /// </summary>
    private void ResetGameManagers()
    {
        Debug.Log("[SceneTransitionManager] Resetting Singleton Instances for Intro Scene.");

        // 프로젝트에 존재하는 모든 싱글톤 매니저 파괴
        if (DataManager.Instance != null) Destroy(DataManager.Instance.gameObject);
        if (GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);
        if (ControllerInputManager.Instance != null) Destroy(ControllerInputManager.Instance.gameObject);
        if (PlayerManager.Instance != null) Destroy(PlayerManager.Instance.gameObject);
    }

    #endregion
}