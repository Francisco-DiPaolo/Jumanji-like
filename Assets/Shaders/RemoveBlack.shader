Shader "Custom/RemoveBlack"
{
    Properties
    {
        [MainTexture] _BaseMap   ("Sprite Texture", 2D)    = "white" {}
        [MainColor]   _BaseColor ("Tint Color",     Color) = (1,1,1,1)
        _Threshold               ("Black Threshold", Range(0,1)) = 0.1
        _Softness                ("Edge Softness",   Range(0.001,1)) = 0.15
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

                // Luminancia del pixel — cuanto mas negro, mas cerca de 0
                half lum = dot(tex.rgb, half3(0.2126h, 0.7152h, 0.0722h));

                // Suavizar el borde entre transparente y opaco
                half alpha = smoothstep(_Threshold, _Threshold + _Softness, lum);

                // Multiplicar por el alpha original del sprite si tiene
                alpha *= tex.a;

                return half4(tex.rgb * _BaseColor.rgb, alpha * _BaseColor.a);
            }

            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
