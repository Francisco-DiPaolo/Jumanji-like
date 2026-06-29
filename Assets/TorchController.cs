using Fusion;
using UnityEngine;

public class TorchController : NetworkBehaviour
{
    [SerializeField] bool isGreenTorch;

    [Header("Sonidos")]
    [SerializeField] AudioClip igniteClip;  // Sonido al encenderse (one-shot)
    [SerializeField] AudioClip loopClip;    // Sonido mientras está prendida (loop)
    [SerializeField] AudioClip extinguishClip; // Sonido al apagarse (one-shot)

    [Networked] public NetworkBool IsLit { get; set; }

    ChangeDetector _changeDetector;
    GameObject _fireVfx;
    AudioSource _audioSource;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _fireVfx = transform.Find("FireVFX")?.gameObject;

        // Obtenemos o creamos el AudioSource en este mismo GameObject
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f; // 3D sound

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
        }
    }

    void ApplyVisuals(bool playSounds = true)
    {
        if (_fireVfx != null)
            _fireVfx.SetActive(IsLit);

        if (_audioSource == null) return;

        if (IsLit)
        {
            if (playSounds)
            {
                // One-shot del encendido
                if (igniteClip != null)
                    _audioSource.PlayOneShot(igniteClip);

                // Loop de la llama mientras está prendida
                if (loopClip != null)
                {
                    _audioSource.clip = loopClip;
                    _audioSource.loop = true;
                    _audioSource.Play();
                }
            }
        }
        else
        {
            // Apagamos el loop cuando se extingue
            _audioSource.Stop();
            _audioSource.loop = false;
            
            if (playSounds && extinguishClip != null)
            {
                _audioSource.PlayOneShot(extinguishClip);
            }
        }
    }
}

