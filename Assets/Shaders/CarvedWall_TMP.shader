// ============================================================
//  CarvedWall_TMP.shader
//  Unity 6 · URP · TextMesh Pro Surface Shader (SDF)
//
//  Efecto: texto tallado / rasgado sobre concreto o piedra
//  ─ Sombra de grabado (inner shadow / ambient occlusion falsa)
//  ─ Borde irregular tipo corte de cuchillo (noise UV distortion)
//  ─ Textura de concreto procedural (fbm + Voronoi light)
//  ─ Specular anisotrópico en el fondo del corte
//  ─ Rim light lateral configurable (simula iluminación rasante)
//  ─ Soporte completo de SDF de TMP (outline, softness, dilate)
//
//  INSTRUCCIONES DE USO
//  ────────────────────
//  1. Copia este archivo en Assets/Shaders/
//  2. En el Material de tu TextMeshPro component cambia el Shader
//     a  "Custom/CarvedWall_TMP"
//  3. Asigna el Atlas SDF de TMP al slot "Main Texture (SDF Atlas)"
//  4. Ajusta los parámetros en el Inspector
//
//  NOTA: requiere URP 17+ (Unity 6).  Para Unity 2022 LTS / URP 14
//  cambia "#pragma target 4.5" por "#pragma target 3.5" y elimina
//  el bloque DOTS_INSTANCING_ON si tu proyecto no usa DOTS.
// ============================================================

Shader "Custom/CarvedWall_TMP"
{
    Properties
    {
        // ── TMP obligatorios ──────────────────────────────────
        _MainTex            ("Main Texture (SDF Atlas)",    2D)     = "white" {}
        _FaceColor          ("Face Color",                  Color)  = (1,1,1,1)
        _FaceDilate         ("Face Dilate",                 Range(-1,1)) = 0
        _OutlineColor       ("Outline Color",               Color)  = (0,0,0,1)
        _OutlineWidth       ("Outline Width",               Range(0,1)) = 0
        _OutlineSoftness    ("Outline Softness",            Range(0,1)) = 0
        _Softness           ("Edge Softness",               Range(0,1)) = 0.05

        // ── Concreto / Piedra (fondo) ─────────────────────────
        [Header(Wall Surface)]
        _WallColorA         ("Wall Color A (dark)",         Color)  = (0.22,0.20,0.18,1)
        _WallColorB         ("Wall Color B (light)",        Color)  = (0.38,0.34,0.30,1)
        _WallNoiseScale     ("Wall Noise Scale",            Float)  = 6.0
        _WallNoiseStrength  ("Wall Noise Strength",         Range(0,1)) = 0.55
        _WallRoughness      ("Wall Roughness",              Range(0,1)) = 0.82

        // ── Corte / Grabado ───────────────────────────────────
        [Header(Carved Cut)]
        _CutDepthColor      ("Cut Depth Color",             Color)  = (0.08,0.07,0.06,1)
        _CutEdgeColor       ("Cut Edge / Chip Color",       Color)  = (0.55,0.50,0.44,1)
        _CutDepth           ("Cut Depth (AO strength)",     Range(0,1)) = 0.72
        _CutEdgeWidth       ("Cut Edge Width",              Range(0,0.5)) = 0.12
        _CutEdgeSoftness    ("Cut Edge Softness",           Range(0.001,0.3)) = 0.06

        // ── Irregularidad / ruido del filo ────────────────────
        [Header(Knife Edge Noise)]
        _EdgeNoiseScale     ("Edge Noise Scale",            Float)  = 28.0
        _EdgeNoiseStrength  ("Edge Noise Strength",         Range(0,0.15)) = 0.045
        _EdgeNoiseLayers    ("Edge Noise Octaves (1-4)",    Range(1,4)) = 3

        // ── Specular del fondo del corte ─────────────────────
        [Header(Cut Specular)]
        _SpecularColor      ("Specular Color",              Color)  = (0.9,0.85,0.75,1)
        _SpecularStrength   ("Specular Strength",           Range(0,1)) = 0.35
        _SpecularSharpness  ("Specular Sharpness",          Range(1,128)) = 32

        // ── Iluminación rasante (rim) ─────────────────────────
        [Header(Rim Light)]
        _RimColor           ("Rim Light Color",             Color)  = (1.0,0.92,0.78,1)
        _RimStrength        ("Rim Strength",                Range(0,1)) = 0.28
        _RimDirection       ("Rim Direction XY",            Vector) = (0.6,0.8,0,0)

        // ── Polvo / suciedad en el corte ──────────────────────
        [Header(Dust and Grime)]
        _GrimeColor         ("Grime Color",                 Color)  = (0.12,0.10,0.08,1)
        _GrimeStrength      ("Grime Strength",              Range(0,1)) = 0.40
        _GrimeNoiseScale    ("Grime Noise Scale",           Float)  = 14.0

        // ── Mezcla global ─────────────────────────────────────
        [Header(Blend)]
        _Cutoff             ("Alpha Cutoff",                Range(0,1)) = 0.01
        [Enum(UnityEngine.Rendering.BlendMode)]
        _SrcBlend           ("Src Blend",  Float) = 5   // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)]
        _DstBlend           ("Dst Blend",  Float) = 10  // OneMinusSrcAlpha
        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull               ("Cull",       Float) = 2   // Back
        [Toggle] _ZWrite    ("ZWrite",     Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend   [_SrcBlend] [_DstBlend]
        Cull    [_Cull]
        ZWrite  [_ZWrite]
        ZTest   LEqual

        Pass
        {
            Name "CarvedWallTMP"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   4.5

            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ─── Declaraciones de propiedades ────────────────────────────
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4  _MainTex_ST;

                // TMP
                float4  _FaceColor;
                float   _FaceDilate;
                float4  _OutlineColor;
                float   _OutlineWidth;
                float   _OutlineSoftness;
                float   _Softness;

                // Wall
                float4  _WallColorA;
                float4  _WallColorB;
                float   _WallNoiseScale;
                float   _WallNoiseStrength;
                float   _WallRoughness;

                // Cut
                float4  _CutDepthColor;
                float4  _CutEdgeColor;
                float   _CutDepth;
                float   _CutEdgeWidth;
                float   _CutEdgeSoftness;

                // Edge noise
                float   _EdgeNoiseScale;
                float   _EdgeNoiseStrength;
                float   _EdgeNoiseLayers;

                // Specular
                float4  _SpecularColor;
                float   _SpecularStrength;
                float   _SpecularSharpness;

                // Rim
                float4  _RimColor;
                float   _RimStrength;
                float4  _RimDirection;

                // Grime
                float4  _GrimeColor;
                float   _GrimeStrength;
                float   _GrimeNoiseScale;

                // Blend
                float   _Cutoff;
            CBUFFER_END

            // ─── Estructuras ──────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float2 uvRaw        : TEXCOORD1;   // UV sin transformar (para noise)
                float4 color        : COLOR;
                float3 worldPos     : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ─── Utilidades de ruido ──────────────────────────────────────

            // Hash 2D → float
            float hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            // Value noise suave 2D
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // fBm (fractal Brownian Motion) — hasta 4 octavas
            float fbm(float2 p, int octaves)
            {
                float val    = 0.0;
                float amp    = 0.5;
                float freq   = 1.0;
                float total  = 0.0;
                for (int o = 0; o < 4; o++)
                {
                    if (o >= octaves) break;
                    val   += valueNoise(p * freq) * amp;
                    total += amp;
                    amp   *= 0.5;
                    freq  *= 2.1;
                }
                return val / total;
            }

            // Voronoi 2D simple (para grietas de concreto)
            float voronoi(float2 p)
            {
                float2 i  = floor(p);
                float2 f  = frac(p);
                float minD = 1.0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 neighbor = float2(x, y);
                    float2 point    = float2(hash21(i + neighbor), hash21(i + neighbor + 0.37));
                    point = 0.5 + 0.5 * sin(6.2831 * point);
                    float2 diff     = neighbor + point - f;
                    minD = min(minD, dot(diff, diff));
                }
                return sqrt(minD);
            }

            // ─── Vertex ───────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.uvRaw       = IN.uv;
                OUT.color       = IN.color;
                UNITY_TRANSFER_FOG(OUT, OUT.positionHCS);
                return OUT;
            }

            // ─── Fragment ─────────────────────────────────────────────────
            float4 frag(Varyings IN) : SV_Target
            {
                // ── 1. Muestrear atlas SDF de TMP ────────────────────────
                float sdf = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;

                // Aplicar dilate (desplazamiento del umbral)
                float dilate = _FaceDilate * 0.5;
                float bias   = 0.5 - dilate;

                // ── 2. Distorsión del borde (irregularidad tipo cuchillo) ─
                //    Desplazamos las UV antes de evaluar umbrales
                int   noiseOct  = (int)clamp(_EdgeNoiseLayers, 1, 4);
                float edgeNoise = fbm(IN.uvRaw * _EdgeNoiseScale, noiseOct);
                edgeNoise       = (edgeNoise - 0.5) * 2.0; // remap −1..1

                // Perturbamos el SDF sumando el ruido (en espacio SDF ~0.5 es el borde)
                float sdfDistorted = sdf + edgeNoise * _EdgeNoiseStrength;

                // ── 3. Máscara de cara (interior del glifo) ───────────────
                float softness = max(_Softness, 0.001);
                float faceMask = smoothstep(bias - softness, bias + softness, sdfDistorted);

                // ── 4. Outline ─────────────────────────────────────────────
                float outBias  = bias - _OutlineWidth * 0.5;
                float outSoft  = max(_OutlineSoftness, 0.001);
                float outMask  = smoothstep(outBias - outSoft, outBias + outSoft, sdfDistorted);
                outMask        = outMask - faceMask;
                outMask        = saturate(outMask);

                // ── 5. Textura de pared (concreto procedural) ────────────
                float2 worldUV = IN.worldPos.xy * _WallNoiseScale;
                float  wallFbm = fbm(worldUV, 3);
                float  wallVor = voronoi(worldUV * 0.7);
                float  wallN   = wallFbm * 0.7 + (1.0 - wallVor) * 0.3;
                wallN          = lerp(0.5, wallN, _WallNoiseStrength);
                float4 wallCol = lerp(_WallColorA, _WallColorB, wallN);

                // ── 6. Color del corte (grabado profundo) ─────────────────
                //    El interior del glifo es el corte; modelamos AO falsa:
                //    - Centro del corte → color más oscuro (_CutDepthColor)
                //    - Borde del corte  → color más claro/despostillado (_CutEdgeColor)

                // Distancia al borde interior del SDF (0 en borde, 1 en centro)
                float distToEdge = smoothstep(
                    bias - _CutEdgeWidth - _CutEdgeSoftness,
                    bias - _CutEdgeWidth + _CutEdgeSoftness,
                    sdfDistorted);

                // AO: cuanto más al centro, más sombra
                float ao = lerp(1.0, 1.0 - _CutDepth, distToEdge);

                // Ruido de polvo/suciedad dentro del corte
                float grimeN = fbm(IN.worldPos.xy * _GrimeNoiseScale, 2);
                float4 grimeContrib = lerp(float4(0,0,0,0), _GrimeColor, grimeN * _GrimeStrength * faceMask);

                float4 edgeChip  = lerp(_CutEdgeColor, _CutDepthColor, distToEdge);
                float4 cutColor  = edgeChip * ao + grimeContrib;

                // ── 7. Iluminación del corte ──────────────────────────────
                // Calculamos una normal aproximada a partir del gradiente del SDF
                // (derivadas de pantalla → normal en espacio de pantalla)
                float2 ddx_sdf = float2(ddx(sdfDistorted), ddy(sdfDistorted));
                float3 cutNormal = normalize(float3(ddx_sdf * _CutDepth, 0.4));

                // Luz principal (primera luz direccional URP)
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);

                float NdotL   = saturate(dot(cutNormal, lightDir));
                float4 diffuse = float4(mainLight.color, 1) * NdotL * 0.5;

                // Specular Blinn-Phong en el fondo del corte
                float3 viewDir  = normalize(_WorldSpaceCameraPos - IN.worldPos);
                float3 halfDir  = normalize(lightDir + viewDir);
                float  NdotH    = saturate(dot(cutNormal, halfDir));
                float  specular = pow(NdotH, _SpecularSharpness) * _SpecularStrength;
                float4 specCol  = _SpecularColor * specular * faceMask;

                // Rim light (iluminación rasante lateral)
                float2 rimDir2D = normalize(_RimDirection.xy);
                float  rimDot   = saturate(dot(rimDir2D, ddx_sdf * 4.0));
                float4 rimCol   = _RimColor * rimDot * _RimStrength * faceMask;

                // ── 8. Composición final ──────────────────────────────────
                // a) Fondo de pared (visible donde NO hay texto ni outline)
                float4 bgColor   = wallCol;

                // b) Color del corte con iluminación
                float4 finalCut  = cutColor + diffuse * 0.3 + specCol + rimCol;
                finalCut.a       = 1.0;

                // c) Color de outline (borde astillado más claro)
                float4 finalOut  = lerp(_OutlineColor, _CutEdgeColor, 0.6);

                // d) Mezclar: fondo → outline → corte
                float4 col       = bgColor;
                col              = lerp(col, finalOut, outMask * _OutlineColor.a);
                col              = lerp(col, finalCut, faceMask);

                // Tinte de vertex color de TMP (respeta colores por carácter)
                col.rgb         *= IN.color.rgb;

                // Alpha total: la malla de TMP puede tener alpha < 1 en los bordes
                float totalAlpha = max(faceMask, outMask);
                col.a            = totalAlpha * IN.color.a;

                // Clip invisible
                clip(col.a - _Cutoff);

                // Fog
                UNITY_APPLY_FOG(IN.fogCoord, col);

                return col;
            }
            ENDHLSL
        }

        // ── Shadow caster pass (recibe sombras correctamente) ──────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag
            #pragma target   4.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _FaceDilate;
                float  _Softness;
                float  _OutlineWidth;
                float  _Cutoff;
                // (resto ignorado en shadow pass)
                float4  _FaceColor, _OutlineColor, _WallColorA, _WallColorB;
                float   _WallNoiseScale, _WallNoiseStrength, _WallRoughness;
                float4  _CutDepthColor, _CutEdgeColor;
                float   _CutDepth, _CutEdgeWidth, _CutEdgeSoftness;
                float   _EdgeNoiseScale, _EdgeNoiseStrength, _EdgeNoiseLayers;
                float4  _SpecularColor;
                float   _SpecularStrength, _SpecularSharpness;
                float4  _RimColor;
                float   _RimStrength;
                float4  _RimDirection;
                float4  _GrimeColor;
                float   _GrimeStrength, _GrimeNoiseScale;
                float   _OutlineSoftness;
            CBUFFER_END

            struct Attributes { float4 pos : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings   { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings shadowVert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.pos = TransformObjectToHClip(IN.pos.xyz);
                OUT.uv  = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            float4 shadowFrag(Varyings IN) : SV_Target
            {
                float sdf   = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                float bias  = 0.5 - _FaceDilate * 0.5;
                float soft  = max(_Softness, 0.001);
                float alpha = smoothstep(bias - soft, bias + soft, sdf);
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    // Fallback para cuando URP no está disponible
    FallBack "Hidden/InternalErrorShader"

    // Editor GUI personalizado (opcional, comentar si no se usa)
    // CustomEditor "CarvedWallTMPShaderGUI"
}
