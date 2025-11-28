using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance;

    [Header("Components")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private Canvas fadeCanvas;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 1.0f;

    private bool isFading = false;

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // [핵심] 모든 카메라가 렌더링하기 직전에 호출되는 이벤트 구독
        Camera.onPreCull += HandleCameraPreCull;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Camera.onPreCull -= HandleCameraPreCull;
    }

    private void Start()
    {
        // 초기 설정
        ForceAssignCamera();
        StartCoroutine(Fade(1f, 0f));
    }

    // 씬 로드 완료 시 호출
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ForceAssignCamera();
    }

    // [마법의 해결책] 카메라가 그림을 그리기 직전에 낚아채서 연결
    private void HandleCameraPreCull(Camera cam)
    {
        // 페이드 중이거나 화면이 어두운 상태라면 무조건 연결 시도
        if (isFading || fadeCanvasGroup.alpha > 0.01f)
        {
            // 메인 카메라(태그 확인)이고, 아직 캔버스에 연결 안 됐다면 즉시 연결
            if (cam.CompareTag("MainCamera") && fadeCanvas.worldCamera != cam)
            {
                fadeCanvas.worldCamera = cam;
                fadeCanvas.planeDistance = 0.1f; // 거리 재설정 (안전장치)
            }
        }
    }

    // 수동으로 카메라 찾는 함수 (백업용)
    private void ForceAssignCamera()
    {
        if (fadeCanvas != null && fadeCanvas.worldCamera == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                fadeCanvas.worldCamera = mainCam;
                fadeCanvas.planeDistance = 0.1f;
            }
        }
    }

    public void LoadScene(string sceneName)
    {
        if (isFading) return;

        if (sceneName == "IntroScene")
        {
            Debug.Log("[SceneTransitionManager] Destroying Manager instance before loading Intro scene.");
            if(DataManager.Instance != null) Destroy(DataManager.Instance.gameObject);
            if(GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);
            if(ControllerInputManager.Instance != null) Destroy(ControllerInputManager.Instance.gameObject);
            if(PlayerManager.Instance != null) Destroy(PlayerManager.Instance.gameObject);
            
        }

        StartCoroutine(TransitionRoutine(sceneName));
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        isFading = true;

        // 1. 페이드 아웃 (투명 -> 검정)
        yield return StartCoroutine(Fade(0f, 1f));

        // 2. 씬 로딩 시작
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        // 로딩 대기
        while (op.progress < 0.9f)
        {
            yield return null;
        }

        // 3. 씬 전환 승인
        op.allowSceneActivation = true;

        // [중요] 씬이 바뀌는 프레임까지 대기
        // 이 사이의 틈을 Camera.onPreCull이 방어해줍니다.
        while (!op.isDone)
        {
            yield return null;
        }

        // 확실하게 한 번 더 연결 시도
        ForceAssignCamera();

        // 4. 페이드 인 (검정 -> 투명)
        yield return StartCoroutine(Fade(1f, 0f));

        isFading = false;
    }

    private IEnumerator Fade(float startAlpha, float endAlpha)
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
                fadeCanvasGroup.alpha = newAlpha;

            yield return null;
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = endAlpha;
            // 페이드가 끝나서 투명해졌을 때만 레이캐스트(터치) 허용
            // alpha가 0이면(페이드인 끝) blocksRaycasts = false (터치 가능)
            // alpha가 1이면(페이드아웃 끝) blocksRaycasts = true (터치 불가)
            fadeCanvasGroup.blocksRaycasts = (endAlpha > 0.9f);
        }
    }
}