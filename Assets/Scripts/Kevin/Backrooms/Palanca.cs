using System.Collections;
using UnityEngine;

// Palanca de pared de los Backrooms (la maneja PuzzlePalancas). Con su Interactable se baja (una sola vez).
// Una puede estar ROTA: le falta la manija. Al revisarla el protagonista lo dice, y con la manija en la mano se
// le pone apuntandole (su ReceptorObjeto "ranura" solo acepta esa manija). Ya reparada se baja como las demas.
[RequireComponent(typeof(Interactable))]
public class Palanca : MonoBehaviour
{
    [Tooltip("Lo que gira al bajarla (la manija cuelga de aqui)")]
    public Transform pivote;
    [Tooltip("La manija que se ve (escondida mientras esta rota)")]
    public GameObject manija;
    public bool rota;
    [Tooltip("Donde se coloca la manija que falta (solo la rota)")]
    public ReceptorObjeto ranura;
    public PuzzlePalancas puzzle;
    [Tooltip("Giro del pivote (grados en X local) arriba y abajo")]
    public float anguloArriba = -45f, anguloAbajo = 45f;
    public float duracion = 0.4f;
    [Tooltip("Sonido al bajarla (Assets/Sounds/SFX/sonido-palanca; lo asigna Tools > Sonidos > Asignar sonidos nuevos)")]
    public AudioClip sonido;
    [Range(0f, 1f)] public float volumen = 0.9f;

    public string promptBajar = "Bajar palanca";
    public string promptRota = "Revisar palanca";
    [TextArea] public string pensamientoRota = "Está rota... le falta la manija.\nTiene que estar por algún lado.";
    [TextArea] public string pensamientoReparada = "Listo. Ahora sí debería funcionar.";

    public bool Bajada { get; private set; }
    public bool RotaDescubierta { get; private set; }
    public bool Reparada { get; private set; }

    Interactable inter;
    bool moviendo;

    void Awake()
    {
        inter = GetComponent<Interactable>();
        inter.onInteract.AddListener(Usar);
        if (ranura != null)
        {
            ranura.alRecibir.AddListener(Reparar);
            ranura.enabled = rota;
        }
        if (manija != null) manija.SetActive(!rota);
        if (pivote != null) pivote.localRotation = Quaternion.Euler(anguloArriba, 0f, 0f);
        Actualizar();
    }

    void Usar(PlayerInteractor jugador)
    {
        if (Bajada || moviendo) return;
        if (rota)
        {
            RotaDescubierta = true;
            Pensamiento.Decir("palanca.rota", 0.1f, pensamientoRota);
            // Se mueve un poquito: no hay de donde agarrarla
            StartCoroutine(Temblar());
            return;
        }
        if (puzzle != null && puzzle.Ocupado) return;
        StartCoroutine(Bajar());
    }

    IEnumerator Bajar()
    {
        moviendo = true;
        Bajada = true;
        Actualizar();
        Sonar();
        // Anticipa un poco hacia arriba y baja de golpe, con un rebote corto al tocar el tope
        yield return Tirar(anguloArriba, anguloArriba - 6f, 0.1f);
        float desde = anguloArriba - 6f;
        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            float k = Jugo.EntradaCubica(t / duracion);
            if (pivote != null) pivote.localRotation = Quaternion.Euler(Mathf.Lerp(desde, anguloAbajo, k), 0f, 0f);
            yield return null;
        }
        Jugo.Sacudir(0.12f);
        yield return Tirar(anguloAbajo, anguloAbajo - 5f, 0.07f);
        yield return Tirar(anguloAbajo - 5f, anguloAbajo, 0.08f);
        moviendo = false;
        if (puzzle != null) puzzle.AlBajar(this);
    }

    // Desde la palanca (3D, se oye bien de cerca)
    void Sonar()
    {
        if (sonido == null) return;
        var fuente = GetComponent<AudioSource>();
        if (fuente == null)
        {
            fuente = gameObject.AddComponent<AudioSource>();
            fuente.playOnAwake = false;
            fuente.spatialBlend = 0.85f;
            fuente.rolloffMode = AudioRolloffMode.Linear;
            fuente.minDistance = 2f;
            fuente.maxDistance = 18f;
            fuente.dopplerLevel = 0f;
        }
        fuente.PlayOneShot(sonido, volumen);
    }

    // Lleva el pivote de un angulo a otro (grados en X local)
    IEnumerator Tirar(float de, float a, float seg)
    {
        if (pivote == null) yield break;
        for (float t = 0f; t < seg; t += Time.deltaTime)
        {
            pivote.localRotation = Quaternion.Euler(Mathf.Lerp(de, a, Jugo.SalidaCubica(t / seg)), 0f, 0f);
            yield return null;
        }
        pivote.localRotation = Quaternion.Euler(a, 0f, 0f);
    }

    // La parte que queda de la rota se mueve un poquito y vuelve
    IEnumerator Temblar()
    {
        moviendo = true;
        for (float t = 0f; t < 0.25f && pivote != null; t += Time.deltaTime)
        {
            pivote.localRotation = Quaternion.Euler(anguloArriba + Mathf.Sin(t / 0.25f * Mathf.PI) * 8f, 0f, 0f);
            yield return null;
        }
        if (pivote != null) pivote.localRotation = Quaternion.Euler(anguloArriba, 0f, 0f);
        moviendo = false;
    }

    // La llama la ranura al recibir la manija
    void Reparar(Grabbable pieza)
    {
        rota = false;
        Reparada = true;
        RotaDescubierta = true;
        if (pieza != null) pieza.gameObject.SetActive(false);
        if (ranura != null) ranura.enabled = false;
        if (manija != null) StartCoroutine(Aparecer(manija.transform));
        Pensamiento.Decir("palanca.reparada", 0.2f, pensamientoReparada);
        Actualizar();
    }

    // La manija aparece con un "pop"
    IEnumerator Aparecer(Transform m)
    {
        Vector3 escala = m.localScale;
        m.gameObject.SetActive(true);
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            m.localScale = escala * Jugo.SalidaAtras(t / 0.3f, 2.4f);
            yield return null;
        }
        m.localScale = escala;
    }

    void Actualizar()
    {
        inter.SetPrompt(rota ? promptRota : promptBajar);
        inter.enabled = !Bajada;
    }
}
