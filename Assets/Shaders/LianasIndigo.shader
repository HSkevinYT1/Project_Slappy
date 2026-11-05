// Lianas / venas indigo que recorren las paredes del laberinto.
// Lo usa LianasLaberinto.cs, que genera la malla y escribe en cada vertice (UV2):
//   x = distancia (en metros, por el laberinto) hasta la zona segura -> fase de la onda
//   y = estado anterior (0 apagada, 1 encendida)
//   z = estado nuevo
//   w = momento (en _TiempoLianas) en que empezo el cambio de estado
// Asi el encendido/apagado se anima en la GPU sin tocar la malla cada frame.
Shader "Custom/LianasIndigo"
{
    Properties
    {
        [HDR] _Color ("Color encendida (indigo)", Color) = (0.294, 0, 0.51, 1)
        _Intensidad ("Intensidad del brillo", Range(0, 20)) = 6
        _ColorApagado ("Color apagada", Color) = (0.035, 0.0, 0.07, 1)
        _MinimoOnda ("Brillo minimo de la onda (0-1)", Range(0, 1)) = 0.08
        _Velocidad ("Velocidad de la onda", Range(0, 10)) = 2.5
        _Frecuencia ("Frecuencia de la onda (por metro)", Range(0, 5)) = 0.6
        _DuracionFade ("Duracion del encendido (s)", Range(0.01, 5)) = 0.8
        _TiempoLianas ("Tiempo (lo pone el script)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        half _Intensidad;
        half4 _ColorApagado;
        half _MinimoOnda;
        float _Velocidad;
        float _Frecuencia;
        float _DuracionFade;
        float _TiempoLianas;
        CBUFFER_END

        ENDHLSL

        Pass
        {
            Name "Lianas"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 datos      : TEXCOORD1; // fase, estadoAnterior, estadoNuevo, tiempoCambio
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float2 onda       : TEXCOORD2; // x = encendido (0-1), y = pulso seno (0-1)
                float  fog        : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(posWS);

                // Transicion suave entre estado anterior y nuevo.
                float t = saturate((_TiempoLianas - v.datos.w) / _DuracionFade);
                t = t * t * (3.0 - 2.0 * t);
                float encendido = lerp(v.datos.y, v.datos.z, t);

                // Onda seno: la cresta avanza hacia la zona segura (fase = distancia a ella).
                float pulso = 0.5 + 0.5 * sin(_TiempoLianas * _Velocidad + v.datos.x * _Frecuencia);

                o.onda = float2(encendido, pulso);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Sombreado falso de "tubo" para que se vean redondas.
                half borde = saturate(dot(normalize(i.normalWS), normalize(i.viewDirWS)));
                half volumen = lerp(0.45h, 1.0h, borde);

                half brillo = lerp(_MinimoOnda, 1.0h, i.onda.y);
                half3 encendida = _Color.rgb * _Intensidad * brillo;
                half3 apagada = _ColorApagado.rgb;

                half3 col = lerp(apagada, encendida, i.onda.x) * volumen;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // Para que las lianas escriban en la textura de profundidad (niebla, efectos, etc).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            struct AttributesD
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsD
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsD vertDepth(AttributesD v)
            {
                VaryingsD o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half fragDepth(VaryingsD i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
