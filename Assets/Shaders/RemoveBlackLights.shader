Shader "Custom/RemoveBlackLights"
{
    Properties
    {
        [MainTexture] _BaseMap   ("Sprite Texture", 2D)    = "white" {}
        [MainColor]   _BaseColor ("Tint Color",     Color) = (1,1,1,1)
        _Threshold               ("Black Threshold", Range(0,1)) = 0.1
        _Softness                ("Edge Softness",   Range(0.001,1)) = 0.15
        _AmbientStrength         ("Ambient Strength", Range(0,2)) = 1.0
        _DiffuseStrength         ("Diffuse Strength", Range(0,2)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            // ── Fog ───────────────────────────────────────────────────────────
            #pragma multi_compile_fog

            // ── Sombras y luces adicionales URP ──────────────────────────────
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _Threshold;
                half   _Softness;
                half   _AmbientStrength;
                half   _DiffuseStrength;
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
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float  fogCoord    : TEXCOORD3;   // <-- fog
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);

                // Factor de fog calculado desde la profundidad clip-space
                OUT.fogCoord   = ComputeFogFactor(posInputs.positionCS.z);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // ── Remocion de negros ────────────────────────────────────────
                half lum   = dot(tex.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half alpha = smoothstep(_Threshold, _Threshold + _Softness, lum);
                alpha *= tex.a * _BaseColor.a;

                // ── Normal double-sided (Cull Off) ────────────────────────────
                float3 normalWS = normalize(IN.normalWS);

                // ── Luz ambiente (Spherical Harmonics) ────────────────────────
                half3 ambient = SampleSH(normalWS) * _AmbientStrength;

                // ── Luz principal con sombras ─────────────────────────────────
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                    Light mainLight    = GetMainLight(shadowCoord);
                #else
                    Light mainLight = GetMainLight();
                #endif

                // Double-sided: maximo de cara frontal y trasera
                half NdotL = max(
                    saturate(dot( normalWS, mainLight.direction)),
                    saturate(dot(-normalWS, mainLight.direction))
                );
                half3 mainContrib = mainLight.color * NdotL * mainLight.shadowAttenuation;

                // ── Luces adicionales ─────────────────────────────────────────
                half3 addLights = half3(0, 0, 0);
                #ifdef _ADDITIONAL_LIGHTS
                    uint count = GetAdditionalLightsCount();
                    for (uint i = 0u; i < count; ++i)
                    {
                        Light light = GetAdditionalLight(i, IN.positionWS);
                        half NdL = max(
                            saturate(dot( normalWS, light.direction)),
                            saturate(dot(-normalWS, light.direction))
                        );
                        addLights += light.color * NdL
                                   * light.distanceAttenuation
                                   * light.shadowAttenuation;
                    }
                #endif

                // ── Color final ───────────────────────────────────────────────
                half3 lighting = ambient + (mainContrib + addLights) * _DiffuseStrength;
                half3 rgb      = tex.rgb * _BaseColor.rgb * lighting;

                // ── Aplicar fog (mezcla rgb con el color de niebla de la escena)
                rgb = MixFog(rgb, IN.fogCoord);

                return half4(rgb, alpha);
            }

            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }

    FallBack "Universal Render Pipeline/Lit"
}
