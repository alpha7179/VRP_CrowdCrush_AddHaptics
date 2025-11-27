using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 인게임 UI 매니저: HUD 요소, 각종 패널, 포스트 프로세싱 효과 등을 관리합니다.
/// </summary>
public class IngameUIManager : MonoBehaviour
{
    // =================================================================================
    // Inspector Field (직렬화된 변수)
    // =================================================================================

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
        "정상 (Safe)", "주의 (Caution)", "경고 (Warning)", "위험 (Critical)", "압박 (Pressure)", "최대 위험 (DANGER)"
    };
    [SerializeField] public Image[] pressureGaugeImages;      // 압박 게이지 이미지 배열

    [Header("Effects")]
    [SerializeField] private Volume postProcessVolume; // 포스트 프로세싱 볼륨
    [SerializeField] private Transform cameraOffset;    // (사용되지 않음 - 카메라 관련 오프셋용으로 보임)

    [Header("External References")]
    [SerializeField] private OuttroUIManager outtroManager; // 게임 종료 UI 관리자

    // =================================================================================
    // 내부 상태 변수
    // =================================================================================
    private Vignette vignette;              // 포스트 프로세싱의 비네팅 효과 (압박 시 빨간색/강도 조절)
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

        // 비네팅 효과 초기화 및 참조
        if (postProcessVolume && postProcessVolume.profile.TryGet(out vignette))
        {
            vignette.intensity.value = 0f;
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
        // 2. 안내 패널이 떠있음: 닫기 (ShowStepTextAndDelay의 조기 종료를 유발)
        else if (instructionPanel != null && instructionPanel.activeSelf)
        {
            CloseInstructionPanel();
            SetDisplayPanel(false);
        }
        // 3. 주의사항 패널: 닫기 (ScenarioRoutine의 대기 상태를 해제)
        else if (cautionPanel != null && cautionPanel.activeSelf)
        {
            CloseCautionPanel();
            SetDisplayPanel(false);
        }
    }

    // [B 버튼] 상황별 동작
    private void HandleBButtonInput()
    {
        // 1. 일시정지 상태: 게임 재개 (TogglePause 호출)
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

    // 비네팅(압박) 효과 강도 설정
    public void SetPressureIntensity(float intensity)
    {
        if (vignette != null)
        {
            vignette.intensity.value = intensity;
            // 강도에 따라 색상을 검은색에서 빨간색으로 보간
            vignette.color.value = Color.Lerp(Color.black, Color.red, intensity);
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
                tipsImage[i].gameObject.SetActive(i == pageIndex); // 해당 인덱스 이미지만 활성화
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
        if (pressureGaugeImages == null || pressureGaugeImages.Length == 0) return;

        // 게이지 이미지 활성화 (level만큼 채워짐)
        for (int i = 0; i < pressureGaugeImages.Length; i++)
        {
            if (pressureGaugeImages[i] != null)
            {
                pressureGaugeImages[i].gameObject.SetActive(i < level);
            }
        }

        // 상태 텍스트 업데이트
        if (pressureStateText)
        {
            // level은 PressureState 배열의 인덱스로 사용됨
            pressureStateText.text = PressureState[level];
        }
    }

    // =================================================================================
    // 코루틴: 미션 타이머/진행 상황 표시
    // =================================================================================

    /// <summary>
    /// 미션 타이머를 시작하고, 조건이 충족될 때까지 UI를 업데이트합니다.
    /// </summary>
    /// <param name="missionText">진행 상황 패널에 표시할 미션 텍스트</param>
    /// <param name="totalTime">총 제한 시간</param>
    /// <param name="isMissionCompleteCondition">미션 완료 조건 함수</param>
    /// <param name="progressCalculator">진행 상황(0~1)을 계산하는 함수 (null이면 타이머 모드)</param>
    public IEnumerator StartMissionTimer(string missionText, float totalTime, System.Func<bool> isMissionCompleteCondition, System.Func<float> progressCalculator = null)
    {
        float currentTime = totalTime;
        float timeSpent = 0f;

        // 초기 텍스트 설정 (진행 계산기가 있으면 퍼센트, 없으면 시간)
        if (progressCalculator != null && progressText) progressText.text = $"0 %";
        else if (progressText) progressText.text = $"{totalTime} s";

        OpenProgressPanel(missionText); // 진행 상황 패널 열기

        // 미션 완료 조건이 충족될 때까지 반복
        while (!isMissionCompleteCondition.Invoke())
        {
            currentTime -= Time.deltaTime;
            timeSpent += Time.deltaTime;

            if (progressCalculator != null)
            {
                // **1. 퍼센트/게이지 모드 (특정 액션 유지)**
                float currentProgress = progressCalculator.Invoke();

                if (progressText) progressText.text = $"{(currentProgress * 100f).ToString("F0")} %";
                if (barSlider) barSlider.value = currentProgress; // 슬라이더: 0 -> 1
            }
            else
            {
                // **2. 타이머 모드 (목표 지점 도달 등)**
                // 남은 시간 표시
                if (progressText) progressText.text = $"{Mathf.CeilToInt(currentTime).ToString()} s";

                // 슬라이더: 줄어듦 (1 -> 0)
                if (barSlider) barSlider.value = currentTime / totalTime;
            }

            yield return null;
        }

        CloseProgressPanel();
        // 미션 완료까지 걸린 시간 반환
        yield return timeSpent;
    }
}