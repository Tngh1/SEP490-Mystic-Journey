using UnityEngine;
using UnityEngine.EventSystems;

public class QuestTrackerHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Animator animator;

    // Initializes internal component caches and dependencies for QuestTrackerHover upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    // Executes on pointer enter operation.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (animator != null)
            animator.SetBool("Expanded", true);
    }

    // Executes on pointer exit operation.
    public void OnPointerExit(PointerEventData eventData)
    {
        if (animator != null)
            animator.SetBool("Expanded", false);
    }
}
