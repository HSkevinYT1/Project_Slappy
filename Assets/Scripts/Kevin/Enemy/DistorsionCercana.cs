using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// La imagen se distorsiona cuando este enemigo esta cerca (el de la Zona Roja): la lente se deforma y ondula,
// los colores se separan en los bordes (aberracion cromatica), aparece grano y, muy cerca, la camara tiembla un poco.
// Cuanto mas cerca, mas fuerte; mas todavia si lo esta persiguiendo. Es un Volume global de URP propio (prioridad
// alta), con su peso segun la distancia: con el enemigo lejos no hace nada.
// Lo agrega MisionFaro al enemigo de la Zona Roja; tambien se puede poner a mano en cualquier enemigo.
public class DistorsionCercana : MonoBehaviour
{
    [Tooltip("Metros a los que empieza a notarse")]
    public float desde = 24f;
    [Tooltip("Metros a los que ya es maxima")]
    public float maxima = 4f;
    [Tooltip("Persiguiendo, se nota desde mas lejos (multiplica 'desde')")]
    public float persiguiendo = 1.35f;
    [Tooltip("Segundos de subida y bajada")]
    public float suavizado = 0.6f;

    [Header("Fuerza (al maximo)")]
    [Range(0f, 1f)] public float lente = 0.55f;
    [Range(0f, 1f)] public float colores = 1f;
    [Range(0f, 1f)] public float grano = 0.6f;
    [Tooltip("Temblor de camara por segundo, bien cerca")]
    public float temblor = 0.6f;

    Volume volumen;
    VolumeProfile perfil;
    LensDistortion ld;
    ChromaticAberration ca;
    FilmGrain fg;
    JefeIndigoAI ia;
    Transform jugador;
    float k;

    public float Fuerza => k;

    void Start()
    {
        ia = GetComponent<JefeIndigoAI>();
        var go = new GameObject("Distorsion cercana (" + name + ")");
        go.layer = 0;   // la camara del jugador mira los Volumes de la capa Default
        volumen = go.AddComponent<Volume>();
        volumen.isGlobal = true;
        volumen.priority = 200f;   // por encima del Volume de la zona
        volumen.weight = 0f;
        perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        perfil.name = "Distorsion cercana";
        ld = perfil.Add<LensDistortion>(true);
        ca = perfil.Add<ChromaticAberration>(true);
        fg = perfil.Add<FilmGrain>(true);
        fg.type.value = FilmGrainLookup.Medium3;
        volumen.sharedProfile = perfil;
    }

    void OnDisable()
    {
        k = 0f;
        if (volumen != null) volumen.weight = 0f;
    }

    void OnDestroy()
    {
        if (volumen != null) Destroy(volumen.gameObject);
        if (perfil != null) Destroy(perfil);
    }

    void Update()
    {
        if (volumen == null) return;
        if (jugador == null) jugador = HUD.Jugador();

        float objetivo = 0f;
        if (jugador != null && !PantallaMuerte.Muerto)
        {
            bool caza = ia != null && ia.CurrentState == JefeIndigoAI.State.Chase;
            float lejos = desde * (caza ? persiguiendo : 1f);
            float d = Vector3.Distance(transform.position, jugador.position);
            objetivo = 1f - Mathf.Clamp01((d - maxima) / Mathf.Max(0.1f, lejos - maxima));
            objetivo = objetivo * objetivo * (3f - 2f * objetivo);   // suave al empezar y al llegar al maximo
        }
        k = Mathf.MoveTowards(k, objetivo, Time.deltaTime / Mathf.Max(0.01f, suavizado));
        volumen.weight = k;
        if (k <= 0.001f) return;

        // La lente "respira" y se tuerce de forma irregular (mas rapido cuanto mas cerca)
        float t = Time.time * (1.2f + 2.5f * k);
        float onda = Mathf.Sin(t) * 0.6f + (Mathf.PerlinNoise(t * 0.7f, 1.3f) - 0.5f) * 0.9f;
        ld.intensity.value = Mathf.Clamp(-lente * (0.55f + 0.45f * onda), -1f, 1f);
        ld.xMultiplier.value = 0.75f + 0.25f * Mathf.PerlinNoise(t * 0.5f, 7.1f);
        ld.yMultiplier.value = 0.75f + 0.25f * Mathf.PerlinNoise(2.4f, t * 0.5f);
        ld.center.value = new Vector2(0.5f + (Mathf.PerlinNoise(t * 0.3f, 4f) - 0.5f) * 0.12f * k,
                                      0.5f + (Mathf.PerlinNoise(9f, t * 0.3f) - 0.5f) * 0.12f * k);
        ld.scale.value = 1f + 0.04f * k;   // un poco de zoom para que no se vean los bordes al deformarse
        ca.intensity.value = Mathf.Clamp01(colores * (0.65f + 0.35f * Mathf.Abs(onda)));
        fg.intensity.value = Mathf.Clamp01(grano * (0.7f + 0.3f * Mathf.PerlinNoise(t * 3f, 0.5f)));
        fg.response.value = 0.6f;

        // Encima: la camara tiembla un poco
        if (k > 0.6f && temblor > 0f && Time.timeScale > 0f) Jugo.Sacudir(temblor * (k - 0.6f) / 0.4f * Time.deltaTime);
    }
}
