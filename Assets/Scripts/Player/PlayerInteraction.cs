using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public void OnInteract(InputValue value)
    {
        if (!value.isPressed)
            return;

        Debug.Log("Interact");
    }
}