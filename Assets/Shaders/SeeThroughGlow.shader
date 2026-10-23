// Contorno que se ve a traves de todo (paredes, otros objetos).
// No reemplaza el material del objeto: se AÑADE como segundo material en el
// MeshRenderer (Materials > +), asi el objeto se sigue viendo normal y solo
// se le dibuja un borde brillante alrededor.
Shader "Custom/SeeThroughOutline"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (0, 4, 4, 1)
        _OutlineWidth ("Outline Width (pixeles)", Range(1, 20)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        half4 _GlowColor;
        float _OutlineWidth;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        ENDHLSL

        // Pase 1: marca en el stencil la silueta del objeto (no dibuja color).
        // ZTest Always para que la marca exista aunque haya una pared delante.
        Pass
        {
            Name "OutlineMask"
            // URP dibuja SRPDefaultUnlit antes que UniversalForward: asi la marca va primero
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ColorMask 0
            ZWrite Off
            ZTest Always

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 vert(Attributes input) : SV_POSITION
            {
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half4 frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Pase 2: dibuja la silueta un poco mas grande, solo donde NO esta el objeto.
        // Asi queda unicamente el borde, visible a traves de todo.
        Pass
        {
            Name "OutlineGlow"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 1
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 vert(Attributes input) : SV_POSITION
            {
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // Empuja cada vertice hacia afuera en pantalla, siguiendo su normal.
                // Se multiplica por w para que el grosor sea el mismo en pixeles a cualquier distancia.
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                float2 offset = normalize(normalCS + 1e-5) * _OutlineWidth * 2.0 / _ScreenParams.xy;

                positionCS.xy += offset * positionCS.w;
                return positionCS;
            }

            half4 frag() : SV_Target
            {
                return _GlowColor;
            }
            ENDHLSL
        }
    }
}
