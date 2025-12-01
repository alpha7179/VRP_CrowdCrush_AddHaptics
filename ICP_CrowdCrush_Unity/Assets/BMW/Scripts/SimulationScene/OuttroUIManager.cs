using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [Tooltip("게임 결과(별점)를 보여주는 패널")]
    [SerializeField] private GameObject resultPanel;

    [Tooltip("게임 요약(페이지)을 보여주는 패널")]
    [SerializeField] private GameObject summaryPanel;

    #endregion

    #region Inspector Settings (Result UI)

    [Header("Result Elements")]
    [Tooltip("별점 아이콘 배열 (순서대로 1점, 2점, 3점)")]
    [SerializeField] private GameObject[] starIcons;

    [Tooltip("점수 텍스트 (사용하지 않을 경우 비워두세요)")]
    [SerializeField] private TextMeshProUGUI scoreText;

    #endregion

    #region Inspector Settings (Summary UI)

    [Header("Summary Elements")]
    [Tooltip("요약 화면의 페이지들")]
    [SerializeField] private GameObject[] summaryPages;

    [Tooltip("페이지 번호 표시 오브젝트 (마지막 페이지에선 숨김)")]
    [SerializeField] private GameObject pageNumber;

    [Tooltip("인트로(메인)로 돌아가는 버튼 오브젝트")]
    [SerializeField] private GameObject introButton;

    [Tooltip("현재 페이지 번호 텍스트")]
    [SerializeField] private TextMeshProUGUI pageNumberText;

    [Header("Visual Feedback")]
    [Tooltip("이전 페이지 버튼 비주얼 (입력은 조이스틱으로 처리)")]
    [SerializeField] private GameObject prevBtnVisual;

    [Tooltip("다음 페이지 버튼 비주얼 (입력은 조이스틱으로 처리)")]
    [SerializeField] private GameObject nextBtnVisual;

    #endregion

    #region Inspector Settings (Animation)

    [Header("UI Fade & Pulse Settings")]
    [Tooltip("패널이 켜지고 꺼지는 페이드 시간(초)")]
    [SerializeField] private float panelFadeDuration = 0.2f;

    [Tooltip("이미지(별) 페이드 시간(초)")]
    [SerializeField] private float imageFadeDuration = 0.3f;

    [Tooltip("펄스(두근거림) 효과 속도")]
    [SerializeField] private float pulseSpeed = 5.0f;

    [Tooltip("펄스 효과 시 최소 알파값")]
    [SerializeField] private float minPulseAlpha = 0.2f;

    #endregion

    #region Internal State

    private int currentPageIndex = 0;

    // 조이스틱 중복 입력 방지용
    private bool isJoystickReady = true;
    private const float JoystickThreshold = 0.5f;

    // 코루틴 관리 (중복 실행 방지)
    private Dictionary<GameObject, Coroutine> panelCoroutines = new Dictionary<GameObject, Coroutine>();
    private Dictionary<Image, Coroutine> imageCoroutines = new Dictionary<Image, Coroutine>();

    // 펄스 효과용 원본 알파값 캐싱
    private float cachedOriginalAlpha = 1.0f;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        // 컨트롤러 입력 이벤트 구독
        if (ControllerInputManager.Instance != null)
        {
            ControllerInputManager.Instance.OnAButtonDown += HandleAButtonInput;
        }
    }

    private void OnDisable()
    {
        if (ControllerInputManager.Instance != null)
        {
            ControllerInputManager.Instance.OnAButtonDown -= HandleAButtonInput;
        }
    }

    private void Update()
    {
        // 요약 패널이 활성화된 상태에서만 조이스틱 입력 처리
        if (summaryPanel.activeSelf)
        {
            HandleJoystickInput();
        }
    }

    #endregion

    #region Input Handlers

    /// <summary>
    /// A 버튼 입력 처리: [결과 -> 요약] 또는 [요약 -> 메인]으로 상태 전환
    /// </summary>
    private void HandleAButtonInput()
    {
        // 오브젝트가 비활성화 상태라면 입력 무시
        if (this == null || !gameObject.activeInHierarchy) return;

        // 1. 결과 화면인 경우 -> 요약 화면으로 전환
        if (resultPanel != null && resultPanel.activeSelf)
        {
            ShowSummary();
        }
        // 2. 요약 화면인 경우 -> 메인으로 이동
        else if (summaryPanel != null && summaryPanel.activeSelf)
        {
            // 마지막 페이지에서만 홈으로 이동 가능
            if (currentPageIndex == summaryPages.Length - 1)
            {
                GoHome();
            }
        }
    }

    /// <summary>
    /// 오른쪽 조이스틱 좌우 입력 처리: 페이지 넘김
    /// </summary>
    private void HandleJoystickInput()
    {
        if (ControllerInputManager.Instance == null) return;

        Vector2 input = ControllerInputManager.Instance.RightJoystickValue;

        // 입력 대기 상태이고, 임계값을 넘었을 때 동작
        if (isJoystickReady)
        {
            if (input.x > JoystickThreshold) // 오른쪽 -> 다음
            {
                NextPage();
                isJoystickReady = false; // 입력 잠금
            }
            else if (input.x < -JoystickThreshold) // 왼쪽 -> 이전
            {
                PrevPage();
                isJoystickReady = false; // 입력 잠금
            }
        }

        // 조이스틱이 중앙(Deadzone)으로 돌아오면 다시 입력 가능 상태로 변경
        if (Mathf.Abs(input.x) < 0.1f)
        {
            isJoystickReady = true;
        }
    }

    #endregion

    #region Logic Methods (Initialization & Navigation)

    /// <summary>
    /// 결과 화면 초기화 코루틴: 데이터를 불러와 별점을 계산하고 애니메이션을 재생합니다.
    /// </summary>
    public IEnumerator InitializeRoutine()
    {
        FadePanel(resultPanel, true);
        FadePanel(summaryPanel, false);

        // 1. 데이터 불러오기
        int successCount = 0;
        int mistakeCount = 0;
        float playTime = 0f;

        if (DataManager.Instance != null)
        {
            successCount = DataManager.Instance.SuccessCount;
            mistakeCount = DataManager.Instance.MistakeCount;
            playTime = DataManager.Instance.PlayTime;
        }

        // 2. 별점 계산 로직
        int starCount = CalculateStarCount(successCount, mistakeCount, playTime);

        // 3. 별 애니메이션 재생 (초기화 -> 순차적 켜짐)
        foreach (var star in starIcons)
        {
            StopPulseAndFadeOutStar(star); // 일단 모두 끔
        }

        yield return new WaitForSeconds(panelFadeDuration);

        // 계산된 별점만큼 순차적으로 켜기
        for (int i = 0; i < starCount; i++)
        {
            if (i < starIcons.Length)
            {
                FadeInAndPulseStar(starIcons[i]);
                yield return new WaitForSeconds(0.2f); // 순차적 딜레이
            }
        }
    }

    /// <summary>
    /// 성공 횟수, 실수, 시간을 기반으로 별점(0~3)을 계산합니다.
    /// </summary>
    private int CalculateStarCount(int successCount, int mistakeCount, float playTime)
    {
        // 기준: 기본 3점 만점 시작 -> 감점 방식 적용
        int starCount = 3;

        // 페널티 1: 실수 횟수
        if (mistakeCount >= 3) starCount -= 2;
        else if (mistakeCount >= 1) starCount -= 1;

        // 보너스/페널티 2: 플레이 시간
        // 예: 5분(300초) 이내 완료 시 보너스, 7분(420초) 초과 시 감점
        float timeLimitForMaxStar = 300f;
        float timeLimitForMinStar = 420f;

        if (playTime <= timeLimitForMaxStar) starCount += 1;
        else if (playTime > timeLimitForMinStar) starCount -= 1;

        // 최종 클램핑 (0 ~ 최대 별 개수)
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
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadScene("IntroScene");
        }
        else
        {
            // Fallback
            UnityEngine.SceneManagement.SceneManager.LoadScene("IntroScene");
        }
    }

    private void NextPage()
    {
        if (currentPageIndex < summaryPages.Length - 1)
        {
            currentPageIndex++;
            UpdateSummaryPage();
        }
    }

    private void PrevPage()
    {
        if (currentPageIndex > 0)
        {
            currentPageIndex--;
            UpdateSummaryPage();
        }
    }

    private void UpdateSummaryPage()
    {
        // 해당 페이지만 활성화
        for (int i = 0; i < summaryPages.Length; i++)
        {
            if (summaryPages[i] != null)
                summaryPages[i].SetActive(i == currentPageIndex);
        }

        // 페이지 번호 갱신
        if (pageNumberText)
        {
            pageNumberText.text = $"{currentPageIndex + 1} / {summaryPages.Length}";
        }

        // 시각적 피드백 (화살표)
        if (prevBtnVisual) prevBtnVisual.SetActive(currentPageIndex > 0);
        if (nextBtnVisual) nextBtnVisual.SetActive(currentPageIndex < summaryPages.Length - 1);

        // 마지막 페이지 처리 (Intro 버튼 표시)
        bool isLastPage = (currentPageIndex == summaryPages.Length - 1);
        if (pageNumber) pageNumber.SetActive(!isLastPage);
        if (introButton) introButton.SetActive(isLastPage);
    }

    #endregion

    #region Visual Effects (Coroutines)

    /// <summary>
    /// 패널의 CanvasGroup Alpha를 조절하여 페이드 인/아웃 합니다.
    /// </summary>
    private void FadePanel(GameObject panel, bool show)
    {
        if (panel == null) return;

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        // 중복 실행 방지
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

        if (!show)
        {
            panel.SetActive(false);
        }
    }

    /// <summary>
    /// 이미지(별)의 투명도를 조절하고, 완료 후 펄스 효과를 실행할지 결정합니다.
    /// </summary>
    private IEnumerator FadeImageRoutine(Image targetImage, float targetAlpha, bool activeState, bool startPulseAfterFade)
    {
        // 활성화 처리
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

        // 1. 페이드 애니메이션
        while (elapsed < imageFadeDuration)
        {
            elapsed += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / imageFadeDuration);
            targetImage.color = new Color(color.r, color.g, color.b, newAlpha);
            yield return null;
        }

        targetImage.color = new Color(color.r, color.g, color.b, targetAlpha);

        // 2. 후처리 (비활성화 또는 펄스 시작)
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

    /// <summary>
    /// 이미지를 주기적으로 깜빡이게(Pulse) 합니다.
    /// </summary>
    private IEnumerator PulseImageRoutine(Image targetImage)
    {
        Color originalColor = targetImage.color;

        while (true)
        {
            // Sine 파동 (0~1)
            float alphaRatio = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) / 2.0f;

            // Min ~ Original Alpha 사이 보간
            float targetAlpha = Mathf.Lerp(minPulseAlpha, cachedOriginalAlpha, alphaRatio);

            targetImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, targetAlpha);
            yield return null;
        }
    }

    // --- Star Effect Helpers ---

    private void FadeInAndPulseStar(GameObject starObject)
    {
        if (starObject == null) return;
        Image starImage = starObject.GetComponent<Image>();
        if (starImage == null) return;

        if (imageCoroutines.ContainsKey(starImage) && imageCoroutines[starImage] != null)
        {
            StopCoroutine(imageCoroutines[starImage]);
            imageCoroutines.Remove(starImage);
        }

        StartCoroutine(FadeImageRoutine(starImage, 1.0f, true, true));
    }

    private void StopPulseAndFadeOutStar(GameObject starObject)
    {
        if (starObject == null) return;
        Image starImage = starObject.GetComponent<Image>();
        if (starImage == null) return;

        if (imageCoroutines.ContainsKey(starImage) && imageCoroutines[starImage] != null)
        {
            StopCoroutine(imageCoroutines[starImage]);
            imageCoroutines.Remove(starImage);
        }

        StartCoroutine(FadeImageRoutine(starImage, 0.0f, false, false));
    }

    #endregion
}