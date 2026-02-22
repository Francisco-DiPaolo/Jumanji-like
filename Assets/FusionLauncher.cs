using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FusionLauncher : MonoBehaviour
{
    public NetworkRunner runner;

    async void Start()
{
    Debug.Log("Runner instantiated");

    if (runner == null)
    {
        Debug.LogError("Runner is NULL! Make sure it's assigned in the inspector.");
        return;
    }

    var sceneInfo = new NetworkSceneInfo();
    sceneInfo.AddSceneRef(SceneRef.FromIndex(
        SceneManager.GetActiveScene().buildIndex
    ));

    var result = await runner.StartGame(new StartGameArgs()
{
    GameMode = GameMode.AutoHostOrClient,
    SessionName = "Room1",
    Scene = sceneInfo,
    SceneManager = runner.GetComponent<INetworkSceneManager>()
});

    Debug.Log($"StartGame result: {result.Ok}, ShutdownReason: {result.ShutdownReason}");
    if (result.Ok)
    {
        Debug.Log($"Joined session: {runner.SessionInfo.Name} as {runner.LocalPlayer}");
    }
}
}