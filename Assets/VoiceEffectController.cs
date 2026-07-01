using System;
using Fusion;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;

public class VoiceEffectController : NetworkBehaviour, IProcessor<float>, IProcessor<short>
{
    public enum VoiceMode
    {
        Normal = 1,
        Squirrel = 2,
        Robot = 3,
        Deep = 4,
        Echo = 5,
        Muted = 6,
        Underwater = 7
    }

    [Networked]
    public VoiceMode CurrentMode { get; set; }

    private Recorder recorder;
    private int sampleRate = 48000;
    private VoiceMode cachedMode;

    private float robotPhase;
    private const float RobotFrequency = 50.0f;

    private float[] echoBufferF;
    private short[] echoBufferS;
    private int echoIndex;
    private const float EchoDelay = 0.3f;
    private const float EchoFeedback = 0.4f;

    private float[] resampleBufferF;
    private short[] resampleBufferS;
    private float resampleReadIndex;
    private int resampleWriteIndex;
    private const int ResampleBufferSize = 96000;

    // --- Underwater effect parameters ---
    private float underwaterLpStateF;       // Low-pass filter state (float)
    private float underwaterLpStateS;       // Low-pass filter state (short path, stored as float)
    private float underwaterWobblePhase;    // Phase for subtle amplitude wobble
    private const float UnderwaterCutoff = 900f;   // Low-pass cutoff Hz (keeps speech intelligible)
    private const float UnderwaterWobbleFreq = 2.5f; // Slow wobble frequency Hz
    private const float UnderwaterWobbleDepth = 0.12f; // Subtle wobble depth (0-1)
    private const float UnderwaterPitch = 0.85f;   // Slight pitch down (less extreme than Deep's 0.7)

    // Separate resample state for Underwater so it doesn't conflict with Squirrel/Deep
    private float[] uwResampleBufferF;
    private short[] uwResampleBufferS;
    private float uwResampleReadIndex;
    private int uwResampleWriteIndex;

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            recorder = GetComponent<Recorder>();
            Debug.Log($"[VoiceEffect] Spawned. Has Recorder: {recorder != null}");
        }
    }

    private void PhotonVoiceCreated(PhotonVoiceCreatedParams p)
    {
        if (HasInputAuthority)
        {
            if (p.Voice is LocalVoiceAudioFloat floatVoice)
            {
                sampleRate = floatVoice.Info.SamplingRate;
                floatVoice.AddPostProcessor(this);
                Debug.Log($"[VoiceEffect] Registered as Float processor. Rate: {sampleRate}");
            }
            else if (p.Voice is LocalVoiceAudioShort shortVoice)
            {
                sampleRate = shortVoice.Info.SamplingRate;
                shortVoice.AddPostProcessor(this);
                Debug.Log($"[VoiceEffect] Registered as Short processor. Rate: {sampleRate}");
            }
            else
            {
                Debug.LogWarning($"[VoiceEffect] Unsupported voice type: {p.Voice.GetType()}");
            }
        }
    }

    private void PhotonVoiceRemoved()
    {
        Debug.Log("[VoiceEffect] Voice removed.");
    }

    public override void Render()
    {
        if (HasInputAuthority)
        {
            VoiceMode nextMode = VoiceMode.Normal;
            bool changed = false;

            if (Input.GetKeyDown(KeyCode.Alpha1)) { nextMode = VoiceMode.Normal; changed = true; }
            else if (Input.GetKeyDown(KeyCode.Alpha2)) { nextMode = VoiceMode.Squirrel; changed = true; }
            else if (Input.GetKeyDown(KeyCode.Alpha3)) { nextMode = VoiceMode.Robot; changed = true; }
            else if (Input.GetKeyDown(KeyCode.Alpha4)) { nextMode = VoiceMode.Deep; changed = true; }
            else if (Input.GetKeyDown(KeyCode.Alpha5)) { nextMode = VoiceMode.Echo; changed = true; }
            else if (Input.GetKeyDown(KeyCode.Alpha6)) { nextMode = VoiceMode.Muted; changed = true; }

            if (changed)
            {
                RPC_SetVoiceMode(nextMode);
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetVoiceMode(VoiceMode mode)
    {
        CurrentMode = mode;
        Debug.Log($"[VoiceEffect] Mode updated to: {mode}");
    }

    public override void FixedUpdateNetwork()
    {
        cachedMode = CurrentMode;
    }

    public float[] Process(float[] data)
    {
        if (data == null || data.Length == 0) return data;
        if (cachedMode == VoiceMode.Normal) return data;
        if (cachedMode == VoiceMode.Muted) return null;

        switch (cachedMode)
        {
            case VoiceMode.Squirrel: return ProcessResamplingF(data, 1.5f);
            case VoiceMode.Robot: return ProcessRobotF(data);
            case VoiceMode.Deep: return ProcessResamplingF(data, 0.7f);
            case VoiceMode.Echo: return ProcessEchoF(data);
            case VoiceMode.Underwater: return ProcessUnderwaterF(data);
            default: return data;
        }
    }

    public short[] Process(short[] data)
    {
        if (data == null || data.Length == 0) return data;
        if (cachedMode == VoiceMode.Normal) return data;
        if (cachedMode == VoiceMode.Muted) return null;

        switch (cachedMode)
        {
            case VoiceMode.Squirrel: return ProcessResamplingS(data, 1.5f);
            case VoiceMode.Robot: return ProcessRobotS(data);
            case VoiceMode.Deep: return ProcessResamplingS(data, 0.7f);
            case VoiceMode.Echo: return ProcessEchoS(data);
            case VoiceMode.Underwater: return ProcessUnderwaterS(data);
            default: return data;
        }
    }

    private unsafe float[] ProcessRobotF(float[] data)
    {
        float step = 2.0f * (float)Math.PI * RobotFrequency / sampleRate;
        fixed (float* pData = data)
        {
            float* ptr = pData;
            for (int i = 0; i < data.Length; i++)
            {
                *ptr *= (float)Math.Sin(robotPhase);
                robotPhase += step;
                if (robotPhase > 2.0f * (float)Math.PI) robotPhase -= 2.0f * (float)Math.PI;
                ptr++;
            }
        }
        return data;
    }

    private unsafe short[] ProcessRobotS(short[] data)
    {
        float step = 2.0f * (float)Math.PI * RobotFrequency / sampleRate;
        fixed (short* pData = data)
        {
            short* ptr = pData;
            for (int i = 0; i < data.Length; i++)
            {
                *ptr = (short)(*ptr * (float)Math.Sin(robotPhase));
                robotPhase += step;
                if (robotPhase > 2.0f * (float)Math.PI) robotPhase -= 2.0f * (float)Math.PI;
                ptr++;
            }
        }
        return data;
    }

    private unsafe float[] ProcessEchoF(float[] data)
    {
        if (echoBufferF == null) echoBufferF = new float[(int)(sampleRate * EchoDelay)];
        fixed (float* pData = data, pEcho = echoBufferF)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float delayed = pEcho[echoIndex];
                float output = pData[i] + delayed * EchoFeedback;
                pData[i] = output;
                pEcho[echoIndex] = output;
                echoIndex = (echoIndex + 1) % echoBufferF.Length;
            }
        }
        return data;
    }

    private unsafe short[] ProcessEchoS(short[] data)
    {
        if (echoBufferS == null) echoBufferS = new short[(int)(sampleRate * EchoDelay)];
        fixed (short* pData = data, pEcho = echoBufferS)
        {
            for (int i = 0; i < data.Length; i++)
            {
                short delayed = pEcho[echoIndex];
                short output = (short)(pData[i] + (short)(delayed * EchoFeedback));
                pData[i] = output;
                pEcho[echoIndex] = output;
                echoIndex = (echoIndex + 1) % echoBufferS.Length;
            }
        }
        return data;
    }

    private unsafe float[] ProcessResamplingF(float[] data, float pitch)
    {
        if (resampleBufferF == null) resampleBufferF = new float[ResampleBufferSize];
        fixed (float* pData = data, pBuf = resampleBufferF)
        {
            for (int i = 0; i < data.Length; i++) { pBuf[resampleWriteIndex] = pData[i]; resampleWriteIndex = (resampleWriteIndex + 1) % ResampleBufferSize; }
            for (int i = 0; i < data.Length; i++)
            {
                int i1 = (int)resampleReadIndex; int i2 = (i1 + 1) % ResampleBufferSize; float t = resampleReadIndex - i1;
                pData[i] = pBuf[i1] * (1.0f - t) + pBuf[i2] * t;
                resampleReadIndex += pitch; if (resampleReadIndex >= ResampleBufferSize) resampleReadIndex -= ResampleBufferSize;
            }
            float dist = (resampleWriteIndex - resampleReadIndex + ResampleBufferSize) % ResampleBufferSize;
            if (dist < data.Length || dist > ResampleBufferSize - data.Length) resampleReadIndex = (resampleWriteIndex - data.Length * 2 + ResampleBufferSize) % ResampleBufferSize;
        }
        return data;
    }

    private unsafe short[] ProcessResamplingS(short[] data, float pitch)
    {
        if (resampleBufferS == null) resampleBufferS = new short[ResampleBufferSize];
        fixed (short* pData = data, pBuf = resampleBufferS)
        {
            for (int i = 0; i < data.Length; i++) { pBuf[resampleWriteIndex] = pData[i]; resampleWriteIndex = (resampleWriteIndex + 1) % ResampleBufferSize; }
            for (int i = 0; i < data.Length; i++)
            {
                int i1 = (int)resampleReadIndex; int i2 = (i1 + 1) % ResampleBufferSize; float t = resampleReadIndex - i1;
                pData[i] = (short)(pBuf[i1] * (1.0f - t) + pBuf[i2] * t);
                resampleReadIndex += pitch; if (resampleReadIndex >= ResampleBufferSize) resampleReadIndex -= ResampleBufferSize;
            }
            float dist = (resampleWriteIndex - resampleReadIndex + ResampleBufferSize) % ResampleBufferSize;
            if (dist < data.Length || dist > ResampleBufferSize - data.Length) resampleReadIndex = (resampleWriteIndex - data.Length * 2 + ResampleBufferSize) % ResampleBufferSize;
        }
        return data;
    }

    // ==================== Underwater Effect ====================
    // Combines: pitch-down resampling → one-pole low-pass filter → subtle amplitude wobble.
    // The result sounds muffled like speaking inside a fishbowl/underwater, but remains intelligible.

    private unsafe float[] ProcessUnderwaterF(float[] data)
    {
        // Step 1: Pitch-down resampling (using separate buffers to not conflict with Squirrel/Deep)
        if (uwResampleBufferF == null) uwResampleBufferF = new float[ResampleBufferSize];
        fixed (float* pData = data, pBuf = uwResampleBufferF)
        {
            for (int i = 0; i < data.Length; i++)
            {
                pBuf[uwResampleWriteIndex] = pData[i];
                uwResampleWriteIndex = (uwResampleWriteIndex + 1) % ResampleBufferSize;
            }
            for (int i = 0; i < data.Length; i++)
            {
                int i1 = (int)uwResampleReadIndex;
                int i2 = (i1 + 1) % ResampleBufferSize;
                float t = uwResampleReadIndex - i1;
                pData[i] = pBuf[i1] * (1.0f - t) + pBuf[i2] * t;
                uwResampleReadIndex += UnderwaterPitch;
                if (uwResampleReadIndex >= ResampleBufferSize) uwResampleReadIndex -= ResampleBufferSize;
            }
            float dist = (uwResampleWriteIndex - uwResampleReadIndex + ResampleBufferSize) % ResampleBufferSize;
            if (dist < data.Length || dist > ResampleBufferSize - data.Length)
                uwResampleReadIndex = (uwResampleWriteIndex - data.Length * 2 + ResampleBufferSize) % ResampleBufferSize;
        }

        // Step 2: One-pole low-pass filter + amplitude wobble
        float dt = 1.0f / sampleRate;
        float rc = 1.0f / (2.0f * (float)Math.PI * UnderwaterCutoff);
        float alpha = dt / (rc + dt);
        float wobbleStep = 2.0f * (float)Math.PI * UnderwaterWobbleFreq / sampleRate;

        fixed (float* pData = data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                // Low-pass filter
                underwaterLpStateF += alpha * (pData[i] - underwaterLpStateF);
                // Subtle amplitude wobble (1.0 ± depth)
                float wobble = 1.0f - UnderwaterWobbleDepth + UnderwaterWobbleDepth * (float)Math.Sin(underwaterWobblePhase);
                pData[i] = underwaterLpStateF * wobble;
                underwaterWobblePhase += wobbleStep;
                if (underwaterWobblePhase > 2.0f * (float)Math.PI) underwaterWobblePhase -= 2.0f * (float)Math.PI;
            }
        }
        return data;
    }

    private unsafe short[] ProcessUnderwaterS(short[] data)
    {
        // Step 1: Pitch-down resampling
        if (uwResampleBufferS == null) uwResampleBufferS = new short[ResampleBufferSize];
        fixed (short* pData = data, pBuf = uwResampleBufferS)
        {
            for (int i = 0; i < data.Length; i++)
            {
                pBuf[uwResampleWriteIndex] = pData[i];
                uwResampleWriteIndex = (uwResampleWriteIndex + 1) % ResampleBufferSize;
            }
            for (int i = 0; i < data.Length; i++)
            {
                int i1 = (int)uwResampleReadIndex;
                int i2 = (i1 + 1) % ResampleBufferSize;
                float t = uwResampleReadIndex - i1;
                pData[i] = (short)(pBuf[i1] * (1.0f - t) + pBuf[i2] * t);
                uwResampleReadIndex += UnderwaterPitch;
                if (uwResampleReadIndex >= ResampleBufferSize) uwResampleReadIndex -= ResampleBufferSize;
            }
            float dist = (uwResampleWriteIndex - uwResampleReadIndex + ResampleBufferSize) % ResampleBufferSize;
            if (dist < data.Length || dist > ResampleBufferSize - data.Length)
                uwResampleReadIndex = (uwResampleWriteIndex - data.Length * 2 + ResampleBufferSize) % ResampleBufferSize;
        }

        // Step 2: One-pole low-pass filter + amplitude wobble
        float dt = 1.0f / sampleRate;
        float rc = 1.0f / (2.0f * (float)Math.PI * UnderwaterCutoff);
        float alpha = dt / (rc + dt);
        float wobbleStep = 2.0f * (float)Math.PI * UnderwaterWobbleFreq / sampleRate;

        fixed (short* pData = data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                // Low-pass filter (work in float precision)
                underwaterLpStateS += alpha * (pData[i] - underwaterLpStateS);
                // Subtle amplitude wobble
                float wobble = 1.0f - UnderwaterWobbleDepth + UnderwaterWobbleDepth * (float)Math.Sin(underwaterWobblePhase);
                pData[i] = (short)(underwaterLpStateS * wobble);
                underwaterWobblePhase += wobbleStep;
                if (underwaterWobblePhase > 2.0f * (float)Math.PI) underwaterWobblePhase -= 2.0f * (float)Math.PI;
            }
        }
        return data;
    }

    public void Dispose() { }
}
