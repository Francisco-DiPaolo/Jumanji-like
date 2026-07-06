using System;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] NetworkPrefabRef playerPrefab;
    [SerializeField] Transform[] playerSpawnPositions;
    [SerializeField] float spawnRotationY = -344.922f;

    int spawnIndex;
    bool registered;

    void Update()
    {
        if (registered)
            return;

        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null)
        {
            runner.AddCallbacks(this);
            registered = true;
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            Quaternion rotation = Quaternion.Euler(0, spawnRotationY, 0);
            int index = Mathf.Min(spawnIndex, playerSpawnPositions.Length - 1);
            Transform spawnPoint = playerSpawnPositions[index];
            spawnIndex++;
            runner.Spawn(playerPrefab, spawnPoint.position, rotation, player);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        PlayerInputData data = new PlayerInputData();

        data.move = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        // data.look ya no se usa: la cámara lee Input.GetAxisRaw directamente en
        // PlayerMovement.Update() / LateUpdate() para evitar el desincronismo de FixedUpdate.

        data.buttons.Set(InputButton.Jump, Input.GetKey(KeyCode.Space));
        data.buttons.Set(InputButton.Sprint, Input.GetKey(KeyCode.LeftShift));
        data.buttons.Set(InputButton.Interact, Input.GetKey(KeyCode.E));
        data.buttons.Set(InputButton.Weave, Input.GetKeyDown(KeyCode.G));

        input.Set(data);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {}
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {}
    public void OnConnectedToServer(NetworkRunner runner) {}
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {}
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {}
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) {}
    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) {}
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {}
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}
    public void OnSceneLoadDone(NetworkRunner runner) {}
    public void OnSceneLoadStart(NetworkRunner runner) {}
}