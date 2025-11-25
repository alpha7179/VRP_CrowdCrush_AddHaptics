using UnityEngine;

// =========================================================
// 1. 이 부분이 [ReadOnly]를 사용할 수 있게 만들어주는 설계도입니다.
//    이게 없으면 using을 아무리 해도 에러가 납니다.
// =========================================================
public class ReadOnlyAttribute : PropertyAttribute { }


// 2. 여기서부터 실제 빌보드 기능입니다.
public class UIBillboard : MonoBehaviour
{
    [Header("Billboard Settings")]
    [Tooltip("빌보드가 바라볼 카메라 (null이면 메인 카메라 사용)")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("빌보드 회전 모드")]
    [SerializeField] private BillboardMode mode = BillboardMode.LookAtCamera;

    [Tooltip("Y축 회전만 적용 (수평 회전만)")]
    [SerializeField] private bool lockYAxis = false;

    // 위에서 ReadOnlyAttribute 클래스를 만들었기 때문에 이제 에러가 안 납니다!
    [Header("Debug Info")]
    [SerializeField, ReadOnly] private Transform cameraTransform;

    public enum BillboardMode
    {
        LookAtCamera,
        CameraForward,
        OppositeDirection
    }

    private void OnEnable()
    {
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
        // 카메라가 없으면 다시 찾기 시도
        if (cameraTransform == null)
        {
            TryFindCamera();
            if (cameraTransform == null) return;
        }

        Vector3 targetPosition;

        switch (mode)
        {
            case BillboardMode.LookAtCamera:
                targetPosition = cameraTransform.position;
                break;
            case BillboardMode.CameraForward:
                targetPosition = transform.position + cameraTransform.forward;
                break;
            case BillboardMode.OppositeDirection:
                targetPosition = transform.position - (cameraTransform.position - transform.position);
                break;
            default:
                targetPosition = cameraTransform.position;
                break;
        }

        Vector3 directionToCamera = targetPosition - transform.position;

        if (lockYAxis)
        {
            directionToCamera.y = 0;
        }

        if (directionToCamera != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(directionToCamera);
        }
    }

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

    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        cameraTransform = camera != null ? camera.transform : null;
    }

    public void SetBillboardMode(BillboardMode newMode)
    {
        mode = newMode;
    }
}