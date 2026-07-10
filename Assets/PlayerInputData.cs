using Fusion;
using UnityEngine;

public struct PlayerInputData : INetworkInput
{
    public Vector2 move;
    public Vector2 look;
    public NetworkButtons buttons;
    public float yaw;
}

public enum InputButton
{
    Jump,
    Sprint,
    Interact,
    Weave
}