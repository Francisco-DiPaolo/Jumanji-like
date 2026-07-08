using System;
using Fusion;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;

public class VoiceEffectController : NetworkBehaviour, IProcessor<float>, IProcessor<short>
{
    public enum VoiceMode
    {
        Normal = 0,
        Squirrel = 1,
        Robot = 2,
        Deep = 3,
        Echo = 4,
        Muted = 5,
        Underwater = 6
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

    // ==================== Underwater Effect State ====================
    // Referencia acústica: bajo el agua el sonido pierde frecuencias altas bruscamente
    // (el agua absorbe >1kHz), rebota creando un eco corto denso, y las olas
    // modulan suavemente el volumen a ~2-3 Hz.
    //
    // Cadena de procesado:
    //   1. Low-pass AGRESIVO a ~500 Hz   → quita los altos, suena "tapado"
    //   2. Comb filter (delay ~40ms)      → rebote del agua, efecto "cámara"
    //   3. Tremolo lento 2.5 Hz           → movimiento del agua sobre el oído

    // Low-pass state (un estado por canal de audio, float y short path)
    private float uwLp1F, uwLp2F; // dos polos encadenados para pendiente más pronunciada
    private float uwLp1S, uwLp2S;

    // Comb filter (eco corto ~40 ms)
    private float[] uwCombF;
    private short[] uwCombS;
    private int    uwCombIdx;
    private const float UwCombDelaySec = 0.04f;   // 40 ms
    private const float UwCombFeedback = 0.45f;   // fuerza del rebote (0-1)

    // Tremolo lento
    private float uwTremoloPhase;
    private const float UwTremoloFreq  = 2.5f;    // Hz
    private const float UwTremoloDepth = 0.18f;   // profundidad (0 = sin efecto, 1 = corta el audio)

    // Parámetros del filtro LP (dos polos en serie = -40 dB/oct, ~500 Hz cutoff)
    private const float UwLpCutoff = 500f;         // Hz

    public override void Spawned()
    {
        if (HasInputAuthority)
        {
            recorder = GetComponent<Recorder>();
            if (recorder != null)
            {
                // Forzar opciones para eliminar ruido de fondo, teclado y respiración
                recorder.VoiceDetection = true;
                recorder.VoiceDetectionThreshold = 0.015f; // Ajustar si se corta mucho (más bajo = más sensible)
                
                // Activar WebRTC DSP para limpieza de audio profesional
                // En tu versión de Photon Voice, debes añadir el componente "WebRtcAudioDsp" manualmente 
                // al GameObject del Recorder desde el Inspector para cancelar el ruido de teclado.
                
                Debug.Log($"[VoiceEffect] Spawned. Recorder configured for VAD.");
            }
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

    private float[] GetOrCreateUwCombF()
    {
        if (uwCombF == null) uwCombF = new float[(int)(sampleRate * UwCombDelaySec)];
        return uwCombF;
    }

    private short[] GetOrCreateUwCombS()
    {
        if (uwCombS == null) uwCombS = new short[(int)(sampleRate * UwCombDelaySec)];
        return uwCombS;
    }

    private unsafe float[] ProcessUnderwaterF(float[] data)
    {
        float[] comb = GetOrCreateUwCombF();
        int combLen = comb.Length;

        // Pre-calcula coeficiente LP (dos polos en serie)
        float dt    = 1.0f / sampleRate;
        float rc    = 1.0f / (2.0f * (float)Math.PI * UwLpCutoff);
        float alpha = dt / (rc + dt);

        // Paso del tremolo por sample
        float tremStep = 2.0f * (float)Math.PI * UwTremoloFreq / sampleRate;

        fixed (float* pData = data, pComb = comb)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float x = pData[i];

                // 1. Filtro LP de dos polos (doble pasada = mayor pendiente)
                uwLp1F += alpha * (x      - uwLp1F);
                uwLp2F += alpha * (uwLp1F - uwLp2F);
                float filtered = uwLp2F;

                // 2. Comb filter (realimentación corta para efecto cámara de agua)
                float delayed   = pComb[uwCombIdx];
                float combOut   = filtered + delayed * UwCombFeedback;
                pComb[uwCombIdx] = combOut;
                uwCombIdx = (uwCombIdx + 1) % combLen;

                // 3. Tremolo lento (movimiento del agua)
                float tremolo = 1.0f - UwTremoloDepth + UwTremoloDepth * (float)Math.Sin(uwTremoloPhase);
                uwTremoloPhase += tremStep;
                if (uwTremoloPhase > 2.0f * (float)Math.PI) uwTremoloPhase -= 2.0f * (float)Math.PI;

                pData[i] = combOut * tremolo;
            }
        }
        return data;
    }

    private unsafe short[] ProcessUnderwaterS(short[] data)
    {
        short[] comb = GetOrCreateUwCombS();
        int combLen = comb.Length;

        float dt    = 1.0f / sampleRate;
        float rc    = 1.0f / (2.0f * (float)Math.PI * UwLpCutoff);
        float alpha = dt / (rc + dt);
        float tremStep = 2.0f * (float)Math.PI * UwTremoloFreq / sampleRate;

        fixed (short* pData = data, pComb = comb)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float x = pData[i];

                // 1. Filtro LP de dos polos
                uwLp1S += alpha * (x      - uwLp1S);
                uwLp2S += alpha * (uwLp1S - uwLp2S);
                float filtered = uwLp2S;

                // 2. Comb filter
                float delayed   = pComb[uwCombIdx];
                float combOut   = filtered + delayed * UwCombFeedback;
                pComb[uwCombIdx] = (short)Mathf.Clamp(combOut, short.MinValue, short.MaxValue);
                uwCombIdx = (uwCombIdx + 1) % combLen;

                // 3. Tremolo
                float tremolo = 1.0f - UwTremoloDepth + UwTremoloDepth * (float)Math.Sin(uwTremoloPhase);
                uwTremoloPhase += tremStep;
                if (uwTremoloPhase > 2.0f * (float)Math.PI) uwTremoloPhase -= 2.0f * (float)Math.PI;

                pData[i] = (short)Mathf.Clamp(combOut * tremolo, short.MinValue, short.MaxValue);
            }
        }
        return data;
    }

    public void Dispose() { }
}
