Shader "Custom/StylizedSurface"
{
    Properties
    {
        [MainColor]   _BaseColor      ("Base Color",       Color)          = (1,1,1,1)
        [MainTexture] _BaseMap        ("Base Texture",     2D)             = "white" {}
        _ShadowColor                  ("Shadow Color",     Color)          = (0.15, 0.10, 0.20, 1)
        _ShadowSharpness              ("Shadow Sharpness", Range(0.01, 1)) = 0.4
        _ShadowStrength               ("Shadow Strength",  Range(0, 1))    = 0.5
        _LightFlattening              ("Light Flattening", Range(0, 1))    = 0.5
        _SaturationBoost              ("Saturation Boost", Range(1, 2))    = 1.2
        _AmbientBoost                 ("Ambient Boost",    Range(0, 2))    = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        // ─────────────────────────────────────────────────────────────
        // PASS 1 — Forward Lit
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _ShadowColor;
                half   _ShadowSharpness;
                half   _ShadowStrength;
                half   _LightFlattening;
                half   _SaturationBoost;
                half   _AmbientBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half   fogFactor   : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 AdjustSaturation(half3 color, half sat)
            {
                half lum = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                return lerp(half3(lum, lum, lum), color, sat);
            }

            half ShadowRamp(half NdotL, half sharpness)
            {
                half width = (1.0h - sharpness) * 0.49h + 0.001h;
                return smoothstep(0.5h - width, 0.5h + width, NdotL * 0.5h + 0.5h);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   norm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS  = pos.positionCS;
                OUT.positionWS  = pos.positionWS;
                OUT.normalWS    = norm.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.shadowCoord = GetShadowCoord(pos);
                OUT.fogFactor   = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Base color
                half4 tex       = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 baseColor = tex.rgb * _BaseColor.rgb;
                baseColor       = AdjustSaturation(baseColor, _SaturationBoost);

                // Lighting
                half3 normalWS  = normalize(IN.normalWS);
                Light mainLight = GetMainLight(IN.shadowCoord);
                half  NdotL     = dot(normalWS, normalize(mainLight.direction));
                half  ramp      = ShadowRamp(NdotL, _ShadowSharpness);
                half  flatRamp  = lerp(ramp, 1.0h, _LightFlattening);

                // Shadow attenuation — nunca negro puro
                half shadowAtten = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);

                // Color lit vs shadow (sombra va hacia color custom, no al negro)
                half3 litColor    = baseColor * mainLight.color;
                half3 shadowColor = baseColor * _ShadowColor.rgb;
                half3 diffuse     = lerp(shadowColor, litColor, flatRamp * shadowAtten);

                // Ambient llena las sombras con color de cielo
                half3 ambient   = SampleSH(normalWS) * _AmbientBoost;
                half3 final     = diffuse + ambient * (1.0h - flatRamp * 0.5h) * baseColor;

                final = MixFog(final, IN.fogFactor);
                return half4(final, tex.a * _BaseColor.a);
            }

            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // PASS 2 — Shadow Caster (self-contained, sin includes externos)
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _ShadowColor;
                half   _ShadowSharpness;
                half   _ShadowStrength;
                half   _LightFlattening;
                half   _SaturationBoost;
                half   _AmbientBoost;
            CBUFFER_END

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 GetShadowPositionHClip(ShadowAttributes input)
            {
                float3 posWS  = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif

                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, lightDir));

                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return posCS;
            }

            ShadowVaryings shadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = GetShadowPositionHClip(IN);
                return OUT;
            }

            half4 shadowFrag(ShadowVaryings IN) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────
        // PASS 3 — Depth Only (self-contained)
        // ─────────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex   depthVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _ShadowColor;
                half   _ShadowSharpness;
                half   _ShadowStrength;
                half   _LightFlattening;
                half   _SaturationBoost;
                half   _AmbientBoost;
            CBUFFER_END

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings depthVert(DepthAttributes IN)
            {
                DepthVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 depthFrag(DepthVaryings IN) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
