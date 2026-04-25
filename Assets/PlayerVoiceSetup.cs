using Fusion;
using Photon.Voice.Unity;
using UnityEngine;

public class PlayerVoiceSetup : NetworkBehaviour
{
    [SerializeField] Recorder recorder;
    [SerializeField] Speaker speaker;
    [SerializeField] AudioSource speakerAudioSource;

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            ConfigureLocalRecorder();
        }
        else
        {
            ConfigureRemoteSpeaker();
        }
    }

    void ConfigureLocalRecorder()
    {
        if (recorder == null)
            return;

        recorder.TransmitEnabled = true;
        recorder.VoiceDetection = true;
        recorder.VoiceDetectionThreshold = 0.005f;

        if (speaker != null)
            speaker.enabled = false;
    }

    void ConfigureRemoteSpeaker()
    {
        if (recorder != null)
            recorder.TransmitEnabled = false;

        if (speakerAudioSource == null)
            return;

        speakerAudioSource.spatialBlend = 1.0f;
        speakerAudioSource.rolloffMode = AudioRolloffMode.Linear;
        speakerAudioSource.minDistance = 1f;
        speakerAudioSource.maxDistance = 25f;
        speakerAudioSource.volume = 1.0f;
        speakerAudioSource.spread = 180f;
    }
}
