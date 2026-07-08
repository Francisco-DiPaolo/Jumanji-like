using UnityEngine;
using System.Collections.Generic;

public class WaterSplashDetector : MonoBehaviour
{
    [Header("Splash Settings")]
    [Tooltip("El sonido que se reproducirá al caer al agua")]
    public AudioClip splashClip;
    
    [Range(0f, 1f)] 
    public float volume = 1f;
    
    [Tooltip("Selecciona aquí la capa (Layer) que tiene el jugador para detectarlo")]
    public LayerMask playerLayer;

    // Diccionario para contar cuántos colliders del mismo jugador están dentro del agua
    private Dictionary<GameObject, int> _playersInWater = new Dictionary<GameObject, int>();

    private void OnTriggerEnter(Collider other)
    {
        // Comprobamos si el objeto que entró pertenece a la capa del jugador
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            // Usamos el objeto "Raíz" (Root) del jugador. 
            GameObject playerRoot = other.transform.root.gameObject;
            
            if (!_playersInWater.ContainsKey(playerRoot))
            {
                _playersInWater[playerRoot] = 0;
            }

            _playersInWater[playerRoot]++;

            // Solo reproducimos el sonido si es el PRIMER collider de este jugador que toca el agua
            if (_playersInWater[playerRoot] == 1)
            {
                PlaySplashSound(other.transform.position);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            GameObject playerRoot = other.transform.root.gameObject;
            
            if (_playersInWater.ContainsKey(playerRoot))
            {
                _playersInWater[playerRoot]--;
                
                // Si el contador llega a 0, significa que el jugador salió por completo del agua
                if (_playersInWater[playerRoot] <= 0)
                {
                    _playersInWater.Remove(playerRoot);
                }
            }
        }
    }

    public void PlaySplashSound(Vector3 spawnPosition)
    {
        if (splashClip == null) return;

        // Creamos un GameObject temporal para reproducir el audio
        GameObject audioObj = new GameObject("SplashAudio");
        
        // Lo posicionamos exactamente en la posición del objeto que cayó (el jugador)
        audioObj.transform.position = spawnPosition;
        
        // Añadimos el componente AudioSource
        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.clip = splashClip;
        
        // spatialBlend en 1 lo hace un sonido 3D. 
        // Esto garantiza que todos los jugadores lo escuchen desde la posición correcta en el mapa.
        source.spatialBlend = 1f; 
        
        source.volume = volume;
        source.priority = 94; // Prioridad solicitada
        
        source.Play();
        
        // El objeto se destruye automáticamente cuando termina el clip
        Destroy(audioObj, splashClip.length);
    }
}
