using System.Collections;
using System.Collections.Generic;
using System.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.InputSystem.HID.HID;

/// <summary>
/// 인게임 UI(HUD), 팝업 패널, 압박 효과(Vignette) 및 미션 진행 상황을 총괄하는 매니저입니다.
/// <para>
/// 1. HUD 요소(텍스트, 게이지)를 갱신하고 안내/일시정지/경고 패널을 제어합니다.<br/>
/// 2. PressureVignette와 연동하여 게임 내 압박감(시각적 왜곡)을 조절합니다.<br/>
/// 3. 컨트롤러 입력을 받아 일시정지(Y버튼), 패널 닫기(A버튼) 등의 상호작용을 처리합니다.
/// </para>
/// </summary>
public class IngameUIManager : MonoBehaviour
{
    #region Inspector Settings (Panels)

    [Header("HUD Elements")]
    [Tooltip("인게임 HUD 전체를 포함하는 캔버스")]
    [SerializeField] private Canvas IngameCanvas;

    [Header("Popup Panels")]
    [Tooltip("주의 사항(경고) 패널")]
    [SerializeField] private GameObject cautionPanel;
    [Tooltip("안내 사항 패널")]
    [SerializeField] private GameObject situationPanel;
    [Tooltip("일시정지 메뉴 패널")]
    [SerializeField] private GameObject pausePanel;
    [Tooltip("조작 설명 및 안내 패널")]
    [SerializeField] private GameObject instructionPanel;
    [Tooltip("미션 진행도(프로그레스 바) 패널")]
    [SerializeField] private GameObject progressPanel;
    [Tooltip("압박감 상태를 보여주는 패널")]
    [SerializeField] private GameObject pressurePanel;

    #endregion

    #region Inspector Settings (UI Elements)

    [Header("Text Elements")]
    [Tooltip("안내 패널의 본문 텍스트")]
    [SerializeField] private GameObject[] instruction;
    private int currentInstruction = 0;
    [Tooltip("피드백/결과 텍스트")]
    [SerializeField] private GameObject[] feedback;
    [SerializeField] private GameObject[] negativeFeedback;
    private int currentFeedback = 0;
    private int currentNegativeFeedback = 0;

    [Header("Progress Elements")]
    [Tooltip("진행도 패널의 미션 설명 텍스트")]
    [SerializeField] private TextMeshProUGUI progressMissionText;
    [Tooltip("진행도 퍼센트/시간 텍스트")]
    [SerializeField] public TextMeshProUGUI progressText;
    [Tooltip("진행도 슬라이더")]
    [SerializeField] public Image barSlider;
    [Tooltip("진행 단계별 팁 이미지 배열")]
    [SerializeField] public Image[] tipsImage;

    [Header("Pressure Elements")]
    [Tooltip("압박감 상태 텍스트 (정상 ~ 위험)")]
    [SerializeField] private TextMeshProUGUI pressureStateText;
    [Tooltip("압박감 단계별 게이지 이미지 배열")]
    [SerializeField] public Image[] pressureGaugeImages;
    [SerializeField] public Image[] pressureHighlightImages;

    private readonly string[] PressureState = new string[] { "정상", "주의", "경고", "압박", "위험", "최대 위험" };

    #endregion

    #region Inspector Settings (Effects & Settings)

    [Header("Visual Effects")]
    [Tooltip("화면 가장자리 비네팅 효과 제어 스크립트")]
    [SerializeField] private PressureVignette pressureVignette;

    [Header("Animation Settings")]
    [Tooltip("비네팅 강도가 변경될 때 걸리는 시간(초)")]
    [SerializeField] private float vignetteSmoothTime = 0.5f;
    [Tooltip("UI 이미지(게이지 등) 페이드 시간")]
    [SerializeField] private float imageFadeDuration = 0.3f;
    [Tooltip("패널이 켜지고 꺼지는 페이드 시간")]
    [SerializeField] private float panelFadeDuration = 0.2f;

    [Header("Pulse Settings")]
    [Tooltip("게이지 깜빡임(펄스) 속도")]
    [SerializeField] private float pulseSpeed = 3.0f;
    [Tooltip("펄스 효과 시 최소 알파값")]
    [SerializeField] private float minPulseAlpha = 0.2f;

    #endregion

    #region External References

    [Header("External References")]
    [Tooltip("게임 종료 화면 매니저")]
    [SerializeField] private OuttroUIManager outtroManager;

    #endregion

    #region Internal State

    // 현재 패널이 열려있는지 여부 (입력 제어용)
    private bool isDisplayPanel = false;

    // 코루틴 관리 (중복 실행 방지)
    private Dictionary<GameObject, Coroutine> panelCoroutines = new Dictionary<GameObject, Coroutine>();
    private Dictionary<Image, Coroutine> imageCoroutines = new Dictionary<Image, Coroutine>();

    // 비네팅 제어 변수
    private float currentVignetteValue = 0f;
    private Coroutine vignetteCoroutine;

    // 펄스 효과용 캐싱 변수
    private float cachedOriginalAlpha = 1.0f;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        InitializeUI();
        InitializeEvents();
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

    #endregion

    #region Initialization

    private void InitializeUI()
    {
        // 모든 팝업 패널 비활성화
        if (cautionPanel) cautionPanel.SetActive(true);
        if (situationPanel) situationPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);
        if (instructionPanel) instructionPanel.SetActive(false);
        if (progressPanel) progressPanel.SetActive(false);
        if (outtroManager) outtroManager.gameObject.SetActive(false);

        HideAllTipsImages();

        // 압박 효과 초기화 (0)
        if (pressureVignette != null)
        {
            pressureVignette.SetIntensity(0f);
        }
    }

    private void InitializeEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPauseStateChanged += HandlePauseState;
        }
    }

    #endregion

    #region Input Handlers

    /// <summary>
    /// Y 버튼: 일시정지 토글
    /// </summary>
    private void HandleYButtonInput()
    {
        if (this == null || !gameObject.activeInHierarchy) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TogglePause();
            SetDisplayPanel(true); // 일시정지 시 패널 상태 true
        }
    }

    /// <summary>
    /// A 버튼: 확인 / 패널 닫기 / 메인으로 이동
    /// </summary>
    private void HandleAButtonInput()
    {
        if (this == null || !gameObject.activeInHierarchy) return;

        // 1. 일시정지 상태라면 -> 인트로(메인)로 이동
        if (pausePanel != null && pausePanel.activeSelf)
        {
            Time.timeScale = 1f; // 시간 정상화 후 이동
            GameManager.Instance.LoadScene("Main_Intro");
        }
        // 2. 안내 패널이 열려있다면 -> 닫기
        else if (instructionPanel != null && instructionPanel.activeSelf)
        {
            CloseInstructionPanel();
            SetDisplayPanel(false);
        }
        // 3. 주의 패널이 열려있다면 -> 닫기
        else if (cautionPanel != null && cautionPanel.activeSelf)
        {
            CloseCautionPanel();
            SetDisplayPanel(false);
        }
        // 4. 상황 패널이 열려있다면 -> 닫기
        else if (situationPanel != null && situationPanel.activeSelf)
        {
            CloseSituationPanel();
            SetDisplayPanel(false);
        }
    }

    /// <summary>
    /// B 버튼: 취소 / 게임 재개
    /// </summary>
    private void HandleBButtonInput()
    {
        if (this == null || !gameObject.activeInHierarchy) return;

        // 일시정지 상태라면 -> 게임 재개
        if (pausePanel != null && pausePanel.activeSelf)
        {
            if (GameManager.Instance != null) GameManager.Instance.TogglePause();
            SetDisplayPanel(false);
        }
    }

    #endregion

    #region Public UI API (Panel Control)

    public void OpenCautionPanel() { FadePanel(cautionPanel, true); SetDisplayPanel(true); }
    public void CloseCautionPanel() { FadePanel(cautionPanel, false); SetDisplayPanel(false); }

    public void OpenSituationPanel() { FadePanel(situationPanel, true); SetDisplayPanel(true); }
    public void CloseSituationPanel() { FadePanel(situationPanel, false); SetDisplayPanel(false); }

    public void OpenInstructionPanel() { FadePanel(instructionPanel, true); SetDisplayPanel(true); }
    public void CloseInstructionPanel()
    {
        FadePanel(instructionPanel, false);
        SetDisplayPanel(false);
        CloseInstruction();
        CloseFeedBack();
    }

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

    // 게임 종료 화면 표시 (Canvas 끄고 OuttroManager 활성화)
    public void ShowOuttroUI()
    {
        if (IngameCanvas) IngameCanvas.enabled = false;
        if (outtroManager)
        {
            outtroManager.gameObject.SetActive(true);
            StartCoroutine(outtroManager.InitializeRoutine());
        }
    }

    #endregion

    #region Public UI API (Content Updates)

    public void UpdateInstruction(int instructionNum)
    { 
        if(instruction[currentInstruction].activeSelf ) instruction[currentInstruction].SetActive(false); 
        instruction[instructionNum].SetActive(true);
        currentInstruction = instructionNum;
    }
    public void CloseInstruction() { instruction[currentInstruction].SetActive(false); }

    public void UpdateFeedBack(int FeedbackNum)
    {
        if (feedback[currentFeedback].activeSelf) feedback[currentFeedback].SetActive(false);
        feedback[FeedbackNum].SetActive(true);
        currentFeedback = FeedbackNum;
    }

    public void UpdateNegativeFeedback(int FeedbackNum)
    {
        if (negativeFeedback[currentNegativeFeedback].activeSelf) negativeFeedback[currentNegativeFeedback].SetActive(false);
        negativeFeedback[FeedbackNum].SetActive(true);
        currentNegativeFeedback = FeedbackNum;
    }

    public void CloseFeedBack()
    {
        if (feedback[currentFeedback].activeSelf) feedback[currentFeedback].SetActive(false);
        if (negativeFeedback[currentNegativeFeedback].activeSelf) negativeFeedback[currentNegativeFeedback].SetActive(false);
    }

    public void SetDisplayPanel(bool state) { isDisplayPanel = state; }
    public bool GetDisplayPanel() { return isDisplayPanel; }

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

    #endregion

    #region Pressure & Vignette Logic

    /// <summary>
    /// 비네팅 강도를 부드럽게 변경합니다.
    /// </summary>
    public void SetPressureIntensity(float targetIntensity)
    {
        if (pressureVignette == null) return;

        // 기존 코루틴 중지하고 새로운 전환 시작
        if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(SmoothVignetteRoutine(targetIntensity));
    }

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

    /// <summary>
    /// 압박 게이지 UI를 레벨(1~6)에 맞춰 업데이트하고, 해당 단계까지 이미지를 켜거나 펄스 효과를 줍니다.
    /// </summary>
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

        // 2. 게이지 이미지 시각 효과
        if (pressureGaugeImages == null || pressureGaugeImages.Length == 0 || pressureHighlightImages == null || pressureHighlightImages.Length == 0) return;

        int targetIndex = level - 1; // 현재 레벨 (0-based index)

        for (int i = 0; i < pressureGaugeImages.Length; i++)
        {
            Image img = pressureGaugeImages[i];
            Image highlightImg = pressureHighlightImages[i];
            if (img == null || highlightImg == null) continue;

            // 중복 실행 방지
            if (imageCoroutines.ContainsKey(img) && imageCoroutines[img] != null)
            {
                StopCoroutine(imageCoroutines[img]);
            }
            if (imageCoroutines.ContainsKey(highlightImg) && imageCoroutines[highlightImg] != null)
            {
                StopCoroutine(imageCoroutines[highlightImg]);
            }

            bool shouldBeOn = (i < level); // 현재 레벨 이하의 게이지는 켜짐
            bool isPulseTarget = (i == targetIndex); // 현재 레벨의 게이지는 깜빡임

            if (shouldBeOn)
            {
                // 켜기 (Fade In -> Pulse if target)
                imageCoroutines[img] = StartCoroutine(FadeImageRoutine(img, 1.0f, true, false));
            }
            else
            {
                // 끄기 (Fade Out)
                imageCoroutines[img] = StartCoroutine(FadeImageRoutine(img, 0.0f, false, false));
            }

            if (isPulseTarget)
            {
                // 켜기 (Fade In -> Pulse if target)
                imageCoroutines[highlightImg] = StartCoroutine(FadeImageRoutine(highlightImg, 1.0f, true, isPulseTarget));
            }
            else
            {
                // 끄기 (Fade Out)
                imageCoroutines[highlightImg] = StartCoroutine(FadeImageRoutine(highlightImg, 0.0f, false, false));
            }
        }
    }

    #endregion

    #region Mission Timer Logic

    /// <summary>
    /// 미션 타이머 코루틴. 지정된 시간 동안 진행도를 갱신하며 대기합니다.
    /// </summary>
    /// <param name="missionText">진행 패널에 표시할 텍스트</param>
    /// <param name="totalTime">총 제한 시간</param>
    /// <param name="isMissionCompleteCondition">미션 완료 조건 함수 (true 반환 시 즉시 종료)</param>
    /// <param name="progressCalculator">진행도(0~1) 계산 함수 (null이면 시간 기준)</param>
    public IEnumerator StartMissionTimer(string missionText, float totalTime, System.Func<bool> isMissionCompleteCondition, System.Func<float> progressCalculator = null, bool isDisplyPanel = false)
    {
        float currentTime = totalTime;
        float timeSpent = 0f;

        // 초기 텍스트 설정
        if (progressCalculator != null && progressText) progressText.text = "0 %";
        else if (progressText) progressText.text = $"{totalTime} s";

        if(isDisplyPanel) OpenProgressPanel(missionText);

        // 완료 조건이 충족될 때까지 루프
        while (!isMissionCompleteCondition.Invoke())
        {
            currentTime -= Time.deltaTime;
            timeSpent += Time.deltaTime;

            // 진행도 갱신
            if (progressCalculator != null)
            { 
                float currentProgress = progressCalculator.Invoke();
                if (progressText) progressText.text = $"{(currentProgress * 100f):F0} %";
                if (barSlider) barSlider.fillAmount = currentProgress;
            }
            else
            {
                // 시간 기준 진행도 (남은 시간 표시)
                if (progressText) progressText.text = $"{Mathf.CeilToInt(currentTime)} s";
                if (barSlider) barSlider.fillAmount = currentTime / totalTime;
            }

            yield return null;
        }

        // 미션 완료 후 데이터 갱신
        if (DataManager.Instance != null)
        {
            DataManager.Instance.AddSuccessCount();
            DataManager.Instance.AddPlayTime(timeSpent);
        }

        if (isDisplyPanel) CloseProgressPanel();
    }

    #endregion

    #region Visual Effects (Fades & Pulse)

    private void HandlePauseState(bool isPaused)
    {
        if (pausePanel) pausePanel.SetActive(isPaused);
    }

    // --- Panel Fade ---
    private void FadePanel(GameObject panel, bool show)
    {
        if (panel == null) return;

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        if (panelCoroutines.ContainsKey(panel) && panelCoroutines[panel] != null)
        {
            StopCoroutine(panelCoroutines[panel]);
        }
        panelCoroutines[panel] = StartCoroutine(FadePanelRoutine(panel, cg, show));
    }

    private IEnumerator FadePanelRoutine(GameObject panel, CanvasGroup cg, bool show)
    {
        float targetAlpha = show ? 1.0f : 0.0f;
        float startAlpha = cg.alpha;
        float elapsed = 0f;

        if (show)
        {
            panel.SetActive(true);
            cg.alpha = 0f;
            startAlpha = 0f;
        }

        while (elapsed < panelFadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / panelFadeDuration);
            yield return null;
        }
        cg.alpha = targetAlpha;

        if (!show) panel.SetActive(false);
    }

    // --- Image Fade & Pulse ---
    private IEnumerator FadeImageRoutine(Image targetImage, float targetAlpha, bool activeState, bool startPulseAfterFade)
    {
        if (activeState && !targetImage.gameObject.activeSelf)
        {
            targetImage.gameObject.SetActive(true);
            Color c = targetImage.color;
            targetImage.color = new Color(c.r, c.g, c.b, 0f);
        }
        else if (!activeState && !targetImage.gameObject.activeSelf)
        {
            yield break;
        }

        Color color = targetImage.color;
        float startAlpha = color.a;
        float elapsed = 0f;

        while (elapsed < imageFadeDuration)
        {
            elapsed += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / imageFadeDuration);
            targetImage.color = new Color(color.r, color.g, color.b, newAlpha);
            yield return null;
        }

        targetImage.color = new Color(color.r, color.g, color.b, targetAlpha);

        if (!activeState)
        {
            targetImage.gameObject.SetActive(false);
        }
        else if (startPulseAfterFade)
        {
            cachedOriginalAlpha = targetAlpha;
            imageCoroutines[targetImage] = StartCoroutine(PulseImageRoutine(targetImage));
        }
    }

    private IEnumerator PulseImageRoutine(Image targetImage)
    {
        Color originalColor = targetImage.color;
        while (true)
        {
            float alphaRatio = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) / 2.0f;
            float targetAlpha = Mathf.Lerp(minPulseAlpha, cachedOriginalAlpha, alphaRatio);
            targetImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, targetAlpha);
            yield return null;
        }
    }

    #endregion
}