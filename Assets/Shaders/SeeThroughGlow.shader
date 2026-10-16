Shader "Custom/SeeThroughOutline"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (0, 4, 4, 1)
        _OutlinePower ("Outline Thickness", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "GlowOutline"

            ZWrite Off
            ZTest Always

            Blend SrcAlpha One

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)

            half4 _GlowColor;
            float _OutlinePower;

            CBUFFER_END


            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS =
                    TransformObjectToWorld(input.positionOS.xyz);

                output.positionHCS =
                    TransformWorldToHClip(positionWS);

                output.normalWS =
                    TransformObjectToWorldNormal(input.normalOS);

                output.viewDirWS =
                    GetWorldSpaceNormalizeViewDir(positionWS);

                return output;
            }


            half4 frag(Varyings input) : SV_Target
            {
                float3 normal =
                    normalize(input.normalWS);

                float3 viewDir =
                    normalize(input.viewDirWS);


                float fresnel =
                    1.0 - saturate(
                        dot(normal, viewDir)
                    );


                fresnel =
                    pow(
                        fresnel,
                        _OutlinePower
                    );


                return half4(
                    _GlowColor.rgb * fresnel,
                    _GlowColor.a * fresnel
                );
            }

            ENDHLSL
        }
    }
}