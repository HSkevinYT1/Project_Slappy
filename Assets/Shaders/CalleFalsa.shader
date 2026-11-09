// Calle falsa detras de una ventana (idea de "interior mapping", como las ventanas de los edificios en Spider-Man).
// No hay geometria ni camaras: por cada pixel se lanza un rayo desde la camara a traves del vidrio y se calcula
// que veria en una calle hecha con matematicas (acera, calle con lineas, faroles, autos que pasan, fachadas con
// ventanas encendidas, edificios lejanos y cielo de noche). Por eso tiene paralaje real al moverse.
// Las dos ventanas comparten el mismo "origen" de calle, asi la calle sigue de una ventana a la otra.
Shader "Slappy/CalleFalsa"
{
    Properties
    {
        [Header(Calle (en metros desde el vidrio))]
        _Origen ("Origen (punto del vidrio a nivel del suelo de la calle)", Vector) = (0, 0, 0, 0)
        _Afuera ("Direccion hacia afuera (mundo)", Vector) = (0, 0, 1, 0)
        _Acera ("Ancho de la acera", Float) = 3
        _Calle ("Ancho de la calle", Float) = 10
        _AlturaAcera ("Alto del borde de la acera", Float) = 0.15
        _SepFaroles ("Separacion entre faroles", Float) = 18

        [Header(Ambiente)]
        _Faroles ("Intensidad de los faroles", Float) = 1
        _ColorFarol ("Color de los faroles", Color) = (1, 0.72, 0.42, 1)
        _Luna ("Luz de luna", Color) = (0.03, 0.035, 0.06, 1)
        _Niebla ("Niebla", Float) = 0.012
        _ColorNiebla ("Color de la niebla", Color) = (0.05, 0.05, 0.07, 1)
        _Autos ("Autos (0 = ninguno)", Range(0, 1)) = 1
        _Exposicion ("Exposicion", Float) = 1

        [Header(Vidrio)]
        _Tinte ("Tinte del vidrio", Color) = (0.86, 0.92, 0.95, 1)
        _Suciedad ("Suciedad del vidrio", Range(0, 1)) = 0.25
    }

    SubShader
    {
        // Transparente: desde adentro se dibuja la calle opaca (alfa 1); desde afuera el vidrio deja ver la Tienda.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CalleFalsa"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Origen, _Afuera;
                float _Acera, _Calle, _AlturaAcera, _SepFaroles;
                float _Faroles; float4 _ColorFarol, _Luna; float _Niebla; float4 _ColorNiebla;
                float _Autos, _Exposicion;
                float4 _Tinte; float _Suciedad;
            CBUFFER_END

            struct Atributos { float4 posOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; float2 uv : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Atributos i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.posWS = TransformObjectToWorld(i.posOS.xyz);
                o.posCS = TransformWorldToHClip(o.posWS);
                o.uv = i.uv;
                return o;
            }

            // ---------------------------------------------------------------- ruido
            float H1(float n) { return frac(sin(n * 127.1) * 43758.5453); }
            float H2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Ruido(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(H2(i), H2(i + float2(1, 0)), f.x), lerp(H2(i + float2(0, 1)), H2(i + 1), f.x), f.y);
            }

            // ---------------------------------------------------------------- escena
            // Coordenadas de la calle: x = a lo largo de la calle, y = alto sobre la calzada, z = distancia desde el vidrio.
            static const float AltoFarol = 5.0;

            float ZFarol(float lado) { return lado < 0.5 ? _Acera - 0.45 : _Acera + _Calle + 0.45; }
            float OffFarol(float lado) { return lado < 0.5 ? 4.0 : 4.0 + _SepFaroles * 0.5; }

            // Posicion de la lampara del farol k de un lado (cuelga un poco hacia la calle)
            float3 Lampara(float lado, float k)
            {
                float z = ZFarol(lado) + (lado < 0.5 ? 0.7 : -0.7);
                return float3(k * _SepFaroles + OffFarol(lado), AltoFarol - 0.2, z);
            }

            // Autos: uno por carril, cada uno pasa cada tanto
            float3 Auto(float carril, out float sentido, out float3 color)
            {
                sentido = carril < 0.5 ? 1.0 : -1.0;
                float periodo = carril < 0.5 ? 340.0 : 410.0;     // metros de recorrido por vuelta (casi siempre fuera de vista)
                float vel = carril < 0.5 ? 11.0 : 9.0;
                float x = (frac((_Time.y * vel + carril * 170.0) / periodo) - 0.5) * periodo * sentido;
                float z = _Acera + _Calle * (carril < 0.5 ? 0.3 : 0.7);
                float id = floor((_Time.y * vel + carril * 170.0) / periodo) + carril * 7.0;
                float3 paleta[4] = { float3(0.35, 0.04, 0.04), float3(0.08, 0.1, 0.16), float3(0.5, 0.5, 0.48), float3(0.05, 0.05, 0.05) };
                color = paleta[(int)(H1(id) * 3.99)];
                return float3(x, 0, z);
            }

            float3 LuzEn(float3 p, float3 n)
            {
                float3 luz = _Luna.rgb * (0.5 + 0.5 * saturate(n.y));
                for (int lado = 0; lado < 2; lado++)
                {
                    float k0 = round((p.x - OffFarol(lado)) / _SepFaroles);
                    for (int j = -1; j <= 1; j++)
                    {
                        float3 L = Lampara(lado, k0 + j) - p;
                        float d2 = dot(L, L);
                        float lambert = saturate(dot(n, L * rsqrt(d2))) * 0.8 + 0.2;
                        luz += _ColorFarol.rgb * _Faroles * lambert * 6.0 / (1.0 + d2 * 0.6);
                    }
                }
                // Faros de los autos sobre el suelo (mancha delante del auto)
                if (_Autos > 0.01)
                    for (int c = 0; c < 2; c++)
                    {
                        float s; float3 col;
                        float3 a = Auto(c, s, col);
                        float3 delante = a + float3(s * 6.0, 0, 0) - p;
                        luz += float3(1, 0.95, 0.85) * _Autos * 2.0 / (1.0 + dot(delante, delante) * 0.35);
                    }
                return luz;
            }

            // Interseccion rayo-caja (centro c, mitad h): devuelve t de entrada o -1
            float Caja(float3 o, float3 d, float3 c, float3 h, out float3 n)
            {
                float3 inv = 1.0 / d;
                float3 t1 = (c - h - o) * inv, t2 = (c + h - o) * inv;
                float3 tmin = min(t1, t2), tmax = max(t1, t2);
                float te = max(max(tmin.x, tmin.y), tmin.z), ts = min(min(tmax.x, tmax.y), tmax.z);
                n = -sign(d) * step(tmin.yzx, tmin.xyz) * step(tmin.zxy, tmin.xyz);
                return (ts > max(te, 0.0)) ? te : -1.0;
            }

            float3 Cielo(float3 d)
            {
                float alto = saturate(d.y);
                float3 c = lerp(float3(0.09, 0.075, 0.09), float3(0.008, 0.01, 0.025), pow(alto, 0.45)); // resplandor de la ciudad abajo
                float2 q = d.xz / max(d.y, 0.05) * 60.0;
                float est = step(0.996, H2(floor(q))) * saturate(alto * 3.0);
                return c + est * 0.6;
            }

            // Fachada de los edificios de enfrente
            float3 Fachada(float3 p, out float altoEdificio)
            {
                float ancho = 12.0;
                float b = floor(p.x / ancho);
                altoEdificio = 10.0 + floor(H1(b) * 5.0) * 3.0;
                float u = p.x - b * ancho;
                float3 paredes[4] = { float3(0.25, 0.11, 0.08), float3(0.22, 0.21, 0.2), float3(0.28, 0.24, 0.17), float3(0.12, 0.15, 0.14) };
                float3 pared = paredes[(int)(H1(b + 3.1) * 3.99)] * (0.8 + Ruido(p.xy * 3.0) * 0.3);
                // Ladrillo suave
                float fila = floor(p.y / 0.25);
                float lad = step(0.03, frac(p.y / 0.25)) * step(0.04, frac((p.x + fila * 0.25) / 0.5));
                pared *= lerp(0.8, 1.0, lad);
                float3 emis = 0;
                float3 n = float3(0, 0, -1);

                if (p.y < 4.0)
                {
                    // Planta baja: local con vidriera, puerta y letrero
                    float abierto = step(0.45, H1(b + 9.7));
                    bool vidriera = u > 1.0 && u < ancho - 1.0 && p.y > 0.6 && p.y < 3.0 && abs(u - ancho * 0.5) > 0.7;
                    bool letrero = u > 1.2 && u < ancho - 1.2 && p.y > 3.15 && p.y < 3.75;
                    if (vidriera)
                    {
                        float3 adentro = lerp(float3(1.0, 0.8, 0.55), float3(0.75, 0.9, 1.0), step(0.5, H1(b + 1.3)));
                        pared = float3(0.02, 0.02, 0.025);
                        emis = adentro * abierto * (0.6 + 0.4 * Ruido(float2(u * 2.0, p.y * 2.0))) * 0.75;
                    }
                    else if (letrero)
                    {
                        float3 colores[4] = { float3(1, 0.15, 0.2), float3(0.2, 0.7, 1), float3(1, 0.75, 0.2), float3(0.4, 1, 0.5) };
                        float3 col = colores[(int)(H1(b + 5.5) * 3.99)];
                        float parpadeo = H1(b) > 0.8 ? step(0.15, frac(_Time.y * 0.7 + H1(b) * 3.0)) : 1.0; // algun letrero falla
                        pared = col * 0.1;
                        emis = col * 1.4 * step(0.35, H1(b + 2.2)) * parpadeo;
                    }
                }
                else if (p.y < altoEdificio - 0.8)
                {
                    // Pisos de 3 m con ventanas cada 2.4 m
                    float pf = floor((p.y - 4.0) / 3.0), fy = frac((p.y - 4.0) / 3.0);
                    float col = floor((u - 0.6) / 2.4), fx = frac((u - 0.6) / 2.4);
                    if (u > 0.6 && u < ancho - 0.6 && fx > 0.22 && fx < 0.78 && fy > 0.25 && fy < 0.82)
                    {
                        float h = H2(float2(b * 13.0 + col, pf));
                        float encendida = step(0.66, h);
                        float3 tono = h > 0.93 ? float3(0.45, 0.6, 1.0) * (0.7 + 0.3 * sin(_Time.y * 9.0 + h * 50.0)) // tele
                                                : lerp(float3(1.0, 0.7, 0.4), float3(1.0, 0.88, 0.7), frac(h * 7.0));
                        float cortina = step(0.5, frac(h * 13.0)) * smoothstep(0.55, 0.6, fy);
                        pared = float3(0.015, 0.018, 0.025) + Cielo(reflect(float3(0, 0, 1), n)) * 0.4; // vidrio oscuro
                        emis = tono * encendida * (1.0 - cortina * 0.6) * (0.45 + 0.25 * frac(h * 31.0));
                        // marco
                        float marco = step(fx, 0.25) + step(0.75, fx) + step(fy, 0.28) + step(0.79, fy);
                        pared = lerp(pared, float3(0.35, 0.33, 0.3), saturate(marco));
                        emis *= 1.0 - saturate(marco);
                    }
                    // Cornisas entre pisos
                    if (fy < 0.04) pared *= 1.3;
                }
                else pared *= 1.2; // remate del techo
                return pared * LuzEn(p, n) + emis;
            }

            float3 Escena(float3 o, float3 d, out float tFin)
            {
                float A = _Acera, C = _Calle, hA = _AlturaAcera;
                float D = A + C + A;          // fachada de enfrente
                float tMejor = 1e6;
                float3 col = 0;

                // --- Suelo: aceras (un poco mas altas) y calzada
                if (d.y < 0)
                {
                    float t1 = (hA - o.y) / d.y;
                    float3 p = o + d * t1;
                    if ((p.z >= 0 && p.z <= A) || (p.z >= A + C && p.z <= D))
                    {
                        tMejor = t1;
                        float2 losa = frac(p.xz / 1.5);
                        float junta = step(0.03, losa.x) * step(0.03, losa.y);
                        float3 c = float3(0.22, 0.215, 0.21) * (0.8 + Ruido(p.xz * 4.0) * 0.3) * lerp(0.6, 1.0, junta);
                        c *= 1.0 - smoothstep(0.6, 0.75, Ruido(p.xz * 0.4)) * 0.35; // manchas
                        col = c * LuzEn(p, float3(0, 1, 0));
                    }
                    else
                    {
                        float t0 = (0 - o.y) / d.y;
                        p = o + d * t0;
                        if (p.z >= A && p.z <= A + C)
                        {
                            tMejor = t0;
                            float3 c = float3(0.075, 0.075, 0.08) * (0.75 + Ruido(p.xz * 9.0) * 0.4 + Ruido(p.xz * 0.6) * 0.2);
                            float zc = A + C * 0.5;
                            float linea = step(abs(p.z - zc), 0.07) * step(frac(p.x / 6.0), 0.5);       // linea central discontinua
                            float bordes = step(abs(p.z - (A + 0.3)), 0.06) + step(abs(p.z - (A + C - 0.3)), 0.06);
                            c = lerp(c, float3(0.75, 0.62, 0.25), linea);
                            c = lerp(c, float3(0.6, 0.6, 0.58), saturate(bordes));
                            float charco = smoothstep(0.74, 0.8, Ruido(p.xz * 0.5)) * smoothstep(0.4, 0.6, Ruido(p.xz * 2.3 + 7.0));
                            float3 luz = LuzEn(p, float3(0, 1, 0));
                            // Charcos: mas oscuros y con un brillo de la luz que tienen cerca
                            col = c * luz * (1.0 - charco * 0.6) + charco * (Cielo(reflect(d, float3(0, 1, 0))) * 0.25 + luz * 0.06 * pow(saturate(-d.y), 0.3));
                        }
                        else if (p.z > A + C)
                        {
                            // Borde (cordon) de la acera de enfrente
                            float tc = (A + C - o.z) / d.z;
                            float3 q = o + d * tc;
                            tMejor = tc;
                            col = float3(0.4, 0.4, 0.38) * LuzEn(q, float3(0, 0, -1));
                        }
                    }
                }

                // --- Fachada de enfrente
                {
                    float tf = (D - o.z) / d.z;
                    float3 p = o + d * tf;
                    float alto;
                    float3 c = Fachada(p, alto);
                    if (tf < tMejor && p.y < alto && p.y > 0) { tMejor = tf; col = c; }
                }

                // --- Edificios lejanos (siluetas con pocas luces)
                if (tMejor > 1e5)
                {
                    float D2 = D + 30.0;
                    float t2 = (D2 - o.z) / d.z;
                    float3 p = o + d * t2;
                    float b = floor(p.x / 17.0);
                    float alto = 22.0 + H1(b * 1.7) * 40.0;
                    if (p.y < alto)
                    {
                        tMejor = t2;
                        float2 cel = floor(float2(p.x / 1.6, p.y / 2.6));
                        float vent = step(0.86, H2(cel + b)) * step(0.3, frac(p.x / 1.6)) * step(0.3, frac(p.y / 2.6));
                        col = float3(0.012, 0.013, 0.02) + vent * float3(1.0, 0.78, 0.5) * 0.8;
                        col += (p.y > alto - 0.6 && H1(b) > 0.6) ? float3(1, 0.1, 0.1) * step(0.5, frac(_Time.y * 0.5)) * 2.0 : 0; // luz roja de antena
                    }
                    else { col = Cielo(d); tMejor = 400.0; }
                }

                // --- Autos (caja simple) que pasan por la calle
                if (_Autos > 0.01)
                    for (int c = 0; c < 2; c++)
                    {
                        float s; float3 colAuto;
                        float3 a = Auto(c, s, colAuto);
                        float3 nA, nB;
                        float tA = Caja(o, d, a + float3(0, 0.65, 0), float3(2.2, 0.45, 0.9), nA);
                        float tB = Caja(o, d, a + float3(-s * 0.3, 1.35, 0), float3(1.2, 0.3, 0.8), nB);
                        if (tB > 0 && (tA < 0 || tB < tA)) { tA = tB; nA = nB; colAuto = lerp(colAuto, float3(0.05, 0.06, 0.08), 0.6); }
                        if (tA > 0 && tA < tMejor)
                        {
                            float3 p = o + d * tA;
                            tMejor = tA;
                            col = colAuto * LuzEn(p, nA) + pow(saturate(dot(reflect(d, nA), float3(0, 1, 0))), 4.0) * 0.08;
                            // faros adelante y luces rojas atras
                            float frente = step(0.9, dot(nA, float3(s, 0, 0)));
                            float atras = step(0.9, dot(nA, float3(-s, 0, 0)));
                            float2 lp = float2(abs(p.z - a.z), p.y);
                            float foco = step(0.5, lp.x) * step(lp.x, 0.8) * step(0.6, lp.y) * step(lp.y, 0.85);
                            col += foco * (frente * float3(4, 3.8, 3.4) + atras * float3(3, 0.1, 0.08));
                        }
                        // brillo de los faros
                        float3 faro = a + float3(s * 2.25, 0.72, 0) - o;
                        float tc = dot(faro, d);
                        if (tc > 0 && tc < tMejor + 1.0)
                        {
                            float dist = length(faro - d * tc);
                            float mira = saturate(dot(normalize(-faro), float3(s, 0, 0)) * 0.5 + 0.5);
                            col += float3(1, 0.95, 0.85) * (exp(-dist * dist * 8.0) * 3.0 + exp(-dist * 1.2) * 0.25) * (0.3 + mira) * _Autos;
                        }
                    }

                // --- Postes de los faroles y el brillo de sus lamparas
                for (int lado = 0; lado < 2; lado++)
                {
                    float zl = ZFarol(lado);
                    float tl = (zl - o.z) / d.z;
                    if (tl > 0 && tl < tMejor)
                    {
                        float3 p = o + d * tl;
                        float k = round((p.x - OffFarol(lado)) / _SepFaroles);
                        float px = k * _SepFaroles + OffFarol(lado);
                        if (abs(p.x - px) < 0.07 + tl * 0.0008 && p.y > 0 && p.y < AltoFarol)
                        {
                            tMejor = tl;
                            col = float3(0.05, 0.05, 0.055) * LuzEn(p + float3(0, 0, -0.2), float3(0, 0, -1)) + 0.01;
                        }
                    }
                    float k0 = round(((o + d * max(tl, 0.0)).x - OffFarol(lado)) / _SepFaroles);
                    for (int j = -2; j <= 2; j++)
                    {
                        float3 L = Lampara(lado, k0 + j) - o;
                        float tc = dot(L, d);
                        if (tc <= 0 || tc > tMejor + 0.5) continue;
                        float dist = length(L - d * tc);
                        col += _ColorFarol.rgb * _Faroles * (exp(-dist * dist * 30.0) * 4.0 + exp(-dist * 0.9) * 0.12);
                    }
                }

                tFin = tMejor;
                return col;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 cam = GetCameraPositionWS();
                float3 n = normalize(_Afuera.xyz);
                float3 r = normalize(cross(float3(0, 1, 0), n));
                float3 rel = cam - _Origen.xyz;
                float3 o = float3(dot(rel, r), rel.y, dot(rel, n));
                float3 dw = normalize(i.posWS - cam);
                float3 d = float3(dot(dw, r), dw.y, dot(dw, n));

                // Visto desde afuera: vidrio casi transparente (se ve el interior de la Tienda),
                // con un reflejo suave del cielo, mas fuerte de costado, y la misma suciedad.
                if (d.z <= 0.001)
                {
                    float sucioA = Ruido(i.uv * float2(18, 9)) * Ruido(i.uv * float2(4, 40) + 3.0);
                    float fresA = pow(1.0 - saturate(abs(dot(dw, n))), 4.0);
                    float3 reflejo = Cielo(reflect(dw, -n)) * 1.5 + _ColorFarol.rgb * 0.02;
                    float alfa = saturate(0.08 + fresA * 0.6 + sucioA * _Suciedad * 0.25);
                    return half4(lerp(reflejo, float3(0.1, 0.1, 0.09), sucioA * _Suciedad), alfa);
                }

                // El rayo empieza en el vidrio
                float3 inicio = o + d * max(0.0, -o.z / d.z);
                float t;
                float3 col = Escena(inicio, d, t);
                col = lerp(col, _ColorNiebla.rgb, 1.0 - exp(-t * _Niebla));
                col *= _Exposicion;

                // Vidrio: tinte, un poco de suciedad y reflejo suave del interior (sin luz real)
                float sucio = Ruido(i.uv * float2(18, 9)) * Ruido(i.uv * float2(4, 40) + 3.0);
                col = col * _Tinte.rgb * (1.0 - sucio * _Suciedad * 0.6) + sucio * _Suciedad * 0.03;
                float fresnel = pow(1.0 - saturate(abs(dot(dw, n))), 4.0);
                col += fresnel * 0.05;
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vert(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; };
            V vert(float4 p : POSITION, float3 n : NORMAL) { V o; o.pos = TransformObjectToHClip(p.xyz); o.n = TransformObjectToWorldNormal(n); return o; }
            half4 frag(V i) : SV_Target { return half4(normalize(i.n), 0); }
            ENDHLSL
        }
    }
}
