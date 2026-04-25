using System.Threading.Tasks;
using Fusion;
using Photon.Voice.Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SessionLauncher : MonoBehaviour
{
    [SerializeField] NetworkRunner runnerPrefab;

    NetworkRunner runner;

    public static string LocalNickname { get; private set; }

    public async Task<bool> StartSession(string nickname)
    {
        LocalNickname = nickname;

        runner = FindFirstObjectByType<NetworkRunner>();

        if (runner == null && runnerPrefab != null)
        {
            runner = Instantiate(runnerPrefab);
            runner.name = "NetworkRunner";
        }

        if (runner == null)
        {
            Debug.LogError("No NetworkRunner found in scene and no prefab assigned.");
            return false;
        }

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = "Room1",
            Scene = sceneInfo,
            SceneManager = runner.GetComponent<INetworkSceneManager>()
        });

        if (!result.Ok)
        {
            Debug.LogError($"StartGame failed: {result.ShutdownReason}");
            return false;
        }

        Debug.Log($"Joined session: {runner.SessionInfo.Name} as {runner.LocalPlayer}");

        var voiceClient = FindFirstObjectByType<FusionVoiceClient>();
        if (voiceClient != null)
            voiceClient.ConnectAndJoinRoom();

        return true;
    }
}
