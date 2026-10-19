// Codigo compartido por los shaders de niebla: ruido para los jirones de niebla
// e iluminacion de un punto de niebla (ambiente + luz principal + luces puntuales como antorchas)
#ifndef SLAPPY_FOG_COMMON_INCLUDED
#define SLAPPY_FOG_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float FogHash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float FogValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = FogHash(i);
    float b = FogHash(i + float3(1, 0, 0));
    float c = FogHash(i + float3(0, 1, 0));
    float d = FogHash(i + float3(1, 1, 0));
    float e = FogHash(i + float3(0, 0, 1));
    float g = FogHash(i + float3(1, 0, 1));
    float h = FogHash(i + float3(0, 1, 1));
    float k = FogHash(i + float3(1, 1, 1));

    return lerp(lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y),
                lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y), f.z);
}

// Ruido de 0 a 1 con varias capas de detalle
float FogFbm(float3 p, int octaves)
{
    float v = 0.0;
    float amp = 0.5;
    float total = 0.0;
    [unroll(4)] for (int o = 0; o < octaves; o++)
    {
        v += amp * FogValueNoise(p);
        total += amp;
        p = p * 2.02 + 19.7;
        amp *= 0.5;
    }
    return v / total;
}

half3 FogAddLight(Light light, half strength)
{
    return light.color * (light.distanceAttenuation * light.shadowAttenuation * strength);
}

// Luz que llega a un punto de la niebla. No usa normal: la niebla dispersa la luz en todas direcciones
half3 FogLighting(float3 positionWS, float2 screenUV, half ambientStrength, half mainLightStrength, half pointLightStrength, half maxBrightness)
{
    half3 lighting = max(half3(0, 0, 0), half3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w)) * ambientStrength;
    lighting += FogAddLight(GetMainLight(TransformWorldToShadowCoord(positionWS)), mainLightStrength);

    #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
    // LIGHT_LOOP_BEGIN usa "inputData" para buscar las luces del cluster en Forward+
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = screenUV;
    half4 shadowMask = half4(1, 1, 1, 1);
    uint lightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        lighting += FogAddLight(GetAdditionalLight(lightIndex, positionWS, shadowMask), pointLightStrength);
    }
    #endif

    LIGHT_LOOP_BEGIN(lightCount)
        lighting += FogAddLight(GetAdditionalLight(lightIndex, positionWS, shadowMask), pointLightStrength);
    LIGHT_LOOP_END
    #endif

    // Recorta el brillo sin cambiar el tono (junto a la antorcha sigue siendo naranja y no se quema)
    half peak = max(lighting.r, max(lighting.g, lighting.b));
    lighting *= min(1.0, maxBrightness / max(peak, 0.0001));
    return lighting;
}

#endif
