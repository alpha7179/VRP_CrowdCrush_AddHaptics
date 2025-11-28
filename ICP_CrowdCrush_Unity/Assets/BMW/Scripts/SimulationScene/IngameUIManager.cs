using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 UI 매니저: HUD 요소, 각종 패널 관리 및 XR Vignette 기반 압박 효과 제어
/// </summary>
public class IngameUIManager : MonoBehaviour
{

    [Header("HUD Elements")]
    [SerializeField] private Canvas IngameCanvas; // 최상위 UI 캔버스

    [Header("Panels")]
    [SerializeField] private GameObject cautionPanel;    // 주의사항 패널
    [SerializeField] private GameObject pausePanel;      // 일시정지 패널
    [SerializeField] private GameObject instructionPanel; // 미션/안내 텍스트 패널
    [SerializeField] private GameObject progressPanel;   // 미션 진행 상황 (타이머/게이지) 패널
    [SerializeField] private GameObject pressurePanel;   // 압박 게이지 표시 패널

    [Header("Panels UI Elements")]
    [SerializeField] private TextMeshProUGUI instructionText; // 안내/지시 텍스트
    [SerializeField] private TextMeshProUGUI missionText;     // 미션 목표 텍스트
    [SerializeField] private TextMeshProUGUI feedBackText;    // 결과 피드백 텍스트

    [SerializeField] private TextMeshProUGUI progressMissionText; // 진행 패널의 미션명
    [SerializeField] public TextMeshProUGUI progressText;      // 진행 상황 (시간 또는 퍼센트) 텍스트
    [SerializeField] public Slider barSlider;                 // 진행 상황을 표시하는 슬라이더
    [SerializeField] public Image[] tipsImage;                // 단계별 팁 이미지 (페이지)

    [SerializeField] private TextMeshProUGUI pressureStateText; // 현재 압박 상태 텍스트
    // 압박 상태 문자열 배열
    private readonly string[] PressureState = new string[]
    {
        "정상", "주의", "경고", "압박", "위험", "최대 위험"
    };
    [SerializeField] public Image[] pressureGaugeImages;      // 압박 게이지 이미지 배열

    [Header("Effects")]
    [SerializeField] private PressureVignette pressureVignette;
    [SerializeField] private float pulseSpeed = 5.0f; // 깜빡임 속도
    [SerializeField] private float minPulseAlpha = 0.2f; // 최소 투명도 (0~1)
    private Coroutine gaugePulseCoroutine; // 현재 돌아가는 깜빡임 코루틴 저장용
    private Image currentPulsingImage;     // 현재 깜빡이고 있는 이미지 저장용
    private float cachedOriginalAlpha;

    [Header("External References")]
    [SerializeField] private OuttroUIManager outtroManager; // 게임 종료 UI 관리자

    private bool isDisplayPanel = false;    // 현재 UI 패널이 열려있는지 여부 (입력 제어용)


    // =================================================================================
    // Unity 생명 주기 메서드
    // =================================================================================

    private void Start()
    {
        // 초기 UI 상태 설정: 대부분의 패널 비활성화
        if (pausePanel) pausePanel.SetActive(false);
        if (instructionPanel) instructionPanel.SetActive(false);
        if (outtroManager) outtroManager.gameObject.SetActive(false);
        if (progressPanel) progressPanel.SetActive(false);
        HideAllTipsImages();

        // 압박 효과 초기화 (0으로 설정)
        if (pressureProvider != null)
        {
            pressureProvider.SetPressure(0f);
        }

        // GameManager의 일시정지 이벤트 구독
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPauseStateChanged += HandlePauseState;
        }
    }

    private void OnEnable()
    {
        // 컨트롤러 입력 이벤트 구독
        if (ControllerInputManager.Instance != null)
        {
            ControllerInputManager.Instance.OnAButtonDown += HandleAButtonInput;
            ControllerInputManager.Instance.OnBButtonDown += HandleBButtonInput;
            ControllerInputManager.Instance.OnYButtonDown += HandleYButtonInput;
        }
    }

    private void OnDisable()
    {
        // 컨트롤러 입력 이벤트 구독 해제
        if (ControllerInputManager.Instance != null)
        {
            ControllerInputManager.Instance.OnAButtonDown -= HandleAButtonInput;
            ControllerInputManager.Instance.OnBButtonDown -= HandleBButtonInput;
            ControllerInputManager.Instance.OnYButtonDown -= HandleYButtonInput;
        }
    }

    private void OnDestroy()
    {
        // GameManager 이벤트 구독 해제
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPauseStateChanged -= HandlePauseState;
        }
    }

    // =================================================================================
    // 입력 핸들러 (Input Handlers)
    // =================================================================================

    // [Y 버튼] 게임 일시정지/재개 토글
    private void HandleYButtonInput()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TogglePause();
            SetDisplayPanel(true); // 일시정지 패널이 열렸으므로 패널 표시 상태를 true로 설정
        }
    }

    // [A 버튼] 상황별 동작 (주로 UI 닫기)
    private void HandleAButtonInput()
    {
        // 1. 일시정지 상태: 인트로 씬으로 이동 (게임 종료 및 재시작)
        if (pausePanel != null && pausePanel.activeSelf)
        {
            Time.timeScale = 1f;
            GameManager.Instance.LoadScene("IntroScene");
        }
        // 2. 안내 패널이 떠있음: 닫기
        else if (instructionPanel != null && instructionPanel.activeSelf)
        {
            CloseInstructionPanel();
            SetDisplayPanel(false);
        }
        // 3. 주의사항 패널: 닫기
        else if (cautionPanel != null && cautionPanel.activeSelf)
        {
            CloseCautionPanel();
            SetDisplayPanel(false);
        }
    }

    // [B 버튼] 상황별 동작
    private void HandleBButtonInput()
    {
        // 1. 일시정지 상태: 게임 재개
        if (pausePanel.activeSelf)
        {
            if (GameManager.Instance != null) GameManager.Instance.TogglePause();
            SetDisplayPanel(false);
        }
    }

    // =================================================================================
    // UI 제어 메서드 (외부 호출용)
    // =================================================================================

    // 주의사항 패널 제어
    public void OpenCautionPanel() { cautionPanel.SetActive(true); SetDisplayPanel(true); }
    public void CloseCautionPanel() { cautionPanel.SetActive(false); SetDisplayPanel(false); }

    // 안내 패널 제어
    public void OpenInstructionPanel() { instructionPanel.SetActive(true); SetDisplayPanel(true); }
    public void CloseInstructionPanel()
    {
        instructionPanel.SetActive(false);
        SetDisplayPanel(false);
        UpdateInstruction(""); // 텍스트 초기화
        UpdateMission("");
        UpdateFeedBack("");
    }

    // 진행 상황 패널 제어
    public void OpenProgressPanel(string missionText)
    {
        if (progressMissionText) progressMissionText.text = missionText;
        if (progressPanel) progressPanel.SetActive(true);
    }
    public void CloseProgressPanel()
    {
        if (progressPanel) progressPanel.SetActive(false);
        HideAllTipsImages();
    }

    // 압박 패널 제어
    public void OpenPressurePanel() { if (pressurePanel) pressurePanel.SetActive(true); }
    public void ClosePressurePanel() { if (pressurePanel) pressurePanel.SetActive(false); }

    // 텍스트 업데이트
    public void UpdateInstruction(string text) { if (instructionText) instructionText.text = text; }
    public void UpdateMission(string text)
    {
        if (missionText) missionText.text = text;
        if (feedBackText) feedBackText.text = ""; // 미션 갱신 시 피드백 초기화
    }
    public void UpdateFeedBack(string text) { if (feedBackText) feedBackText.text = text; }

    // 패널 표시 상태 관리
    public void SetDisplayPanel(bool state) { isDisplayPanel = state; }
    public bool GetDisplayPanel() { return isDisplayPanel; }

    //  비네팅(압박) 효과 강도 설정 (Provider 호출)
    public void SetPressureIntensity(float intensity)
    {

        // Tunneling Vignette Provider 호출
        if (pressureProvider != null)
        {
            pressureProvider.SetPressure(intensity);
        }
    }

    // Tips 이미지 표시 관리
    public void DisplayTipsImage(int pageIndex)
    {
        if (tipsImage == null || tipsImage.Length == 0) return;
        for (int i = 0; i < tipsImage.Length; i++)
        {
            if (tipsImage[i] != null)
            {
                tipsImage[i].gameObject.SetActive(i == pageIndex);
            }
        }
    }
    public void HideAllTipsImages()
    {
        if (tipsImage == null || tipsImage.Length == 0) return;
        foreach (var img in tipsImage)
        {
            if (img != null) { img.gameObject.SetActive(false); }
        }
    }

    // 일시정지 상태 변경 이벤트 핸들러
    private void HandlePauseState(bool isPaused)
    {
        if (pausePanel) pausePanel.SetActive(isPaused);
    }

    // 아웃트로 UI 표시
    public void ShowOuttroUI()
    {
        if (IngameCanvas) IngameCanvas.enabled = false;
        if (outtroManager)
        {
            outtroManager.gameObject.SetActive(true);
            outtroManager.Initialize();
        }
    }

    // 압박 게이지 UI 업데이트
    public void UpdatePressureGauge(int level)
    {
        // 1. 이전에 깜빡이던 게 있다면 멈추고 '원래 알파값'으로 복구
        if (gaugePulseCoroutine != null)
        {
            StopCoroutine(gaugePulseCoroutine);
        }

        if (currentPulsingImage != null)
        {
            Color c = currentPulsingImage.color;
            // [수정] 1.0f가 아니라 저장해둔 cachedOriginalAlpha로 복구
            currentPulsingImage.color = new Color(c.r, c.g, c.b, cachedOriginalAlpha);
            currentPulsingImage = null;
        }

        // 2. 게이지 이미지 활성화/비활성화 (기존 로직 유지)
        if (pressureGaugeImages == null || pressureGaugeImages.Length == 0) return;

        for (int i = 0; i < pressureGaugeImages.Length; i++)
        {
            if (pressureGaugeImages[i] != null)
            {
                pressureGaugeImages[i].gameObject.SetActive(i < level);
            }
        }

        // 3. 텍스트 및 비네팅 업데이트 (기존 로직 유지)
        if (pressureStateText)
        {
            int stateIndex = Mathf.Clamp(level, 0, PressureState.Length - 1);
            pressureStateText.text = PressureState[stateIndex];
        }

        float maxLevel = 5.0f;
        float intensity = Mathf.Clamp01((float)level / maxLevel);
        SetPressureIntensity(intensity);

        // 4. 새로운 타겟 이미지 설정 및 '현재 알파값 저장'
        int targetIndex = level - 1;

        if (targetIndex >= 0 && targetIndex < pressureGaugeImages.Length)
        {
            currentPulsingImage = pressureGaugeImages[targetIndex];

            if (currentPulsingImage.gameObject.activeSelf)
            {
                // 깜빡이기 시작하기 전, 지금의 알파값을 저장해둠!
                cachedOriginalAlpha = currentPulsingImage.color.a;

                gaugePulseCoroutine = StartCoroutine(PulseImageRoutine(currentPulsingImage));
            }
        }
    }

    // =================================================================================
    // 코루틴: 미션 타이머/진행 상황 표시
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

        CloseProgressPanel();
        yield return timeSpent;
    }

    // 특정 이미지를 계속 깜빡이게 하는 코루틴
    private IEnumerator PulseImageRoutine(Image targetImage)
    {
        Color originalColor = targetImage.color;

        while (true)
        {
            // Time.time을 이용해 -1 ~ 1 사이의 값을 0 ~ 1 사이로 변환하여 Alpha값 계산
            float alphaRatio = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) / 2.0f;

            // 최소 투명도 ~ 1.0 사이에서 보간
            float targetAlpha = Mathf.Lerp(minPulseAlpha, cachedOriginalAlpha, alphaRatio);

            targetImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, targetAlpha);

            yield return null; // 다음 프레임까지 대기
        }
    }
}