using UnityEngine;

/// <summary>
/// 셰이더와 연동하여 화면 가장자리를 어둡게 하거나 색상을 입히는 비네팅(Vignette) 효과를 제어합니다.
/// <para>
/// 1. 심리적 압박감(Pressure) 수치에 따라 시야가 좁아지는 효과를 연출합니다.<br/>
/// 2. Pulse 기능을 통해 심장 박동처럼 화면이 울렁거리는 효과를 줍니다.<br/>
/// 3. MaterialPropertyBlock을 사용하여 런타임 성능을 최적화합니다.
/// </para>
/// </summary>
public class PressureVignette : MonoBehaviour
{
    #region Inspector Settings

    [Header("Visual Settings")]
    [Tooltip("비네팅 효과의 색상입니다. (주로 붉은색이나 검은색 사용)")]
    [SerializeField] private Color vignetteColor = new Color(1f, 0f, 0f, 0.5f);

    [Tooltip("비네팅 경계의 부드러운 정도입니다. (0에 가까울수록 날카로움)")]
    [SerializeField] private float feathering = 0.5f;

    [Header("Pulse Settings")]
    [Tooltip("체크 시: 심장 박동처럼 화면이 주기적으로 울렁거립니다.")]
    [SerializeField] private bool usePulse = true;

    [Tooltip("기본 박동 속도입니다.")]
    [SerializeField] private float basePulseSpeed = 2.0f;

    [Tooltip("박동 시 조리개(Aperture) 크기의 변화 폭입니다.")]
    [SerializeField] private float pulseMagnitude = 0.05f;

    [Header("Debug (Play Mode Only)")]
    [Tooltip("테스트용 강도 슬라이더입니다. 플레이 모드에서 실시간으로 조절해 볼 수 있습니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float testIntensity = 0f;

    #endregion

    #region Internal State

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;

    /// <summary>
    /// 현재 적용된 압박감 강도 (0.0 ~ 1.0)
    /// </summary>
    private float currentIntensity = 0f;

    // Shader Property IDs (성능을 위해 미리 해싱)
    private static readonly int ApertureSizeID = Shader.PropertyToID("_ApertureSize");
    private static readonly int VignetteColorID = Shader.PropertyToID("_VignetteColor");
    private static readonly int FeatheringEffectID = Shader.PropertyToID("_FeatheringEffect");

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propBlock = new MaterialPropertyBlock();

        // 초기화: 효과를 보이지 않게 설정 (조리개 완전 개방)
        UpdateVignette(1.0f);
    }

    private void Update()
    {
        // 디버그용: Inspector의 슬라이더 값이 변경되면 실시간 적용
        if (testIntensity > 0)
        {
            currentIntensity = testIntensity;
        }

        // 시각 효과 매 프레임 갱신 (Pulse 애니메이션 때문)
        UpdateVisuals();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 비네팅 효과의 강도를 설정합니다.
    /// </summary>
    /// <param name="intensity">0.0(없음) ~ 1.0(최대) 사이의 값</param>
    public void SetIntensity(float intensity)
    {
        currentIntensity = Mathf.Clamp01(intensity);
        testIntensity = currentIntensity; // 디버그 슬라이더 동기화

        // 강도가 미미하면 컴포넌트 자체를 꺼서 연산 절약 (최적화)
        // 단, Pulse 애니메이션이 자연스럽게 사라지게 하려면 임계값을 잘 조절해야 함
        bool shouldEnable = currentIntensity > 0.01f;

        if (enabled != shouldEnable)
        {
            enabled = shouldEnable;
            if (!enabled) UpdateVignette(1.0f); // 꺼질 때 구멍 완전히 열기
        }
    }

    #endregion

    #region Internal Logic

    /// <summary>
    /// 현재 강도와 시간(Time)을 기반으로 최종 조리개 크기를 계산합니다.
    /// </summary>
    private void UpdateVisuals()
    {
        // 1. 기본 조리개 크기 계산 (강도가 높을수록 0.3까지 줄어듦)
        float minAperture = 0.3f;
        float targetAperture = Mathf.Lerp(1.0f, minAperture, currentIntensity);

        // 2. Pulse(박동) 효과 적용
        if (usePulse && currentIntensity > 0.1f)
        {
            // 강도가 높을수록 더 빨리 뜀
            float dynamicSpeed = basePulseSpeed + (currentIntensity * 5.0f);

            // Sin 파동을 이용해 크기 변화 (강도에 비례하여 진폭 커짐)
            float pulseOffset = Mathf.Sin(Time.time * dynamicSpeed) * pulseMagnitude * currentIntensity;

            targetAperture += pulseOffset;
        }

        // 3. 최종값 적용
        UpdateVignette(Mathf.Clamp01(targetAperture));
    }

    /// <summary>
    /// MaterialPropertyBlock을 사용하여 셰이더에 값을 전달합니다.
    /// (Material 인스턴스를 생성하지 않아 배칭이 깨지지 않음)
    /// </summary>
    private void UpdateVignette(float apertureSize)
    {
        if (meshRenderer == null) return;

        meshRenderer.GetPropertyBlock(propBlock);

        propBlock.SetFloat(ApertureSizeID, apertureSize);
        propBlock.SetColor(VignetteColorID, vignetteColor);
        propBlock.SetFloat(FeatheringEffectID, feathering);

        meshRenderer.SetPropertyBlock(propBlock);
    }

    #endregion
}