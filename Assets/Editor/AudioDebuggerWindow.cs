using UnityEngine;
using UnityEditor;

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
        if (GUILayout.Button(isMuted ? "DESMUTEAR JUEGO" : "MUTEAR TODO EL JUEGO", GUILayout.Height(30)))
        {
            isMuted = !isMuted;
            AudioListener.volume = isMuted ? 0f : 1f;
        }
        GUILayout.EndHorizontal();

        if (isMuted)
        {
            GUI.color = Color.red;
            GUILayout.Label("EL AUDIO DEL JUEGO ESTÁ MUTEADO. Si sigues escuchando fuego, NO viene del juego.");
            GUI.color = Color.white;
        }

        GUILayout.Space(10);
        showOnlyAudible = GUILayout.Toggle(showOnlyAudible, "Mostrar SÓLO lo que el jugador puede escuchar");

        if (Application.isPlaying)
        {
            AudioListener listener = Object.FindAnyObjectByType<AudioListener>();
            Vector3 listenerPos = listener != null ? listener.transform.position : Vector3.zero;
            
            if (listener == null)
            {
                GUILayout.Label("ATENCIÓN: No hay AudioListener activo.", EditorStyles.boldLabel);
            }

            // Usamos Resources para encontrar TODO, incluso lo que esté oculto o instanciado temporalmente
            AudioSource[] sources = Resources.FindObjectsOfTypeAll<AudioSource>();
            
            GUILayout.Space(10);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            int count = 0;
            
            foreach (var src in sources)
            {
                // Ignorar prefabs o assets que no están en la escena
                if (src.gameObject.scene.rootCount == 0 && src.gameObject.hideFlags != HideFlags.HideAndDontSave)
                    continue;

                if (src.isPlaying)
                {
                    float perceivedVolume = 1f;
                    
                    if (listener != null)
                    {
                        perceivedVolume = CalculatePerceivedVolume(src, listenerPos);
                        if (showOnlyAudible && perceivedVolume <= 0.001f)
                            continue;
                    }

                    count++;
                    GUILayout.BeginHorizontal();
                    
                    EditorGUILayout.ObjectField(src.gameObject, typeof(GameObject), true, GUILayout.Width(200));
                    
                    string clipName = src.clip != null ? src.clip.name : "Sin Clip";
                    GUILayout.Label(clipName, GUILayout.Width(150));
                    
                    if (listener != null)
                    {
                        GUI.color = perceivedVolume > 0.05f ? Color.green : Color.yellow;
                        GUILayout.Label("Volumen real: " + (perceivedVolume * 100f).ToString("F1") + "%", GUILayout.Width(130));
                        GUI.color = Color.white;

                        float dist = Vector3.Distance(src.transform.position, listenerPos);
                        GUILayout.Label("Dist: " + dist.ToString("F1") + "m", GUILayout.Width(80));
                    }
                    else
                    {
                        GUILayout.Label("Volumen: " + src.volume, GUILayout.Width(130));
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
                }
            }
            
            if (count == 0)
            {
                GUILayout.Label("Ningún AudioSource está reproduciéndose en este momento.");
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

        return volume2D + volume3D;
    }
}
