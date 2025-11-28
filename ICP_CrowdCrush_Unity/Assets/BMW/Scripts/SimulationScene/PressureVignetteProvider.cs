using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

/// <summary>
/// XR Interaction Toolkit의 TunnelingVignette 시스템을 활용하여
/// 압박감(붉은색 비네팅)을 표현하는 제공자(Provider) 클래스
/// </summary>
public class PressureVignetteProvider : MonoBehaviour, ITunnelingVignetteProvider
{
    [Header("References")]
    [SerializeField] private TunnelingVignetteController vignetteController;

    [Header("Pressure Settings")]
    [SerializeField] private Color pressureColor = Color.red;
    [Range(0f, 1f)]
    [SerializeField] private float minApertureSize = 0.3f; // 최대 압박 시 구멍 크기 (0.3 = 30%)
    [SerializeField] private bool usePulse = true; // 두근거림 효과 사용 여부

    // 인터페이스 구현: 컨트롤러가 가져갈 설정값
    public VignetteParameters vignetteParameters { get; } = new VignetteParameters();

    private bool isActive = false;
    private float currentPressureLevel = 0f; // 0.0 ~ 1.0

    private void Awake()
    {
        // 컨트롤러 자동 찾기
        if (vignetteController == null)
            vignetteController = FindAnyObjectByType<TunnelingVignetteController>();

        // 기본 파라미터 초기화 (빨간색 설정)
        InitializeParameters();
    }

    private void InitializeParameters()
    {
        vignetteParameters.apertureSize = 1.0f; // 처음엔 100% 열림
        vignetteParameters.featheringEffect = 0.5f; // 가장자리 부드럽게
        vignetteParameters.easeInTime = 0.1f; // 반응 속도 빠르게
        vignetteParameters.easeOutTime = 0.1f;
        vignetteParameters.vignetteColor = pressureColor; // ★ 핵심: 빨간색
        vignetteParameters.vignetteColorBlend = pressureColor;
    }

    /// <summary>
    /// 외부(IngameUIManager)에서 압박 강도(0.0 ~ 1.0)를 설정하는 함수
    /// </summary>
    public void SetPressure(float intensity)
    {
        currentPressureLevel = Mathf.Clamp01(intensity);

        // 강도가 0보다 크면 비네팅 시작, 아니면 종료
        if (currentPressureLevel > 0.01f)
        {
            if (!isActive)
            {
                isActive = true;
                vignetteController.BeginTunnelingVignette(this);
            }
        }
        else
        {
            if (isActive)
            {
                isActive = false;
                vignetteController.EndTunnelingVignette(this);
            }
        }
    }

    private void Update()
    {
        if (!isActive) return;

        // 1. 압박 강도에 따른 구멍 크기 계산 (강할수록 구멍이 작아짐)
        // Lerp(1.0, 0.3, intensity) -> intensity가 1이면 0.3(좁음)이 됨
        float targetAperture = Mathf.Lerp(1.0f, minApertureSize, currentPressureLevel);

        // 2. 심장 박동(Pulse) 효과 (선택 사항)
        if (usePulse && currentPressureLevel > 0.3f)
        {
            float pulseSpeed = 2f + (currentPressureLevel * 8f); // 압박이 심하면 더 빨리 뜀
            float pulseAmount = 0.05f * currentPressureLevel;    // 구멍 크기 변화폭

            // 사인파를 이용해 구멍 크기를 흔듬
            targetAperture += Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        }

        // 3. 파라미터 실시간 업데이트
        vignetteParameters.apertureSize = Mathf.Clamp01(targetAperture);
        vignetteParameters.vignetteColor = pressureColor;
    }
}