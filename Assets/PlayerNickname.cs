using Fusion;
using UnityEngine;

public class PlayerNickname : NetworkBehaviour
{
    [Networked, Capacity(32)] public NetworkString<_32> Nickname { get; set; }

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            RpcSetNickname(SessionLauncher.LocalNickname ?? "Player");
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RpcSetNickname(NetworkString<_32> nickname)
    {
        Nickname = nickname;
    }

    public override void Render()
    {
        if (Nickname.Length > 0)
            gameObject.name = $"Player [{Nickname}]";
    }
}
