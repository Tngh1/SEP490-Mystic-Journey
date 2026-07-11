using UnityEngine;

/// <summary>
/// Bit indices for <see cref="Fusion.NetworkButtons"/> carried in
/// <see cref="NetworkInputData"/>. These are the ONLY inputs that must be
/// simulated by Fusion (they affect the networked world). Client-local actions
/// (Interact, Inventory, Map) are NOT here — they are polled directly on the
/// local player from <see cref="GameplayInputProvider"/> and never travel over
/// the network.
/// </summary>
public static class InputButtons
{
    /// <summary>Basic attack.</summary>
    public const int Attack = 0;

    /// <summary>Skill slot 1.</summary>
    public const int Skill1 = 1;

    /// <summary>Skill slot 2.</summary>
    public const int Skill2 = 2;

    /// <summary>Skill slot 3.</summary>
    public const int Skill3 = 3;
}
