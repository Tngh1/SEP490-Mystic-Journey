using Fusion;
using UnityEngine;


public struct NetworkInputData : INetworkInput
{
    public Vector2 Move;


    public Vector2 AimWorldPosition;

    public NetworkButtons Buttons;
}