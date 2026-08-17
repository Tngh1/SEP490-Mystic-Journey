using Fusion;
using UnityEngine;


// Executes i network input operation.
public struct NetworkInputData : INetworkInput
{
    public Vector2 Move;


    public Vector2 AimWorldPosition;

    public NetworkButtons Buttons;
}
