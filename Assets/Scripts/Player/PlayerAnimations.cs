using System;
using UnityEngine;

// Executes mono behaviour operation.
public class PlayerAnimations: MonoBehaviour
{
   	private Animator animator;
	[SerializeField] private PlayerBehaviour playerBehaviour;

    private const string IS_RUNNING = "IsRunning";
    private const string ATTACK = "PlayerAttack";

    // Initializes internal component caches and dependencies for PlayerAnimations upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
	{
		animator = GetComponent<Animator>();
    }

    // Performs startup initialization for PlayerAnimations on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        TriggerAttAnimTurnOff();
        playerBehaviour.OnPlayerAttack += PlayerBehaviour_OnPlayerAttack;
    }

    // Executes player behaviour_on player attack operation.
    private void PlayerBehaviour_OnPlayerAttack(object sender, System.EventArgs e)
	{
		animator.SetTrigger(ATTACK);
        TriggerAttAnimTurnOn();
    }

    // Executes trigger att anim turn off operation.
    public void TriggerAttAnimTurnOff()
    {
        playerBehaviour.PolyCollTurnOff();
    }
    // Executes trigger att anim turn on operation.
    public void TriggerAttAnimTurnOn()
    {
        playerBehaviour.PolyCollTurnOn();
    }


    // Per-frame update loop for PlayerAnimations.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
	{
		if (animator)
		{
			animator.SetBool(IS_RUNNING, PlayerBehaviour.Instance.IsRunning());
		}
	}

}
