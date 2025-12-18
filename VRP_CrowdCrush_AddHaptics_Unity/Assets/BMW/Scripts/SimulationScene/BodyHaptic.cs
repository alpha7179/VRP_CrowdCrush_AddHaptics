using UnityEngine;
using System.Collections;
using Bhaptics.SDK2;

public class BodyHaptic : MonoBehaviour
{
    public static BodyHaptic Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("진동 반복 간격 (초). 0.5 ~ 1.0초 사이로 짧게 설정해보세요.")]
    [SerializeField] private float loopInterval = 1.0f; // 기본값을 1초로 단축

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = true;

    // 8방향 prefix
    private readonly string[] directionPrefixes = new string[]
    {
        "b_right", "b_left", "f_right", "f_left",
        "front", "back", "right", "left"
    };

    private Coroutine _hapticCoroutine;
    private int _currentLevel = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            transform.parent = null;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 해당 레벨의 햅틱을 반복 재생합니다.
    /// </summary>
    public void PlayBodyHaptics(int level)
    {
        // 1. 같은 레벨 요청이면 무시 (중복 방지)
        if (_currentLevel == level) return;

        if (showDebugLog) Debug.Log($"[BodyHaptic] Start Loop Level: {level}");

        // 2. 기존 루프 깔끔하게 정리
        StopBodyHaptics(false); // false: 로그 중복 출력 방지

        // 3. 범위 체크 (1~6 아니면 실행 안 함)
        if (level < 1 || level > 6)
        {
            return;
        }

        // 4. 새 루프 시작
        _currentLevel = level;
        _hapticCoroutine = StartCoroutine(HapticLoopRoutine(level));
    }

    /// <summary>
    /// 햅틱 재생을 즉시 중단합니다.
    /// </summary>
    public void StopBodyHaptics(bool log = true)
    {
        if (log && showDebugLog) Debug.Log("[BodyHaptic] Stop Loop");

        if (_hapticCoroutine != null)
        {
            StopCoroutine(_hapticCoroutine);
            _hapticCoroutine = null;
        }

        // bHaptics SDK에 정지 명령 (진동 잔여 제거)
        BhapticsLibrary.StopAll();

        _currentLevel = -1; // 상태 초기화
    }

    // 무한 반복 코루틴
    private IEnumerator HapticLoopRoutine(int level)
    {
        while (true)
        {
            if (showDebugLog) Debug.Log($"[BodyHaptic] Playing Pulse... (Level {level})");

            // 8방향 재생 명령 전송
            foreach (var prefix in directionPrefixes)
            {
                string eventId = $"{prefix}_{level}";
                BhapticsLibrary.Play(eventId);
            }

            // [중요] 0초면 무한 루프로 멈출 수 있으므로 최소값 보정
            float waitTime = Mathf.Max(0.1f, loopInterval);
            yield return new WaitForSeconds(waitTime);
        }
    }

    // 테스트용
    [Header("Button에서 사용할 기본 레벨")]
    [Range(1, 6)]
    public int defaultLevel = 1;

    public void PlayDefaultLevel()
    {
        PlayBodyHaptics(defaultLevel);
    }
}