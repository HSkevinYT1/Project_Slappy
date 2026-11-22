using UnityEngine;

// Puerta que se esconde en el piso al abrirse (o en el techo, segun "desplazamiento").
// Se controla con Abrir() / Cerrar() / SetAbierta(bool), por ejemplo desde ProgresoCaminos o un UnityEvent.
public class PuertaDeslizante : MonoBehaviour
{
    public bool abiertaAlIniciar;
    // Cuanto se mueve (en local) desde la posicion cerrada hasta la abierta
    public Vector3 desplazamiento = new Vector3(0f, -4.5f, 0f);
    // Segundos que tarda en abrirse o cerrarse
    public float duracion = 1.5f;
    public AudioClip sonido;
    [Tooltip("Al abrirse se desactiva el objeto de golpe (y se reactiva al cerrarse) en vez de deslizarse")]
    public bool desactivarAlAbrir;
    [Tooltip("En vez de deslizarse: desaparece (y aparece) parpadeando, como una interferencia. Las puertas de los caminos lo usan (ProgresoCaminos)")]
    public bool parpadeo;
    [Header("Jugo")]
    [Tooltip("Temblor de camara al terminar de cerrarse (con el jugador cerca)")]
    public float golpeAlCerrar = 0.45f;
    [Tooltip("Hasta que distancia (m) se nota el golpe")]
    public float radioGolpe = 16f;

    public bool Abierta { get; private set; }

    private Vector3 posCerrada;
    private float valor;
    private bool iniciada;

    void Awake() => Iniciar();

    void Iniciar()
    {
        if (iniciada) return;
        iniciada = true;
        posCerrada = transform.localPosition;
        Abierta = abiertaAlIniciar;
        valor = Abierta ? 1f : 0f;
        if (desactivarAlAbrir) { if (Abierta) gameObject.SetActive(false); return; }
        Aplicar();
    }

    public void Abrir() => SetAbierta(true);
    public void Cerrar() => SetAbierta(false);

    public void SetAbierta(bool abierta, bool instantaneo = false)
    {
        Iniciar();
        if (Abierta != abierta && !instantaneo && sonido != null)
            AudioSource.PlayClipAtPoint(sonido, transform.position);
        if (parpadeo)
        {
            bool cambia = Abierta != abierta;
            Abierta = abierta;
            valor = abierta ? 1f : 0f;
            transform.localPosition = posCerrada;
            if (rutina != null) { StopCoroutine(rutina); rutina = null; }
            if (instantaneo || !cambia || !isActiveAndEnabled) Mostrar(!abierta, true);
            else rutina = StartCoroutine(Parpadear(!abierta));
            return;
        }
        if (desactivarAlAbrir)
        {
            Abierta = abierta;
            valor = abierta ? 1f : 0f;
            gameObject.SetActive(!abierta);
            return;
        }
        // Al empezar a abrirse: un retumbe suave
        if (!Abierta && abierta && !instantaneo) Jugo.SacudirEn(transform.position, golpeAlCerrar * 0.3f, radioGolpe);
        Abierta = abierta;
        if (instantaneo) { valor = abierta ? 1f : 0f; Aplicar(); }
    }

    // ------------------------------------------------------------------ Parpadeo

    Coroutine rutina;

    // Se muestra u oculta (mallas y paredes)
    void Mostrar(bool visible, bool conParedes)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        if (conParedes) foreach (Collider c in GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) c.enabled = visible;
    }

    // Interferencia: parpadea cada vez mas rapido y se queda (des)aparecida. Al abrirse ya no bloquea desde el
    // principio; al cerrarse bloquea al final
    System.Collections.IEnumerator Parpadear(bool terminaVisible)
    {
        if (!terminaVisible) Mostrar(true, false);
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) if (!c.isTrigger && !terminaVisible) c.enabled = false;
        Jugo.SacudirEn(transform.position, golpeAlCerrar * 0.35f, radioGolpe);
        float[] tiempos = { 0.09f, 0.05f, 0.11f, 0.04f, 0.07f, 0.03f, 0.05f, 0.03f };
        for (int i = 0; i < tiempos.Length; i++)
        {
            bool v = (i % 2 == 0) != terminaVisible;
            Mostrar(v, false);
            yield return new WaitForSeconds(tiempos[i]);
        }
        Mostrar(terminaVisible, true);
        if (terminaVisible && golpeAlCerrar > 0f) Jugo.SacudirEn(transform.position, golpeAlCerrar * 0.6f, radioGolpe);
        rutina = null;
    }

    void Update()
    {
        if (parpadeo) return;
        float objetivo = Abierta ? 1f : 0f;
        if (Mathf.Approximately(valor, objetivo)) return;
        valor = Mathf.MoveTowards(valor, objetivo, Time.deltaTime / Mathf.Max(0.01f, duracion));
        Aplicar();
        // Termino de cerrarse: temblor si el jugador esta cerca
        if (!Abierta && valor <= 0f && golpeAlCerrar > 0f)
        {
            Jugo.SacudirEn(transform.position, golpeAlCerrar, radioGolpe);
        }
    }

    void Aplicar()
    {
        transform.localPosition = posCerrada + desplazamiento * Mathf.SmoothStep(0f, 1f, valor);
    }
}
