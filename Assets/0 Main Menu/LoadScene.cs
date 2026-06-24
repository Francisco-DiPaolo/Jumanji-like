using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;

public class LoadScene : MonoBehaviour
{
    public void Play()
    {
        // Buscamos si hay un NetworkRunner activo en la escena (partida online)
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();

        if (runner != null)
        {
            // En Photon Fusion, solo el Host/Server tiene la autoridad para cambiar la escena
            if (runner.IsSceneAuthority)
            {
                int gameSceneIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Game.unity");
                if (gameSceneIndex != -1)
                {
                    runner.LoadScene(SceneRef.FromIndex(gameSceneIndex));
                }
                else
                {
                    Debug.LogError("No se pudo encontrar la escena 'Game' en los Build Settings. Asegúrate de agregarla.");
                }
            }
            else
            {
                Debug.LogWarning("Solo el Host o Servidor puede cambiar la escena en una sesión activa.");
            }
        }
        else
        {
            // Si no hay sesión de Fusion (modo offline o pruebas), cargamos de manera clásica
            SceneManager.LoadScene("Game");
        }
    }
}
