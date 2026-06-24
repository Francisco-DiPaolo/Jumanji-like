using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Fusion;

public class LoadScene : MonoBehaviour
{
    private AsyncOperation preloadedSceneOperation;

    private void Start()
    {
        // Iniciamos la precarga de la escena "Game" en segundo plano
        StartCoroutine(PreloadSceneCoroutine());
    }

    private IEnumerator PreloadSceneCoroutine()
    {
        // Esperamos 3 segundos antes de iniciar la precarga para dejar que el menú se estabilice
        yield return new WaitForSeconds(3f);

        // Comenzamos la carga asíncrona de la escena "Game"
        preloadedSceneOperation = SceneManager.LoadSceneAsync("Game");

        if (preloadedSceneOperation != null)
        {
            // Impedimos que la escena se active automáticamente al terminar de cargar.
            // Esto detiene el progreso en aproximadamente 90% en el fondo.
            preloadedSceneOperation.allowSceneActivation = false;
        }
    }

    public void Play()
    {
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();

        if (runner != null)
        {
            // --- MODO ONLINE (Photon Fusion) ---
            // Nota: En multijugador activo, Fusion debe gestionar la carga y sincronización
            // de escenas para todos los clientes, por lo que no se usa la precarga local.
            if (runner.IsSceneAuthority)
            {
                int gameSceneIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Game.unity");
                if (gameSceneIndex != -1)
                {
                    runner.LoadScene(SceneRef.FromIndex(gameSceneIndex));
                }
                else
                {
                    Debug.LogError("No se pudo encontrar la escena 'Game' en los Build Settings.");
                }
            }
            else
            {
                Debug.LogWarning("Solo el Host o Servidor puede cambiar la escena en una sesión activa.");
            }
        }
        else
        {
            // --- MODO OFFLINE / LOCAL ---
            if (preloadedSceneOperation != null)
            {
                // Permitimos la activación de la escena precargada.
                // Si ya llegó al 90%, se activará de forma casi instantánea.
                // Si aún no ha terminado de cargar, se activará automáticamente en cuanto finalice.
                preloadedSceneOperation.allowSceneActivation = true;
            }
            else
            {
                // Salvaguarda por si la precarga no se inició o falló
                SceneManager.LoadScene("Game");
            }
        }
    }
}
