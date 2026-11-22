using System.Collections;
using UnityEngine;

// Una de las 4 piedras (gemas) coleccionables del guion: una por zona (Laberinto, Backrooms, Cloacas y Zona Roja).
// Son las gemas que le faltan al collar de la caja del sotano. No ocupan el inventario: al recogerlas se cuentan
// (Recogidas) y el protagonista dice su frase (segun cuantas lleva, asi cuadra aunque se salte alguna).
// Si al final del juego tiene las 4, empieza la cinematica secreta (FinalJuego).
// Las coloca Tools > Guion > Novedades (NovedadesGuionSetup). Se pueden mover a mano.
[RequireComponent(typeof(Interactable))]
public class PiedraColeccionable : MonoBehaviour
{
    public const int Total = 4;
    public static int Recogidas { get; private set; }
    public static event System.Action<int> AlRecoger;

    [Tooltip("Frase al recogerla. Vacio = la del guion segun cuantas lleva (1a, 2a, 3a o 4a)")]
    [TextArea] public string frase = "";

    // Frases del guion, por orden de recogida
    public static readonly string[] Frases =
    {
        "Qué linda piedra. Si salgo de esta, me servirá como recuerdo.",
        "Vale, esta piedra también me la llevo.",
        "¿Por qué hay tantas piedras iguales? Bueno, al menos es bonita. Irá con las otras dos.",
        "Tal parece que esta debe de ser la última. Mejor me la llevo.",
    };

    [Header("Aspecto")]
    public float flotar = 0.06f;
    public float girar = 40f;

    Vector3 inicio;
    Light brillo;
    float intensidad;
    bool recogida;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { Recogidas = 0; AlRecoger = null; }

    // Al volver al inicio (se recarga la escena) las piedras estan otra vez en su sitio: la cuenta empieza de cero
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AlCargar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
    }

    static void AlCargarEscena(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Recogidas = 0;

    // Debug (PanelDebug): como si ya tuviera las 4
    public static void DarTodas()
    {
        foreach (PiedraColeccionable p in FindObjectsByType<PiedraColeccionable>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (!p.recogida) Destroy(p.gameObject);
        Recogidas = Total;
        AlRecoger?.Invoke(Recogidas);
    }

    void Awake()
    {
        inicio = transform.position;
        brillo = GetComponentInChildren<Light>();
        intensidad = brillo != null ? brillo.intensity : 0f;
        var inter = GetComponent<Interactable>();
        inter.SetPrompt("Recoger piedra");
        inter.onInteract.AddListener(_ => Recoger());
    }

    void Update()
    {
        if (recogida) return;
        float t = Time.time + inicio.x * 0.37f;
        transform.position = inicio + Vector3.up * (Mathf.Sin(t * 1.6f) * flotar);
        transform.Rotate(0f, girar * Time.deltaTime, 0f, Space.World);
        // Un destello de vez en cuando, para que se note entre las sombras
        if (brillo != null) brillo.intensity = intensidad * (0.6f + 0.4f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 0.9f)), 8f) * 3f);
    }

    public void Recoger()
    {
        if (recogida) return;
        recogida = true;
        Recogidas = Mathf.Min(Total, Recogidas + 1);
        string texto = !string.IsNullOrEmpty(frase) ? frase : Frases[Mathf.Clamp(Recogidas - 1, 0, Frases.Length - 1)];
        Pensamiento.Decir("piedra.coleccion." + Recogidas, 0.25f, texto);
        Jugo.PatadaFOV(-1.5f);
        AlRecoger?.Invoke(Recogidas);
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        GetComponent<Interactable>().enabled = false;
        StartCoroutine(Guardar());
    }

    // Vuela hacia la camara, se achica y desaparece
    IEnumerator Guardar()
    {
        Camera cam = Camera.main;
        Vector3 desde = transform.position, escala = transform.localScale;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            float k = t / 0.4f;
            Vector3 hasta = cam != null ? cam.transform.position + cam.transform.forward * 0.35f - cam.transform.up * 0.2f : desde;
            transform.position = Vector3.Lerp(desde, hasta, k * k);
            transform.localScale = escala * (1f - k);
            if (brillo != null) brillo.intensity = intensidad * 3f * (1f - k);
            yield return null;
        }
        Destroy(gameObject);
    }
}
