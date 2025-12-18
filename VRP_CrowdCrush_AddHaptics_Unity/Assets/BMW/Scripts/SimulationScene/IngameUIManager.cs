using System.Collections;
using System.Collections.Generic;
using System.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

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
    [SerializeField] private Canvas IngameCanvas;
    [Header("Popup Panels")]
    [SerializeField] private GameObject cautionPanel;
    [SerializeField] private GameObject situationPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject instructionPanel;
    [SerializeField] private GameObject progressPanel;
    [SerializeField] private GameObject pressurePanel;
    #endregion

    #region Inspector Settings (UI Elements)
    [Header("Text Elements")]
    [SerializeField] private GameObject[] instruction;
    private int currentInstruction = 0;
    [SerializeField] private GameObject[] feedback;
    [SerializeField] private GameObject[] negativeFeedback;
    private int currentFeedback = 0;
    private int currentNegativeFeedback = 0;

    [Header("Progress Elements")]
    [SerializeField] private TextMeshProUGUI progressMissionText;
    [SerializeField] public TextMeshProUGUI progressText;
    [SerializeField] public Image barSlider;
    [SerializeField] public Image[] tipsImage;

    [Header("Pressure Elements")]
    [SerializeField] private TextMeshProUGUI pressureStateText;
    [SerializeField] public Image[] pressureGaugeImages;
    [SerializeField] public Image[] pressureHighlightImages;
    private readonly string[] PressureState = new string[] { "안전", "경고", "압박", "위험", "마비", "치명" };
    #endregion

    #region Inspector Settings (Effects & Settings)
    [Header("Visual Effects")]
    [SerializeField] private PressureVignette pressureVignette;
    [Header("Animation Settings")]
    [SerializeField] private float vignetteSmoothTime = 0.5f;
    [SerializeField] private float imageFadeDuration = 0.3f;
    [SerializeField] private float panelFadeDuration = 0.2f;
    [Header("Pulse Settings")]
    [SerializeField] private float pulseSpeed = 3.0f;
    [SerializeField] private float minPulseAlpha = 0.2f;
    #endregion

    #region External References
    [Header("External References")]
    [SerializeField] private OuttroUIManager outtroManager;
    #endregion

    #region Internal State
    private bool isDisplayPanel = false;
    private Dictionary<GameObject, Coroutine> panelCoroutines = new Dictionary<GameObject, Coroutine>();
    private Dictionary<Image, Coroutine> imageCoroutines = new Dictionary<Image, Coroutine>();
    private float currentVignetteValue = 0f;
    private Coroutine vignetteCoroutine;
    private float cachedOriginalAlpha = 1.0f;
    private int currentPressureLevel = 0;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        InitializeUI();
        InitializeEvents();
        InitializePressureSound();
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
        if (GameManager.Instance != null) GameManager.Instance.OnPauseStateChanged -= HandlePauseState;
        StopAllPressureSounds();
    }
    #endregion

    #region Initialization
    private void InitializeUI()
    {
        if (cautionPanel) cautionPanel.SetActive(true);
        if (situationPanel) situationPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);
        if (instructionPanel) instructionPanel.SetActive(false);
        if (progressPanel) progressPanel.SetActive(false);
        if (outtroManager) outtroManager.gameObject.SetActive(false);

        HideAllTipsImages();

        if (pressureVignette != null) pressureVignette.SetIntensity(0f);
    }

    private void InitializeEvents()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnPauseStateChanged += HandlePauseState;
    }

    private void InitializePressureSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(SFXType.heartbeat, isLoop: true);
            AudioManager.Instance.PlaySFX(SFXType.breath, isLoop: true);
            AudioManager.Instance.PlaySFX(SFXType.EarRinging, isLoop: true);
            UpdatePressureSoundVolume(0);
        }
    }

    private void StopAllPressureSounds()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopSFX(SFXType.heartbeat);
            AudioManager.Instance.StopSFX(SFXType.breath);
            AudioManager.Instance.StopSFX(SFXType.EarRinging);
        }
    }
    #endregion

    #region Input Handlers & Haptics

    private void TriggerInteractionFeedback()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SFXType.UI_Click);
        TriggerHapticImpulse();
    }

    private void TriggerHapticImpulse(float rawAmplitude = 0.5f, float duration = 0.1f)
    {
        // [진동 정규화 적용] DataManager를 통해 보정된 값 가져오기
        float finalAmplitude = rawAmplitude;
        if (DataManager.Instance != null)
        {
            finalAmplitude = DataManager.Instance.GetAdjustedHapticStrength(rawAmplitude);
        }

        if (finalAmplitude <= 0.01f) return;

        var inputDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, inputDevices);

        foreach (var device in inputDevices)
        {
            if (device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
            {
                device.SendHapticImpulse(0, finalAmplitude, duration);
            }
        }
    }

    private void HandleYButtonInput()
    {
        if (this == null || !gameObject.activeInHierarchy) return;
        TriggerInteractionFeedback();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TogglePause();
            SetDisplayPanel(true);
        }
    }

    private void HandleAButtonInput()
    {
        if (this == null || !gameObject.activeInHierarchy) return;
        bool actionTaken = false;

        if (pausePanel != null && pausePanel.activeSelf)
        {
            TriggerInteractionFeedback();
            Time.timeScale = 1f;
            StopAllPressureSounds();
            GameManager.Instance.LoadScene("Main_Intro");
            actionTaken = true;
        }
        else if (instructionPanel != null && instructionPanel.activeSelf)
        {
            CloseInstructionPanel();
            SetDisplayPanel(false);
            actionTaken = true;
        }
        else if (cautionPanel != null && cautionPanel.activeSelf)
        {
            CloseCautionPanel();
            SetDisplayPanel(false);
            actionTaken = true;
        }
        else if (situationPanel != null && situationPanel.activeSelf)
        {
            CloseSituationPanel();
            SetDisplayPanel(false);
            actionTaken = true;
        }
        if (actionTaken) TriggerInteractionFeedback();
    }

    private void HandleBButtonInput()
    {
        if (this == null || !gameObject.activeInHierarchy) return;
        if (pausePanel != null && pausePanel.activeSelf)
        {
            TriggerInteractionFeedback();
            if (GameManager.Instance != null) GameManager.Instance.TogglePause();
            SetDisplayPanel(false);
        }
    }
    #endregion

    #region Public UI API (Panel Control)
    public void OpenCautionPanel() { if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SFXType.Fail_Feedback); FadePanel(cautionPanel, true); SetDisplayPanel(true); }
    public void CloseCautionPanel() { FadePanel(cautionPanel, false); SetDisplayPanel(false); }
    public void OpenSituationPanel() { if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SFXType.Pause_Feedback); FadePanel(situationPanel, true); SetDisplayPanel(true); }
    public void CloseSituationPanel() { FadePanel(situationPanel, false); SetDisplayPanel(false); }
    public void OpenInstructionPanel() { if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SFXType.Pause_Feedback); FadePanel(instructionPanel, true); SetDisplayPanel(true); }
    public void CloseInstructionPanel() { FadePanel(instructionPanel, false); SetDisplayPanel(false); CloseInstruction(); CloseFeedBack(); }
    public void OpenProgressPanel(string missionText) { if (progressMissionText) progressMissionText.text = missionText; if (progressPanel) FadePanel(progressPanel, true); }
    public void CloseProgressPanel() { if (progressPanel) FadePanel(progressPanel, false); HideAllTipsImages(); }
    public void OpenPressurePanel() { if (pressurePanel) FadePanel(pressurePanel, true); }
    public void ClosePressurePanel() { if (pressurePanel) FadePanel(pressurePanel, false); }
    public void ShowOuttroUI() { StopAllPressureSounds(); if (IngameCanvas) IngameCanvas.enabled = false; if (outtroManager) { outtroManager.gameObject.SetActive(true); StartCoroutine(outtroManager.InitializeRoutine()); } }
    #endregion

    #region Public UI API (Content Updates)
    public void UpdateInstruction(int instructionNum) { if (instruction[currentInstruction].activeSelf) instruction[currentInstruction].SetActive(false); instruction[instructionNum].SetActive(true); currentInstruction = instructionNum; }
    public void CloseInstruction() { instruction[currentInstruction].SetActive(false); }
    public void UpdateFeedBack(int FeedbackNum) { if (feedback[currentFeedback].activeSelf) feedback[currentFeedback].SetActive(false); feedback[FeedbackNum].SetActive(true); currentFeedback = FeedbackNum; if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SFXType.Success_Feedback); }
    public void UpdateNegativeFeedback(int FeedbackNum) { if (negativeFeedback[currentNegativeFeedback].activeSelf) negativeFeedback[currentNegativeFeedback].SetActive(false); negativeFeedback[FeedbackNum].SetActive(true); currentNegativeFeedback = FeedbackNum; if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SFXType.Fail_Feedback); }
    public void CloseFeedBack() { if (feedback[currentFeedback].activeSelf) feedback[currentFeedback].SetActive(false); if (negativeFeedback[currentNegativeFeedback].activeSelf) negativeFeedback[currentNegativeFeedback].SetActive(false); }
    public void SetDisplayPanel(bool state) { isDisplayPanel = state; }
    public bool GetDisplayPanel() { return isDisplayPanel; }
    public void DisplayTipsImage(int pageIndex) { if (tipsImage == null) return; for (int i = 0; i < tipsImage.Length; i++) if (tipsImage[i] != null) tipsImage[i].gameObject.SetActive(i == pageIndex); }
    public void HideAllTipsImages() { if (tipsImage == null) return; foreach (var img in tipsImage) if (img != null) img.gameObject.SetActive(false); }
    #endregion

    #region Pressure & Vignette Logic
    public void SetPressureIntensity(float targetIntensity) { if (pressureVignette == null) return; if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine); vignetteCoroutine = StartCoroutine(SmoothVignetteRoutine(targetIntensity)); }
    private IEnumerator SmoothVignetteRoutine(float target) { float start = currentVignetteValue; float elapsed = 0f; while (elapsed < vignetteSmoothTime) { elapsed += Time.deltaTime; currentVignetteValue = Mathf.Lerp(start, target, elapsed / vignetteSmoothTime); pressureVignette.SetIntensity(currentVignetteValue); yield return null; } currentVignetteValue = target; pressureVignette.SetIntensity(target); }

    public void UpdatePressureGauge(int level)
    {
        currentPressureLevel = level;
        UpdatePressureSoundVolume(level);
        if (pressureStateText) { int stateIndex = Mathf.Clamp(level, 0, PressureState.Length - 1); pressureStateText.text = PressureState[stateIndex]; }
        float maxLevel = 5.0f; float intensity = Mathf.Clamp01((float)level / maxLevel); SetPressureIntensity(intensity);
        if (pressureGaugeImages == null) return; int targetIndex = level - 1;
        for (int i = 0; i < pressureGaugeImages.Length; i++)
        {
            Image img = pressureGaugeImages[i]; Image highlightImg = pressureHighlightImages[i];
            if (img == null || highlightImg == null) continue;
            if (imageCoroutines.ContainsKey(img) && imageCoroutines[img] != null) StopCoroutine(imageCoroutines[img]);
            if (imageCoroutines.ContainsKey(highlightImg) && imageCoroutines[highlightImg] != null) StopCoroutine(imageCoroutines[highlightImg]);
            bool shouldBeOn = (i < level); bool isPulseTarget = (i == targetIndex);
            if (shouldBeOn) imageCoroutines[img] = StartCoroutine(FadeImageRoutine(img, 1.0f, true, false)); else imageCoroutines[img] = StartCoroutine(FadeImageRoutine(img, 0.0f, false, false));
            if (isPulseTarget) imageCoroutines[highlightImg] = StartCoroutine(FadeImageRoutine(highlightImg, 1.0f, true, isPulseTarget)); else imageCoroutines[highlightImg] = StartCoroutine(FadeImageRoutine(highlightImg, 0.0f, false, false));
        }
    }

    private void UpdatePressureSoundVolume(int level)
    {
        if (AudioManager.Instance == null) return;
        float maxLevel = 5.0f;
        float ratio = Mathf.Clamp01((float)level / maxLevel);
        AudioManager.Instance.SetLoopingSFXScale(SFXType.heartbeat, ratio);
        AudioManager.Instance.SetLoopingSFXScale(SFXType.breath, ratio);
        AudioManager.Instance.SetLoopingSFXScale(SFXType.EarRinging, ratio);
    }
    #endregion

    #region Mission Timer Logic
    public IEnumerator StartMissionTimer(string missionText, float totalTime, System.Func<bool> isMissionCompleteCondition, System.Func<float> progressCalculator = null, bool isDisplyPanel = false)
    {
        float currentTime = totalTime; float timeSpent = 0f;
        if (progressCalculator != null && progressText) progressText.text = "0 %"; else if (progressText) progressText.text = $"{totalTime} s";
        if (isDisplyPanel) OpenProgressPanel(missionText);
        while (!isMissionCompleteCondition.Invoke())
        {
            currentTime -= Time.deltaTime; timeSpent += Time.deltaTime;
            if (progressCalculator != null) { float currentProgress = progressCalculator.Invoke(); if (progressText) progressText.text = $"{(currentProgress * 100f):F0} %"; if (barSlider) barSlider.fillAmount = currentProgress; }
            else { if (progressText) progressText.text = $"{Mathf.CeilToInt(currentTime)} s"; if (barSlider) barSlider.fillAmount = currentTime / totalTime; }
            yield return null;
        }
        if (DataManager.Instance != null) { DataManager.Instance.AddSuccessCount(); DataManager.Instance.AddPlayTime(timeSpent); }
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SFXType.Success_Feedback);
        if (isDisplyPanel) CloseProgressPanel();
    }
    #endregion

    #region Visual Effects (Fades & Pulse)
    private void HandlePauseState(bool isPaused) { if (pausePanel) pausePanel.SetActive(isPaused); if (isPaused && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SFXType.Pause_Feedback); }
    private void FadePanel(GameObject panel, bool show) { if (panel == null) return; CanvasGroup cg = panel.GetComponent<CanvasGroup>(); if (cg == null) cg = panel.AddComponent<CanvasGroup>(); if (panelCoroutines.ContainsKey(panel) && panelCoroutines[panel] != null) StopCoroutine(panelCoroutines[panel]); panelCoroutines[panel] = StartCoroutine(FadePanelRoutine(panel, cg, show)); }
    private IEnumerator FadePanelRoutine(GameObject panel, CanvasGroup cg, bool show) { float targetAlpha = show ? 1.0f : 0.0f; float startAlpha = cg.alpha; float elapsed = 0f; if (show) { panel.SetActive(true); cg.alpha = 0f; startAlpha = 0f; } while (elapsed < panelFadeDuration) { elapsed += Time.deltaTime; cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / panelFadeDuration); yield return null; } cg.alpha = targetAlpha; if (!show) panel.SetActive(false); }
    private IEnumerator FadeImageRoutine(Image targetImage, float targetAlpha, bool activeState, bool startPulseAfterFade) { if (activeState && !targetImage.gameObject.activeSelf) { targetImage.gameObject.SetActive(true); Color c = targetImage.color; targetImage.color = new Color(c.r, c.g, c.b, 0f); } else if (!activeState && !targetImage.gameObject.activeSelf) { yield break; } Color color = targetImage.color; float startAlpha = color.a; float elapsed = 0f; while (elapsed < imageFadeDuration) { elapsed += Time.deltaTime; float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / imageFadeDuration); targetImage.color = new Color(color.r, color.g, color.b, newAlpha); yield return null; } targetImage.color = new Color(color.r, color.g, color.b, targetAlpha); if (!activeState) { targetImage.gameObject.SetActive(false); } else if (startPulseAfterFade) { cachedOriginalAlpha = targetAlpha; imageCoroutines[targetImage] = StartCoroutine(PulseImageRoutine(targetImage)); } }
    private IEnumerator PulseImageRoutine(Image targetImage) { Color originalColor = targetImage.color; while (true) { float alphaRatio = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) / 2.0f; float targetAlpha = Mathf.Lerp(minPulseAlpha, cachedOriginalAlpha, alphaRatio); targetImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, targetAlpha); yield return null; } }
    #endregion
}