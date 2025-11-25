using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 인게임 UI 매니저
/// </summary>
public class IngameUIManager : MonoBehaviour

{

    [Header("HUD Elements")]
    [SerializeField] private Canvas IngameCanvas;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI missionText;
    [SerializeField] private TextMeshProUGUI feedBackText;
    [SerializeField] private Image actionGauge;

    [Header("Panels")]
    [SerializeField] private GameObject cautionPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject instructionPanel;

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
        if (actionGauge) actionGauge.fillAmount = 0f;
        if (pausePanel) pausePanel.SetActive(false);
        if (instructionPanel) instructionPanel.SetActive(false);
        if (outtroManager) outtroManager.gameObject.SetActive(false);

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
    public void OpenCautionPanel() => cautionPanel.SetActive(true);
    public void CloseCautionPanel() => cautionPanel.SetActive(false);
    public void OpenInstructionPanel() => instructionPanel.SetActive(true);
    public void CloseInstructionPanel() => instructionPanel.SetActive(false);

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

    public void UpdateActionGauge(float ratio)
    {
        if (actionGauge) actionGauge.fillAmount = ratio;
    }

    public void SetPressureIntensity(float intensity)
    {
        if (vignette != null)
        {
            vignette.intensity.value = intensity;
            vignette.color.value = Color.Lerp(Color.black, Color.red, intensity);
        }
    }

    public void SetCameraShake(bool isShaking)
    {
        if (cameraOffset == null) return;
        if (isShaking) StartCoroutine(ShakeRoutine());
        else
        {
            StopAllCoroutines();
            cameraOffset.localPosition = Vector3.zero;
        }
    }

    private IEnumerator ShakeRoutine()
    {
        while (true)
        {
            cameraOffset.localPosition = Random.insideUnitSphere * 0.05f;
            yield return null;
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
}