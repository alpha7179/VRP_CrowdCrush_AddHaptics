using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion; // XRI 3.x 이동 관련
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation; // 텔레포트 관련

/// <summary>
/// 플레이어의 머리(카메라)를 따라다니는 VR UI 스크립트입니다.
/// 위치 지연(Lerp)을 끄면 화면 밖으로 벗어나는 현상을 막을 수 있습니다.
/// X축, Z축 회전을 잠가 항상 수직 상태를 유지합니다.
/// </summary>
public class UIFollowHead : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("따라다닐 대상 (주로 Main Camera)")]
    [SerializeField] private Transform targetHead;

    [Tooltip("텔레포트 이벤트를 감지할 Provider (XR Origin에 있음)")]
    [SerializeField] private TeleportationProvider teleportationProvider;

    [Header("Follow Settings")]
    [Tooltip("머리로부터 UI까지의 거리")]
    [SerializeField] private float distance = 3.0f;

    [Tooltip("높이(Y축) 오프셋 (눈높이보다 약간 아래 권장)")]
    [SerializeField] private float heightOffset = 0f;

    [Tooltip("체크 해제(권장): UI가 머리 위치에 '즉시' 고정되어 화면 밖으로 나가지 않습니다.\n체크 시: UI가 부드럽게 따라오지만 빠르게 움직이면 화면 밖으로 밀릴 수 있습니다.")]
    [SerializeField] private bool enableSmoothFollow = false;

    [Tooltip("따라오는 속도 (enableSmoothFollow가 켜져있을 때만 작동)")]
    [SerializeField] private float smoothSpeed = 20f;


    [Header("Rotation Constraints")]
    [Tooltip("체크 시: 고개를 숙이거나 들어도 UI가 눕지 않고 항상 수직으로 서 있습니다. (X축 회전 잠금)")]
    [SerializeField] private bool freezeXRotation = true;

    [Tooltip("체크 시: 고개를 좌우로 갸웃거려도 UI가 기울어지지 않고 수평을 유지합니다. (Z축 회전 잠금)")]
    [SerializeField] private bool freezeZRotation = true;

    // 내부 변수: 마지막으로 유효했던 바라보는 방향 (하늘을 볼 때 튀는 현상 방지용)
    private Vector3 lastProjectedForward = Vector3.forward;

    private void OnEnable()
    {
        // 1. 타겟 카메라 자동 할당
        if (targetHead == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null) targetHead = mainCam.transform;
        }

        // 2. 텔레포트 프로바이더 자동 할당
        if (teleportationProvider == null)
        {
            teleportationProvider = FindAnyObjectByType<TeleportationProvider>();
        }

        // 3. 텔레포트 종료 이벤트 연결
        if (teleportationProvider != null)
        {
            teleportationProvider.locomotionEnded += OnTeleportEnded;
        }
    }

    private void OnDisable()
    {
        if (teleportationProvider != null)
        {
            teleportationProvider.locomotionEnded -= OnTeleportEnded;
        }
    }

    private void LateUpdate()
    {
        if (targetHead == null) return;

        // 1. 목표 위치 계산
        Vector3 targetPosition = GetTargetPosition();

        // 2. 위치 업데이트 (Lerp 선택 적용)
        if (enableSmoothFollow)
        {
            // 부드럽게 이동 (지연 발생 가능)
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
        }
        else
        {
            // [수정됨] 즉시 이동 (화면 밖으로 벗어나지 않음)
            transform.position = targetPosition;
        }

        // 3. 회전 업데이트 (X, Z축 잠금 처리 적용)
        UpdateRotation();
    }

    private void OnTeleportEnded(LocomotionProvider provider)
    {
        if (targetHead == null) return;

        // 텔레포트 직후에는 무조건 즉시 이동 (Snap)
        transform.position = GetTargetPosition();
        UpdateRotation();
    }

    // UI가 위치해야 할 목표 좌표 계산
    private Vector3 GetTargetPosition()
    {
        // 카메라의 앞방향을 가져오되, 수평 성분만 추출
        Vector3 projectedForward = targetHead.forward;
        projectedForward.y = 0;

        // 하늘/바닥을 똑바로 쳐다봐서 벡터가 0이 되는 경우, 이전 방향 유지 (튀는 현상 방지)
        if (projectedForward.sqrMagnitude < 0.01f)
        {
            projectedForward = lastProjectedForward;
        }
        else
        {
            projectedForward.Normalize();
            lastProjectedForward = projectedForward; // 유효한 방향 저장
        }

        // 카메라 위치 + (수평 방향 * 거리) + 높이 오프셋
        Vector3 targetPos = targetHead.position + (projectedForward * distance);
        targetPos.y = targetHead.position.y + heightOffset;

        return targetPos;
    }

    // UI 회전 처리
    private void UpdateRotation()
    {
        // 1. 기본적으로 카메라가 보는 방향을 따라가도록 설정
        Quaternion targetRotation = Quaternion.LookRotation(targetHead.forward);

        // 2. 오일러 각도로 변환하여 축별 제어
        Vector3 euler = targetRotation.eulerAngles;

        // X축(Pitch) 잠금: 고개 숙임/들기 무시 -> 항상 수직
        if (freezeXRotation)
        {
            euler.x = 0;
        }

        // Z축(Roll) 잠금: 고개 갸웃거림 무시 -> 항상 수평 유지
        if (freezeZRotation)
        {
            euler.z = 0;
        }

        // 3. 최종 회전 적용
        transform.rotation = Quaternion.Euler(euler);
    }
}