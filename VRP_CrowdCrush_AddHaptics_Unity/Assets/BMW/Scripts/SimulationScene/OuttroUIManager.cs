using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// 게임 종료 후 결과(Result) 및 요약(Summary) 화면의 UI 흐름과 연출을 관리하는 매니저입니다.
/// <para>
/// 1. DataManager의 데이터를 기반으로 별점을 계산하고 애니메이션(펄스)을 재생합니다.<br/>
/// 2. 컨트롤러 입력(A버튼, 조이스틱)을 통해 페이지를 넘기거나 메인으로 이동합니다.<br/>
/// 3. 페이드(Fade) 및 펄스(Pulse) 효과를 코루틴으로 처리하여 시각적 피드백을 제공합니다.
/// </para>
/// </summary>
 
public class OuttroUIManager : MonoBehaviour
{
    #region Inspector Settings (Panels)
    [Header("Panels")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject summaryPanel;
    #endregion

    #region Inspector Settings (Result UI)
    [Header("Result Elements")]
    [SerializeField] private GameObject[] starIcons;
    [SerializeField] private TextMeshProUGUI scoreText;
    #endregion

    #region Inspector Settings (Summary UI)
    [Header("Summary Elements")]
    [SerializeField] private GameObject[] summaryPages;
    [SerializeField] private GameObject pageNumber;
    [SerializeField] private GameObject introButton;
    [SerializeField] private TextMeshProUGUI pageNumberText;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject prevBtnVisual;
    [SerializeField] private GameObject nextBtnVisual;
    #endregion

    #region Inspector Settings (Animation)
    [Header("UI Fade & Pulse Settings")]
    [SerializeField] private float panelFadeDuration = 0.2f;
    [SerializeField] private float imageFadeDuration = 0.3f;
    [SerializeField] private float pulseSpeed = 5.0f;
    [SerializeField] private float minPulseAlpha = 0.2f;
    #endregion

    #region Internal State
    private int currentPageIndex = 0;
    private bool isJoystickReady = true;
    private const float JoystickThreshold = 0.5f;
    private Dictionary<GameObject, Coroutine> panelCoroutines = new Dictionary<GameObject, Coroutine>();
    private Dictionary<Image, Coroutine> imageCoroutines = new Dictionary<Image, Coroutine>();
    private float cachedOriginalAlpha = 1.0f;
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        if (ControllerInputManager.Instance != null) ControllerInputManager.Instance.OnAButtonDown += HandleAButtonInput;
    }
    private void OnDisable()
    {
        if (ControllerInputManager.Instance != null) ControllerInputManager.Instance.OnAButtonDown -= HandleAButtonInput;
    }
    private void Update()
    {
        if (summaryPanel.activeSelf) HandleJoystickInput();
    }
    #endregion

    #region Feedback Helpers (Audio & Haptics)
    private void TriggerInteractionFeedback()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SFXType.UI_Click);
        TriggerHapticImpulse(0.3f, 0.1f);
    }
    private void TriggerStarFeedback()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SFXType.Success_Feedback);
        TriggerHapticImpulse(0.7f, 0.2f);
    }
    private void TriggerResultFeedback()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SFXType.Finish_Feedback);
    }

    private void TriggerHapticImpulse(float rawAmplitude, float duration)
    {
        // [진동 정규화 적용]
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
    #endregion

    #region Input Handlers
    private void HandleAButtonInput()
    {
        if (this == null || !gameObject.activeInHierarchy) return;
        if (resultPanel != null && resultPanel.activeSelf)
        {
            TriggerInteractionFeedback();
            ShowSummary();
        }
        else if (summaryPanel != null && summaryPanel.activeSelf)
        {
            if (currentPageIndex == summaryPages.Length - 1)
            {
                TriggerInteractionFeedback();
                GoHome();
            }
        }
    }

    private void HandleJoystickInput()
    {
        if (ControllerInputManager.Instance == null) return;
        Vector2 input = ControllerInputManager.Instance.RightJoystickValue;
        if (isJoystickReady)
        {
            if (input.x > JoystickThreshold) { NextPage(); isJoystickReady = false; }
            else if (input.x < -JoystickThreshold) { PrevPage(); isJoystickReady = false; }
        }
        if (Mathf.Abs(input.x) < 0.1f) isJoystickReady = true;
    }
    #endregion

    #region Logic Methods
    public IEnumerator InitializeRoutine()
    {
        FadePanel(resultPanel, true);
        FadePanel(summaryPanel, false);
        TriggerResultFeedback();

        int successCount = 0; int mistakeCount = 0; float playTime = 0f;
        if (DataManager.Instance != null)
        {
            successCount = DataManager.Instance.GetSuccessCount();
            mistakeCount = DataManager.Instance.GetMistakeCount();
            playTime = DataManager.Instance.GetPlayTime();
        }

        int starCount = CalculateStarCount(successCount, mistakeCount, playTime);
        foreach (var star in starIcons) star.SetActive(false);

        yield return new WaitForSeconds(panelFadeDuration);

        for (int i = 0; i < starCount; i++)
        {
            if (i < starIcons.Length)
            {
                FadeInAndPulseStar(starIcons[i]);
                TriggerStarFeedback();
                yield return new WaitForSeconds(0.4f);
            }
        }
    }

    private int CalculateStarCount(int successCount, int mistakeCount, float playTime)
    {
        int starCount = 3;
        if (mistakeCount >= 3) starCount -= 1;
        float timeLimitForMaxStar = 300f;
        float timeLimitForMinStar = 420f;
        if (playTime <= timeLimitForMaxStar) starCount += 1;
        else if (playTime > timeLimitForMinStar) starCount -= 1;
        return Mathf.Clamp(starCount, 0, starIcons.Length);
    }

    private void ShowSummary()
    {
        FadePanel(resultPanel, false);
        FadePanel(summaryPanel, true);
        currentPageIndex = 0;
        UpdateSummaryPage();
    }

    private void GoHome()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.StopAllAudio();
        if (GameManager.Instance != null) GameManager.Instance.LoadScene("Main_Intro");
        else UnityEngine.SceneManagement.SceneManager.LoadScene("Main_Intro");
    }

    private void NextPage() { if (currentPageIndex < summaryPages.Length - 1) { TriggerInteractionFeedback(); currentPageIndex++; UpdateSummaryPage(); } }
    private void PrevPage() { if (currentPageIndex > 0) { TriggerInteractionFeedback(); currentPageIndex--; UpdateSummaryPage(); } }

    private void UpdateSummaryPage()
    {
        for (int i = 0; i < summaryPages.Length; i++) if (summaryPages[i] != null) summaryPages[i].SetActive(i == currentPageIndex);
        if (pageNumberText) pageNumberText.text = $"{currentPageIndex + 1} / {summaryPages.Length}";
        if (prevBtnVisual) prevBtnVisual.SetActive(currentPageIndex > 0);
        if (nextBtnVisual) nextBtnVisual.SetActive(currentPageIndex < summaryPages.Length - 1);
        bool isLastPage = (currentPageIndex == summaryPages.Length - 1);
        if (pageNumber) pageNumber.SetActive(!isLastPage);
        if (introButton) introButton.SetActive(isLastPage);
    }
    #endregion

    #region Visual Effects (Coroutines)
    private void FadePanel(GameObject panel, bool show) { if (panel == null) return; CanvasGroup cg = panel.GetComponent<CanvasGroup>(); if (cg == null) cg = panel.AddComponent<CanvasGroup>(); if (panelCoroutines.ContainsKey(panel) && panelCoroutines[panel] != null) StopCoroutine(panelCoroutines[panel]); panelCoroutines[panel] = StartCoroutine(FadePanelRoutine(panel, cg, show)); }
    private IEnumerator FadePanelRoutine(GameObject panel, CanvasGroup cg, bool show) { float targetAlpha = show ? 1.0f : 0.0f; float startAlpha = cg.alpha; float elapsed = 0f; if (show) { panel.SetActive(true); cg.alpha = 0f; startAlpha = 0f; } while (elapsed < panelFadeDuration) { elapsed += Time.deltaTime; cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / panelFadeDuration); yield return null; } cg.alpha = targetAlpha; if (!show) panel.SetActive(false); }
    private IEnumerator FadeImageRoutine(Image targetImage, float targetAlpha, bool activeState, bool startPulseAfterFade) { if (activeState && !targetImage.gameObject.activeSelf) { targetImage.gameObject.SetActive(true); Color c = targetImage.color; targetImage.color = new Color(c.r, c.g, c.b, 0f); } else if (!activeState && !targetImage.gameObject.activeSelf) { yield break; } Color color = targetImage.color; float startAlpha = color.a; float elapsed = 0f; while (elapsed < imageFadeDuration) { elapsed += Time.deltaTime; float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / imageFadeDuration); targetImage.color = new Color(color.r, color.g, color.b, newAlpha); yield return null; } targetImage.color = new Color(color.r, color.g, color.b, targetAlpha); if (!activeState) { targetImage.gameObject.SetActive(false); } else if (startPulseAfterFade) { cachedOriginalAlpha = targetAlpha; imageCoroutines[targetImage] = StartCoroutine(PulseImageRoutine(targetImage)); } }
    private IEnumerator PulseImageRoutine(Image targetImage) { Color originalColor = targetImage.color; while (true) { float alphaRatio = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) / 2.0f; float targetAlpha = Mathf.Lerp(minPulseAlpha, cachedOriginalAlpha, alphaRatio); targetImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, targetAlpha); yield return null; } }
    private void FadeInAndPulseStar(GameObject starObject) { if (starObject == null) return; Image starImage = starObject.GetComponent<Image>(); if (starImage == null) return; if (imageCoroutines.ContainsKey(starImage) && imageCoroutines[starImage] != null) { StopCoroutine(imageCoroutines[starImage]); imageCoroutines.Remove(starImage); } StartCoroutine(FadeImageRoutine(starImage, 1.0f, true, true)); }
    #endregion
}