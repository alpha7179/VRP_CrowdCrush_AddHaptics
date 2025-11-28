using UnityEngine;

/// <summary>
/// XR Tunneling Vignette의 메쉬와 쉐이더를 그대로 활용하되,
/// 로코모션 시스템과 무관하게 직접 제어하는 스크립트입니다.
/// </summary>
public class PressureVignette : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Color vignetteColor = new Color(1f, 0f, 0f, 0.5f); // 붉은색
    [SerializeField] private float feathering = 0.5f; // 경계선 부드러움

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;

    // 쉐이더 프로퍼티 ID 미리 찾아두기 (최적화)
    private static readonly int ApertureSizeID = Shader.PropertyToID("_ApertureSize");
    private static readonly int VignetteColorID = Shader.PropertyToID("_VignetteColor");
    private static readonly int FeatheringEffectID = Shader.PropertyToID("_FeatheringEffect");

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propBlock = new MaterialPropertyBlock();

        // 초기화: 구멍을 100% 열어서 안 보이게 함
        UpdateVignette(1.0f);
    }

    /// <summary>
    /// 외부에서 호출: 압박 강도 (0.0 ~ 1.0)
    /// </summary>
    public void SetIntensity(float intensity)
    {
        // 강도(0~1)를 구멍 크기(1~0.3)로 변환
        // 강도가 셀수록(1.0) 구멍은 작아져야 함(0.3)
        float minAperture = 0.3f;
        float targetAperture = Mathf.Lerp(1.0f, minAperture, intensity);

        UpdateVignette(targetAperture);
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