using Fusion;
using Photon.Voice.Unity;
using UnityEngine;

public class PlayerVoiceSetup : NetworkBehaviour
{
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
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 1.0f;
            audioSource.maxDistance = 25.0f;
            audioSource.volume = 1.0f;
        }
    }
}
