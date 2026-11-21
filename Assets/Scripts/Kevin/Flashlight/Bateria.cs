using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Pila que recarga la linterna. Se recoge con su Interactable (On Interact > Bateria > Recoger).
// Al recogerla vuela hacia la camara encogiendose (con un pulso en la pila del inventario) y
// desaparece. Pasado un rato vuelve a aparecer en el mismo sitio con un "pop", pero solo si el jugador
// esta lejos (no aparece delante de sus ojos).
public class Bateria : MonoBehaviour
{
    // Energia que devuelve (la linterna tiene 100 como maximo)
    public float carga = 35f;
    public AudioClip sonidoRecoger;

    [Header("Reaparecer")]
    public bool reaparece = true;
    [Tooltip("Segundos hasta que vuelve a aparecer (minimo y maximo, se elige al azar)")]
    public Vector2 tiempoReaparecer = new Vector2(60f, 100f);
    [Tooltip("Solo reaparece si el jugador esta al menos a esta distancia (m)")]
    public float distanciaMinima = 8f;

    public bool Disponible { get; private set; } = true;

    Renderer[] renderers;
    Collider[] colliders;
    Behaviour[] interactuables;
    Light[] luces;
    Vector3 posicion, escala;
    Quaternion rotacion;
    Coroutine rutina;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        luces = GetComponentsInChildren<Light>(true);
        var lista = new List<Behaviour>();
        foreach (Interactable i in GetComponentsInChildren<Interactable>(true)) lista.Add(i);
        interactuables = lista.ToArray();
        posicion = transform.position;
        rotacion = transform.rotation;
        escala = transform.localScale;
    }

    public void Recoger(PlayerInteractor interactor)
    {
        if (!Disponible) return;
        Flashlight linterna = FindFirstObjectByType<Flashlight>(FindObjectsInactive.Include);
        if (linterna == null)
        {
            Debug.LogWarning(name + ": no hay ninguna linterna que recargar.", this);
            return;
        }

        linterna.Recharge(carga);
        if (sonidoRecoger != null) AudioSource.PlayClipAtPoint(sonidoRecoger, transform.position);
        Jugo.PatadaFOV(-1.2f);
        if (Inventario.Instance != null) Inventario.Instance.PulsoBateria();

        Disponible = false;
        foreach (Highlightable h in GetComponentsInChildren<Highlightable>(true)) h.Hide();
        SetVisible(true, false);
        if (rutina != null) StopCoroutine(rutina);
        Camera cam = interactor != null ? interactor.GetComponentInChildren<Camera>() : Camera.main;
        if (cam == null) cam = Camera.main;
        rutina = StartCoroutine(Recogida(cam != null ? cam.transform : null));
    }

    // Vuela hacia la camara encogiendose, desaparece y (si reaparece) espera para volver
    IEnumerator Recogida(Transform cam)
    {
        Vector3 desde = transform.position;
        const float dur = 0.2f;
        for (float t = 0f; t < dur && cam != null; t += Time.deltaTime)
        {
            float k = Jugo.EntradaCubica(t / dur);
            Vector3 hacia = cam.position + cam.forward * 0.35f - cam.up * 0.15f;
            transform.position = Vector3.Lerp(desde, hacia, k);
            transform.localScale = escala * Mathf.Lerp(1f, 0.15f, k);
            transform.Rotate(Vector3.up, 900f * Time.deltaTime, Space.World);
            yield return null;
        }
        SetVisible(false, false);
        transform.SetPositionAndRotation(posicion, rotacion);
        transform.localScale = escala;
        rutina = reaparece ? StartCoroutine(Reaparecer()) : null;
    }

    // Si se desactivo mientras esperaba (ej: se apago su zona), sigue esperando al volver
    void OnEnable()
    {
        if (!Disponible && reaparece && rutina == null && posicion != Vector3.zero)
        {
            SetVisible(false, false);
            transform.SetPositionAndRotation(posicion, rotacion);
            transform.localScale = escala;
            rutina = StartCoroutine(Reaparecer());
        }
    }

    void OnDisable() => rutina = null;

    IEnumerator Reaparecer()
    {
        yield return new WaitForSeconds(Random.Range(tiempoReaparecer.x, Mathf.Max(tiempoReaparecer.x, tiempoReaparecer.y)));
        // Espera a que el jugador no este cerca
        Transform jugador = HUD.Jugador();
        while (jugador != null && Vector3.Distance(jugador.position, posicion) < distanciaMinima)
            yield return new WaitForSeconds(1f);

        // Pop: crece pasandose un poco y se asienta
        SetVisible(true, false);
        const float pop = 0.4f;
        for (float t = 0f; t < pop; t += Time.deltaTime)
        {
            transform.localScale = escala * Jugo.SalidaAtras(t / pop, 2.6f);
            yield return null;
        }
        transform.localScale = escala;
        SetVisible(true, true);
        Disponible = true;
        rutina = null;
    }

    void SetVisible(bool visible, bool interactuable)
    {
        foreach (Renderer r in renderers) if (r != null) r.enabled = visible;
        foreach (Light l in luces) if (l != null) l.enabled = visible;
        foreach (Collider c in colliders) if (c != null) c.enabled = interactuable;
        foreach (Behaviour b in interactuables) if (b != null) b.enabled = interactuable;
    }
}
