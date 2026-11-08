using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Cajas del sotano: al bajar (entrar en este trigger) se marcan las cajas y se pueden revisar una por una.
// En las primeras el jugador saca el objeto, lo mira y lo vuelve a dejar. En la ULTIMA que revise
// (sea cual sea) encuentra la linterna y se la queda en la mano.
// La linterna esta escondida hasta entonces.
[RequireComponent(typeof(BoxCollider))]
public class RevisionCajas : MonoBehaviour
{
    public CajaRevisable[] cajas;
    // La linterna (su Grabbable). Se esconde al empezar y aparece en la ultima caja
    public Grabbable linterna;

    [Header("Mirar objeto")]
    [Tooltip("Distancia delante de la camara a la que se mira el objeto.")]
    public float distanciaMirar = 0.7f;
    [Tooltip("Tamano maximo del objeto mientras se mira (m), para que no tape toda la pantalla.")]
    public float tamanoMirar = 0.35f;
    [Tooltip("Segundos que tarda en salir hacia arriba de la caja (y en volver a bajar al dejarlo).")]
    public float duracionSubir = 0.45f;
    public float duracionSacar = 0.5f;
    public float duracionMirar = 1.8f;
    public float duracionDejar = 0.5f;
    public AudioClip sonidoRevisar;

    public UnityEvent alBajar;
    public UnityEvent alEncontrarLinterna;

    public bool Activa { get; private set; }
    public bool Ocupada { get; private set; }

    void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (cajas == null || cajas.Length == 0) cajas = GetComponentsInChildren<CajaRevisable>();
        if (linterna != null) linterna.gameObject.SetActive(false);
        foreach (CajaRevisable c in cajas)
        {
            if (c == null) continue;
            c.revision = this;
            SetInteractuable(c, false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (Activa || !EsJugador(other)) return;
        Activar();
    }

    // Marca las cajas (tambien se puede llamar desde un UnityEvent)
    public void Activar()
    {
        if (Activa) return;
        Activa = true;
        foreach (CajaRevisable c in cajas)
        {
            if (c == null || c.Revisada) continue;
            SetInteractuable(c, true);
            if (c.marca != null) c.marca.Show();
        }
        alBajar?.Invoke();
    }

    public void Revisar(CajaRevisable caja, PlayerInteractor interactor)
    {
        if (!Activa || Ocupada || caja.Revisada) return;
        caja.Revisada = true;
        SetInteractuable(caja, false);
        if (caja.marca != null) caja.marca.Hide();

        int quedan = 0;
        foreach (CajaRevisable c in cajas) if (c != null && !c.Revisada) quedan++;
        if (sonidoRevisar != null) AudioSource.PlayClipAtPoint(sonidoRevisar, caja.transform.position);

        StartCoroutine(quedan == 0 ? DarLinterna(caja, interactor) : MirarObjeto(caja));
    }

    IEnumerator MirarObjeto(CajaRevisable caja)
    {
        Transform obj = caja.objeto;
        Camera cam = Camera.main;
        if (obj == null || cam == null) yield break;
        Ocupada = true;
        var apagados = BloquearJugador();

        // Se saca de la caja hasta delante de la camara (y se achica si es muy grande)
        Transform padre = obj.parent;
        Vector3 pos = obj.position, escala = obj.localScale;
        Quaternion rot = obj.rotation;
        var colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders) c.enabled = false;
        obj.SetParent(null, true);
        Vector3 escalaMundo = obj.localScale;
        float factor = 1f;
        if (Limites(obj, out Bounds b))
        {
            float tam = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (tam > tamanoMirar) factor = tamanoMirar / tam;
        }
        Vector3 centroLocal = Limites(obj, out Bounds b0) ? obj.InverseTransformPoint(b0.center) : Vector3.zero;

        Vector3 Delante() => cam.transform.position + cam.transform.forward * distanciaMirar - cam.transform.up * 0.05f;

        // Primero sale recto hacia arriba hasta quedar por encima de la caja (asi no la atraviesa)
        float subir = Mathf.Max(0f, BordeCaja(caja) + 0.05f - (Limites(obj, out Bounds bb) ? bb.min.y : pos.y));
        Vector3 arriba = pos + Vector3.up * subir;
        yield return Animar(duracionSubir, k => obj.position = Vector3.Lerp(pos, arriba, k));

        // Despues va hasta delante de la camara
        yield return Animar(duracionSacar, k =>
        {
            obj.localScale = escalaMundo * Mathf.Lerp(1f, factor, k);
            obj.rotation = Quaternion.Slerp(rot, Quaternion.LookRotation(-cam.transform.forward) * Quaternion.Euler(15f, 0f, 0f), k);
            Vector3 centro = Vector3.Lerp(arriba + rot * Vector3.Scale(centroLocal, escalaMundo), Delante(), k);
            obj.position = centro - obj.rotation * Vector3.Scale(centroLocal, obj.localScale);
        });
        // Mirarlo girandolo un poco
        Quaternion inicioMirar = obj.rotation;
        yield return Animar(duracionMirar, k =>
        {
            obj.rotation = inicioMirar * Quaternion.Euler(0f, Mathf.Sin(k * Mathf.PI * 2f) * 50f + k * 30f, 0f);
            obj.position = Delante() - obj.rotation * Vector3.Scale(centroLocal, obj.localScale);
        });
        // Dejarlo donde estaba: vuelve encima de la caja y baja recto
        Vector3 desde = obj.position;
        Quaternion rotDesde = obj.rotation;
        yield return Animar(duracionDejar, k =>
        {
            obj.localScale = escalaMundo * Mathf.Lerp(factor, 1f, k);
            obj.rotation = Quaternion.Slerp(rotDesde, rot, k);
            obj.position = Vector3.Lerp(desde, arriba, k);
        });
        yield return Animar(duracionSubir, k => obj.position = Vector3.Lerp(arriba, pos, k));

        obj.SetParent(padre, true);
        obj.SetPositionAndRotation(pos, rot);
        obj.localScale = escala;
        foreach (Collider c in colliders) if (c != null) c.enabled = true;
        Desbloquear(apagados);
        Ocupada = false;
    }

    IEnumerator DarLinterna(CajaRevisable caja, PlayerInteractor interactor)
    {
        if (linterna == null) yield break;
        Ocupada = true;
        var apagados = BloquearJugador();
        Camera cam = Camera.main;

        // Sale del fondo de la caja hacia la mano
        Transform t = linterna.transform;
        t.SetParent(null, true);
        t.position = caja.PuntoFondo;
        t.rotation = Quaternion.Euler(-90f, Random.Range(0f, 360f), 0f);
        linterna.gameObject.SetActive(true);
        // Sube recto hasta salir de la caja y despues va a la mano
        Vector3 fondo = t.position;
        Vector3 arriba = new Vector3(fondo.x, Mathf.Max(fondo.y, BordeCaja(caja) + 0.2f), fondo.z);
        yield return Animar(duracionSubir, k => t.position = Vector3.Lerp(fondo, arriba, k));
        Vector3 desde = t.position;
        Quaternion rotDesde = t.rotation;
        if (cam != null)
        {
            yield return Animar(duracionSacar * 1.2f, k =>
            {
                Vector3 hasta = cam.transform.position + cam.transform.forward * distanciaMirar - cam.transform.up * 0.1f;
                t.position = Vector3.Lerp(desde, hasta, k);
                t.rotation = Quaternion.Slerp(rotDesde, cam.transform.rotation, k);
            });
        }

        Desbloquear(apagados);
        if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null) interactor.Hold(linterna);
        alEncontrarLinterna?.Invoke();
        Ocupada = false;
    }

    List<Behaviour> BloquearJugador()
    {
        var lista = new List<Behaviour>();
        void Apagar<T>() where T : Behaviour
        {
            foreach (T b in FindObjectsByType<T>(FindObjectsSortMode.None))
                if (b.enabled) { b.enabled = false; lista.Add(b); }
        }
        Apagar<PlayerMovement>();
        Apagar<MouseLook>();
        Apagar<HeadBob>();
        Apagar<PlayerInteractor>();
        foreach (PlayerMovement pm in FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (pm.TryGetComponent(out Rigidbody rb)) rb.linearVelocity = Vector3.zero;
        return lista;
    }

    static void Desbloquear(List<Behaviour> lista)
    {
        foreach (Behaviour b in lista) if (b != null) b.enabled = true;
    }

    // Altura del borde de arriba de la caja (con las solapas)
    static float BordeCaja(CajaRevisable caja)
    {
        Collider c = caja.GetComponent<Collider>();
        if (c != null) return c.bounds.max.y;
        return Limites(caja.transform, out Bounds b) ? b.max.y : caja.transform.position.y;
    }

    static void SetInteractuable(CajaRevisable c, bool activo)
    {
        if (c.TryGetComponent(out Interactable i)) i.enabled = activo;
    }

    static bool EsJugador(Collider c)
    {
        for (Transform t = c.transform; t != null; t = t.parent)
            if (t.CompareTag("Player")) return true;
        return false;
    }

    static bool Limites(Transform t, out Bounds b)
    {
        b = default;
        bool hay = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
        }
        return hay;
    }

    static IEnumerator Animar(float duracion, System.Action<float> paso)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, duracion))
        {
            paso(Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        paso(1f);
    }
}
