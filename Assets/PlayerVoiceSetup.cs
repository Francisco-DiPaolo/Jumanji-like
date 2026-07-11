using Fusion;
using Photon.Voice.Unity;
using UnityEngine;

public class PlayerVoiceSetup : NetworkBehaviour
{
    [Header("Spatial Audio Settings")]
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;
    [SerializeField] private float minDistance = 1.0f;
    [SerializeField] private float maxDistance = 25.0f;
    [SerializeField] private float voiceVolume = 1.0f;

    public override void Spawned()
    {
        // Local setup
        if (HasInputAuthority)
        {
            var recorder = GetComponent<Recorder>();
            if (recorder != null)
            {
                recorder.TransmitEnabled = true;
                recorder.VoiceDetection = true;
                recorder.VoiceDetectionThreshold = 0.005f; // Optimized for low volume
            }
            
            // Disable local speaker to avoid feedback/echo
            var speaker = GetComponent<Speaker>();
            if (speaker != null) speaker.enabled = false;
        }

        // Global audio setup for spatial voice
        var audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.spatialize = true;
            audioSource.spatialBlend = 1.0f; // 3D Spatial
            audioSource.rolloffMode = rolloffMode;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.volume = voiceVolume;
        }
    }
}
