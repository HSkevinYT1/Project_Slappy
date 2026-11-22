// Superficie del portal de los Backrooms: muestra lo que ve la camara del otro lado (PortalSalida), tomada en
// coordenadas de pantalla, asi parece una ventana. En Resources para que Shader.Find la encuentre en la build.
Shader "Slappy/PortalVista"
{
    Properties
    {
        _MainTex ("Vista", 2D) = "black" {}
        _Borde ("Color del borde", Color) = (0.35, 0.8, 1.6, 1)
        _Aparecer ("Aparecer", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Vista"
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Borde;
                half _Aparecer;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screenPos : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uvPantalla = i.screenPos.xy / i.screenPos.w;
                half3 vista = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvPantalla).rgb;
                // Un brillo fino en el borde de la puerta (y todo brilla mientras se abre)
                float2 d = min(i.uv, 1.0 - i.uv);
                half borde = 1.0 - smoothstep(0.0, 0.06, min(d.x, d.y * 0.6));
                half abriendo = 1.0 - _Aparecer;
                return half4(lerp(vista, _Borde.rgb, saturate(borde * 0.85 + abriendo)), 1.0);
            }
            ENDHLSL
        }
    }
}
