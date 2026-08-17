using UnityEngine;

// Executes mono behaviour operation.
public class StampAnimation : MonoBehaviour
{
    // Update visibility for stamp; it updates active.
    public void HideStamp()
    {
        gameObject.SetActive(false);
    }
}
