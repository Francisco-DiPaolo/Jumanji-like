// ---------------------------------------------------------------
//  CarvedStone_TMP.shader
//  Unity 6 URP — compatible con TextMesh Pro (surface shader SDF)
//
//  Coloca este archivo en tu proyecto. En el material de TMP:
//    Shader → Custom/CarvedStone_TMP
//
//  El fondo es TRANSPARENTE: la pared de abajo se ve a través.
//  Las letras simulan un corte rugoso con:
//    · Borde irregular via noise desplazado
//    · Sombra interior (profundidad de talla)
//    · Face oscura/desgastada con variación de noise
// ---------------------------------------------------------------
Shader "Custom/CarvedStone_TMP"
{
    Properties
    {
        // --- TMP requiere estas propiedades obligatorias ---
        _FaceColor          ("Face Color",          Color)          = (0.18, 0.15, 0.12, 1)
        _FaceDilate         ("Face Dilate",         Range(-1,1))    = 0.0

        // Borde principal (el "corte")
        _OutlineColor       ("Outline Color",       Color)          = (0.08, 0.06, 0.05, 1)
        _OutlineWidth       ("Outline Width",       Range(0,1))     = 0.15
        _OutlineSoftness    ("Outline Softness",    Range(0,1))     = 0.0

        // --- Efecto tallado ---
        _NoiseScale         ("Noise Scale",         Range(1,200))   = 80.0
        _NoiseStrength      ("Edge Roughness",      Range(0,0.3))   = 0.12
        _CrackDensity       ("Crack Density",       Range(0,1))     = 0.55

        // Sombra interior (simula profundidad del tajo)
        _InnerShadowColor   ("Inner Shadow Color",  Color)          = (0.03, 0.02, 0.01, 1)
        _InnerShadowWidth   ("Inner Shadow Width",  Range(0,1))     = 0.35
        _InnerShadowPower   ("Inner Shadow Depth",  Range(0.1,5))   = 2.5

        // Desgaste en la cara del texto
        _WearColor          ("Wear/Dust Color",     Color)          = (0.55, 0.50, 0.44, 1)
        _WearStrength       ("Wear Strength",       Range(0,1))     = 0.45
        _WearScale          ("Wear Scale",          Range(1,300))   = 140.0

        // TMP internals (no tocar)
        [HideInInspector] _MainTex          ("Font Atlas",       2D) = "white" {}
        [HideInInspector] _TextureWidth     ("Texture Width",    Float) = 512
        [HideInInspector] _TextureHeight    ("Texture Height",   Float) = 512
        [HideInInspector] _GradientScale    ("Gradient Scale",   Float) = 5
        [HideInInspector] _ScaleX           ("Scale X",          Float) = 1
        [HideInInspector] _ScaleY           ("Scale Y",          Float) = 1
        [HideInInspector] _PerspectiveFilter("Perspective Corr", Range(0,1)) = 0.875
        [HideInInspector] _Sharpness        ("Sharpness",        Range(-1,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "IgnoreProjector"= "True"
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType"    = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        Pass
        {
            Name "CarvedStone"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ---- Samplers ------------------------------------------------
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _FaceColor;
                half   _FaceDilate;
                half4  _OutlineColor;
                half   _OutlineWidth;
                half   _OutlineSoftness;
                half4  _InnerShadowColor;
                half   _InnerShadowWidth;
                half   _InnerShadowPower;
                half4  _WearColor;
                half   _WearStrength;
                float  _WearScale;
                float  _NoiseScale;
                half   _NoiseStrength;
                half   _CrackDensity;
                float  _GradientScale;
                float  _ScaleX;
                float  _ScaleY;
                float  _PerspectiveFilter;
                float  _Sharpness;
            CBUFFER_END

            // ---- Structs -------------------------------------------------
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float2 worldPos     : TEXCOORD1;
                float4 color        : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---- Hash / Noise helpers ------------------------------------
            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Value noise — devuelve [0,1]
            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash2(i).x;
                float b = Hash2(i + float2(1,0)).x;
                float c = Hash2(i + float2(0,1)).x;
                float d = Hash2(i + float2(1,1)).x;

                return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);
            }

            // FBM — 4 octavas, da rugosidad tipo piedra
            float FBM(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                float2x2 rot = float2x2(1.6, 1.2, -1.2, 1.6);
                for (int i = 0; i < 4; i++)
                {
                    v   += amp * VNoise(p);
                    p    = mul(rot, p);
                    amp *= 0.5;
                }
                return v;
            }

            // ---- Vertex --------------------------------------------------
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.worldPos   = IN.positionOS.xy;
                OUT.color      = IN.color;
                return OUT;
            }

            // ---- Fragment ------------------------------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                // -- SDF base de TMP --
                half d = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;

                // Noise que desplaza el umbral del borde (rugosidad del corte)
                float2 np     = IN.worldPos * _NoiseScale;
                float  nEdge  = FBM(np) * 2.0 - 1.0;          // [-1, 1]
                float  nEdge2 = FBM(np * 1.7 + 5.3) * 2.0 - 1.0;
                float  roughness = (nEdge * 0.6 + nEdge2 * 0.4) * _NoiseStrength;

                // Grietas adicionales (ruido de alta frecuencia restado al SDF)
                float  crackNoise = FBM(np * 2.5 + 13.7);
                float  crack = step(_CrackDensity, crackNoise) * 0.05; // fragmentos que "faltan"

                // SDF efectivo con rugosidad aplicada
                float  sdf = d + roughness - crack;

                // -- Dilate / escala del outline --
                float  dilate   = _FaceDilate * 0.5 + 0.5;
                float  outline  = dilate - _OutlineWidth * 0.5;
                float  softness = max(0.001, _OutlineSoftness);

                // Alfa de la cara (interior del texto)
                half   faceAlpha    = smoothstep(dilate - softness, dilate + softness, sdf);
                // Alfa del borde (corte exterior)
                half   outlineAlpha = smoothstep(outline - softness, outline + softness, sdf);

                // -- Sombra interior (profundidad del tajo) --
                float  innerEdge    = dilate - _InnerShadowWidth * 0.5;
                half   innerT       = 1.0 - smoothstep(innerEdge, dilate, sdf);
                innerT              = pow(saturate(innerT), _InnerShadowPower);

                // -- Desgaste / polvo en la cara --
                float2 wp2    = IN.worldPos * _WearScale;
                float  wear   = FBM(wp2 + 3.1) * FBM(wp2 * 0.5 + 7.9);
                wear          = saturate(wear * 2.0);

                // -- Ensamble de colores --
                // Base: color del corte (outline = borde exterior rugoso)
                half4 col = lerp(_OutlineColor, _FaceColor, faceAlpha);

                // Sombra interior encima
                col.rgb = lerp(col.rgb, _InnerShadowColor.rgb, innerT * faceAlpha);

                // Desgaste encima de la cara
                col.rgb = lerp(col.rgb, _WearColor.rgb, wear * _WearStrength * faceAlpha);

                // Tint del vertice (TMP lo usa para gradientes de color)
                col.rgb *= IN.color.rgb;

                // Alpha final: borde rugoso opaco, fuera = transparente
                col.a = outlineAlpha * IN.color.a * _FaceColor.a;

                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
