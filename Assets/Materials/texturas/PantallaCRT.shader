// Pantalla CRT para un plano: muestra una textura con mascara de subpixeles RGB, lineas de barrido,
// curvatura y vineta, y cada cierto tiempo una ondulacion que recorre la imagen como en los televisores viejos.
Shader "Custom/PantallaCRT"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _MainTex("Textura del monitor", 2D) = "white" {}
        _Brillo("Brillo", Range(0, 5)) = 1.6
        [Header(Pixeles RGB)]
        _Resolucion("Resolucion (pixeles en X, Y)", Vector) = (320, 240, 0, 0)
        _Mascara("Intensidad de la mascara RGB", Range(0, 1)) = 0.8
        _Lineas("Intensidad de las lineas de barrido", Range(0, 1)) = 0.5
        [Header(Ondulacion)]
        _Intervalo("Segundos entre ondulaciones", Float) = 4
        _Duracion("Duracion de cada ondulacion", Float) = 1.2
        _Amplitud("Amplitud de la ondulacion", Range(0, 0.1)) = 0.02
        _Frecuencia("Frecuencia de la ondulacion", Float) = 30
        _Banda("Alto de la banda que baja", Range(0.05, 1)) = 0.3
        _Temblor("Temblor constante", Range(0, 0.01)) = 0.0015
        _Aberracion("Separacion de color en la onda", Range(0, 0.02)) = 0.004
        [Header(Tubo)]
        _Curvatura("Curvatura de la pantalla", Range(0, 0.5)) = 0
        _Vineta("Vineta", Range(0, 2)) = 0.6
        _Parpadeo("Parpadeo", Range(0, 0.3)) = 0.04
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half _Brillo;
                float4 _Resolucion;
                half _Mascara, _Lineas;
                float _Intervalo, _Duracion, _Amplitud, _Frecuencia, _Banda, _Temblor, _Aberracion;
                half _Curvatura, _Vineta, _Parpadeo;
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

            float Hash(float n) { return frac(sin(n) * 43758.5453); }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;

                // curvatura de tubo: estira las esquinas hacia afuera
                float2 c = i.uv * 2 - 1;
                c *= 1 + _Curvatura * dot(c, c) * float2(0.25, 0.35);
                float2 uv = c * 0.5 + 0.5;
                if (any(uv < 0) || any(uv > 1)) return half4(0, 0, 0, 1);

                // ondulacion: cada _Intervalo segundos una banda baja por la pantalla y tuerce las lineas
                float ciclo = fmod(t, max(_Intervalo, 0.01));
                float avance = saturate(ciclo / max(_Duracion, 0.01));
                float activa = step(ciclo, _Duracion);
                float centro = 1 + _Banda - avance * (1 + 2 * _Banda);   // de arriba hacia abajo
                float dist = (uv.y - centro) / _Banda;
                float env = activa * exp(-dist * dist * 2) * sin(avance * PI);
                float onda = sin(uv.y * _Frecuencia + t * 25) * _Amplitud * env;
                float linea = floor(uv.y * _Resolucion.y);
                float temblor = (Hash(linea + floor(t * 30)) - 0.5) * _Temblor;
                uv.x += onda + temblor;

                // pixelado a la resolucion del monitor
                float2 celda = uv * _Resolucion.xy;
                // la imagen ocupa el plano completo; la onda no hace que se repita por el otro lado
                float2 uvPix = saturate((floor(celda) + 0.5) / _Resolucion.xy);

                // separacion de color mas fuerte mientras pasa la onda
                float ab = _Aberracion * (0.25 + env * 2);
                half3 col;
                col.r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uvPix + float2(ab, 0))).r;
                col.g = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvPix).g;
                col.b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uvPix - float2(ab, 0))).b;

                // mascara de subpixeles: cada pixel se parte en tres columnas R, G y B
                float sub = frac(celda.x) * 3;
                half3 mascara = half3(saturate(1 - abs(sub - 0.5) * 1.6),
                                      saturate(1 - abs(sub - 1.5) * 1.6),
                                      saturate(1 - abs(sub - 2.5) * 1.6));
                mascara = lerp(half3(1, 1, 1), mascara * 2.2, _Mascara);
                col *= mascara;

                // lineas de barrido entre filas de pixeles y separacion entre pixeles
                float fy = frac(celda.y);
                col *= lerp(1, smoothstep(0, 0.35, fy) * smoothstep(1, 0.65, fy) * 1.6, _Lineas);

                // vineta, parpadeo y una franja clara cuando pasa la onda
                float vin = saturate(1 - dot(c, c) * 0.25 * _Vineta);
                col *= vin * (1 - _Parpadeo * Hash(floor(t * 60))) * _Brillo;
                col += env * 0.08;

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Unlit/DEPTHONLY"
    }
    FallBack Off
}
