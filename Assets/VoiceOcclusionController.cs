using Fusion;
using Photon.Voice.Unity;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Speaker))]
public class VoiceOcclusionController : NetworkBehaviour
{
    [Header("Occlusion Settings")]
    [SerializeField] private LayerMask occlusionLayerMask = 1; // Default to Default layer
    [SerializeField] private float muffledFrequency = 800f;
    [SerializeField] private float clearFrequency = 22000f;
    [SerializeField] private float occlusionVolumeFactor = 0.6f;
    [SerializeField] private float occlusionCheckInterval = 0.1f;
    [SerializeField] private float smoothingSpeed = 5f;

    private AudioSource audioSource;
    private AudioLowPassFilter lowPassFilter;
    private AudioListener localListener;
    
    private float lastCheckTime;
    private float targetFrequency;
    private float targetVolume;
    private float originalVolume;
    private bool isInitialized;

    public override void Spawned()
    {
        // This logic only applies to remote players from the local client's perspective.
        // We disable the script on our own player object.
        if (HasInputAuthority)
        {
            enabled = false;
            return;
        }

        audioSource = GetComponent<AudioSource>();
        lowPassFilter = GetComponent<AudioLowPassFilter>();
        
        if (lowPassFilter == null)
        {
            lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
        }

        originalVolume = audioSource.volume;
        targetFrequency = clearFrequency;
        targetVolume = originalVolume;
        
        lowPassFilter.cutoffFrequency = clearFrequency;
        isInitialized = true;
    }

    public override void Render()
    {
        // Only proceed if initialized and not the local player
        if (!isInitialized || HasInputAuthority) return;

        // Try to find the local AudioListener if we don't have one yet
        if (localListener == null)
        {
            localListener = FindFirstObjectByType<AudioListener>();
            if (localListener == null) return;
        }

        float distance = Vector3.Distance(transform.position, localListener.transform.position);

        // Performance Optimization: Only check occlusion if within audible range
        if (distance <= audioSource.maxDistance)
        {
            if (Time.time - lastCheckTime > occlusionCheckInterval)
            {
                lastCheckTime = Time.time;
                UpdateOcclusionState();
            }
        }
        else
        {
            // Reset to clear state if out of range to avoid starting muffled when coming back in range
            targetFrequency = clearFrequency;
            targetVolume = originalVolume;
        }

        ApplySmoothTransitions();
    }

    private void UpdateOcclusionState()
    {
        Vector3 listenerPos = localListener.transform.position;
        Vector3 speakerPos = transform.position;

        // Perform Linecast to check for physical obstacles
        if (Physics.Linecast(listenerPos, speakerPos, out RaycastHit hit, occlusionLayerMask))
        {
            // Obstacle detected: Muffle the sound
            targetFrequency = muffledFrequency;
            targetVolume = originalVolume * occlusionVolumeFactor;
        }
        else
        {
            // Clear path: Normal sound
            targetFrequency = clearFrequency;
            targetVolume = originalVolume;
        }
    }

    private void ApplySmoothTransitions()
    {
        // Smoothly interpolate cutoff frequency and volume to prevent audio popping
        lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassFilter.cutoffFrequency, targetFrequency, Time.deltaTime * smoothingSpeed);
        audioSource.volume = Mathf.Lerp(audioSource.volume, targetVolume, Time.deltaTime * smoothingSpeed);
    }
}
