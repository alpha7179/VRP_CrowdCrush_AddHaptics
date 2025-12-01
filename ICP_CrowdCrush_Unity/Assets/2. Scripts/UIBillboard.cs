using UnityEngine;

// =========================================================
// Attributes
// =========================================================

/// <summary>
/// 인스펙터(Inspector)에서 필드를 읽기 전용(Greyed out)으로 표시하기 위한 속성입니다.
/// <para>참고: 별도의 Editor 스크립트(PropertyDrawer)가 있어야 실제로 작동합니다.</para>
/// </summary>
public class ReadOnlyAttribute : PropertyAttribute { }


// =========================================================
// Main Logic
// =========================================================

/// <summary>
/// UI 혹은 오브젝트가 항상 특정 카메라를 바라보게 하는 빌보드(Billboard) 스크립트입니다.
/// </summary>
public class UIBillboard : MonoBehaviour
{
    #region Inspector Settings

    [Header("Billboard Settings")]
    [Tooltip("빌보드가 바라볼 타겟 카메라입니다. (Null일 경우 MainCamera를 자동으로 탐색합니다)")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("빌보드 회전 계산 방식입니다.")]
    [SerializeField] private BillboardMode mode = BillboardMode.LookAtCamera;

    [Tooltip("체크 시 Y축 회전만 적용합니다. (수평 회전 유지, 수직 회전 무시)")]
    [SerializeField] private bool lockYAxis = false;

    [Header("Debug Info")]
    [Tooltip("현재 참조 중인 카메라의 Transform입니다.")]
    [SerializeField, ReadOnly] private Transform cameraTransform;

    #endregion

    #region Data Types

    /// <summary>
    /// 빌보드의 회전 방식을 정의합니다.
    /// </summary>
    public enum BillboardMode
    {
        /// <summary>
        /// 오브젝트가 카메라의 위치를 직접 바라봅니다. (가장 일반적인 방식)
        /// </summary>
        LookAtCamera,

        /// <summary>
        /// 카메라가 바라보는 방향과 평행하게 정렬합니다. (카메라와 완벽한 평면 유지)
        /// </summary>
        CameraForward,

        /// <summary>
        /// 카메라의 반대 방향을 바라봅니다. (거울상 혹은 특수 목적)
        /// </summary>
        OppositeDirection
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        // 컴포넌트 활성화 시 카메라 참조 초기화
        if (targetCamera == null)
        {
            TryFindCamera();
        }
        else
        {
            cameraTransform = targetCamera.transform;
        }
    }

    private void LateUpdate()
    {
        // 카메라가 이동한 "후"에 UI가 따라가야 떨림(Jitter)이 없으므로 LateUpdate 사용
        if (cameraTransform == null)
        {
            TryFindCamera();
            if (cameraTransform == null) return; // 여전히 카메라가 없으면 로직 중단
        }

        UpdateBillboardRotation();
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// 현재 모드에 따라 타겟 방향을 계산하고 회전을 적용합니다.
    /// </summary>
    private void UpdateBillboardRotation()
    {
        Vector3 targetPosition;

        // 1. 타겟 위치 계산
        switch (mode)
        {
            case BillboardMode.CameraForward:
                // 카메라의 앞방향 벡터를 더해 평행한 면을 만듦
                targetPosition = transform.position + cameraTransform.forward;
                break;

            case BillboardMode.OppositeDirection:
                // 카메라와 반대 방향 벡터 계산
                targetPosition = transform.position - (cameraTransform.position - transform.position);
                break;

            case BillboardMode.LookAtCamera:
            default:
                // 카메라 위치 자체를 타겟으로 설정
                targetPosition = cameraTransform.position;
                break;
        }

        // 2. 바라볼 방향 벡터 계산
        Vector3 directionToCamera = targetPosition - transform.position;

        // 3. Y축 잠금 처리 (수직 회전 방지)
        if (lockYAxis)
        {
            directionToCamera.y = 0;
        }

        // 4. 최종 회전 적용
        if (directionToCamera != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(directionToCamera);
        }
    }

    /// <summary>
    /// MainCamera 태그가 붙은 카메라를 찾아 참조를 설정합니다.
    /// </summary>
    private void TryFindCamera()
    {
        if (targetCamera != null)
        {
            cameraTransform = targetCamera.transform;
            return;
        }

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            targetCamera = mainCam;
            cameraTransform = mainCam.transform;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// 런타임에 타겟 카메라를 변경합니다.
    /// </summary>
    /// <param name="camera">새로 지정할 카메라</param>
    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        cameraTransform = camera != null ? camera.transform : null;
    }

    /// <summary>
    /// 런타임에 빌보드 모드를 변경합니다.
    /// </summary>
    /// <param name="newMode">변경할 모드</param>
    public void SetBillboardMode(BillboardMode newMode)
    {
        mode = newMode;
    }

    #endregion
}