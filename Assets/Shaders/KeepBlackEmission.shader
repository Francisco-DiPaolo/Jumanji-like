Shader "Custom/KeepBlackEmission"
{
    Properties
    {
        [MainTexture] _BaseMap        ("Sprite Texture",   2D)            = "white" {}
        [MainColor]   _BaseColor      ("Tint Color",       Color)         = (1,1,1,1)
        _Threshold                    ("White Threshold",  Range(0,1))    = 0.9
        _Softness                     ("Edge Softness",    Range(0.001,1))= 0.15

        [HDR] _EmissionColor          ("Emission Color",   Color)         = (0,0,0,1)
        _EmissionIntensity            ("Emission Intensity", Range(0,10)) = 1.0
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
                half4  _BaseColor;
                half   _Threshold;
                half   _Softness;
                half4  _EmissionColor;
                half   _EmissionIntensity;
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

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // Luminancia — negro = 0, colores brillantes = 1
                half lum = dot(tex.rgb, half3(0.2126h, 0.7152h, 0.0722h));

                // INVERTIDO: lo oscuro es opaco, lo claro desaparece
                half alpha = 1.0h - smoothstep(_Threshold - _Softness, _Threshold, lum);
                alpha *= tex.a;

                // Color base con tint
                half3 color = tex.rgb * _BaseColor.rgb;

                // Emisión — basada en luminancia invertida (brilla en zonas oscuras)
                half invertedLum = 1.0h - lum;
                half3 emission = _EmissionColor.rgb * _EmissionIntensity * invertedLum;
                color += emission;

                return half4(color, alpha * _BaseColor.a);
            }

            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
