using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;

public class FixGravity : MonoBehaviour
{
    private CharacterController characterController;
    private ClimbProvider climbProvider;
    private bool forceGravityCheck = true;

    void Awake()
    {
        characterController = FindAnyObjectByType<CharacterController>();
        climbProvider = FindAnyObjectByType<ClimbProvider>();
    }

    // subscribe/unsubscribe to events that happen when
    // climbing starts/ends.
    private void OnEnable()
    {
        climbProvider.locomotionStarted += LocomotionStarted;
        climbProvider.locomotionEnded += LocomotionEnded;
    }

    private void OnDisable()
    {
        climbProvider.locomotionStarted -= LocomotionStarted;
        climbProvider.locomotionEnded -= LocomotionEnded;
    }

    private void Update()
    {
        if(forceGravityCheck)
        {
            // force CharacterController to apply gravity
            characterController.SimpleMove(Vector3.zero);
        }
    }

    // gravity check is only done if we're not climbing
    // (otherwise we'd fall)
    private void LocomotionEnded(LocomotionProvider provider)
    {
        forceGravityCheck = true;
    }
    
    private void LocomotionStarted(LocomotionProvider provider)
    {
        forceGravityCheck = false;
    }
}