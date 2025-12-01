using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion; // XRI 3.x 이동 관련
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation; // 텔레포트 관련

/// <summary>
/// VR 환경에서 플레이어의 머리(카메라)를 따라다니는 World Space UI 스크립트입니다.
/// <para>
/// 1. 플레이어의 시선 이동에 따라 UI가 부드럽게 또는 즉시 따라옵니다.<br/>
/// 2. XRI 텔레포트 이벤트를 감지하여 순간 이동 시 UI를 즉시 재정렬합니다.<br/>
/// 3. 특정 축의 회전을 잠가 UI가 항상 수직/수평을 유지하도록 합니다.
/// </para>
/// </summary>
public class UIFollowHead : MonoBehaviour
{
    #region Inspector Settings

    [Header("Target Settings")]
    [Tooltip("UI가 따라다닐 타겟입니다. (일반적으로 Main Camera의 Transform)")]
    [SerializeField] private Transform targetHead;

    [Tooltip("텔레포트 이벤트를 감지할 Provider입니다. (XR Origin의 Locomotion System)")]
    [SerializeField] private TeleportationProvider teleportationProvider;

    [Header("Follow Settings")]
    [Tooltip("머리(카메라)로부터 UI가 떨어져 있을 거리(m)입니다.")]
    [SerializeField] private float distance = 3.0f;

    [Tooltip("UI의 높이(Y축) 오프셋입니다. (0이면 눈높이, 음수면 눈보다 아래)")]
    [SerializeField] private float heightOffset = 0f;

    [Tooltip("체크 시: UI가 목표 위치로 부드럽게 이동합니다. (Lerp 사용)\n체크 해제: UI가 머리 위치에 즉시 고정됩니다. (지연 없음, 멀미 최소화)")]
    [SerializeField] private bool enableSmoothFollow = false;

    [Tooltip("따라오는 속도입니다. (Enable Smooth Follow가 켜져 있을 때만 적용)")]
    [SerializeField] private float smoothSpeed = 20f;

    [Header("Rotation Constraints")]
    [Tooltip("체크 시: 고개를 숙이거나 들어도 UI가 눕지 않고 항상 수직으로 서 있습니다. (X축 Pitch 회전 잠금)")]
    [SerializeField] private bool freezeXRotation = true;

    [Tooltip("체크 시: 고개를 좌우로 갸웃거려도 UI가 기울어지지 않고 수평을 유지합니다. (Z축 Roll 회전 잠금)")]
    [SerializeField] private bool freezeZRotation = true;

    #endregion

    #region Internal Variables

    /// <summary>
    /// 마지막으로 유효했던 '바라보는 방향' 벡터입니다.
    /// (플레이어가 하늘/땅을 수직으로 바라볼 때 방향 벡터가 소실되는 것을 방지)
    /// </summary>
    private Vector3 lastProjectedForward = Vector3.forward;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        // 1. 타겟 카메라 자동 할당 (없을 경우)
        if (targetHead == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null) targetHead = mainCam.transform;
        }

        // 2. 텔레포트 프로바이더 자동 할당 (없을 경우)
        if (teleportationProvider == null)
        {
            teleportationProvider = FindAnyObjectByType<TeleportationProvider>();
        }

        // 3. 텔레포트 종료 이벤트 구독
        if (teleportationProvider != null)
        {
            teleportationProvider.locomotionEnded += OnTeleportEnded;
        }
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (teleportationProvider != null)
        {
            teleportationProvider.locomotionEnded -= OnTeleportEnded;
        }
    }

    private void LateUpdate()
    {
        if (targetHead == null) return;

        // 카메라 이동이 끝난 후(LateUpdate) UI 위치를 계산해야 떨림(Jitter)이 발생하지 않음

        // 1. 목표 위치 계산
        Vector3 targetPosition = CalculateTargetPosition();

        // 2. 위치 업데이트 (보간 vs 즉시)
        if (enableSmoothFollow)
        {
            // Lerp를 사용하여 부드럽게 이동 (급격한 회전 시 UI가 시야 밖으로 밀릴 수 있음)
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
        }
        else
        {
            // 즉시 이동 (UI가 화면에 딱 붙어있는 느낌)
            transform.position = targetPosition;
        }

        // 3. 회전 업데이트 (축 잠금 적용)
        UpdateRotation();
    }

    #endregion

    #region Core Logic

    /// <summary>
    /// XRI 텔레포트가 끝난 직후 호출됩니다.
    /// UI를 강제로 목표 위치로 순간 이동시켜, 플레이어가 도착했을 때 UI가 이미 앞에 있도록 합니다.
    /// </summary>
    private void OnTeleportEnded(LocomotionProvider provider)
    {
        if (targetHead == null) return;

        transform.position = CalculateTargetPosition();
        UpdateRotation();
    }

    /// <summary>
    /// 타겟(머리)의 위치와 방향을 기반으로 UI가 배치될 월드 좌표를 계산합니다.
    /// </summary>
    private Vector3 CalculateTargetPosition()
    {
        // 카메라의 앞방향(Forward)을 가져오되, Y축(높이) 성분을 제거하여 수평 벡터만 추출
        Vector3 projectedForward = targetHead.forward;
        projectedForward.y = 0;

        // 예외 처리: 하늘이나 땅을 수직으로 쳐다보면 수평 벡터 길이가 0에 수렴함
        if (projectedForward.sqrMagnitude < 0.01f)
        {
            projectedForward = lastProjectedForward; // 이전 프레임의 유효한 방향 사용
        }
        else
        {
            projectedForward.Normalize();
            lastProjectedForward = projectedForward; // 유효한 방향 저장
        }

        // 최종 위치 = 머리 위치 + (바라보는 수평 방향 * 거리) + 높이 오프셋
        Vector3 finalPos = targetHead.position + (projectedForward * distance);
        finalPos.y = targetHead.position.y + heightOffset;

        return finalPos;
    }

    /// <summary>
    /// 타겟을 바라보되, 설정된 축(X, Z)의 회전을 잠급니다.
    /// </summary>
    private void UpdateRotation()
    {
        // 1. 기본적으로 카메라가 보는 방향을 따라가도록 회전값 계산
        Quaternion targetRotation = Quaternion.LookRotation(targetHead.forward);

        // 2. 오일러 각도로 변환하여 개별 축 제어
        Vector3 euler = targetRotation.eulerAngles;

        // X축(Pitch) 잠금: 고개 숙임/들기 무시 -> 항상 수직으로 서 있음
        if (freezeXRotation)
        {
            euler.x = 0;
        }

        // Z축(Roll) 잠금: 고개 갸웃거림 무시 -> 항상 수평선 유지
        if (freezeZRotation)
        {
            euler.z = 0;
        }

        // 3. 수정된 회전값 적용
        transform.rotation = Quaternion.Euler(euler);
    }

    #endregion
}