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
    // Referencia acústica: el agua absorbe frecuencias altas a partir de ~1-2 kHz.
    // El efecto apunta a sonar "tapado" pero INTELIGIBLE: las consonantes (s,f,t)
    // deben seguir distinguiéndose. El comb filter es solo textura sutil, no eco.
    //
    // Cadena de procesado (calibrada):
    //   1. Low-pass a 1600 Hz   → quita brillo sin perder inteligibilidad (mín: 1000 Hz)
    //   2. Comb filter 40ms, feedback 0.12 → textura leve de "gárgara"
    //   3. Tremolo 2.5 Hz, depth 0.08  → ondulación muy suave del agua
    //
    // PROBLEMAS DEL SETUP ANTERIOR (500 Hz + comb 0.45 + tremolo 0.18):
    //   - 500 Hz eliminaba todas las consonantes fricativas → voz ininteligible
    //   - Comb feedback 0.45 → eco metálico perceptible y "cámara de eco"
    //   - Tremolo 0.18 → vibrato de amplitud artificial demasiado notorio

    // ---- Ciclo automático Underwater (variable pública para el Inspector) ----
    [Header("Underwater Auto-Cycle")]
    [Tooltip("Si > 0, activa Underwater durante UnderwaterOnDuration segundos, luego vuelve a Normal, en loop.")]
    public float UnderwaterCyclePeriod   = 0f;   // segundos entre ciclos (0 = desactivado)
    [Tooltip("Tiempo que permanece en modo Underwater por ciclo (seg).")]
    public float UnderwaterOnDuration    = 5f;

    private Coroutine _autoCycleCoroutine;

    // Low-pass state (un estado por canal de audio, float y short path)
    private float uwLp1F, uwLp2F; // dos polos encadenados para pendiente más pronunciada
    private float uwLp1S, uwLp2S;

    // Cutoff actual del LP (interpolado en tiempo real para transición suave)
    private float _uwCutoffCurrent  = 20000f;  // arranca en "sin filtro" (paso total)
    private float _uwCutoffTarget   = 20000f;
    private const float UwCutoffNormal    = 20000f; // Hz — sin filtro en modo Normal
    private const float UwCutoffUnderwater = 1600f; // Hz — cutoff objetivo underwater
    private const float UwCutoffTransitionSec = 0.6f; // segundos de transición

    // Comb filter (eco corto ~40 ms) — feedback reducido para textura, no eco
    private float[] uwCombF;
    private short[] uwCombS;
    private int    uwCombIdx;
    private const float UwCombDelaySec = 0.04f;   // 40 ms
    private const float UwCombFeedback = 0.12f;   // textura sutil (era 0.45 → eco agresivo)

    // Tremolo lento — depth reducido para que sea solo "movimiento de agua"
    private float uwTremoloPhase;
    private const float UwTremoloFreq  = 2.5f;    // Hz
    private const float UwTremoloDepth = 0.08f;   // muy suave (era 0.18 → vibrato artificial)

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

            // Iniciar ciclo automático si está configurado
            if (UnderwaterCyclePeriod > 0f)
            {
                _autoCycleCoroutine = StartCoroutine(AutoUnderwaterCycle());
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_autoCycleCoroutine != null) StopCoroutine(_autoCycleCoroutine);
    }

    // Ciclo automático: activa Underwater durante UnderwaterOnDuration seg,
    // luego vuelve a Normal, espera (UnderwaterCyclePeriod - UnderwaterOnDuration) seg, repite.
    private System.Collections.IEnumerator AutoUnderwaterCycle()
    {
        while (true)
        {
            // Activar underwater
            if (HasStateAuthority) CurrentMode = VoiceMode.Underwater;
            else RPC_SetVoiceMode(VoiceMode.Underwater);

            yield return new WaitForSeconds(UnderwaterOnDuration);

            // Volver a normal
            if (HasStateAuthority) CurrentMode = VoiceMode.Normal;
            else RPC_SetVoiceMode(VoiceMode.Normal);

            float waitTime = Mathf.Max(0.1f, UnderwaterCyclePeriod - UnderwaterOnDuration);
            yield return new WaitForSeconds(waitTime);
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

    // Interpola el cutoff del LP en Update para una transición suave entre estados.
    // Esto evita el corte abrupto de parámetros al activar/desactivar el efecto.
    private void Update()
    {
        float targetCutoff = (cachedMode == VoiceMode.Underwater) ? UwCutoffUnderwater : UwCutoffNormal;
        if (!Mathf.Approximately(_uwCutoffTarget, targetCutoff))
            _uwCutoffTarget = targetCutoff;

        float speed = Mathf.Abs(UwCutoffNormal - UwCutoffUnderwater) / UwCutoffTransitionSec;
        _uwCutoffCurrent = Mathf.MoveTowards(_uwCutoffCurrent, _uwCutoffTarget, speed * Time.deltaTime);
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

        // Usa _uwCutoffCurrent (interpolado en Update) para transición suave
        float cutoff = Mathf.Clamp(_uwCutoffCurrent, 1000f, 20000f);
        float dt    = 1.0f / sampleRate;
        float rc    = 1.0f / (2.0f * (float)Math.PI * cutoff);
        float alpha = dt / (rc + dt);

        float tremStep = 2.0f * (float)Math.PI * UwTremoloFreq / sampleRate;

        fixed (float* pData = data, pComb = comb)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float x = pData[i];

                // 1. LP dos polos — cutoff 1600 Hz preserva consonantes (s, f, t)
                uwLp1F += alpha * (x      - uwLp1F);
                uwLp2F += alpha * (uwLp1F - uwLp2F);
                float filtered = uwLp2F;

                // 2. Comb filter — solo textura, feedback bajo (0.12) para no crear eco
                float delayed   = pComb[uwCombIdx];
                float combOut   = filtered + delayed * UwCombFeedback;
                pComb[uwCombIdx] = combOut;
                uwCombIdx = (uwCombIdx + 1) % combLen;

                // 3. Tremolo muy suave — depth 0.08, apenas ondulación de agua
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

        float cutoff = Mathf.Clamp(_uwCutoffCurrent, 1000f, 20000f);
        float dt    = 1.0f / sampleRate;
        float rc    = 1.0f / (2.0f * (float)Math.PI * cutoff);
        float alpha = dt / (rc + dt);
        float tremStep = 2.0f * (float)Math.PI * UwTremoloFreq / sampleRate;

        fixed (short* pData = data, pComb = comb)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float x = pData[i];

                // 1. LP dos polos — cutoff 1600 Hz
                uwLp1S += alpha * (x      - uwLp1S);
                uwLp2S += alpha * (uwLp1S - uwLp2S);
                float filtered = uwLp2S;

                // 2. Comb filter — textura sutil, no eco
                float delayed   = pComb[uwCombIdx];
                float combOut   = filtered + delayed * UwCombFeedback;
                pComb[uwCombIdx] = (short)Mathf.Clamp(combOut, short.MinValue, short.MaxValue);
                uwCombIdx = (uwCombIdx + 1) % combLen;

                // 3. Tremolo suave
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
