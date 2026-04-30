// ---------------------------------------------------------------
//  CarvedStone_TMP_v2.shader
//  Unity 6 URP — compatible con TextMesh Pro
//  v2: desgaste interior multi-capa, no uniforme
// ---------------------------------------------------------------
Shader "Custom/CarvedStone_TMP_v2"
{
    Properties
    {
        // --- TMP obligatorias ---
        _FaceColor          ("Face Color",          Color)          = (0.18, 0.15, 0.12, 1)
        _FaceDilate         ("Face Dilate",         Range(-1,1))    = 0.0

        _OutlineColor       ("Outline Color",       Color)          = (0.06, 0.05, 0.04, 1)
        _OutlineWidth       ("Outline Width",       Range(0,1))     = 0.18
        _OutlineSoftness    ("Outline Softness",    Range(0,1))     = 0.0

        // --- Borde rugoso ---
        _NoiseScale         ("Edge Noise Scale",    Range(1,200))   = 85.0
        _NoiseStrength      ("Edge Roughness",      Range(0,0.3))   = 0.13
        _CrackDensity       ("Crack Density",       Range(0,1))     = 0.58

        // --- Sombra interior ---
        _InnerShadowColor   ("Inner Shadow Color",  Color)          = (0.02, 0.01, 0.01, 1)
        _InnerShadowWidth   ("Inner Shadow Width",  Range(0,1))     = 0.38
        _InnerShadowPower   ("Inner Shadow Depth",  Range(0.1,5))   = 2.8

        // --- Desgaste interior (multi-capa) ---
        _DustColor          ("Dust / Chalk Color",  Color)          = (0.82, 0.78, 0.70, 1)
        _ScrapColor         ("Scrape Streak Color", Color)          = (0.50, 0.44, 0.36, 1)
        _DustScaleCoarse    ("Dust Scale Coarse",   Range(1,300))   = 55.0
        _DustScaleFine      ("Dust Scale Fine",     Range(1,500))   = 180.0
        _DustStrength       ("Dust Strength",       Range(0,1))     = 0.60
        _ScrapStrength      ("Scrape Strength",     Range(0,1))     = 0.40
        _ScrapAnisotropy    ("Scrape Anisotropy",   Range(0.1,8))   = 3.5
        _WearContrast       ("Wear Contrast",       Range(0.5,6))   = 3.0

        // --- TMP internals ---
        [HideInInspector] _MainTex          ("Font Atlas",       2D)    = "white" {}
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
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "PreviewType"     = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        Pass
        {
            Name "CarvedStone_v2"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                half4  _DustColor;
                half4  _ScrapColor;
                float  _DustScaleCoarse;
                float  _DustScaleFine;
                half   _DustStrength;
                half   _ScrapStrength;
                half   _ScrapAnisotropy;
                half   _WearContrast;
                float  _NoiseScale;
                half   _NoiseStrength;
                half   _CrackDensity;
                float  _GradientScale;
                float  _ScaleX;
                float  _ScaleY;
                float  _PerspectiveFilter;
                float  _Sharpness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 worldXY    : TEXCOORD1;
                float4 color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---- Noise helpers ------------------------------------------

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash2(i            ).x;
                float b = Hash2(i + float2(1,0)).x;
                float c = Hash2(i + float2(0,1)).x;
                float d = Hash2(i + float2(1,1)).x;
                return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);
            }

            // FBM standard
            float FBM(float2 p)
            {
                float v = 0; float amp = 0.5;
                float2x2 R = float2x2(1.6,1.2,-1.2,1.6);
                for(int i=0;i<4;i++){ v+=amp*VNoise(p); p=mul(R,p); amp*=0.5; }
                return v;
            }

            // FBM anisotrópico — estirado en X para simular rasgaduras horizontales
            float FBM_Aniso(float2 p, float aniso)
            {
                p.x *= aniso;   // estira horizontalmente → rayas tipo cuchillazo
                return FBM(p);
            }

            // ---- Vertex -------------------------------------------------
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                // Posición en mundo para noise fijo en la pared
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldXY    = worldPos.xy;
                OUT.color      = IN.color;
                return OUT;
            }

            // ---- Fragment -----------------------------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                // SDF de TMP
                half d = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;

                // -- Ruido de borde (rugosidad del corte) --
                float2 np    = IN.worldXY * _NoiseScale;
                float  n1    = FBM(np) * 2.0 - 1.0;
                float  n2    = FBM(np * 1.9 + 5.3) * 2.0 - 1.0;
                float  rough = (n1 * 0.6 + n2 * 0.4) * _NoiseStrength;

                float  crack = step(_CrackDensity, FBM(np * 2.5 + 13.7)) * 0.05;
                float  sdf   = d + rough - crack;

                // -- Thresholds --
                float  dilate  = _FaceDilate * 0.5 + 0.5;
                float  outline = dilate - _OutlineWidth * 0.5;
                float  soft    = max(0.001, _OutlineSoftness);

                half faceAlpha    = smoothstep(dilate - soft, dilate + soft, sdf);
                half outlineAlpha = smoothstep(outline - soft, outline + soft, sdf);

                // -- Sombra interior --
                float innerEdge = dilate - _InnerShadowWidth * 0.5;
                half  innerT    = pow(saturate(1.0 - smoothstep(innerEdge, dilate, sdf)), _InnerShadowPower);

                // ============================================================
                // DESGASTE INTERIOR MULTI-CAPA
                // Capa 1: polvo/tiza grueso — manchas grandes irregulares
                float2 wc  = IN.worldXY * _DustScaleCoarse;
                float  dustCoarse = FBM(wc + 2.71);
                // Elevar contraste: zonas muy claras y muy oscuras, pocas medias
                dustCoarse = saturate(pow(dustCoarse, 1.0 / _WearContrast) * _WearContrast * 0.7);

                // Capa 2: rasgaduras finas anisotrópicas (tipo cuchillazo)
                float2 wf  = IN.worldXY * _DustScaleFine;
                float  scrap = FBM_Aniso(wf + 8.14, _ScrapAnisotropy);
                // Solo las rasgaduras más marcadas (threshold alto)
                scrap = saturate((scrap - 0.55) * 4.0);

                // Capa 3: variación aleatoria por zona (rompe la uniformidad)
                float  variation = VNoise(IN.worldXY * _DustScaleCoarse * 0.3 + 1.41);
                // Modula el polvo: algunas zonas tienen más, otras menos
                dustCoarse *= lerp(0.2, 1.0, variation);

                // Mezcla de capas de desgaste
                half dustMask  = saturate(dustCoarse * _DustStrength);
                half scrapMask = saturate(scrap      * _ScrapStrength);
                // ============================================================

                // -- Ensamble de color --
                half4 col = lerp(_OutlineColor, _FaceColor, faceAlpha);

                // Sombra interior (profundidad del corte)
                col.rgb = lerp(col.rgb, _InnerShadowColor.rgb, innerT * faceAlpha);

                // Polvo/tiza grueso
                col.rgb = lerp(col.rgb, _DustColor.rgb, dustMask * faceAlpha);

                // Rasgaduras finas encima (más delgadas, color tierra/roca)
                col.rgb = lerp(col.rgb, _ScrapColor.rgb, scrapMask * faceAlpha);

                col.rgb *= IN.color.rgb;
                col.a    = outlineAlpha * IN.color.a * _FaceColor.a;

                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
