// Filtro de camara de seguridad (CCTV) para una imagen de UI (RawImage con la RenderTexture de la camara):
// casi blanco y negro con un tinte verdoso, pantalla un poco curvada, lineas de barrido, ruido, banda que
// sube despacio, separacion de colores en los bordes, vineta y "glitch" (cortes horizontales y nieve) al
// entrar y salir. Lo usa VistaSeguridad (la cinematica de las palancas de los Backrooms).
Shader "Slappy/CamaraSeguridad"
{
    Properties
    {
        [PerRendererData] _MainTex ("Imagen de la camara", 2D) = "white" {}
        _Color ("Tinte de la UI", Color) = (1, 1, 1, 1)
        _Tinte ("Tinte del monitor", Color) = (0.78, 1, 0.82, 1)
        _Desaturar ("Desaturar", Range(0, 1)) = 0.88
        _Contraste ("Contraste", Range(0.5, 2)) = 1.25
        _Ruido ("Ruido", Range(0, 0.5)) = 0.1
        _Lineas ("Lineas de barrido", Range(0, 1)) = 0.22
        _Vineta ("Vineta", Range(0, 2)) = 1.1
        _Curva ("Curvatura de la pantalla", Range(0, 0.5)) = 0.14
        _Aberracion ("Separacion de colores", Range(0, 0.02)) = 0.004
        _Glitch ("Glitch (0-1)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _Tinte;
            float _Desaturar, _Contraste, _Ruido, _Lineas, _Vineta, _Curva, _Aberracion, _Glitch;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float azar (float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;

                // Pantalla curvada (barril)
                float2 c = i.uv * 2.0 - 1.0;
                c *= 1.0 + _Curva * dot(c, c) * 0.25;
                float2 uv = c * 0.5 + 0.5;
                float dentro = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);

                // Glitch: franjas que se corren de lado
                float fila = floor(uv.y * 48.0);
                float corte = step(1.0 - _Glitch * 0.6, azar(float2(fila, floor(t * 24.0))));
                uv.x += (azar(float2(fila, floor(t * 31.0))) - 0.5) * 0.12 * _Glitch * (0.25 + corte);
                // Un temblor horizontal muy chico siempre (cinta vieja)
                uv.x += (azar(float2(floor(uv.y * 240.0), floor(t * 12.0))) - 0.5) * 0.0015;

                // Separacion de colores, mas en los bordes
                float ab = _Aberracion * (0.4 + dot(c, c)) + _Glitch * 0.01;
                float r = tex2D(_MainTex, uv + float2(ab, 0)).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - float2(ab, 0)).b;
                float3 col = float3(r, g, b);

                // Casi blanco y negro, con contraste y tinte de monitor
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(col, lum.xxx, _Desaturar);
                col = saturate((col - 0.5) * _Contraste + 0.5);
                col *= _Tinte.rgb;

                // Lineas de barrido y banda clara que sube despacio
                col *= 1.0 - _Lineas * (0.5 + 0.5 * sin(i.uv.y * 1100.0));
                float banda = 1.0 - smoothstep(0.0, 0.08, abs(frac(i.uv.y - t * 0.07) - 0.5));
                col += banda * 0.05;

                // Ruido (nieve) y parpadeo de brillo
                float n = azar(i.uv * float2(731.0, 487.0) + frac(t * 7.0) * 100.0) - 0.5;
                col += n * (_Ruido + _Glitch * 0.5);
                col *= 0.96 + 0.04 * sin(t * 37.0);

                // Vineta
                col *= saturate(1.0 - dot(c, c) * 0.32 * _Vineta);

                // Fuera de la pantalla curvada: negro
                col *= dentro;
                return fixed4(col, 1.0) * i.color;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}
