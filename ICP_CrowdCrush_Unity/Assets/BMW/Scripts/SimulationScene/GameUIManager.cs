using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering; // Volume 사용 시 필요 (URP)
using UnityEngine.Rendering.Universal; // Vignette 사용 시 필요
using System.Collections;

/// <summary>
/// 게임 씬의 HUD(지시사항, 게이지)와 특수 연출(비네팅, 쉐이크, 팝업)을 관리하는 매니저.
/// </summary>
public class GameUIManager : MonoBehaviour
{
    [Header("HUD Elements")]
    [SerializeField] private Canvas hudCanvas;          // 플레이어 추적 캔버스
    [SerializeField] private TextMeshProUGUI instructionText; // 상단 지시사항
    [SerializeField] private Image actionGauge;         // 중앙 원형 게이지 (ABC 자세용)
    [SerializeField] private GameObject pausePanel;     // 일시정지 메뉴 패널

    [Header("Effects")]
    [SerializeField] private Volume postProcessVolume;  // URP Global Volume (비네팅용)
    [SerializeField] private Transform cameraOffset;    // 카메라 흔들림 효과를 위한 오프셋 Transform

    [Header("External References")]
    [SerializeField] private OuttroUIManager outtroManager; // 결과창 매니저

    private Vignette vignette; // 런타임에 제어할 비네팅 효과

    private void Start()
    {
        // 초기화
        if (actionGauge) actionGauge.fillAmount = 0f;
        if (pausePanel) pausePanel.SetActive(false);

        // 결과창 숨김 (처음에는 꺼져 있어야 함)
        if (outtroManager) outtroManager.gameObject.SetActive(false);

        // URP Volume에서 Vignette 컴포넌트 가져오기
        if (postProcessVolume && postProcessVolume.profile.TryGet(out vignette))
        {
            vignette.intensity.value = 0f; // 초기엔 비네팅 없음
        }

        // GameManager의 일시정지 이벤트 구독
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPauseStateChanged += HandlePauseState;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPauseStateChanged -= HandlePauseState;
        }
    }

    // 상단 지시사항 텍스트 갱신
    public void UpdateInstruction(string text)
    {
        if (instructionText) instructionText.text = text;
    }

    // 행동 게이지 업데이트 (0.0 ~ 1.0)
    public void UpdateActionGauge(float ratio)
    {
        if (actionGauge) actionGauge.fillAmount = ratio;
    }

    // 압박감 연출 (비네팅 강도 조절)
    // <param name="intensity">0.0(없음) ~ 1.0(최대)</param>
    public void SetPressureIntensity(float intensity)
    {
        if (vignette != null)
        {
            vignette.intensity.value = intensity;
            // 강도가 높을수록 붉은색으로 변하게 하여 위기감 조성
            vignette.color.value = Color.Lerp(Color.black, Color.red, intensity);
        }
    }

    // 화면 흔들림 효과 (Camera Shake) - 기둥 잡기 단계용
    public void SetCameraShake(bool isShaking)
    {
        if (cameraOffset == null) return;

        if (isShaking)
        {
            StartCoroutine(ShakeRoutine());
        }
        else
        {
            StopAllCoroutines();
            cameraOffset.localPosition = Vector3.zero; // 위치 초기화
        }
    }

    private IEnumerator ShakeRoutine()
    {
        while (true)
        {
            // 무작위 위치로 미세하게 떨림
            cameraOffset.localPosition = Random.insideUnitSphere * 0.05f;
            yield return null;
        }
    }

    // 일시정지 상태 처리 (패널 활성/비활성)
    private void HandlePauseState(bool isPaused)
    {
        if (pausePanel) pausePanel.SetActive(isPaused);

        // 일시정지 시 HUD를 숨길지 말지는 기획에 따라 결정 (여기선 끄지 않음)
    }

    // 결과 화면(Outtro) 호출
    public void ShowOuttroUI()
    {
        if (hudCanvas) hudCanvas.enabled = false; // 인게임 HUD는 끔
        if (outtroManager)
        {
            outtroManager.gameObject.SetActive(true);
            outtroManager.Initialize(); // 결과 데이터 로드 및 표시
        }
    }

    // --- 일시정지 메뉴 버튼 연결용 (UnityEvent) ---

    public void OnClickResume()
    {
        // 재개 (토글)
        if (GameManager.Instance != null) GameManager.Instance.TogglePause();
    }

    public void OnClickExit()
    {
        // 메인 메뉴(IntroScene)로 이동
        if (GameManager.Instance != null) GameManager.Instance.LoadScene("IntroScene");
    }
}