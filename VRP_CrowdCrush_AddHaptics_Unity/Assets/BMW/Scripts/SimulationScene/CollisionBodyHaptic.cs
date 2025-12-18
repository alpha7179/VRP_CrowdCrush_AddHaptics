using System.Collections.Generic;
using UnityEngine;
using Bhaptics.SDK2;

public class CollisionBodyHaptic : MonoBehaviour
{
    public enum Dir8
    {
        Front,
        FrontLeft,
        FrontRight,
        Left,
        Right,
        Back,
        BackLeft,
        BackRight
    }

    [Header("각 방향별 bHaptics Event ID (Designer에서 만든 이름)")]
    public string frontEventId = "front_5";
    public string frontLeftEventId = "f_left_5";
    public string frontRightEventId = "f_right_5";
    public string leftEventId = "left_5";
    public string rightEventId = "right_5";
    public string backEventId = "back_5";
    public string backLeftEventId = "b_left_5";
    public string backRightEventId = "b_right_5";

    [Header("Avatar 태그 이름")]
    public string avatarTag = "Avatar";

    [Header("패턴 길이(초) - 모든 이벤트가 0.3초라고 가정")]
    public float patternDuration = 0.3f;

    [Header("같은 방향 재생 최소 간격 (여유 시간, 초)")]
    public float extraCooldown = 0.02f;   // 패턴 끝나고 0.02초 정도 여유

    // 방향별 마지막 재생 시각
    private readonly Dictionary<Dir8, float> _lastPlayTime =
        new Dictionary<Dir8, float>();

    private void Awake()
    {
        foreach (Dir8 d in System.Enum.GetValues(typeof(Dir8)))
        {
            _lastPlayTime[d] = -999f;
        }
    }

    // Avatar가 트리거 영역 안에 있는 동안 계속 호출됨
    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(avatarTag))
            return;

        // Avatar 위치 기준으로 8방향 중 어디인지 판정
        Vector3 avatarPos = other.ClosestPoint(transform.position);
        Dir8 dir = GetDirection(avatarPos);

        string eventId = GetEventId(dir);
        if (string.IsNullOrEmpty(eventId))
            return;

        // 아직 이 이벤트가 재생 중이면 그대로 두고
        // 끝났고, 최소 간격(0.3초 + extraCooldown)이 지났으면 다시 재생
        bool isPlaying = BhapticsLibrary.IsPlayingByEventId(eventId);
        float elapsed = Time.time - _lastPlayTime[dir];

        if (!isPlaying && elapsed >= (patternDuration + extraCooldown))
        {
            Debug.Log($"[AVATAR HAPTIC] {dir} -> {eventId}");
            BhapticsLibrary.Play(eventId);
            _lastPlayTime[dir] = Time.time;
        }
    }

    // Avatar가 영역 밖으로 나갈 때
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(avatarTag))
            return;

        // 특별히 할 건 없음.
        // 영역 밖으로 나가도 이미 재생 중인 패턴은 자연스럽게 끝나게 둔다.
    }

    // Avatar 위치 기준으로 8방향 판정
    private Dir8 GetDirection(Vector3 avatarWorldPos)
    {
        Vector3 toAvatar = avatarWorldPos - transform.position;
        toAvatar.y = 0f;

        if (toAvatar.sqrMagnitude < 0.0001f)
            return Dir8.Front;  // 거의 같은 위치일 때 그냥 앞 처리

        toAvatar.Normalize();

        // z: forward, x: right
        float angle = Mathf.Atan2(toAvatar.x, toAvatar.z) * Mathf.Rad2Deg;
        // angle 기준:
        //   0°   : 정면
        //   90°  : 오른쪽
        //   180° : 뒤
        //  -90°  : 왼쪽

        if (angle > -22.5f && angle <= 22.5f)
            return Dir8.Front;
        else if (angle > 22.5f && angle <= 67.5f)
            return Dir8.FrontRight;
        else if (angle > 67.5f && angle <= 112.5f)
            return Dir8.Right;
        else if (angle > 112.5f && angle <= 157.5f)
            return Dir8.BackRight;
        else if (angle <= -157.5f || angle > 157.5f)
            return Dir8.Back;
        else if (angle > -157.5f && angle <= -112.5f)
            return Dir8.BackLeft;
        else if (angle > -112.5f && angle <= -67.5f)
            return Dir8.Left;
        else
            return Dir8.FrontLeft;  // -67.5 ~ -22.5
    }

    private string GetEventId(Dir8 dir)
    {
        switch (dir)
        {
            case Dir8.Front: return frontEventId;
            case Dir8.FrontLeft: return frontLeftEventId;
            case Dir8.FrontRight: return frontRightEventId;
            case Dir8.Left: return leftEventId;
            case Dir8.Right: return rightEventId;
            case Dir8.Back: return backEventId;
            case Dir8.BackLeft: return backLeftEventId;
            case Dir8.BackRight: return backRightEventId;
            default: return null;
        }
    }
}
