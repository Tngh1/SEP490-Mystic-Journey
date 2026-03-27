using System;
using UnityEngine;

public class PlayerAnimations: MonoBehaviour
{
   	private Animator animator;
	[SerializeField] private PlayerBehaviour playerBehaviour;

    private const string IS_RUNNING = "IsRunning";
    private const string ATTACK = "PlayerAttack";

    private void Awake()
	{
		animator = GetComponent<Animator>();
    }

    private void Start()
    {
        TriggerAttAnimTurnOff();
        playerBehaviour.OnPlayerAttack += PlayerBehaviour_OnPlayerAttack;
    }

    private void PlayerBehaviour_OnPlayerAttack(object sender, System.EventArgs e)
	{
		animator.SetTrigger(ATTACK);
        TriggerAttAnimTurnOn();
    }

    public void TriggerAttAnimTurnOff()
    {
        playerBehaviour.PolyCollTurnOff();
    }
    public void TriggerAttAnimTurnOn()
    {
        playerBehaviour.PolyCollTurnOn();
    }


    private void Update()
	{
		if (animator)
		{
			animator.SetBool(IS_RUNNING, PlayerBehaviour.Instance.IsRunning());
		}
	}

}
