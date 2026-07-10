using UnityEngine;
using UnityEditor;
using System.Linq;

public class AudioDebuggerWindow : EditorWindow
{
    [MenuItem("Window/Audio Debugger")]
    public static void ShowWindow()
    {
        GetWindow<AudioDebuggerWindow>("Audio Debugger");
    }

    Vector2 scrollPos;
    bool showOnlyAudible = false;
    bool isMuted = false;

    void OnGUI()
    {
        GUILayout.Label("Depurador de Audio Avanzado", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(isMuted ? "DESMUTEAR JUEGO (AudioListener)" : "MUTEAR TODO EL JUEGO (AudioListener)", GUILayout.Height(30)))
        {
            isMuted = !isMuted;
            AudioListener.volume = isMuted ? 0f : 1f;
        }
        GUILayout.EndHorizontal();

        // Chequear si hay múltiples listeners
        AudioListener[] listeners = Object.FindObjectsOfType<AudioListener>(true);
        AudioListener activeListener = null;
        foreach(var l in listeners)
        {
            if (l.isActiveAndEnabled) {
                if (activeListener == null) activeListener = l;
                else GUILayout.Label("⚠️ ADVERTENCIA: Hay múltiples AudioListeners activos en la escena.", EditorStyles.boldLabel);
            }
        }

        GUILayout.Space(10);
        showOnlyAudible = GUILayout.Toggle(showOnlyAudible, "Mostrar SÓLO lo que el jugador puede escuchar (¡DESMARCA ESTO PARA VER TODOS!)");

        if (Application.isPlaying)
        {
            Vector3 listenerPos = activeListener != null ? activeListener.transform.position : Vector3.zero;
            
            // Buscar todos los AudioSource en memoria
            AudioSource[] sources = Resources.FindObjectsOfTypeAll<AudioSource>();
            
            GUILayout.Space(10);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            int count = 0;
            
            foreach (var src in sources)
            {
                // Ignorar assets puros, a menos que sean un objeto DontDestroyOnLoad/Temporal
                if (src.gameObject.scene.rootCount == 0 && src.gameObject.hideFlags != HideFlags.HideAndDontSave)
                    continue;

                if (src.isPlaying)
                {
                    float perceivedVolume = 1f;
                    
                    if (activeListener != null)
                    {
                        perceivedVolume = CalculatePerceivedVolume(src, listenerPos);
                        
                        // Si está marcado, ocultar los inaudibles. 
                        // Pero si el cálculo falló por una curva custom, podríamos estar ocultando el real.
                        if (showOnlyAudible && perceivedVolume <= 0.001f)
                            continue;
                    }

                    count++;
                    GUILayout.BeginVertical("box");
                    GUILayout.BeginHorizontal();
                    
                    EditorGUILayout.ObjectField(src.gameObject, typeof(GameObject), true, GUILayout.Width(180));
                    
                    string clipName = src.clip != null ? src.clip.name : "Sin Clip";
                    GUILayout.Label(clipName, GUILayout.Width(120));
                    
                    if (activeListener != null)
                    {
                        GUI.color = perceivedVolume > 0.05f ? Color.green : Color.yellow;
                        GUILayout.Label("Vol. Calculado: " + (perceivedVolume * 100f).ToString("F1") + "%", GUILayout.Width(130));
                        GUI.color = Color.white;

                        float dist = Vector3.Distance(src.transform.position, listenerPos);
                        GUILayout.Label("Dist: " + dist.ToString("F1") + "m", GUILayout.Width(80));
                    }

                    if (GUILayout.Button("MUTEAR", GUILayout.Width(80)))
                    {
                        src.volume = 0f;
                        src.mute = true;
                    }

                    if (GUILayout.Button("Seleccionar", GUILayout.Width(80)))
                    {
                        Selection.activeGameObject = src.gameObject;
                        EditorGUIUtility.PingObject(src.gameObject);
                    }
                    GUILayout.EndHorizontal();
                    
                    // Mostrar info de Rolloff
                    GUILayout.Label($"Rolloff: {src.rolloffMode} | SpatialBlend: {src.spatialBlend:F2} | Min/Max Dist: {src.minDistance:F1}/{src.maxDistance:F1}", EditorStyles.miniLabel);
                    GUILayout.EndVertical();
                }
            }
            
            if (count == 0)
            {
                GUILayout.Label("Ningún AudioSource en reproducción coincide con los filtros.");
            }
            
            EditorGUILayout.EndScrollView();
            Repaint();
        }
        else
        {
            GUILayout.Label("Dale a Play en el editor para depurar.");
        }
    }

    float CalculatePerceivedVolume(AudioSource src, Vector3 listenerPos)
    {
        float baseVol = src.volume;
        float volume2D = baseVol * (1f - src.spatialBlend);
        
        float distance = Vector3.Distance(src.transform.position, listenerPos);
        float volume3D = 0f;
        
        if (distance <= src.maxDistance)
        {
            float attenuation = 1f;
            if (distance > src.minDistance)
            {
                if (src.rolloffMode == AudioRolloffMode.Linear)
                    attenuation = 1f - ((distance - src.minDistance) / (src.maxDistance - src.minDistance));
                else
                    attenuation = src.minDistance / distance;
            }
            volume3D = baseVol * src.spatialBlend * attenuation;
        }
        
        // Si tiene curva custom, Unity ignora la maxDistance a veces, devolvemos un valor para no filtrarlo sin querer
        if (src.rolloffMode == AudioRolloffMode.Custom)
            return baseVol;

        return volume2D + volume3D;
    }
}
