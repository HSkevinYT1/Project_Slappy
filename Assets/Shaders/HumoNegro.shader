// Humo negro animado que tapa la vista (para las "sombra ..." de la entrada de cada pasillo).
// Ruido fbm en espacio de mundo (asi el humo mide lo mismo en cualquier pieza), con deformacion (remolino),
// deriva (sube / se mueve), dos capas que se cruzan (profundidad), bordes que se deshacen y brillos opcionales.
Shader "Slappy/HumoNegro"
{
    Properties
    {
        _Color ("Color del humo", Color) = (0.01, 0.01, 0.012, 1)
        _ColorClaro ("Color de las volutas claras", Color) = (0.09, 0.09, 0.1, 1)
        _Densidad ("Densidad (opacidad general)", Range(0, 2)) = 1.2
        _Nucleo ("Opacidad minima en el centro", Range(0, 1)) = 0.9
        _Escala ("Tamano de las volutas (1/m)", Float) = 0.6
        _Velocidad ("Deriva (x, y) m/s", Vector) = (0.1, 0.25, 0, 0)
        _Remolino ("Remolino (deformacion)", Range(0, 3)) = 1
        _VelRemolino ("Velocidad del remolino", Float) = 0.3
        _Estirar ("Estirar en vertical", Range(0.3, 3)) = 1
        _Capas ("Mezcla de la segunda capa", Range(0, 1)) = 0.5
        _Contraste ("Contraste de las volutas", Range(0.2, 4)) = 1.5
        _Bordes ("Borde que se deshace (0-0.5 de la pieza)", Range(0, 0.5)) = 0.12
        _Profundidad ("Variacion en profundidad (capas)", Range(0, 2)) = 0
        _Rasante ("Desvanecer al verse de canto", Range(0, 1)) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Escribir profundidad (capa de fondo)", Float) = 0
        _BrilloColor ("Color de los brillos", Color) = (0, 0, 0, 1)
        _Brillo ("Cantidad de brillos", Range(0, 3)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Humo"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _ColorClaro, _BrilloColor;
                float _Densidad, _Nucleo, _Escala, _Remolino, _VelRemolino, _Estirar, _Capas, _Contraste, _Bordes, _Brillo, _Profundidad, _Rasante;
                float4 _Velocidad;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; float3 normalWS : TEXCOORD2; float fog : TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = i.uv;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float ruido(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash(i), hash(i + float3(1,0,0)), f.x), lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x), lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }
            float fbm(float3 p)
            {
                float v = 0.0, a = 0.5;
                [unroll] for (int k = 0; k < 5; k++) { v += a * ruido(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                // Coordenadas sobre la cara: horizontal a lo largo de la pieza, vertical = altura
                float3 n = abs(normalize(i.normalWS));
                float h = n.x > n.z ? i.positionWS.z : i.positionWS.x;
                float2 q = float2(h, i.positionWS.y / _Estirar) * _Escala;
                q -= _Velocidad.xy * _Escala * t;
                // Profundidad: cada capa (a distinta distancia) ve otro corte del humo
                float prof = (n.x > n.z ? i.positionWS.x : i.positionWS.z) * _Escala * _Profundidad;

                // Remolino: el ruido deforma sus propias coordenadas
                float2 w = float2(fbm(float3(q, t * _VelRemolino + prof)), fbm(float3(q + 5.2, t * _VelRemolino + 1.3 + prof))) - 0.5;
                float2 q1 = q + w * _Remolino * 2.0;
                float a1 = fbm(float3(q1, t * 0.15 + prof));
                // Segunda capa mas grande que se mueve al reves (sensacion de profundidad)
                float2 q2 = q * 0.55 + float2(t * 0.07, -t * 0.05) * _Escala + w * _Remolino;
                float a2 = fbm(float3(q2 + 31.7, t * 0.1 + prof * 0.6));
                float d = lerp(a1, a2, _Capas * 0.5) + (a2 - 0.5) * _Capas * 0.5;
                d = saturate((d - 0.5) * _Contraste + 0.5);

                // Bordes de la pieza que se deshacen en volutas (solo las caras grandes; los cantos finos se ocultan)
                float2 uv = i.uv;
                float borde = 1.0;
                if (_Bordes > 0.001)
                {
                    float bx = min(uv.x, 1.0 - uv.x), by = min(uv.y, 1.0 - uv.y);
                    float m = min(bx, by);
                    borde = smoothstep(0.0, _Bordes, m - (d - 0.5) * _Bordes);
                }

                float alfa = saturate(max(d * _Densidad, _Nucleo)) * borde;
                // Las capas vistas de canto se desvanecen (asi no se ven como laminas)
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                alfa *= lerp(1.0, saturate(abs(dot(normalize(i.normalWS), v)) * 2.5), _Rasante);
                half3 col = lerp(_Color.rgb, _ColorClaro.rgb, pow(d, 2.0));
                // Brillos: venas finas donde el ruido cruza un valor
                float vena = 1.0 - saturate(abs(a1 - 0.52) * 25.0);
                col += _BrilloColor.rgb * vena * _Brillo * (0.6 + 0.4 * sin(t * 2.0 + a2 * 12.0));
                // Sin niebla: el humo se queda negro aunque la estacion tenga niebla gris
                return half4(col, alfa);
            }
            ENDHLSL
        }
    }
}
