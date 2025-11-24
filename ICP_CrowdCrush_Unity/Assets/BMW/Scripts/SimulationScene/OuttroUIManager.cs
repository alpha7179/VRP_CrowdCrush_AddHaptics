using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 게임 종료 후 결과 및 요약(Summary)을 보여주는 UI 매니저.
/// 결과 표시 -> 요약 보기 -> 페이지네이션 -> 메인으로 이동 기능을 포함
/// </summary>
public class OuttroUIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject resultPanel;  // 결과 점수 패널
    [SerializeField] private GameObject summaryPanel; // 요약 학습 패널

    [Header("Result Elements")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject[] starIcons; // 별점 아이콘 (3~5개)

    [Header("Summary Elements")]
    [SerializeField] private GameObject[] summaryPages; // 요약 페이지들 (Page 1, 2, 3...)
    [SerializeField] private TextMeshProUGUI pageNumberText;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;

    private int currentPageIndex = 0;

    /// <summary>
    /// 결과창 초기화 (게임 종료 시 호출됨)
    /// </summary>
    public void Initialize()
    {
        resultPanel.SetActive(true);
        summaryPanel.SetActive(false);

        // DataManager2 싱글톤에서 세션 결과 가져오기
        int successCount = 0;
        if (DataManager.Instance != null)
        {
            successCount = DataManager.Instance.SuccessCount;
        }

        scoreText.text = $"성공한 대처: {successCount} / 5";

        // 별점 연출 (성공 횟수만큼 별 활성화)
        for (int i = 0; i < starIcons.Length; i++)
        {
            starIcons[i].SetActive(i < successCount);
        }
    }

    // [요약 보기] 버튼 클릭 시
    public void OnClickShowSummary()
    {
        resultPanel.SetActive(false);
        summaryPanel.SetActive(true);

        currentPageIndex = 0;
        UpdateSummaryPage(); // 첫 페이지 표시
    }

    // [처음으로] 버튼 클릭 시
    public void OnClickGoHome()
    {
        // 인트로 씬 로드
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadScene("IntroScene");
        }
    }

    // --- 페이지네이션 로직 (화살표 버튼) ---

    public void OnClickNextPage()
    {
        if (currentPageIndex < summaryPages.Length - 1)
        {
            currentPageIndex++;
            UpdateSummaryPage();
        }
    }

    public void OnClickPrevPage()
    {
        if (currentPageIndex > 0)
        {
            currentPageIndex--;
            UpdateSummaryPage();
        }
    }
    
    // 현재 페이지 인덱스에 맞춰 UI 갱신
    private void UpdateSummaryPage()
    {
        // 모든 페이지 비활성화 후 현재 페이지만 활성화
        for (int i = 0; i < summaryPages.Length; i++)
        {
            if (summaryPages[i] != null)
                summaryPages[i].SetActive(i == currentPageIndex);
        }

        // 페이지 번호 텍스트 갱신 (예: "1 / 5")
        if (pageNumberText)
        {
            pageNumberText.text = $"{currentPageIndex + 1} / {summaryPages.Length}";
        }

        // 첫 페이지면 이전 버튼 끄기, 마지막 페이지면 다음 버튼 끄기
        if (prevButton) prevButton.interactable = (currentPageIndex > 0);
        if (nextButton) nextButton.interactable = (currentPageIndex < summaryPages.Length - 1);
    }
}