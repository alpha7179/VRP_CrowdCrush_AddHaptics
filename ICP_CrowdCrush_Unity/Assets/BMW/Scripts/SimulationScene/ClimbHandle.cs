using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ClimbHandle : UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable
{
    // XRI의 Climb Provider가 있다면 이 스크립트 대신
    // 컴포넌트 추가 -> [Climb Interactable]을 사용하는 것을 권장합니다.
    // 이 스크립트는 커스텀 로직이 필요할 때만 사용하세요.

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        Debug.Log("Climb Handle Grabbed");
        // GameStepManager에 잡았다는 신호를 보낼 수도 있음
    }
}