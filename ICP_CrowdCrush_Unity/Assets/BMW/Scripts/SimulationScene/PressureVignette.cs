using UnityEngine;

public class PressureVignette : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Color vignetteColor = new Color(1f, 0f, 0f, 0.5f); // 붉은색
    [SerializeField] private float feathering = 0.5f;

    [Header("Pulse Settings")]
    [SerializeField] private bool usePulse = true;
    [SerializeField] private float basePulseSpeed = 2.0f;
    [SerializeField] private float pulseMagnitude = 0.05f;

    [Header("Debug (Play Mode Only)")]
    [Range(0f, 1f)]
    [SerializeField] private float testIntensity = 0f; // 슬라이더로 테스트 가능!

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;
    private float currentIntensity = 0f;

    private static readonly int ApertureSizeID = Shader.PropertyToID("_ApertureSize");
    private static readonly int VignetteColorID = Shader.PropertyToID("_VignetteColor");
    private static readonly int FeatheringEffectID = Shader.PropertyToID("_FeatheringEffect");

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propBlock = new MaterialPropertyBlock();
        UpdateVignette(1.0f); // 일단 안 보이게 시작
    }

    // [수정] Update 문에서 테스트 슬라이더 값을 실시간 반영하도록 변경
    private void Update()
    {
        // 디버그용: Inspector의 슬라이더 값이 변경되면 적용
        if (testIntensity > 0)
        {
            currentIntensity = testIntensity;
        }

        UpdateVisuals();
    }

    public void SetIntensity(float intensity)
    {
        currentIntensity = Mathf.Clamp01(intensity);
        testIntensity = currentIntensity; // 디버그 슬라이더도 동기화

        // 강도가 있으면 켜고, 없으면 끔 (최적화 잠시 해제하여 디버깅 용이하게 함)
        enabled = currentIntensity > 0.01f;

        if (!enabled) UpdateVignette(1.0f); // 꺼질 때 구멍 완전히 열기
    }

    private void UpdateVisuals()
    {
        float minAperture = 0.3f;
        float targetAperture = Mathf.Lerp(1.0f, minAperture, currentIntensity);

        if (usePulse && currentIntensity > 0.1f)
        {
            float dynamicSpeed = basePulseSpeed + (currentIntensity * 5.0f);
            float pulseOffset = Mathf.Sin(Time.time * dynamicSpeed) * pulseMagnitude * currentIntensity;
            targetAperture += pulseOffset;
        }

        UpdateVignette(Mathf.Clamp01(targetAperture));
    }

    private void UpdateVignette(float apertureSize)
    {
        if (meshRenderer == null) return;

        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(ApertureSizeID, apertureSize);
        propBlock.SetColor(VignetteColorID, vignetteColor);
        propBlock.SetFloat(FeatheringEffectID, feathering);
        meshRenderer.SetPropertyBlock(propBlock);
    }
}