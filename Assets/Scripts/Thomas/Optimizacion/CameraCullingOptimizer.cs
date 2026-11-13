using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Limita lo que renderiza la cámara a un área alrededor del jugador.
///
/// Capas de optimización que aplica:
///  1. Far clip plane: nada más lejos de esta distancia se dibuja.
///  2. Distancias por capa (layerCullDistances): objetos de ciertas capas
///     (props pequeños, decoración...) se dejan de dibujar antes que el resto.
///  3. Culling esférico opcional (solo renderer built-in, no URP).
///
/// El frustum culling (no dibujar lo que está fuera del cono de visión) y el
/// occlusion culling (no dibujar lo que está tapado por paredes) los hace Unity;
/// este componente solo se asegura de que el occlusion esté activado en la cámara.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraCullingOptimizer : MonoBehaviour
{
    [Serializable]
    public struct DistanciaPorCapa
    {
        [Tooltip("Capa a la que aplica la distancia.")]
        public SingleLayer capa;

        [Tooltip("Distancia máxima (metros) a la que se dibujan objetos de esta capa. 0 = usa el far clip.")]
        [Min(0f)] public float distancia;
    }

    [Header("Distancia de render")]
    [Tooltip("Distancia máxima a la que la cámara dibuja algo (metros).\n" +
             "Este valor MANDA sobre el campo 'Far' del componente Camera: cámbialo aquí, no en la cámara.")]
    [Min(1f)] public float farClip = 60f;

    [Tooltip("Solo renderer built-in: mide la distancia como esfera alrededor de la cámara. En URP no tiene efecto.")]
    public bool cullingEsferico = true;

    [Tooltip("Distancias más cortas para capas concretas (por ejemplo props pequeños).")]
    public List<DistanciaPorCapa> distanciasPorCapa = new List<DistanciaPorCapa>();

    [Header("Occlusion culling")]
    [Tooltip("Activa el occlusion culling en la cámara. Solo tiene efecto si la escena tiene datos horneados (Window > Rendering > Occlusion Culling > Bake).")]
    public bool usarOcclusionCulling = true;

    [Header("Niebla (opcional)")]
    [Tooltip("Ajusta la niebla lineal para que termine justo antes del far clip y oculte el corte. Modifica RenderSettings de la escena.")]
    public bool sincronizarNiebla = false;

    [Tooltip("Porcentaje del far clip donde empieza la niebla.")]
    [Range(0f, 1f)] public float inicioNiebla = 0.5f;

    [Header("Debug")]
    public bool dibujarGizmos = true;

    Camera _cam;
    readonly float[] _distancias = new float[32];

    public Camera Cam => _cam != null ? _cam : (_cam = GetComponent<Camera>());

    void OnEnable() => Aplicar();

    void OnValidate()
    {
        // OnValidate corre al cambiar valores en el Inspector: se ve el efecto en vivo.
        if (isActiveAndEnabled) Aplicar();
    }

    /// <summary>Aplica la configuración a la cámara. Se puede llamar desde otros scripts tras cambiar valores.</summary>
    public void Aplicar()
    {
        var cam = Cam;
        if (cam == null) return;

        cam.farClipPlane = Mathf.Max(farClip, cam.nearClipPlane + 0.01f);
        cam.useOcclusionCulling = usarOcclusionCulling;

        Array.Clear(_distancias, 0, _distancias.Length);
        foreach (var d in distanciasPorCapa)
        {
            int capa = d.capa.Index;
            if (capa < 0 || capa > 31) continue;
            // Una distancia mayor que el far clip no tiene efecto: se recorta.
            _distancias[capa] = d.distancia <= 0f ? 0f : Mathf.Min(d.distancia, cam.farClipPlane);
        }
        cam.layerCullDistances = _distancias;
        // layerCullSpherical solo existe en el renderer built-in; en URP Unity lo ignora y muestra un aviso.
        if (GraphicsSettings.currentRenderPipeline == null)
            cam.layerCullSpherical = cullingEsferico;

        if (sincronizarNiebla)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = cam.farClipPlane * inicioNiebla;
            RenderSettings.fogEndDistance = cam.farClipPlane * 0.98f;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!dibujarGizmos) return;
        var cam = Cam;
        if (cam == null) return;

        // Frustum hasta el far clip.
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawFrustum(Vector3.zero, cam.fieldOfView, cam.farClipPlane, cam.nearClipPlane, cam.aspect);
        Gizmos.matrix = Matrix4x4.identity;

        // Radio de render general y por capa.
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, cam.farClipPlane);

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.5f);
        foreach (var d in distanciasPorCapa)
            if (d.distancia > 0f) Gizmos.DrawWireSphere(transform.position, Mathf.Min(d.distancia, cam.farClipPlane));
    }
}

/// <summary>Selector de una sola capa para el Inspector.</summary>
[Serializable]
public struct SingleLayer
{
    [SerializeField] int index;
    public int Index => index;
    public SingleLayer(int i) { index = i; }
}
