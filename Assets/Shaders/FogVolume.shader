// Volumen de niebla (URP). Se pone en un cubo: rellena de niebla solo el interior de la caja.
// Recorre el rayo de la camara dentro de la caja (raymarching), se corta con paredes/suelo (Depth Texture),
// se desvanece en los lados y el techo y se ilumina con las luces de la escena (antorchas).
// La base del cubo no se desvanece porque normalmente esta apoyada en el suelo.
Shader "Slappy/Fog Volume"
{
    Properties
    {
        _Color ("Color", Color) = (0.75, 0.74, 0.72, 1)
        // Brillo propio: hace que la niebla se vea gris aunque no le llegue luz
        [HDR] _SelfLight ("Self Light", Color) = (0.19, 0.185, 0.18, 1)
        _Density ("Density (per meter)", Range(0, 2)) = 0.35
        _EdgeSoftness ("Edge Softness (meters)", Float) = 1.5
        _HeightFade ("Height Fade", Range(0, 1)) = 0.6

        _NoiseScale ("Noise Scale", Float) = 0.3
        _NoiseSpeed ("Noise Speed (XYZ)", Vector) = (0.08, 0.02, 0.05, 0)
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.6

        _AmbientStrength ("Ambient Light", Range(0, 4)) = 1
        _MainLightStrength ("Main Light", Range(0, 4)) = 0.5
        _PointLightStrength ("Point Lights", Range(0, 8)) = 0.7
        _MaxBrightness ("Max Brightness", Range(0.1, 8)) = 1

        [IntRange] _Steps ("Quality (steps)", Range(4, 32)) = 12

        // Limite de vision: oscurece todo lo que este lejos de la camara (End = 0 lo desactiva)
        _VisionColor ("Vision Color", Color) = (0, 0, 0, 1)
        _VisionStart ("Vision Start (meters)", Float) = 2
        _VisionEnd ("Vision End (meters)", Float) = 0
        // Cuanto atraviesan la oscuridad las zonas iluminadas por luces adicionales (linterna, antorchas)
        _LightReveal ("Light Reveal", Range(0, 20)) = 4

        // Lo controla FogVolume para las transiciones
        [HideInInspector] _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Color premultiplicado. Se pintan las caras de atras y sin ZTest
            // para que funcione igual con la camara dentro o fuera de la caja
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "FogCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _SelfLight;
                float _Density;
                float _EdgeSoftness;
                half _HeightFade;
                float _NoiseScale;
                float4 _NoiseSpeed;
                half _NoiseAmount;
                half _AmbientStrength;
                half _MainLightStrength;
                half _PointLightStrength;
                half _MaxBrightness;
                float _Steps;
                half4 _VisionColor;
                float _VisionStart;
                float _VisionEnd;
                half _LightReveal;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            // Cuanta niebla hay en un punto (por metro)
            float DensityAt(float3 positionOS, float3 positionWS, float3 boxScale)
            {
                // Metros hasta los lados y el techo de la caja
                float3 toFace = (0.5 - abs(positionOS)) * boxScale;
                float toTop = (0.5 - positionOS.y) * boxScale.y;
                float edge = saturate(min(min(toFace.x, toFace.z), toTop) / max(_EdgeSoftness, 0.001));
                edge = edge * edge * (3.0 - 2.0 * edge);

                // Mas espesa abajo
                float height = lerp(1.0, saturate(0.5 - positionOS.y), _HeightFade);

                float noise = 1.0;
                if (_NoiseAmount > 0.0)
                {
                    float3 p = positionWS * _NoiseScale + _Time.y * _NoiseSpeed.xyz;
                    noise = lerp(1.0, smoothstep(0.2, 0.8, FogFbm(p, 2)), _NoiseAmount);
                }

                return _Density * edge * height * noise;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 cameraWS = GetCameraPositionWS();
                float3 rayWS = normalize(input.positionWS - cameraWS);

                // El rayo en el espacio del cubo (de -0.5 a 0.5). Sin normalizar, asi "t" sigue siendo metros
                float4x4 worldToObject = GetWorldToObjectMatrix();
                float3 cameraOS = mul(worldToObject, float4(cameraWS, 1.0)).xyz;
                float3 rayOS = mul((float3x3)worldToObject, rayWS);
                rayOS = sign(rayOS) * max(abs(rayOS), 1e-6) + (rayOS == 0.0) * 1e-6;

                float3 t0 = (-0.5 - cameraOS) / rayOS;
                float3 t1 = (0.5 - cameraOS) / rayOS;
                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);
                float tEnter = max(max(max(tMin.x, tMin.y), tMin.z), 0.0);
                float tExit = min(min(tMax.x, tMax.y), tMax.z);

                // Se corta donde haya una pared o el suelo
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
                float sceneDistance = sceneEyeDepth / max(dot(rayWS, cameraForward), 0.0001);
                tExit = min(tExit, sceneDistance);
                clip(tExit - tEnter - 0.001);

                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float3 boxScale = float3(length(objectToWorld._m00_m10_m20), length(objectToWorld._m01_m11_m21), length(objectToWorld._m02_m12_m22));

                int steps = max((int)_Steps, 1);
                float stepLength = (tExit - tEnter) / steps;
                // Desplazamiento distinto en cada pixel para que no se vean bandas
                float jitter = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));

                half3 scattered = 0.0;
                half transmittance = 1.0;
                if (_Density > 0.0)
                [loop] for (int s = 0; s < steps; s++)
                {
                    float t = tEnter + (s + jitter) * stepLength;
                    float density = DensityAt(cameraOS + rayOS * t, cameraWS + rayWS * t, boxScale);
                    if (density <= 0.0) continue;

                    half absorbed = 1.0 - exp(-density * stepLength);
                    float3 positionWS = cameraWS + rayWS * t;
                    half3 lighting = FogLighting(positionWS, screenUV, _AmbientStrength, _MainLightStrength, _PointLightStrength, _MaxBrightness);
                    scattered += transmittance * absorbed * (_Color.rgb * lighting + _SelfLight.rgb);
                    transmittance *= 1.0 - absorbed;
                    if (transmittance < 0.01) break;
                }

                half alpha = (1.0 - transmittance) * _Color.a * _Fade;
                half3 color = scattered * _Color.a * _Fade;

                // Niebla de distancia de la escena (Lighting > Fog) a mitad del tramo
                float3 middleWS = cameraWS + rayWS * (0.5 * (tEnter + tExit));
                color = MixFogColor(color, unity_FogColor.rgb * alpha, ComputeFogFactor(TransformWorldToHClip(middleWS).z));

                // Limite de vision: lo que esta mas lejos que _VisionEnd queda del todo tapado (el cielo tambien)
                if (_VisionEnd > 0.0)
                {
                    half vision = smoothstep(_VisionStart, _VisionEnd, sceneDistance) * _Fade;

                    // Lo que ilumina la linterna (o una antorcha) se ve aunque este lejos
                    if (_LightReveal > 0.0 && vision > 0.0)
                    {
                        float3 surfaceWS = cameraWS + rayWS * sceneDistance;
                        half3 surfaceLight = FogAdditionalLights(surfaceWS, screenUV, 1.0);
                        half lit = max(surfaceLight.r, max(surfaceLight.g, surfaceLight.b));
                        vision *= 1.0 - saturate(lit * _LightReveal);
                    }

                    color = lerp(color, _VisionColor.rgb, vision);
                    alpha = lerp(alpha, 1.0, vision);
                }
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
