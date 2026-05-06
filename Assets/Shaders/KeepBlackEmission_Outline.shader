Shader "Custom/KeepBlackEmission_Outline"
{
    Properties
    {
        [MainTexture] _BaseMap          ("Sprite Texture",    2D)             = "white" {}
        [MainColor]   _BaseColor        ("Tint Color",        Color)          = (1,1,1,1)
        _Threshold                      ("White Threshold",   Range(0,1))     = 0.9
        _Softness                       ("Edge Softness",     Range(0.001,1)) = 0.15

        [HDR] _EmissionColor            ("Emission Color",    Color)          = (0,0,0,1)
        _EmissionIntensity              ("Emission Intensity",Range(0,10))    = 1.0

        _OutlineColor                   ("Outline Color",     Color)          = (0,0,0,1)
        _OutlineWidth                   ("Outline Width (px)",Range(0,10))    = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseMap_TexelSize;  // (1/w, 1/h, w, h)
                half4  _BaseColor;
                half   _Threshold;
                half   _Softness;
                half4  _EmissionColor;
                half   _EmissionIntensity;
                half4  _OutlineColor;
                half   _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            // Devuelve el alpha "visible" de un sample segun la logica de este shader (invertida)
            half SampleAlpha(float2 uv)
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half lum = dot(t.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                return (1.0h - smoothstep(_Threshold - _Softness, _Threshold, lum)) * t.a;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex   = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half  lum   = dot(tex.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half  alpha = (1.0h - smoothstep(_Threshold - _Softness, _Threshold, lum)) * tex.a;

                // ---- Outline ------------------------------------------------
                float2 texel = _BaseMap_TexelSize.xy * _OutlineWidth;

                // 8 vecinos
                half maxNeighbour = 0;
                maxNeighbour = max(maxNeighbour, SampleAlpha(IN.uv + float2( texel.x,  0)));
                maxNeighbour = max(maxNeighbour, SampleAlpha(IN.uv + float2(-texel.x,  0)));
                maxNeighbour = max(maxNeighbour, SampleAlpha(IN.uv + float2( 0,  texel.y)));
                maxNeighbour = max(maxNeighbour, SampleAlpha(IN.uv + float2( 0, -texel.y)));
                maxNeighbour = max(maxNeighbour, SampleAlpha(IN.uv + float2( texel.x,  texel.y)));
                maxNeighbour = max(maxNeighbour, SampleAlpha(IN.uv + float2(-texel.x,  texel.y)));
                maxNeighbour = max(maxNeighbour, SampleAlpha(IN.uv + float2( texel.x, -texel.y)));
                maxNeighbour = max(maxNeighbour, SampleAlpha(IN.uv + float2(-texel.x, -texel.y)));

                // Es borde si algún vecino es visible pero este pixel no lo es (o casi)
                half outlineMask = maxNeighbour * (1.0h - alpha);
                // -------------------------------------------------------------

                // Color base + emision invertida
                half3 color       = tex.rgb * _BaseColor.rgb;
                half  invertedLum = 1.0h - lum;
                half3 emission    = _EmissionColor.rgb * _EmissionIntensity * invertedLum;
                color += emission;

                // Mezcla con color de outline
                half3 finalColor = lerp(color, _OutlineColor.rgb, outlineMask);
                half  finalAlpha = max(alpha, outlineMask) * _BaseColor.a;

                return half4(finalColor, finalAlpha);
            }

            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
