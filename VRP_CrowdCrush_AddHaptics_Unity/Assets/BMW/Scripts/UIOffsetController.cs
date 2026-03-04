using UnityEngine;
using static DisplayModeManager; // Enum 타입을 쓰기 위해

public class UIOffsetController : MonoBehaviour
{
    [Header("이동 설정")]
    [Tooltip("Display 모드일 때 X축으로 이동할 거리")]
    public float shiftOffsetX = -90f; // 예: 왼쪽으로 90만큼 이동 (상황에 맞춰 조절)

    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private bool isShifted = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        // 원래 위치 저장 (나중에 되돌리기 위해)
        originalPosition = rectTransform.anchoredPosition;
    }

    private void Start()
    {
        // UI가 생성되자마자 현재 매니저의 상태를 확인하고 위치 조정
        if (DisplayModeManager.Instance != null)
        {
            UpdatePosition(DisplayModeManager.Instance.CurrentDisplayMode);

            // 앞으로 모드가 바뀔 때마다 나한테 알려달라고 구독 신청
            DisplayModeManager.Instance.OnDisplayModeChanged += UpdatePosition;
        }
    }

    private void OnDestroy()
    {
        // UI가 사라질 때 구독 해제 (메모리 누수 방지)
        if (DisplayModeManager.Instance != null)
        {
            DisplayModeManager.Instance.OnDisplayModeChanged -= UpdatePosition;
        }
    }

    // 매니저에서 모드가 바뀌거나, 처음 시작할 때 호출됨
    private void UpdatePosition(DisplayMode mode)
    {
        if (mode == DisplayMode.Display)
        {
            // Display 모드라면 -> 이동
            if (!isShifted)
            {
                rectTransform.anchoredPosition = new Vector2(originalPosition.x + shiftOffsetX, originalPosition.y);
                isShifted = true;
            }
        }
        else
        {
            // 그 외 모드라면 -> 원위치 복귀
            if (isShifted)
            {
                rectTransform.anchoredPosition = originalPosition;
                isShifted = false;
            }
        }
    }
}