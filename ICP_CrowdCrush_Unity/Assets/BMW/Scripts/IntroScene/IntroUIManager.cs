using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 인트로 씬의 UI 흐름(패널 전환, 메뉴 조작, 게임 시작)을 총괄하는 매니저입니다.
/// <para>
/// 1. 최상위 패널(Intro vs Start)과 하위 콘텐츠 패널(Place, Manual, Tips, Setting)을 관리합니다.<br/>
/// 2. DataManager와 연동하여 볼륨 및 편의 모드 설정을 초기화하고 변경합니다.<br/>
/// 3. 팁(Tips) 패널의 페이지 넘김 기능을 처리합니다.
/// </para>
/// </summary>
public class IntroUIManager : MonoBehaviour
{
    #region Inspector Settings (Panels)

    [Header("Top Level Panels")]
    [Tooltip("게임 시작 전 '터치하여 시작' 등을 표시하는 첫 화면 패널")]
    [SerializeField] private GameObject introPanel;

    [Tooltip("메인 메뉴 버튼들이 포함된 시작 패널")]
    [SerializeField] private GameObject startPanel;

    [Header("Sub Panels (Inside StartPanel)")]
    [Tooltip("장소 선택 패널")]
    [SerializeField] private GameObject placePanel;
    [SerializeField] private Image placeButton;
    [Tooltip("조작 설명 패널")]
    [SerializeField] private GameObject manualPanel;
    [SerializeField] private Image manualButton;
    [Tooltip("팁(도움말) 패널")]
    [SerializeField] private GameObject tipsPanel;
    [SerializeField] private Image tipsButton;
    [Tooltip("환경 설정 패널")]
    [SerializeField] private GameObject settingPanel;
    [SerializeField] private Image settingButton;

    [Header("Place Panels Elements")]
    [SerializeField] private GameObject place1Panel;
    [SerializeField] private GameObject place2Panel;
    [SerializeField] private Image place1Image;
    [SerializeField] private Image place2Image;

    #endregion

    #region Inspector Settings (UI Elements)

    [Header("Tips Panels Elements")]
    [SerializeField] private GameObject tip1;
    [SerializeField] private GameObject tip2;
    [SerializeField] private GameObject tip3;
    [SerializeField] private GameObject tip4;
    [SerializeField] private GameObject tip5;
    [Tooltip("현재 팁 페이지 번호를 표시할 텍스트")]
    [SerializeField] private TextMeshProUGUI tipPageText;

    [Header("Settings Panels Elements")]
    [Tooltip("오디오 볼륨 슬라이더")]
    [SerializeField] private Slider audioSlider;
    [Tooltip("오디오 볼륨 수치 텍스트 (0-100)")]
    [SerializeField] private TextMeshProUGUI audioText;

    [Tooltip("멀미 방지 모드 슬라이더 (0: OFF, 1: ON)")]
    [SerializeField] private Slider modeSlider;
    [Tooltip("멀미 방지 모드 상태 텍스트 (ON/OFF)")]
    [SerializeField] private TextMeshProUGUI modeText;

    [Header("Debug")]
    [SerializeField] private bool isDebug = true;

    #endregion

    #region Internal State

    private GameObject currentTopPanel;
    private GameObject currentMainPanel;
    private Image currentMainButton;

    private int tipPageNum = 1;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        InitializeSettings();
        InitializeUIState();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// DataManager의 저장된 값을 불러와 UI(슬라이더)에 반영합니다.
    /// </summary>
    private void InitializeSettings()
    {
        // 1. 오디오 슬라이더 설정
        audioSlider.minValue = 0;
        audioSlider.maxValue = 100;
        audioSlider.wholeNumbers = true;

        // DataManager가 있으면 저장된 볼륨 가져오기 (0.0~1.0 -> 0~100 변환)
        float currentVol = DataManager.Instance != null ? DataManager.Instance.MasterVolume : 1.0f;
        int displayVol = Mathf.RoundToInt(currentVol * 100f);

        audioSlider.value = displayVol;
        OnAudioSliderValueChanged(displayVol); // 텍스트 갱신
        audioSlider.onValueChanged.AddListener(OnAudioSliderValueChanged);


        // 2. 모드 슬라이더 설정
        modeSlider.minValue = 0;
        modeSlider.maxValue = 1;
        modeSlider.wholeNumbers = true;

        // DataManager가 있으면 저장된 모드 가져오기 (bool -> 0/1)
        bool isModeOn = DataManager.Instance != null && DataManager.Instance.IsAntiMotionSicknessMode;

        modeSlider.value = isModeOn ? 1 : 0;
        OnModeSliderValueChanged(isModeOn ? 1 : 0); // 텍스트 갱신
        modeSlider.onValueChanged.AddListener(OnModeSliderValueChanged);
    }

    /// <summary>
    /// 앱 시작 시 패널들의 초기 상태(활성/비활성)를 설정합니다.
    /// </summary>
    private void InitializeUIState()
    {
        // 하위 패널 모두 비활성화
        if (placePanel) placePanel.SetActive(false);
        if (manualPanel) manualPanel.SetActive(false);
        if (tipsPanel) tipsPanel.SetActive(false);
        if (settingPanel) settingPanel.SetActive(false);

        // 최상위 패널: IntroPanel 활성화
        if (startPanel) startPanel.SetActive(false);
        if (introPanel) introPanel.SetActive(true);

        currentTopPanel = introPanel;
        currentMainPanel = null;
    }

    #endregion

    #region UI Event Handlers (Navigation)

    /// <summary>
    /// [Intro 패널 클릭 시] IntroPanel을 닫고 StartPanel(메인 메뉴)을 엽니다.
    /// </summary>
    public void OnClickIntroButton()
    {
        if (isDebug) Debug.Log("IntroButton Clicked");
        SwitchTopPanel(startPanel);

        // StartPanel 진입 시 기본으로 '장소 선택' 패널을 보여줌
        SwitchMainPanel(placePanel, placeButton);
    }

    /// <summary>
    /// [장소 선택 버튼]
    /// </summary>
    public void OnClickPlaceButton()
    {
        SwitchMainPanel(placePanel, placeButton);
    }

    /// <summary>
    /// [매뉴얼 버튼]
    /// </summary>
    public void OnClickManualButton()
    {
        SwitchMainPanel(manualPanel, manualButton);
    }

    /// <summary>
    /// [설정 버튼]
    /// </summary>
    public void OnClickSettingButton()
    {
        SwitchMainPanel(settingPanel, settingButton);
    }

    /// <summary>
    /// [팁 버튼] 팁 패널을 열고 1페이지로 초기화합니다.
    /// </summary>
    public void OnClickTipsButton()
    {
        SwitchMainPanel(tipsPanel, tipsButton);
        ResetTipPage();
    }

    /// <summary>
    /// [장소 선택 버튼] 이미지 홠성화됩니다.
    /// </summary>
    public void OnClickPlace1Panel()
    {
        if (place2Image != null && place2Panel != null && place1Image != null && place1Panel != null)
        {
            place2Panel.SetActive(false);
            Color currentColor = place2Image.color;
            currentColor.a = 0.0f;
            place2Image.color = currentColor;

            place1Panel.SetActive(true);
            currentColor = place1Image.color; 
            currentColor.a = 1.0f;
            place1Image.color = currentColor;
        }
    }

    public void OnClickPlace2Panel()
    {
        if (place2Image != null && place2Panel != null && place1Image != null && place1Panel != null)
        {
            place1Panel.SetActive(false);
            Color currentColor = place1Image.color;
            currentColor.a = 0.0f;
            place1Image.color = currentColor;

            place2Panel.SetActive(true);
            currentColor = place2Image.color;
            currentColor.a = 1.0f;
            place2Image.color = currentColor;
        }
    }

    /// <summary>
    /// [체험 시작 버튼] 시뮬레이션 씬으로 전환합니다.
    /// </summary>
    public void OnClickPlayButton()
    {
        if (isDebug) Debug.Log("체험을 시작합니다.");

        // 설정값 저장 (혹시 변경 후 저장이 안 되었을 경우 대비)
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SaveSettings();
        }

        // 씬 전환
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadScene("SimulationScene");
        }
        else
        {
            // SceneTransitionManager가 존재하면 사용하고, 없으면 일반 SceneManager 사용
            // (void 함수에는 ?? 연산자를 사용할 수 없으므로 if문으로 분리)
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadScene("SimulationScene");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("SimulationScene");
            }
        }
    }

    #endregion

    #region UI Event Handlers (Tips)

    public void OnClickPreTipButton()
    {
        if (tipPageNum <= 1) return;
        UpdateTipsPanelPage(tipPageNum - 1);
    }

    public void OnClickNextTipButton()
    {
        if (tipPageNum >= 5) return;
        UpdateTipsPanelPage(tipPageNum + 1);
    }

    private void ResetTipPage()
    {
        UpdateTipsPanelPage(1);
    }

    private void UpdateTipsPanelPage(int newPage)
    {
        tipPageNum = newPage;
        tipPageText.text = $"{tipPageNum}/5";

        if (tip1) tip1.SetActive(tipPageNum == 1);
        if (tip2) tip2.SetActive(tipPageNum == 2);
        if (tip3) tip3.SetActive(tipPageNum == 3);
        if (tip4) tip4.SetActive(tipPageNum == 4);
        if (tip5) tip5.SetActive(tipPageNum == 5);
    }

    #endregion

    #region UI Event Handlers (Settings)

    /// <summary>
    /// 오디오 슬라이더 값 변경 시 호출 (0 ~ 100)
    /// </summary>
    private void OnAudioSliderValueChanged(float value)
    {
        int intValue = Mathf.RoundToInt(value);

        // UI 텍스트 갱신
        if (audioText != null) audioText.text = intValue.ToString();

        // DataManager에 반영 (0.0 ~ 1.0)
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SetVolume(intValue / 100f);
        }
    }

    /// <summary>
    /// 모드 슬라이더 값 변경 시 호출 (0 or 1)
    /// </summary>
    private void OnModeSliderValueChanged(float value)
    {
        int intValue = Mathf.RoundToInt(value);
        bool isModeOn = (intValue == 1);

        // UI 텍스트 갱신
        if (modeText != null)
        {
            modeText.text = isModeOn ? "ON" : "OFF";
        }

        // DataManager에 반영
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SetMotionSicknessMode(isModeOn);
        }
    }

    #endregion

    #region Helper Methods (Panel Switching)

    private void SwitchTopPanel(GameObject panelToActivate)
    {
        if (currentTopPanel == panelToActivate) return;

        if (currentTopPanel != null) currentTopPanel.SetActive(false);

        panelToActivate.SetActive(true);
        currentTopPanel = panelToActivate;
    }

    private void SwitchMainPanel(GameObject panelToActivate, Image buttonToActivate)
    {
        if (currentMainPanel == panelToActivate) return;

        // 팁 패널을 닫을 때는 내부 페이지들도 정리
        if (currentMainPanel == tipsPanel)
        {
            if (tip1) tip1.SetActive(false);
            if (tip2) tip2.SetActive(false);
            if (tip3) tip3.SetActive(false);
            if (tip4) tip4.SetActive(false);
            if (tip5) tip5.SetActive(false);
        }

        if (currentMainPanel != null) currentMainPanel.SetActive(false);
        if (currentMainButton != null)
        {
            Color currentColor = currentMainButton.color;
            currentColor.a = 0.0f;
            currentMainButton.color = currentColor;
        }

        
        panelToActivate.SetActive(true);
        currentMainPanel = panelToActivate;

        Color newColor = buttonToActivate.color;
        newColor.a = 1.0f;
        buttonToActivate.color = newColor;
        currentMainButton = buttonToActivate;

    }

    #endregion
}