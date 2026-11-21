// Humo negro con volumen real que sobresale de la entrada de cada pasillo (va delante del plano opaco de Slappy/HumoNegro).
// Se pinta en un cubo: el rayo de la camara atraviesa el cubo y acumula humo (ruido 3D en espacio de mundo).
// El eje +Z local del cubo apunta hacia afuera (hacia la estacion): pegado al plano opaco el humo es denso
// y hacia afuera solo quedan volutas sueltas, asi de lado se ven "cosas de sombra" que salen del hueco.
Shader "Slappy/HumoVolumen"
{
    Properties
    {
        _Color ("Color del humo", Color) = (0.003, 0.003, 0.004, 1)
        _ColorClaro ("Color de las volutas claras", Color) = (0.05, 0.05, 0.055, 1)
        _Densidad ("Densidad (por metro)", Range(0, 30)) = 10
        _Escala ("Tamano de las volutas (1/m)", Float) = 0.9
        _Velocidad ("Deriva (x, y, z) m/s", Vector) = (0, 0.5, 0, 0)
        _Remolino ("Remolino (deformacion)", Range(0, 2)) = 0.8
        _Alcance ("Que tanto salen las volutas (0-1)", Range(0, 1)) = 0.75
        _Suelo ("Extra de humo arrastrandose por el piso", Range(0, 1)) = 0.4
        _Pasos ("Pasos del rayo", Range(8, 48)) = 24
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+101" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "HumoVolumen"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Front // caras de atras: se sigue viendo con la camara dentro del cubo

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _ColorClaro;
                float _Densidad, _Escala, _Remolino, _Alcance, _Suelo, _Pasos;
                float4 _Velocidad;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.positionOS = i.positionOS.xyz;
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
                [unroll] for (int k = 0; k < 4; k++) { v += a * ruido(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            // Densidad en un punto: o = coordenadas locales del cubo (-0.5..0.5), w = mundo
            float densidad(float3 o, float3 w, float t, out float n)
            {
                float3 p = w * _Escala - _Velocidad.xyz * _Escala * t;
                // Remolino barato: una octava de ruido deforma las coordenadas
                p += (float3(ruido(p * 0.5 + t * 0.2), ruido(p * 0.5 + 9.3 - t * 0.2), ruido(p * 0.5 + 4.1)) - 0.5) * _Remolino * 2.0;
                n = fbm(p);
                float u = o.z + 0.5;                          // 0 pegado al plano opaco, 1 en la punta de las volutas
                float bajo = saturate(1.0 - (o.y + 0.5) * 3.0); // cerca del piso el humo se arrastra mas lejos
                float alcance = saturate(_Alcance + bajo * _Suelo);
                float umbral = lerp(0.3, 0.78, saturate(u / max(alcance, 0.05)));
                float d = saturate((n - umbral) * 5.0);
                // Lados y techo del cubo se deshacen (sin cortes rectos)
                float lado = smoothstep(0.0, 0.06, 0.5 - abs(o.x)) * smoothstep(0.0, 0.08, 0.5 - o.y);
                return d * lado;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float t = _Time.y;
                // Rayo en espacio del objeto: de la camara al punto de la cara frontal
                float3 cam = TransformWorldToObject(GetCameraPositionWS());
                float3 dir = i.positionOS - cam;
                float3 inv = 1.0 / (abs(dir) > 1e-5 ? dir : 1e-5);
                float3 t0 = (-0.5 - cam) * inv, t1 = (0.5 - cam) * inv;
                float3 tmin = min(t0, t1), tmax = max(t0, t1);
                float ta = max(max(max(tmin.x, tmin.y), tmin.z), 0.0);
                float tb = min(min(tmax.x, tmax.y), tmax.z);
                if (tb <= ta) discard;

                float3 oa = cam + dir * ta, ob = cam + dir * tb;
                float3 wa = TransformObjectToWorld(oa), wb = TransformObjectToWorld(ob);
                int pasos = (int)_Pasos;
                float ds = length(wb - wa) / pasos;
                // Desfase por pixel para que no se vean rodajas
                float jit = hash(float3(i.positionCS.xy, frac(t) * 7.0));

                float T = 1.0;
                half3 col = 0;
                [loop] for (int k = 0; k < pasos; k++)
                {
                    float f = (k + jit) / pasos;
                    float3 o = lerp(oa, ob, f);
                    float3 w = lerp(wa, wb, f);
                    float n;
                    float d = densidad(o, w, t, n);
                    if (d > 0.001)
                    {
                        float a = 1.0 - exp(-d * _Densidad * ds);
                        col += T * a * lerp(_Color.rgb, _ColorClaro.rgb, n * n);
                        T *= 1.0 - a;
                        if (T < 0.01) break;
                    }
                }
                return half4(col, 1.0 - T);
            }
            ENDHLSL
        }
    }
}
