using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ClimbHandle : UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing.ClimbInteractable
{
    // 현재 잡혀있는 핸들의 총 개수 (전역 변수)
    public static int ActiveGrabCount = 0;

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args); // 부모(등반 기능)의 로직 실행

        // 잡힐 때마다 카운트 증가
        ActiveGrabCount++;
        Debug.Log($"Climb Handle Grabbed. Count: {ActiveGrabCount}");
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args); // 부모(등반 기능)의 로직 실행

        // 놓을 때마다 카운트 감소
        ActiveGrabCount--;
        if (ActiveGrabCount < 0) ActiveGrabCount = 0;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (isSelected)
        {
            ActiveGrabCount--;
            if (ActiveGrabCount < 0) ActiveGrabCount = 0;
        }
    }
}