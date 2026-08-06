using UnityEngine;
using UnityEngine.EventSystems;

public class QuestTrackerHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Animator animator;

    public void OnPointerEnter(PointerEventData eventData)
    {
        animator.SetBool("Expanded", true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        animator.SetBool("Expanded", false);
    }
}