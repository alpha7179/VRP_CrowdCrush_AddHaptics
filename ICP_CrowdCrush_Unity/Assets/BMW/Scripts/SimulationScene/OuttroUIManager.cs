using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        if (resultPanel.activeSelf)
        {
            // 결과 화면 -> 요약 보기
            ShowSummary();
        }
        else if (summaryPanel.activeSelf)
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

    public void Initialize()
    {
        resultPanel.SetActive(true);
        summaryPanel.SetActive(false);

        int successCount = 0;
        if (DataManager.Instance != null)
        {
            successCount = DataManager.Instance.SuccessCount;
        }

        //if (scoreText) scoreText.text = $"성공한 대처: {successCount} / 5";

        for (int i = 0; i < starIcons.Length; i++)
        {
            starIcons[i].SetActive(i < successCount);
        }
    }

    private void ShowSummary()
    {
        resultPanel.SetActive(false);
        summaryPanel.SetActive(true);
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
}