using System.Collections;
using System.Collections.Generic; // Dictionary 사용을 위해 추가
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 UI 매니저: HUD 요소, 각종 패널 관리 및 XR Vignette 기반 압박 효과 제어
/// </summary>
public class IngameUIManager : MonoBehaviour
{

    [Header("HUD Elements")]
    [SerializeField] private Canvas IngameCanvas;

    [Header("Panels")]
    [SerializeField] private GameObject cautionPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject instructionPanel;
    [SerializeField] private GameObject progressPanel;
    [SerializeField] private GameObject pressurePanel;

    [Header("Panels UI Elements")]
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI missionText;
    [SerializeField] private TextMeshProUGUI feedBackText;

    [SerializeField] private TextMeshProUGUI progressMissionText;
    [SerializeField] public TextMeshProUGUI progressText;
    [SerializeField] public Slider barSlider;
    [SerializeField] public Image[] tipsImage;

    [SerializeField] private TextMeshProUGUI pressureStateText;
    private readonly string[] PressureState = new string[] {"정상", "주의", "경고", "압박", "위험", "최대 위험"};
    [SerializeField] public Image[] pressureGaugeImages;

    [Header("Effects")]
    [SerializeField] private PressureVignette pressureVignette;

    [Header("Visual Settings (Smoothness)")]
    [Tooltip("비네팅이 변하는 속도")]
    [SerializeField] private float vignetteSmoothTime = 0.5f;
    [Tooltip("UI가 페이드되는 시간")]
    [SerializeField] private float imageFadeDuration = 0.3f;

    [Header("Gauge Pulse Settings")]
    [SerializeField] private float pulseSpeed = 3.0f;
    [SerializeField] private float minPulseAlpha = 0.2f;

    [Header("UI Fade Settings")]
    [SerializeField] private float panelFadeDuration = 0.2f; // 패널 페이드 시간

    // 패널별 실행 중인 코루틴 관리 (중복 실행 방지)
    private Dictionary<GameObject, Coroutine> panelCoroutines = new Dictionary<GameObject, Coroutine>();

    // 내부 관리용 변수
    private float currentVignetteValue = 0f; // 현재 비네팅 강도 추적용
    private Coroutine vignetteCoroutine; // 비네팅 코루틴

    // 각 게이지 이미지별로 돌아가는 코루틴을 관리하기 위한 딕셔너리
    private Dictionary<Image, Coroutine> imageCoroutines = new Dictionary<Image, Coroutine>();
    private float cachedOriginalAlpha = 1.0f; // 펄스용 알파값 저장

    [Header("External References")]
    [SerializeField] private OuttroUIManager outtroManager;

    private bool isDisplayPanel = false;


    // =================================================================================
    // Unity 생명 주기 메서드
    // =================================================================================

    private void Start()
    {
        // 초기 UI 상태 설정
        if (pausePanel) pausePanel.SetActive(false);
        if (instructionPanel) instructionPanel.SetActive(false);
        if (outtroManager) outtroManager.gameObject.SetActive(false);
        if (progressPanel) progressPanel.SetActive(false);
        HideAllTipsImages();

        // 압박 효과 초기화
        if (pressureVignette != null)
        {
            pressureVignette.SetIntensity(0f);
        }

        // GameManager 이벤트 구독
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPauseStateChanged += HandlePauseState;
        }
    }

    private void OnEnable()
    {
        if (ControllerInputManager.Instance != null)
        {
            ControllerInputManager.Instance.OnAButtonDown += HandleAButtonInput;
            ControllerInputManager.Instance.OnBButtonDown += HandleBButtonInput;
            ControllerInputManager.Instance.OnYButtonDown += HandleYButtonInput;
        }
    }

    private void OnDisable()
    {
        if (ControllerInputManager.Instance != null)
        {
            ControllerInputManager.Instance.OnAButtonDown -= HandleAButtonInput;
            ControllerInputManager.Instance.OnBButtonDown -= HandleBButtonInput;
            ControllerInputManager.Instance.OnYButtonDown -= HandleYButtonInput;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPauseStateChanged -= HandlePauseState;
        }
    }

    // =================================================================================
    // 입력 핸들러 (Input Handlers)
    // =================================================================================

    private void HandleYButtonInput()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TogglePause();
            SetDisplayPanel(true);
        }
    }

    private void HandleAButtonInput()
    {
        if (pausePanel != null && pausePanel.activeSelf)
        {
            Time.timeScale = 1f;
            GameManager.Instance.LoadScene("IntroScene");
        }
        else if (instructionPanel != null && instructionPanel.activeSelf)
        {
            CloseInstructionPanel();
            SetDisplayPanel(false);
        }
        else if (cautionPanel != null && cautionPanel.activeSelf)
        {
            CloseCautionPanel();
            SetDisplayPanel(false);
        }
    }

    private void HandleBButtonInput()
    {
        if (pausePanel.activeSelf)
        {
            if (GameManager.Instance != null) GameManager.Instance.TogglePause();
            SetDisplayPanel(false);
        }
    }

    // =================================================================================
    // UI 제어 메서드
    // =================================================================================

    public void OpenCautionPanel() { FadePanel(cautionPanel,true); SetDisplayPanel(true); }
    public void CloseCautionPanel() { FadePanel(cautionPanel, false); SetDisplayPanel(false); }

    public void OpenInstructionPanel() { FadePanel(instructionPanel, true); SetDisplayPanel(true); }
    public void CloseInstructionPanel() { FadePanel(instructionPanel, false); SetDisplayPanel(false); UpdateInstruction(""); UpdateMission(""); UpdateFeedBack(""); }

    public void OpenProgressPanel(string missionText)
    {
        if (progressMissionText) progressMissionText.text = missionText;
        if (progressPanel) FadePanel(progressPanel, true);
    }
    public void CloseProgressPanel()
    {
        if (progressPanel) FadePanel(progressPanel, false);
        HideAllTipsImages();
    }

    public void OpenPressurePanel() { if (pressurePanel) FadePanel(pressurePanel, true); }
    public void ClosePressurePanel() { if (pressurePanel) FadePanel(pressurePanel, false); }

    public void UpdateInstruction(string text) { if (instructionText) instructionText.text = text; }
    public void UpdateMission(string text)
    {
        if (missionText) missionText.text = text;
        if (feedBackText) feedBackText.text = "";
    }
    public void UpdateFeedBack(string text) { if (feedBackText) feedBackText.text = text; }

    public void SetDisplayPanel(bool state) { isDisplayPanel = state; }
    public bool GetDisplayPanel() { return isDisplayPanel; }

    // [부드러운 전환 적용] 비네팅 효과 강도 설정
    public void SetPressureIntensity(float targetIntensity)
    {
        if (pressureVignette == null) return;

        // 기존 코루틴 중지하고 새로운 부드러운 전환 시작
        if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(SmoothVignetteRoutine(targetIntensity));
    }

    // 비네팅 값을 서서히 변경하는 코루틴
    private IEnumerator SmoothVignetteRoutine(float target)
    {
        float start = currentVignetteValue;
        float elapsed = 0f;

        while (elapsed < vignetteSmoothTime)
        {
            elapsed += Time.deltaTime;
            currentVignetteValue = Mathf.Lerp(start, target, elapsed / vignetteSmoothTime);
            pressureVignette.SetIntensity(currentVignetteValue);
            yield return null;
        }

        currentVignetteValue = target;
        pressureVignette.SetIntensity(target);
    }


    public void DisplayTipsImage(int pageIndex)
    {
        if (tipsImage == null || tipsImage.Length == 0) return;
        for (int i = 0; i < tipsImage.Length; i++)
        {
            if (tipsImage[i] != null) tipsImage[i].gameObject.SetActive(i == pageIndex);
        }
    }
    public void HideAllTipsImages()
    {
        if (tipsImage == null) return;
        foreach (var img in tipsImage) if (img != null) img.gameObject.SetActive(false);
    }

    private void HandlePauseState(bool isPaused)
    {
        if (pausePanel) pausePanel.SetActive(isPaused);
    }

    public void ShowOuttroUI()
    {
        if (IngameCanvas) IngameCanvas.enabled = false;
        if (outtroManager)
        {
            outtroManager.gameObject.SetActive(true);
            StartCoroutine(outtroManager.InitializeRoutine());
        }
    }

    // [부드러운 전환 적용] 압박 게이지 UI 업데이트
    public void UpdatePressureGauge(int level)
    {
        // 1. 텍스트 및 비네팅 업데이트
        if (pressureStateText)
        {
            int stateIndex = Mathf.Clamp(level, 0, PressureState.Length - 1);
            pressureStateText.text = PressureState[stateIndex];
        }

        float maxLevel = 5.0f;
        float intensity = Mathf.Clamp01((float)level / maxLevel);
        SetPressureIntensity(intensity);

        // 2. 게이지 이미지 페이드 효과 처리
        if (pressureGaugeImages == null || pressureGaugeImages.Length == 0) return;

        int targetIndex = level - 1; // 현재 레벨의 마지막 게이지 인덱스 (0부터 시작하므로 -1)

        for (int i = 0; i < pressureGaugeImages.Length; i++)
        {
            Image img = pressureGaugeImages[i];
            if (img == null) continue;

            // 기존에 돌던 코루틴이 있으면 정지
            if (imageCoroutines.ContainsKey(img) && imageCoroutines[img] != null)
            {
                StopCoroutine(imageCoroutines[img]);
            }

            bool shouldBeOn = (i < level); // 이 게이지가 켜져야 하는가?
            bool isPulseTarget = (i == targetIndex); // 이 게이지가 깜빡여야 하는가?

            if (shouldBeOn)
            {
                // 켜져야 함 (Fade In) -> 다 켜지면 Pulse 할지 결정
                imageCoroutines[img] = StartCoroutine(FadeImageRoutine(img, 1.0f, true, isPulseTarget));
            }
            else
            {
                // 꺼져야 함 (Fade Out) -> 다 꺼지면 비활성화
                imageCoroutines[img] = StartCoroutine(FadeImageRoutine(img, 0.0f, false, false));
            }
        }
    }

    // =================================================================================
    // 코루틴: 페이드 및 펄스 효과
    // =================================================================================

    private void FadePanel(GameObject panel, bool show)
    {
        if (panel == null) return;

        // CanvasGroup이 없으면 추가 (안전장치)
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        // 이미 실행 중인 코루틴이 있다면 중지
        if (panelCoroutines.ContainsKey(panel) && panelCoroutines[panel] != null)
        {
            StopCoroutine(panelCoroutines[panel]);
        }

        // 코루틴 시작
        panelCoroutines[panel] = StartCoroutine(FadePanelRoutine(panel, cg, show));
    }

    private IEnumerator FadePanelRoutine(GameObject panel, CanvasGroup cg, bool show)
    {
        float targetAlpha = show ? 1.0f : 0.0f;
        float startAlpha = cg.alpha;
        float elapsed = 0f;

        // 켤 때는 먼저 Active true
        if (show)
        {
            panel.SetActive(true);
            cg.alpha = 0f; // 깜빡임 방지 (혹시 1로 남아있을까봐)
            startAlpha = 0f;
        }

        while (elapsed < panelFadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / panelFadeDuration);
            yield return null;
        }

        cg.alpha = targetAlpha;

        // 끌 때는 페이드 끝난 후 Active false
        if (!show)
        {
            panel.SetActive(false);
        }
    }

    private IEnumerator FadeImageRoutine(Image targetImage, float targetAlpha, bool activeState, bool startPulseAfterFade)
    {
        // 켜는 거라면 먼저 Active부터 켜준다
        if (activeState && !targetImage.gameObject.activeSelf)
        {
            targetImage.gameObject.SetActive(true);
            // 시작할 때 투명하게 시작 (부드러운 등장을 위해)
            Color startCol = targetImage.color;
            targetImage.color = new Color(startCol.r, startCol.g, startCol.b, 0f);
        }
        else if (!activeState && !targetImage.gameObject.activeSelf)
        {
            // 이미 꺼져있는데 또 끄라고 하면 그냥 종료
            yield break;
        }

        Color color = targetImage.color;
        float startAlpha = color.a;
        float elapsed = 0f;

        // 1. 페이드 애니메이션
        while (elapsed < imageFadeDuration)
        {
            elapsed += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / imageFadeDuration);
            targetImage.color = new Color(color.r, color.g, color.b, newAlpha);
            yield return null;
        }

        // 값 확정
        targetImage.color = new Color(color.r, color.g, color.b, targetAlpha);

        // 2. 종료 처리
        if (!activeState)
        {
            // 끄는 경우: 페이드 아웃 끝났으니 비활성화
            targetImage.gameObject.SetActive(false);
        }
        else if (startPulseAfterFade)
        {
            // 켜는 경우인데, 이 녀석이 주인공(Pulse 대상)이라면 -> 펄스 코루틴 시작
            // Pulse 시작 전 기준 알파값 저장 (보통 1.0f일 것임)
            cachedOriginalAlpha = targetAlpha;
            // 딕셔너리에 펄스 코루틴을 저장해서 나중에 멈출 수 있게 함
            imageCoroutines[targetImage] = StartCoroutine(PulseImageRoutine(targetImage));
        }
    }

    /// <summary>
    /// 이미지를 두근거리는(Pulse) 코루틴
    /// </summary>
    private IEnumerator PulseImageRoutine(Image targetImage)
    {
        Color originalColor = targetImage.color;

        while (true)
        {
            // 0 ~ 1 사이의 사인파
            float alphaRatio = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) / 2.0f;

            // 최소 ~ 원래 알파값 사이 반복
            float targetAlpha = Mathf.Lerp(minPulseAlpha, cachedOriginalAlpha, alphaRatio);

            targetImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, targetAlpha);

            yield return null;
        }
    }

    // =================================================================================
    // 코루틴: 미션 타이머
    // =================================================================================

    public IEnumerator StartMissionTimer(string missionText, float totalTime, System.Func<bool> isMissionCompleteCondition, System.Func<float> progressCalculator = null)
    {
        float currentTime = totalTime;
        float timeSpent = 0f;

        if (progressCalculator != null && progressText) progressText.text = $"0 %";
        else if (progressText) progressText.text = $"{totalTime} s";

        OpenProgressPanel(missionText);

        while (!isMissionCompleteCondition.Invoke())
        {
            currentTime -= Time.deltaTime;
            timeSpent += Time.deltaTime;

            if (progressCalculator != null)
            {
                float currentProgress = progressCalculator.Invoke();
                if (progressText) progressText.text = $"{(currentProgress * 100f).ToString("F0")} %";
                if (barSlider) barSlider.value = currentProgress;
            }
            else
            {
                if (progressText) progressText.text = $"{Mathf.CeilToInt(currentTime).ToString()} s";
                if (barSlider) barSlider.value = currentTime / totalTime;
            }

            yield return null;
        }
        DataManager.Instance.AddSuccessCount();
        DataManager.Instance.AddPlayTime(timeSpent);

        CloseProgressPanel();
        yield return timeSpent;
    }
}