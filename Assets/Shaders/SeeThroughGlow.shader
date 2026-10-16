Shader "Custom/SeeThroughGlow"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (0, 4, 4, 0.6)
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
            Name "Glow"

            ZWrite Off

            // Hace que el objeto se dibuje incluso detrás de paredes.
            ZTest Always

            // Mezcla el brillo con lo que hay detrás.
            Blend SrcAlpha One

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)

            half4 _GlowColor;

            CBUFFER_END


            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionHCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                return output;
            }


            half4 frag(Varyings input) : SV_Target
            {
                return _GlowColor;
            }

            ENDHLSL
        }
    }
}