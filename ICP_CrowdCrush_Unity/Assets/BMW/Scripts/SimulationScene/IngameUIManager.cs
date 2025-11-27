using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 인게임 UI 매니저
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
    private readonly string[] PressureState = new string[]
    {
        "정상 (Safe)",        // Level 0
        "주의 (Caution)",     // Level 1
        "경고 (Warning)",     // Level 2
        "위험 (Critical)",    // Level 3
        "압박 (Pressure)",    // Level 4
        "최대 위험 (DANGER)"  // Level 5
    };
    [SerializeField] public Image[] pressureGaugeImages;

    [Header("Effects")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField] private Transform cameraOffset;

    [Header("External References")]
    [SerializeField] private OuttroUIManager outtroManager;

    private Vignette vignette;
    private bool isDisplayPanel = false;

    private void Start()
    {
        // 초기화
        if (pausePanel) pausePanel.SetActive(false);
        if (instructionPanel) instructionPanel.SetActive(false);
        if (outtroManager) outtroManager.gameObject.SetActive(false);
        if (progressPanel) progressPanel.SetActive(false);
        HideAllTipsImages();

        if (postProcessVolume && postProcessVolume.profile.TryGet(out vignette))
        {
            vignette.intensity.value = 0f;
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

    // --- 입력 핸들러 ---

    // [Y 버튼] 게임 일시정지/재개 토글
    private void HandleYButtonInput()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TogglePause();
            SetDisplayPanel(true);
        }
    }

    // [A 버튼] 상황별 동작
    private void HandleAButtonInput()
    {
        // 1. 일시정지 상태 -> 인트로 씬으로 이동
        // pausePanel에 대한 null 체크 추가
        if (pausePanel != null && pausePanel.activeSelf)
        {
            Time.timeScale = 1f;
            GameManager.Instance.LoadScene("IntroScene");
        }
        // 2. 안내 패널이 떠있음 -> 닫기
        // instructionPanel에 대한 null 체크 추가
        else if (instructionPanel != null && instructionPanel.activeSelf)
        {
            CloseInstructionPanel();
            SetDisplayPanel(false);
        }
        // 3. 주의사항 패널 -> 닫기
        else if (cautionPanel != null && cautionPanel.activeSelf)
        {
            CloseCautionPanel();
            SetDisplayPanel(false);
        }
    }

    // [B 버튼] 상황별 동작
    private void HandleBButtonInput()
    {
        // 1. 일시정지 상태 -> 게임 재개 (정지 풀림)
        if (pausePanel.activeSelf)
        {
            if (GameManager.Instance != null) GameManager.Instance.TogglePause();
            SetDisplayPanel(false);
        }
    }

    // --- UI 제어 메서드 (GameStepManager에서 호출) ---
    public void OpenCautionPanel()
    {
        cautionPanel.SetActive(true);
        SetDisplayPanel(true);
    }
    public void CloseCautionPanel()
    {
        cautionPanel.SetActive(false);
        SetDisplayPanel(false);
    }

    public void OpenInstructionPanel()
    {
        instructionPanel.SetActive(true);
        SetDisplayPanel(true);
    }
    public void CloseInstructionPanel()
    {
        instructionPanel.SetActive(false);
        SetDisplayPanel(false);
        UpdateInstruction("");
        UpdateMission("");
        UpdateFeedBack("");
    }
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
    public void OpenPressurePanel()
    {
        if (pressurePanel) pressurePanel.SetActive(true);
    }

    public void ClosePressurePanel()
    {
        if (pressurePanel) pressurePanel.SetActive(false);
    }

    public void UpdateInstruction(string text)
    {
        if (instructionText) instructionText.text = text;
    }

    public void UpdateMission(string text)
    {
        if (missionText) missionText.text = text;
        if (feedBackText) feedBackText.text = ""; // 미션 갱신 시 피드백 초기화
    }

    public void UpdateFeedBack(string text)
    {
        if (feedBackText) feedBackText.text = text;
        // 피드백 뜰 때 미션 텍스트를 유지할지 지울지는 선택 (여기선 유지)
    }

    public void SetDisplayPanel(bool state)
    {
        isDisplayPanel = state;
    }

    public bool GetDisplayPanel()
    {
        return isDisplayPanel;
    }

    public void SetPressureIntensity(float intensity)
    {
        if (vignette != null)
        {
            vignette.intensity.value = intensity;
            vignette.color.value = Color.Lerp(Color.black, Color.red, intensity);
        }
    }

    // Tips 이미지를 순서(페이지)에 맞게 표시
    public void DisplayTipsImage(int pageIndex)
    {
        if (tipsImage == null || tipsImage.Length == 0) return;

        for (int i = 0; i < tipsImage.Length; i++)
        {
            if (tipsImage[i] != null)
            {
                // 인덱스가 일치하는 이미지만 활성화 (페이지 표시)
                tipsImage[i].gameObject.SetActive(i == pageIndex);
            }
        }
    }

    // Tips 이미지를 모두 비활성화
    public void HideAllTipsImages()
    {
        if (tipsImage == null || tipsImage.Length == 0) return;

        foreach (var img in tipsImage)
        {
            if (img != null)
            {
                img.gameObject.SetActive(false);
            }
        }
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
            outtroManager.Initialize();
        }
    }

    public void UpdatePressureGauge(int level)
    {
        if (pressureGaugeImages == null || pressureGaugeImages.Length == 0) return;

        // 게이지 이미지 활성화/비활성화
        for (int i = 0; i < pressureGaugeImages.Length; i++)
        {
            if (pressureGaugeImages[i] != null)
            {
                // level이 0이면 모두 비활성화, level이 1이면 0번만, level이 6이면 0~5번 모두 활성화
                pressureGaugeImages[i].gameObject.SetActive(i < level);

            }
        }

        // 상태 텍스트 업데이트
        if (pressureStateText)
        {
            pressureStateText.text = PressureState[level];
        }
    }

    public IEnumerator StartMissionTimer(string missionText, float totalTime, System.Func<bool> isMissionCompleteCondition, System.Func<float> progressCalculator = null)
    {
        float currentTime = totalTime;
        float timeSpent = 0f;

        if (progressCalculator != null && progressText) progressText.text = $"0 %";
        else if (progressText) progressText.text = $"{totalTime} s";

        OpenProgressPanel(missionText);

        while (!isMissionCompleteCondition.Invoke())
        {

            // 1. 시간 업데이트
            currentTime -= Time.deltaTime;
            timeSpent += Time.deltaTime;

            // 2. UI 업데이트 (로직 개선)

            if (progressCalculator != null)
            {
                // 퍼센트 모드
                float currentProgress = progressCalculator.Invoke();

                if (progressText) progressText.text = $"{(currentProgress * 100f).ToString("F0")} %";
                if (barSlider) barSlider.value = currentProgress;
            }
            else
            {
                // [B] 타이머 모드
                if (progressText) progressText.text = $"{Mathf.CeilToInt(currentTime).ToString()} s";
                else if (progressText) progressText.text = $"{Mathf.CeilToInt(currentTime).ToString()} s";

                // 슬라이더: 줄어듦 (1 -> 0)
                if (barSlider) barSlider.value = currentTime / totalTime;
            }

            yield return null;
        }

        CloseProgressPanel();
        yield return timeSpent;
    }
}