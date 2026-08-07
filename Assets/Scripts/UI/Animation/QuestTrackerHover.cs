using UnityEngine;
using UnityEngine.EventSystems;

public class QuestTrackerHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Animator animator;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (animator != null)
            animator.SetBool("Expanded", true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (animator != null)
            animator.SetBool("Expanded", false);
    }
}