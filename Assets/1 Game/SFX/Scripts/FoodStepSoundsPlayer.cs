using UnityEngine;

public class FoodStepSoundsPlayer : MonoBehaviour
{
    public AudioClip[] WoodClips;
    public AudioClip[] ConcreteClips;
    public AudioClip[] WaterClips;

    public LayerMask Environment;

    [Header("Volumes")]
    [Range(0f, 1f)] public float woodVolume = 1f;
    [Range(0f, 1f)] public float concreteVolume = 1f;
    [Range(0f, 1f)] public float waterVolume = 1f;

    [Header("Water Settings")]
    [Tooltip("Reproducir sonido de agua cada N pasos (1 = siempre, 2 = cada 2 pasos, etc.)")]
    [Min(1)] public int waterStepFrequency = 2;
    private int _waterStepCounter = 0;

    [Range(0, 256)]
    public int priority = 128;

    public Animator Animator;
    private float _lastFootstep;

    private void OnValidate()
    {
        if (!Animator) Animator = GetComponent<Animator>();
    }

    private void Update()
    {
        var footstep = Animator.GetFloat("Footstep");
        if (Mathf.Abs(footstep) < .00001f) footstep = 0;

        if (_lastFootstep > 0 && footstep < 0 || _lastFootstep < 0 && footstep > 0)
        {
            SurfaceType currentSurface = GetSurfaceData(out AudioClip[] clips, out float currentVolume);
            bool playSound = true;

            if (currentSurface == SurfaceType.Water)
            {
                _waterStepCounter++;
                if (_waterStepCounter % waterStepFrequency != 0)
                {
                    playSound = false; // Saltamos este paso
                }
            }
            else
            {
                _waterStepCounter = 0; // Reiniciamos el contador si salimos del agua
            }

            if (playSound)
            {
                var randomClip = clips[Random.Range(0, clips.Length)];
                
                GameObject audioObj = new GameObject("FootstepAudio");
                audioObj.transform.position = transform.position;
                AudioSource source = audioObj.AddComponent<AudioSource>();
                source.clip = randomClip;
                source.spatialBlend = 1f;
                source.volume = currentVolume;
                source.priority = priority;
                source.Play();
                Destroy(audioObj, randomClip.length);
            }
        }

        _lastFootstep = footstep;
    }

    private SurfaceType GetSurfaceData(out AudioClip[] clips, out float currentVolume)
    {
        // Valores por defecto en caso de no detectar nada
        clips = WoodClips;
        currentVolume = woodVolume;
        SurfaceType type = SurfaceType.Wood;

        var isHit = Physics.Raycast(transform.position + Vector3.up * .01f, Vector3.down, out RaycastHit hit, .1f, Environment);

        if (isHit)
        {
            var surface = hit.collider.GetComponent<SurfaceDefinition>();
            if (surface)
            {
                type = surface.SurfaceType;
                if (type == SurfaceType.Concrete)
                {
                    clips = ConcreteClips;
                    currentVolume = concreteVolume;
                }
                else if (type == SurfaceType.Wood)
                {
                    clips = WoodClips;
                    currentVolume = woodVolume;
                }
                else if (type == SurfaceType.Water)
                {
                    clips = WaterClips;
                    currentVolume = waterVolume;
                }
            }
        }
        
        return type;
    }
}