using UnityEngine;

public class DeadlyCurseSkill : SkillProjectile
{
    private Animator anim;
    private bool isExploding = false;
    
    [SerializeField] private float explodeDuration = 0.5f; // Thời gian chờ nổ xong (chỉnh lại cho khớp với animation của bạn)

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    protected override void Update()
    {
        // Chỉ di chuyển nếu chưa bị nổ
        if (!isExploding)
        {
            base.Update();
        }
    }

    protected override void OnHitTarget()
    {
        // Đánh dấu là đang nổ để không bay tới trước nữa
        isExploding = true;
        
        if (anim != null)
        {
            // Trigger parameter "Hit" như bạn mong muốn
            anim.SetTrigger("Hit"); 
        }

        // Thay vì destroy ngay lập tức như đạn bình thường, ta delay một chút để animation nổ kịp chạy xong
        Destroy(gameObject, explodeDuration);
    }
}
