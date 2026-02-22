using System;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    public NetworkPrefabRef playerPrefab;

    private NetworkRunner runner;

    void Awake()
    {
         runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null)
        {
            runner.AddCallbacks(this);
            Debug.Log("PlayerSpawner: Callbacks added to runner.");
        }
        else
        {
            Debug.LogError("PlayerSpawner: NetworkRunner not found in scene!");
        }
    }
    void Start()
    {
       
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player Joined: ID={player.PlayerId}, IsLocal={player == runner.LocalPlayer}");

        if (runner.IsServer)
        {
            Debug.Log($"Server spawning player for PlayerRef: {player}");
            runner.Spawn(
                playerPrefab,
                new Vector3(player.RawEncoded * 2, 0, 0),
                Quaternion.identity,
                player
            );
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerInputData data = new PlayerInputData();

        data.move = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
         data.look = new Vector2(
        Input.GetAxisRaw("Mouse X"),
        Input.GetAxisRaw("Mouse Y")
    );

        input.Set(data);
    }
    // Métodos obligatorios vacíos
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {}
    
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {}
    public void OnConnectedToServer(NetworkRunner runner) 
    {
        Debug.Log("Connected to Server.");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner) 
    {
        Debug.Log("Disconnected from Server.");
    }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) {}
    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) {}
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }
}