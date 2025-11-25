using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class IntroUIManager : MonoBehaviour
{
    [Header("UI 패널 (Parents)")]
    [SerializeField] private GameObject introPanel;
    [SerializeField] private GameObject startPanel;

    [Header("StartPanel의 하위 패널들")]
    [SerializeField] private GameObject placePanel;
    [SerializeField] private GameObject manualPanel;
    [SerializeField] private GameObject tipsPanel;
    [SerializeField] private GameObject settingPanel;

    [Header("Tips 패널 구성요소")]
    [SerializeField] private GameObject tip1;
    [SerializeField] private GameObject tip2;
    [SerializeField] private GameObject tip3;
    [SerializeField] private GameObject tip4;
    [SerializeField] private GameObject tip5;
    [SerializeField] private TextMeshProUGUI tipPageText; 

    [Header("Setting 패널 구성요소")]
    [SerializeField] private TextMeshProUGUI manualPageText;
    [SerializeField] private TextMeshProUGUI audioText;
    [SerializeField] private Slider audioSlider;
    [SerializeField] private TextMeshProUGUI modeText;
    [SerializeField] private Slider modeSlider;

    [Header("디버그 로그")]
    [SerializeField] private bool isDebug = true;

    private GameObject currentTopPanel;
    private GameObject currentMainPanel;

    private int tipPageNum;
    private int audioValue;
    private int modeValue;

    void Start()
    {

        // 오디오 슬라이더
        audioSlider.minValue = 0;
        audioSlider.maxValue = 100;
        audioSlider.wholeNumbers = true;

        audioValue = 100;
        audioSlider.value = audioValue;
        audioSlider.onValueChanged.AddListener(OnAudioSliderValueChanged);
        OnAudioSliderValueChanged(audioValue);

        // 모드 슬라이더
        modeSlider.minValue = 0;
        modeSlider.maxValue = 1;
        modeSlider.wholeNumbers = true;

        modeValue = 0;
        modeSlider.value = modeValue;
        modeSlider.onValueChanged.AddListener(OnModeSliderValueChanged);
        OnModeSliderValueChanged(modeValue);


        // 모든 하위 패널을 끈 상태로 시작
        if (placePanel) placePanel.SetActive(false);
        if (manualPanel) manualPanel.SetActive(false);
        if (tipsPanel) tipsPanel.SetActive(false);
        if (settingPanel) settingPanel.SetActive(false);

        // 최상위 패널(인트로)만 활성화
        if (startPanel) startPanel.SetActive(false);
        if (introPanel) introPanel.SetActive(true);

        currentTopPanel = introPanel;
        currentMainPanel = null;
    }

    /// <summary>
    /// 최상위 패널(Intro <-> Start)을 전환
    /// </summary>
    private void SwitchTopPanel(GameObject panelToActivate)
    {
        if (currentTopPanel == panelToActivate) return;

        if (currentTopPanel != null)
        {
            currentTopPanel.SetActive(false);
        }

        panelToActivate.SetActive(true);
        currentTopPanel = panelToActivate;
    }

    /// <summary>
    /// StartPanel 내부의 메인 패널(Place, Manual 등)을 전환
    /// </summary>
    private void SwitchMainPanel(GameObject panelToActivate)
    {
        if (currentMainPanel == panelToActivate) return; 

        if (currentMainPanel == tipsPanel)
        {
            tip1.SetActive(false);
            tip2.SetActive(false);
            tip3.SetActive(false);
            tip4.SetActive(false);
            tip5.SetActive(false);
        }

        if (currentMainPanel != null)
        {
            currentMainPanel.SetActive(false);
        }

        panelToActivate.SetActive(true);
        currentMainPanel = panelToActivate;
    }

    /// <summary>
    /// [IntroButton]에 연결: 인트로를 닫고 메인 메뉴(StartPanel)를 염
    /// </summary>
    public void OnClickIntroButton()
    {
        if (isDebug) Debug.Log("IntroButton Clicked");
        SwitchTopPanel(startPanel);

        // StartPanel이 열릴 때 기본으로 보여줄 하위 패널
        SwitchMainPanel(placePanel);
    }

    /// <summary>
    /// [PlaceButton]에 연결: 장소 선택 패널을 염
    /// </summary>
    public void OnClickPlaceButton()
    {
        if (isDebug) Debug.Log("PlaceButton Clicked");
        SwitchMainPanel(placePanel);
    }

    /// <summary>
    /// [ManualButton]에 연결: 매뉴얼 패널을 염
    /// </summary>
    public void OnClickManualButton()
    {
        if (isDebug) Debug.Log("ManualButton Clicked");
        SwitchMainPanel(manualPanel);
    }

    /// <summary>
    /// [TipsButton]에 연결: 팁 패널을 염
    /// </summary>
    public void OnClickTipsButton()
    {
        if (isDebug) Debug.Log("TipsButton Clicked");
        SwitchMainPanel(tipsPanel);

        tipPageNum = 1;
        tipPageText.text = $"{tipPageNum}/5";
        UpdateTipsPanelPage(tipPageNum);
    }

    /// <summary>
    /// [SettingButton]에 연결: 설정 패널을 염
    /// </summary>
    public void OnClickSettingButton()
    {
        if (isDebug) Debug.Log("SettingButton Clicked");
        SwitchMainPanel(settingPanel);
    }

    /// <summary>
    /// [PlayButton]에 연결: 체험을 시작
    /// </summary>
    public void OnClickPlayButton()
    {
        if (isDebug) Debug.Log("체험을 시작합니다.");
        //  씬(Scene) 전환 로직
        //  UnityEngine.SceneManagement.SceneManager.LoadScene("MainGameScene");
    }

    /// <summary>
    /// [PreTipButton]에 연결: 팁 이전 페이지로
    /// </summary>
    public void OnClickPreTipButton()
    {
        if (isDebug) Debug.Log("PreTipButton Clicked");
        if (tipPageNum <= 1) return;

        tipPageNum--;
        tipPageText.text = $"{tipPageNum}/5";
        UpdateTipsPanelPage(tipPageNum);
    }

    /// <summary>
    /// [NextTipButton]에 연결: 팁 다음 페이지로
    /// </summary>
    public void OnClickNextTipButton()
    {
        if (isDebug) Debug.Log("NextTipButton Clicked");
        if (tipPageNum >= 5) return;

        tipPageNum++;
        tipPageText.text = $"{tipPageNum}/5";
        UpdateTipsPanelPage(tipPageNum);
    }

    /// <summary>
    /// 팁 페이지 번호에 맞춰 올바른 페이지만 활성화함
    /// </summary>
    private void UpdateTipsPanelPage(int page)
    {
        if (tip1) tip1.SetActive(page == 1);
        if (tip2) tip2.SetActive(page == 2);
        if (tip3) tip3.SetActive(page == 3);
        if (tip4) tip4.SetActive(page == 4);
        if (tip5) tip5.SetActive(page == 5);
    }

    /// <summary>
    /// 오디오 슬라이더 값이 변경될 때 호출
    /// </summary>
    private void OnAudioSliderValueChanged(float value)
    {
        audioValue = Mathf.RoundToInt(value);
        if (audioText != null)
            audioText.text = audioValue.ToString();

    }

    /// <summary>
    /// 모드 슬라이더 값이 변경될 때 호출
    /// </summary>
    private void OnModeSliderValueChanged(float value)
    {
        modeValue = Mathf.RoundToInt(value);
        if (modeText != null)
        {
            if (modeValue == 1) modeText.text = "ON";
            else modeText.text = "OFF";
        }
    }
}