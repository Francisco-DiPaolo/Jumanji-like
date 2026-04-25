using Fusion;
using UnityEngine;

public struct PlayerInputData : INetworkInput
{
    public Vector2 move;
    public Vector2 look;
    public NetworkButtons buttons;
}

public enum InputButton
{
    Jump,
    Sprint
}