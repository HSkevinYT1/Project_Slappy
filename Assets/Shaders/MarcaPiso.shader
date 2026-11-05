// Marca brillante pintada en el piso (caminos de colores de la Zona Principal).
// La usa MarcaCamino.cs, que genera una cinta (ribbon) con:
//   uv.x = distancia en metros a lo largo del camino (la onda viaja hacia afuera)
//   uv.y = 0..1 a lo ancho de la linea (bordes suaves)
Shader "Custom/MarcaPiso"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.294, 0, 0.51, 1)
        _Intensidad ("Intensidad del brillo", Range(0, 20)) = 4
        _Encendido ("Encendido (lo pone el script)", Range(0, 1)) = 1
        _AlphaApagado ("Visibilidad apagada", Range(0, 1)) = 0.12
        _MinimoOnda ("Brillo minimo de la onda", Range(0, 1)) = 0.35
        _Velocidad ("Velocidad de la onda", Range(0, 10)) = 3
        _Frecuencia ("Frecuencia de la onda (por metro)", Range(0, 5)) = 0.8
        _Borde ("Suavidad del borde", Range(0.01, 0.5)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "MarcaPiso"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Intensidad;
            half _Encendido;
            half _AlphaApagado;
            half _MinimoOnda;
            float _Velocidad;
            float _Frecuencia;
            half _Borde;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fog : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Bordes suaves a lo ancho de la linea.
                half borde = smoothstep(0.0h, _Borde, i.uv.y) * smoothstep(1.0h, 1.0h - _Borde, i.uv.y);

                // Onda que viaja desde la cabina hacia la salida.
                half pulso = 0.5h + 0.5h * sin(_Time.y * _Velocidad - i.uv.x * _Frecuencia);
                half brillo = lerp(_MinimoOnda, 1.0h, pulso);

                half3 col = _Color.rgb * _Intensidad * lerp(0.3h, brillo, _Encendido);
                // El pulso tambien afecta la opacidad: asi funciona incluso con colores oscuros (negro).
                half alpha = borde * lerp(_AlphaApagado, lerp(0.55h, 1.0h, pulso), _Encendido);

                col = MixFog(col, i.fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
