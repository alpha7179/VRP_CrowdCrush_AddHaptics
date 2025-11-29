using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [V5] 게임 종료 후 결과 및 요약 UI 매니저 (V2 - 버튼/조이스틱 조작 버전).
/// B 버튼: [결과 -> 요약] 또는 [요약 -> 메인] 이동.
/// R 조이스틱: 요약 페이지 좌우 넘김.
/// </summary>
public class OuttroUIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private GameObject summaryPanel;

    [Header("Result Elements")]
    [SerializeField] private GameObject[] starIcons;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Summary Elements")]
    [SerializeField] private GameObject[] summaryPages;
    [SerializeField] private GameObject pageNumber;
    [SerializeField] private GameObject IntroButton;
    [SerializeField] private TextMeshProUGUI pageNumberText;

    // 버튼들은 시각적 피드백용으로 남겨둘 수 있지만, 실제 입력은 조이스틱으로 처리
    [SerializeField] private GameObject prevBtnVisual;
    [SerializeField] private GameObject nextBtnVisual;

    private int currentPageIndex = 0;

    // 조이스틱 중복 입력 방지용 플래그
    private bool isJoystickReady = true;
    [SerializeField] private float joystickThreshold = 0.5f;

    [Header("UI Fade, Pulse Settings")]
    [SerializeField] private float panelFadeDuration = 0.2f; // 패널 페이드 시간
    [SerializeField] private float imageFadeDuration = 0.3f;
    [SerializeField] private float pulseSpeed = 5.0f;
    [SerializeField] private float minPulseAlpha = 0.2f;

    // 패널별 실행 중인 코루틴 관리 (중복 실행 방지)
    private Dictionary<GameObject, Coroutine> panelCoroutines = new Dictionary<GameObject, Coroutine>();
    private Dictionary<Image, Coroutine> imageCoroutines = new Dictionary<Image, Coroutine>();
    private float cachedOriginalAlpha = 1.0f; // 펄스용 알파값 저장

    private void OnEnable()
    {
        // ControllerInputManage의 A버튼 이벤트 구독
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
        // 요약 패널이 켜져 있을 때만 조이스틱 입력 체크
        if (summaryPanel.activeSelf)
        {
            HandleJoystickInput();
        }
    }

    /// <summary>
    /// B 버튼 입력 처리 (상태 전환)
    /// </summary>
    private void HandleAButtonInput()
    {
        // [중요] 스크립트가 붙은 게임오브젝트 자체가 파괴되었거나 꺼져있다면 무시
        if (this == null || gameObject == null || !gameObject.activeInHierarchy) return;

        // 1. resultPanel이 실제로 존재하는지(null이 아닌지) 먼저 확인합니다.
        // 유니티 오브젝트는 '!= null' 체크로 파괴 여부를 알 수 있습니다.
        if (resultPanel != null && resultPanel.activeSelf)
        {
            // 결과 화면 -> 요약 보기
            ShowSummary();
        }
        // 2. summaryPanel도 마찬가지로 존재하는지 확인합니다.
        else if (summaryPanel != null && summaryPanel.activeSelf)
        {
            // 요약 화면 -> 메인으로 돌아가기 (인트로)
            if (currentPageIndex == summaryPages.Length - 1)
            {
                GoHome();
            }
        }
    }

    /// <summary>
    /// 오른쪽 조이스틱 좌우 입력 처리 (페이지 넘김)
    /// </summary>
    private void HandleJoystickInput()
    {
        if (ControllerInputManager.Instance == null) return;

        Vector2 input = ControllerInputManager.Instance.RightJoystickValue;

        // 조이스틱이 충분히 기울어졌고, 입력 가능한 상태일 때
        if (isJoystickReady)
        {
            if (input.x > joystickThreshold) // 오른쪽 -> 다음 페이지
            {
                NextPage();
                isJoystickReady = false; // 입력 잠금
            }
            else if (input.x < -joystickThreshold) // 왼쪽 -> 이전 페이지
            {
                PrevPage();
                isJoystickReady = false; // 입력 잠금
            }
        }

        // 조이스틱이 중앙으로 돌아왔을 때 입력 잠금 해제 (Deadzone 처리)
        if (Mathf.Abs(input.x) < 0.1f)
        {
            isJoystickReady = true;
        }
    }

    // --- Logic Methods ---

    public IEnumerator InitializeRoutine()
    {
        FadePanel(resultPanel,true);
        FadePanel(summaryPanel,false);

        int successCount = 0;
        int mistakeCount = 0;
        float playTime = 0f;

        if (DataManager.Instance != null)
        {
            successCount = DataManager.Instance.SuccessCount;
            mistakeCount = DataManager.Instance.MistakeCount;
            playTime = DataManager.Instance.PlayTime;
        }

        // =========================================================================
        // 별점 계산 로직
        // =========================================================================

        int starCount = 0;

        // 1. 기본 점수: 성공 횟수 (총 7페이즈 중 성공 페이즈 수. 여기서는 최대 7)
        // 모든 페이즈를 완료해야 하므로 successCount는 7로 간주하고 로직을 짭니다.
        // 만약 successCount가 7이 아니면 별점 0개로 시작할 수도 있습니다.

        // 2. 실수 횟수(MistakeCount)에 따른 페널티
        // 실수 횟수 1~2회: 별점 1개 감점
        // 실수 횟수 3회 이상: 별점 2개 감점
        int mistakePenalty = 0;
        if (mistakeCount >= 3)
        {
            mistakePenalty = 2;
        }
        else if (mistakeCount >= 1)
        {
            mistakePenalty = 1;
        }

        // 3. 시간(PlayTime) 조건에 따른 보너스/페널티
        // 총 제한 시간: 7 페이즈 * 60초 = 420초 (튜토리얼은 시간 제한 없음으로 가정하고 6 * 60 = 360초를 기준으로 하거나, 전체 미션 완료 시간을 기준으로 합니다.)
        float timeLimitForMaxStar = 300f; // 예시: 5분 (300초) 이내 완료 시 시간 보너스
        float timeLimitForMinStar = 420f; // 예시: 7분 (420초) 초과 시 시간 페널티 (총 제한 시간)

        int timeBonus = 0;
        if (playTime <= timeLimitForMaxStar)
        {
            timeBonus = 1; // 빠르게 완료 시 별점 1개 보너스
        }
        else if (playTime > timeLimitForMinStar)
        {
            timeBonus = -1; // 너무 오래 걸릴 시 별점 1개 페널티
        }

        // 4. 최종 별점 계산
        // 최대 별 3개 기준으로 시작하고, 실수/시간 페널티를 적용합니다.
        // (별 아이콘이 3개라고 가정)
        starCount = 3;
        starCount -= mistakePenalty;
        starCount += timeBonus;

        // 별점은 0개 ~ starIcons.Length (최대 3)개 사이로 제한
        starCount = Mathf.Clamp(starCount, 0, starIcons.Length);

        // 모든 별을 일단 비활성화
        foreach (var star in starIcons)
        {
            // 끄는 코루틴 실행
            StopPulseAndFadeOutStar(star);
        }

        // 잠시 대기 (모든 별이 꺼지는 시간을 확보)
        yield return new WaitForSeconds(panelFadeDuration);

        // 계산된 starCount만큼 순차적으로 켜고 펄스 효과 적용
        for (int i = 0; i < starCount; i++)
        {
            // 별 켜기 및 펄스 시작
            FadeInAndPulseStar(starIcons[i]);

            // 다음 별이 켜지기 전에 잠시 딜레이를 주어 순차적 느낌 연출
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void ShowSummary()
    {
        FadePanel(resultPanel,false);
        FadePanel(summaryPanel,true);
        currentPageIndex = 0;
        UpdateSummaryPage();
    }

    private void GoHome()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadScene("IntroScene");
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
        for (int i = 0; i < summaryPages.Length; i++)
        {
            if (summaryPages[i] != null)
                summaryPages[i].SetActive(i == currentPageIndex);
        }

        if (pageNumberText)
        {
            pageNumberText.text = $"{currentPageIndex + 1} / {summaryPages.Length}";
        }

        // 시각적 피드백 (화살표 활성/비활성)
        if (prevBtnVisual) prevBtnVisual.SetActive(currentPageIndex > 0);
        if (nextBtnVisual) nextBtnVisual.SetActive(currentPageIndex < summaryPages.Length - 1);

        if (currentPageIndex == summaryPages.Length - 1)
        {
            pageNumber.SetActive(false);
            IntroButton.SetActive(true);
        }
        else
        {
            pageNumber.SetActive(true);
            IntroButton.SetActive(false);
        }
    }

    // =================================================================================
    // 코루틴: 페이드 효과
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

    private void FadeInAndPulseStar(GameObject starObject)
    {
        if (starObject == null) return;

        // 1. Image 컴포넌트 가져오기
        Image starImage = starObject.GetComponent<Image>();
        if (starImage == null) return;

        // 2. 이미 실행 중인 코루틴이 있다면 중지
        if (imageCoroutines.ContainsKey(starImage) && imageCoroutines[starImage] != null)
        {
            StopCoroutine(imageCoroutines[starImage]);
            imageCoroutines.Remove(starImage);
        }

        // 3. FadeImageRoutine 코루틴 시작: 
        //    targetAlpha=1.0f, activeState=true, startPulseAfterFade=true 설정
        //    **코루틴 딕셔너리에 저장하지 않고 바로 실행**합니다. (FadeImageRoutine 내부에서 펄스를 저장함)
        StartCoroutine(FadeImageRoutine(starImage, 1.0f, true, true));
    }

    private void StopPulseAndFadeOutStar(GameObject starObject)
    {
        if (starObject == null) return;
        Image starImage = starObject.GetComponent<Image>();
        if (starImage == null) return;

        // 1. Pulse 중지 (Pulse 코루틴이 있다면)
        if (imageCoroutines.ContainsKey(starImage) && imageCoroutines[starImage] != null)
        {
            StopCoroutine(imageCoroutines[starImage]);
            imageCoroutines.Remove(starImage);
        }

        // 2. Fade Out 시작 (targetAlpha=0.0f, activeState=false, startPulseAfterFade=false)
        StartCoroutine(FadeImageRoutine(starImage, 0.0f, false, false));
    }
}