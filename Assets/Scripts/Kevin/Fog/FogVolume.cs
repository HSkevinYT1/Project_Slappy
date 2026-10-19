using UnityEngine;

//este script se encarga de el volumen del fog en unity
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class FogVolume : MonoBehaviour
{
    public Color color = new Color(0.75f, 0.74f, 0.72f);
    //cantidad de niebla, 0 = transparente, 1 = opaco
    [Range(0f, 2f)] public float density = 0.35f;
    // Metros que tarda en desvanecerse en los lados y el techo de la caja
    public float edgeSoftness = 1.5f;
    // 0 = igual de espesa en toda la altura, 1 = casi toda pegada al suelo
    [Range(0f, 1f)] public float heightFade = 0.6f;
    // Cuanto se notan los jirones de niebla que se mueven
    [Range(0f, 1f)] public float noiseAmount = 0.6f;
    // brillo que emite la niebla
    [Range(0f, 1f)] public float glow = 0.25f;
    [Range(0f, 8f)] public float pointLights = 0.7f;

    // Si esta desmarcado empieza apagada hasta llamar a Activate()
    public bool activeOnStart = true;
    // Segundos que tarda en aparecer o desaparecer
    public float fadeDuration = 2f;

    // 0 = sin niebla, 1 = niebla completa
    public float Weight { get; private set; }
    public bool IsActive => wanted;

    private bool wanted;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock block;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int SelfLightId = Shader.PropertyToID("_SelfLight");
    private static readonly int DensityId = Shader.PropertyToID("_Density");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int HeightFadeId = Shader.PropertyToID("_HeightFade");
    private static readonly int NoiseAmountId = Shader.PropertyToID("_NoiseAmount");
    private static readonly int PointLightsId = Shader.PropertyToID("_PointLightStrength");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");

    // Hace aparecer la niebla con transicion (se puede llamar desde un UnityEvent u otro script)
    public void Activate()
    {
        wanted = true;
    }

    // Hace desaparecer la niebla con transicion
    public void Deactivate()
    {
        wanted = false;
    }

    public void Toggle()
    {
        wanted = !wanted;
    }

    private void OnEnable()
    {
        // En el editor siempre se ve, para poder colocarla
        wanted = !Application.isPlaying || activeOnStart;
        Weight = wanted ? 1f : 0f;
        ApplyProperties();
    }

    private void OnValidate()
    {
        edgeSoftness = Mathf.Max(0.01f, edgeSoftness);
        fadeDuration = Mathf.Max(0f, fadeDuration);
        ApplyProperties();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        float target = wanted ? 1f : 0f;
        if (Weight == target) return;

        Weight = fadeDuration > 0f ? Mathf.MoveTowards(Weight, target, Time.deltaTime / fadeDuration) : target;
        ApplyProperties();
    }

    private void ApplyProperties()
    {
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) return;

        if (block == null) block = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(block);
        block.SetColor(ColorId, color);
        block.SetColor(SelfLightId, color * glow);
        block.SetFloat(DensityId, density);
        block.SetFloat(EdgeSoftnessId, edgeSoftness);
        block.SetFloat(HeightFadeId, heightFade);
        block.SetFloat(NoiseAmountId, noiseAmount);
        block.SetFloat(PointLightsId, pointLights);
        block.SetFloat(FadeId, Weight);
        meshRenderer.SetPropertyBlock(block);

        if (Application.isPlaying) meshRenderer.enabled = Weight > 0f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.75f, 0.75f, 0.8f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
#endif
}
