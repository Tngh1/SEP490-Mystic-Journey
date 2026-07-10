using UnityEngine;

public static class InputButtons
{
    /// <summary>Basic attack. Edge-triggered by consumer (WasPressedThisTick equivalent).</summary>
    public const int Attack = 0;

    /// <summary>Skill slot 1.</summary>
    public const int Skill1 = 1;

    /// <summary>Skill slot 2.</summary>
    public const int Skill2 = 2;

    /// <summary>Skill slot 3.</summary>
    public const int Skill3 = 3;

    /// <summary>Interact with nearest WorldInteractable (NPC, chest, dungeon entrance).</summary>
    public const int Interact = 4;

    /// <summary>Aim confirm — left click in AoE aiming mode commits the cast position.</summary>
    public const int AimConfirm = 5;

    /// <summary>Reserved for future action (e.g. dodge roll). Do not assign.</summary>
    public const int Reserved6 = 6;

    /// <summary>Reserved for future action (e.g. block / parry). Do not assign.</summary>
    public const int Reserved7 = 7;

    public static readonly string[] DebugNames = new[]
    {
        "Attack",
        "Skill1",
        "Skill2",
        "Skill3",
        "Interact",
        "AimConfirm",
        "Reserved6",
        "Reserved7"
    };
}