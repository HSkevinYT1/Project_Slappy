// Fede (madera vieja de muneco de ventrilocuo roto; antes hueso): textura triplanar sobre la POSE DE REPOSO del modelo.
// El modelo de Fede no tiene UVs desplegadas (son las de las primitivas de Blender, todas encimadas), asi que la
// textura se proyecta en 3D. Para que no "resbale" al animarse se usa la posicion/normal de reposo de cada vertice
// (TEXCOORD2 / TEXCOORD3), que ModelosAnimadosSetup guarda en una copia de la malla (normalizada: 1 = alto del
// cuerpo, y = arriba). El relieve sale de la altura (alfa de la textura) con derivadas de pantalla, asi funciona
// con la malla deformada por los huesos sin necesitar tangentes.
Shader "Slappy/Fede Hueso (triplanar)"
{
    Properties
    {
        [MainTexture] _BaseMap ("Textura (RGB color, A altura)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tinte", Color) = (1, 1, 1, 1)
        _Escala ("Repeticiones por alto del cuerpo", Float) = 5
        _Nitidez ("Nitidez de la mezcla triplanar", Range(1, 16)) = 6
        _Relieve ("Relieve (1 = 3 mm)", Range(0, 4)) = 1.2
        _Suavidad ("Brillo (smoothness)", Range(0, 1)) = 0.38
        _ColorSuciedad ("Color de la suciedad", Color) = (0.2, 0.15, 0.1, 1)
        _Suciedad ("Suciedad en las piernas", Range(0, 1)) = 0.6
        _AlturaSuciedad ("Hasta donde sube la suciedad (0 pies, 1 cabeza)", Range(0.01, 1)) = 0.35
        _Oclusion ("Oscurecer grietas y poros", Range(0, 1)) = 0.6
        [Header(Ojos (las cuencas del craneo brillan))]
        [HDR] _OjoColor ("Color de los ojos", Color) = (0.68, 0.1, 2.6, 1)
        _OjoIzq ("Ojo izquierdo (reposo normalizado)", Vector) = (0, 0, 0, 0)
        _OjoDer ("Ojo derecho (reposo normalizado)", Vector) = (0, 0, 0, 0)
        _OjoRadio ("Radio de los ojos (xyz, w = 1 encendido)", Vector) = (0.0225, 0.0158, 0.013, 0)
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

        // Igual en todas las pasadas (SRP Batcher). _BaseMap_ST/_BaseColor/_Cutoff los usan las pasadas de URP.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            float _Escala;
            float _Nitidez;
            float _Relieve;
            half _Suavidad;
            half4 _ColorSuciedad;
            half _Suciedad;
            half _AlturaSuciedad;
            half _Oclusion;
            half4 _OjoColor;
            float4 _OjoIzq;
            float4 _OjoDer;
            float4 _OjoRadio;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 reposo : TEXCOORD2;        // posicion de reposo (normalizada)
                float3 normalReposo : TEXCOORD3;  // normal de reposo
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 reposo : TEXCOORD2;
                float3 normalReposo : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3 luzVertice : TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(v.normalOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = nrm.normalWS;
                o.reposo = v.reposo;
                o.normalReposo = v.normalReposo;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                o.luzVertice = VertexLighting(pos.positionWS, nrm.normalWS);
                #endif
                return o;
            }

            half4 Triplanar(float3 p, float3 n)
            {
                float3 w = pow(abs(n) + 1e-4, _Nitidez);
                w /= (w.x + w.y + w.z);
                // Cada plano desplazado: que las tres proyecciones no repitan el mismo dibujo
                half4 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.zy);
                half4 b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xz + float2(0.37, 0.11));
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, p.xy + float2(0.71, 0.53));
                return a * w.x + b * w.y + c * w.z;
            }

            // Relieve sin tangentes (Mikkelsen, "Bump Mapping Unparametrized Surfaces on the GPU")
            float3 Relieve(float3 posWS, float3 nWS, float altura)
            {
                float3 dpdx = ddx(posWS);
                float3 dpdy = ddy(posWS);
                float3 r1 = cross(dpdy, nWS);
                float3 r2 = cross(nWS, dpdx);
                float det = dot(dpdx, r1);
                if (abs(det) < 1e-12) return nWS;
                float3 grad = sign(det) * (ddx(altura) * r1 + ddy(altura) * r2);
                return normalize(abs(det) * nWS - grad);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 nr = i.normalReposo;
                nr = dot(nr, nr) > 1e-6 ? normalize(nr) : float3(0, 1, 0);
                half4 t = Triplanar(i.reposo * _Escala, nr);
                half h = t.a;

                // Suciedad: sube desde los pies y se mete mas en las grietas
                half pies = 1.0h - smoothstep(0.0h, _AlturaSuciedad, (half)(i.reposo.y + 0.5));
                half sucio = saturate(_Suciedad * pies * (0.55h + (1.0h - h)));

                half3 albedo = t.rgb * _BaseColor.rgb;
                albedo = lerp(albedo, _ColorSuciedad.rgb, sucio);

                // Ojos: lo que cae dentro de las cuencas (elipsoides en la pose de reposo) brilla
                float3 rOjo = max(_OjoRadio.xyz, 1e-5);
                half ojo = 0;
                if (_OjoRadio.w > 0.5)
                {
                    half a = 1.0h - smoothstep(0.55h, 1.0h, (half)length((i.reposo - _OjoIzq.xyz) / rOjo));
                    half b = 1.0h - smoothstep(0.55h, 1.0h, (half)length((i.reposo - _OjoDer.xyz) / rOjo));
                    ojo = max(a, b);
                }
                albedo = lerp(albedo, _OjoColor.rgb * 0.05h, ojo);

                float3 nWS = normalize(i.normalWS);
                nWS = Relieve(i.positionWS, nWS, h * _Relieve * 0.003);

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = nWS;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = InitializeInputDataFog(float4(i.positionWS, 1.0), i.fogFactor);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                d.vertexLighting = i.luzVertice;
                #endif
                d.bakedGI = SampleSH(nWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                #if defined(SHADOWS_SHADOWMASK)
                d.shadowMask = unity_ProbesOcclusion;
                #else
                d.shadowMask = half4(1, 1, 1, 1);
                #endif

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.metallic = 0;
                s.specular = half3(0, 0, 0);
                s.smoothness = _Suavidad * lerp(0.45h, 1.0h, h) * (1.0h - sucio * 0.6h);
                s.occlusion = lerp(1.0h, saturate(h * 1.6h), _Oclusion);
                s.normalTS = half3(0, 0, 1);
                s.emission = _OjoColor.rgb * ojo;
                s.alpha = 1;

                half4 color = UniversalFragmentPBR(d, s);
                color.rgb = MixFog(color.rgb, d.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
