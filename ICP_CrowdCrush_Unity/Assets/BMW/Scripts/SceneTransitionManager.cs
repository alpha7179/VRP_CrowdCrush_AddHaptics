using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using System.Collections;

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
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private Canvas fadeCanvas;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 1.0f;
    [SerializeField] private float distFromCamera = 0.2f;

    [Header("Camera Settings")]
    [Tooltip("UI 카메라가 있다면 이 태그를 가진 카메라를 우선적으로 찾습니다.")]
    [SerializeField] private string uiCameraTag = "UICamera";

    #endregion

    #region Internal State
    private bool isFading = false;
    private Camera cachedCamera;
    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Camera.onPreCull += HandleCameraPreCull;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Camera.onPreCull -= HandleCameraPreCull;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void Start()
    {
        if (fadeCanvas != null)
        {
            fadeCanvas.sortingOrder = 32767;
            // 중요: 캔버스의 레이어를 UI 카메라가 볼 수 있는 레이어로 변경 권장 (여기서 강제하지 않고 에디터 설정을 따름)
        }

        ForceUpdateCanvasPosition();
        StartCoroutine(FadeRoutine(1f, 0f));
    }

    private void LateUpdate()
    {
        if (isFading || (fadeCanvasGroup != null && fadeCanvasGroup.alpha > 0.01f))
        {
            ForceUpdateCanvasPosition();
        }
    }

    #endregion

    #region Event Handlers & Sync Logic

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedCamera = null; // 씬 로드 시 캐시 초기화
        ForceUpdateCanvasPosition();
    }

    private void HandleCameraPreCull(Camera cam)
    {
        SyncCanvasToCamera(cam);
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        SyncCanvasToCamera(cam);
    }

    // ==================================================================================
    // [핵심 수정] : 카메라 찾는 로직 변경
    // MainCamera보다 UICamera(오버레이 카메라)를 우선적으로 찾아서 타겟으로 삼습니다.
    // ==================================================================================
    private void ForceUpdateCanvasPosition()
    {
        // 1. 이미 찾은 카메라가 유효하고 활성화되어 있다면 패스
        if (cachedCamera != null && cachedCamera.gameObject.activeInHierarchy)
        {
            SyncCanvasToCamera(cachedCamera);
            return;
        }

        // 2. UI 카메라(Overlay)를 먼저 찾음 (태그 이용)
        GameObject uiCamObj = GameObject.FindGameObjectWithTag(uiCameraTag);
        if (uiCamObj != null)
        {
            cachedCamera = uiCamObj.GetComponent<Camera>();
        }

        // 3. UI 카메라가 없으면 메인 카메라를 찾음
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        // 4. 그래도 없으면 아무 카메라나 찾음 (최후의 수단)
        if (cachedCamera == null)
        {
            cachedCamera = FindAnyObjectByType<Camera>();
        }

        if (cachedCamera != null)
        {
            SyncCanvasToCamera(cachedCamera);
        }
    }

    private void SyncCanvasToCamera(Camera cam)
    {
        // 현재 우리가 타겟팅하고 있는(가장 최상단) 카메라가 아니면 위치 갱신 무시
        if (cachedCamera != null && cam != cachedCamera) return;

        if (cam == null || fadeCanvas == null) return;
        if (!isFading && fadeCanvasGroup.alpha <= 0.01f) return;

        // 1. 캔버스 월드 카메라 설정
        if (fadeCanvas.worldCamera != cam)
        {
            fadeCanvas.worldCamera = cam;
        }

        // 2. 위치 동기화
        Transform camTr = cam.transform;
        Transform canvasTr = fadeCanvas.transform;

        canvasTr.position = camTr.position + (camTr.forward * distFromCamera);
        canvasTr.rotation = camTr.rotation;

        // 중요: UI 카메라가 Orthographic(직교)일 경우 스케일이나 거리가 다를 수 있음.
        // 만약 UI 카메라가 Orthographic이라면 거리를 좀 더 띄우거나 사이즈 조절이 필요할 수 있습니다.
    }

    #endregion

    #region Public API (LoadScene 등)

    public void LoadScene(string sceneName)
    {
        if (isFading) return;
        if (sceneName == "Main_Intro") ResetGameManagers();
        StartCoroutine(TransitionRoutine(sceneName));
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        isFading = true;

        ForceUpdateCanvasPosition();
        yield return StartCoroutine(FadeRoutine(0f, 1f));

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            ForceUpdateCanvasPosition();
            yield return null;
        }

        op.allowSceneActivation = true;

        while (!op.isDone)
        {
            ForceUpdateCanvasPosition();
            yield return null;
        }

        // --------------------------------------------------------
        // [이전 수정사항 유지]: 씬 로드 직후 깜빡임 방지 대기
        cachedCamera = null; // 새 씬의 카메라를 다시 찾도록 초기화
        ForceUpdateCanvasPosition();

        // 5프레임 정도 대기 (UI 카메라 찾고 위치 잡을 시간 벌기)
        for (int i = 0; i < 5; i++)
        {
            ForceUpdateCanvasPosition();
            yield return null;
        }
        // --------------------------------------------------------

        yield return StartCoroutine(FadeRoutine(1f, 0f));

        isFading = false;
    }

    private IEnumerator FadeRoutine(float startAlpha, float endAlpha)
    {
        float elapsedTime = 0f;
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            fadeCanvasGroup.alpha = startAlpha;
        }

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / fadeDuration);

            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = newAlpha;
                ForceUpdateCanvasPosition();
            }
            yield return null;
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = endAlpha;
            fadeCanvasGroup.blocksRaycasts = (endAlpha > 0.9f);
        }
    }

    private void ResetGameManagers()
    {
        // (기존 코드와 동일)
        if (DataManager.Instance != null) Destroy(DataManager.Instance.gameObject);
        if (GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);
        if (ControllerInputManager.Instance != null) Destroy(ControllerInputManager.Instance.gameObject);
        if (PlayerManager.Instance != null) Destroy(PlayerManager.Instance.gameObject);
    }

    #endregion
}