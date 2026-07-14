using Fusion;
using UnityEngine;

public class TorchController : NetworkBehaviour
{
    [SerializeField] bool isGreenTorch;
    public bool IsGreenTorch => isGreenTorch;

    [Header("Colores y Partículas")]
    [SerializeField] ParticleSystem fireParticleSystem1;
    [SerializeField] ParticleSystem fireParticleSystem2;
    [SerializeField] Color orangeColor = new Color(1f, 0.4f, 0f, 1f);
    [SerializeField] Color greenColor = Color.green;

    [Header("Luz y Materiales")]
    [SerializeField] Light torchLight;
    [SerializeField] Color lightOrangeColor = new Color(1f, 0.4f, 0f, 1f);
    [SerializeField] Color lightGreenColor = Color.green;
    [SerializeField] float lightOrangeTemperature = 6500f;
    [SerializeField] float lightGreenTemperature = 6500f;
    [SerializeField] Renderer targetRenderer;
    [SerializeField] Material orangeMaterial;
    [SerializeField] Material greenMaterial;

    [Header("Sonidos")]
    [SerializeField] AudioClip igniteClip;  // Sonido al encenderse (one-shot)
    [Range(0f, 1f)] [SerializeField] float igniteVolume = 0.6f;
    [SerializeField] AudioClip loopClip;    // Sonido mientras está prendida (loop)
    [SerializeField] AudioClip extinguishClip; // Sonido al apagarse (one-shot)
    [Range(0f, 1f)] [SerializeField] float extinguishVolume = 0.6f;

    [Networked] public NetworkBool IsLit { get; set; }
    [Networked] public NetworkBool UseSuccessColor { get; set; }

    ChangeDetector _changeDetector;
    GameObject _fireVfx;
    AudioSource _loopSource;
    AudioSource _sfxSource;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _fireVfx = transform.Find("FireVFX")?.gameObject;

        // Buscamos o creamos los AudioSources necesarios
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length == 0)
        {
            _loopSource = gameObject.AddComponent<AudioSource>();
            _sfxSource = gameObject.AddComponent<AudioSource>();
        }
        else if (sources.Length == 1)
        {
            _loopSource = sources[0];
            _sfxSource = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            _loopSource = sources[0];
            _sfxSource = sources[1];
        }

        _loopSource.playOnAwake = false;
        _loopSource.spatialBlend = 1f; // Completamente 3D para que no se escuche en todo el mapa
        _loopSource.rolloffMode = AudioRolloffMode.Linear;
        _loopSource.minDistance = 3f;
        _loopSource.maxDistance = 20f;
        _loopSource.priority = 94; // Prioridad normal
        
        _sfxSource.playOnAwake = false;
        _sfxSource.spatialBlend = 1f;
        _sfxSource.rolloffMode = AudioRolloffMode.Linear;
        _sfxSource.minDistance = 3f;
        _sfxSource.maxDistance = 20f;
        _sfxSource.priority = 94; // Prioridad más alta para que no se corten

        if (Object.HasStateAuthority)
        {
            IsLit = false; // Asegurar que el estado inicial en red sea estrictamente false
            UseSuccessColor = false;
        }

        UpdateVisualState();
        ApplyVisuals(false); // Falso para no reproducir sonidos al iniciar
    }

    public void Light()
    {
        if (Object.HasStateAuthority)
            IsLit = true;
    }

    public void Extinguish()
    {
        if (Object.HasStateAuthority)
            IsLit = false;
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(IsLit))
                ApplyVisuals(true);
            if (change == nameof(UseSuccessColor))
                ApplyVisuals(false);
        }
    }

    void UpdateVisualState()
    {
        Color targetColor = UseSuccessColor ? greenColor : orangeColor;
        
        // Partículas
        if (fireParticleSystem1 != null)
        {
            var main = fireParticleSystem1.main;
            main.startColor = targetColor;
        }
        if (fireParticleSystem2 != null)
        {
            var main = fireParticleSystem2.main;
            main.startColor = targetColor;
        }

        // Luz (PointLight)
        Color targetLightColor = UseSuccessColor ? lightGreenColor : lightOrangeColor;
        if (torchLight != null)
        {
            torchLight.color = targetLightColor;
            torchLight.useColorTemperature = true;
            torchLight.colorTemperature = UseSuccessColor ? lightGreenTemperature : lightOrangeTemperature;
        }

        // Material del GameObject
        if (targetRenderer != null)
        {
            targetRenderer.sharedMaterial = UseSuccessColor ? greenMaterial : orangeMaterial;
        }
    }

    void ApplyVisuals(bool playSounds = true)
    {
        UpdateVisualState();

        if (_fireVfx != null)
            _fireVfx.SetActive(IsLit);

        if (_loopSource == null || _sfxSource == null) return;

        // Asegurar la prioridad siempre antes de reproducir, como solicitaste.
        _loopSource.priority = 94;
        _sfxSource.priority = 94;

        if (IsLit)
        {
            if (playSounds)
            {
                // One-shot del encendido (SFX dedicado)
                if (igniteClip != null)
                    _sfxSource.PlayOneShot(igniteClip, igniteVolume);

                // Loop de la llama mientras está prendida
                if (loopClip != null)
                {
                    _loopSource.clip = loopClip;
                    _loopSource.loop = true;
                    _loopSource.Play();
                }
            }
        }
        else
        {
            // Apagamos el loop cuando se extingue
            _loopSource.Stop();
            _loopSource.loop = false;
            
            if (playSounds && extinguishClip != null)
            {
                // PlayOneShot en la pista de SFX (no se corta al hacer Stop en el loop)
                _sfxSource.PlayOneShot(extinguishClip, extinguishVolume);
            }
        }
    }
}

