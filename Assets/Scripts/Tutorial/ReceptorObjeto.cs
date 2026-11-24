using UnityEngine;
using UnityEngine.Events;

// Se coloca en un NPC (o cualquier objeto con collider) que espera que el jugador le entregue algo.
// Si el jugador tiene en la mano el objeto esperado y apunta a este receptor, la tecla de interaccion
// lo ENTREGA en lugar de soltarlo (ver PlayerInteractor). El objeto queda en la mano del receptor.
public class ReceptorObjeto : MonoBehaviour
{
    void Awake()
    {
        if (zonaEntrega != null) zonaEntrega.isTrigger = true;
    }

    [Tooltip("Objeto que acepta. Vacio = acepta cualquier objeto agarrable.")]
    public Grabbable objetoEsperado;

    [Tooltip("Texto que se muestra al apuntarle con el objeto en la mano.")]
    public string prompt = "Entregar";

    [Tooltip("Donde queda el objeto recibido (ej: la mano del NPC). Vacio = frente al pecho.")]
    public Transform puntoMano;

    [Tooltip("Con Punto Mano asignado: el objeto toma tambien la rotacion de ese punto (ej: el cuchillo bien agarrado).")]
    public bool alinearConMano = true;

    [Tooltip("Con un personaje animado (AnimacionesModelo): hueso de la mano (final del nombre, ej. RightHand). Al recibir, " +
             "el Punto Mano se pone en el puño de esa mano y la sigue en todas las poses (quieto, caminando...). " +
             "Vacio = el Punto Mano se queda donde esta")]
    public string huesoMano = "RightHand";
    [Tooltip("Distancia (m) del puño al centro del objeto, a lo largo del eje azul del Punto Mano (el cuchillo se agarra por el mango)")]
    public float distanciaAgarre = 0.095f;

    [Tooltip("Si se marca, el objeto recibido se oculta en lugar de quedar en la mano.")]
    public bool ocultarAlRecibir = false;
    [Tooltip("Tamaño maximo (m, el lado mas largo) del objeto en la mano del receptor. Al entregarlo recupera su tamaño " +
             "real (en la mano del jugador se achica para que quepa) y algunos modelos son enormes (el cuchillo mide 8 m): " +
             "si es mas grande se achica a esto. 0 = sin limite")]
    public float tamanoMaximoEnMano = 0.35f;

    [Header("Desde donde se entrega")]
    [Tooltip("Opcional: el jugador tiene que estar dentro de esta caja para entregarlo (ej: detras del mostrador).")]
    public BoxCollider zonaEntrega;
    [Tooltip("Texto al apuntarle con el objeto desde fuera de la Zona Entrega.")]
    public string promptFueraZona = "Entregalo desde el mostrador";

    public UnityEvent<Grabbable> alRecibir;

    public bool Recibido { get; private set; }

    // Opcional (por codigo): que objetos acepta ademas de "Objeto Esperado" (ej: solo paneles con numero)
    public System.Func<Grabbable, bool> filtro;

    // Vuelve a esperar un objeto (ej: el jugador se llevo de vuelta el que se le entrego)
    public void Reabrir() => Recibido = false;
    public string Prompt => JugadorEnZona() ? prompt : promptFueraZona;

    private Transform jugador;

    // Espera este objeto (aunque el jugador todavia no este en la zona de entrega)
    public bool Espera(Grabbable objeto)
    {
        if (!enabled || Recibido || objeto == null) return false;
        if (filtro != null && !filtro(objeto)) return false;
        return objetoEsperado == null || objeto == objetoEsperado;
    }

    // Se le puede entregar ya: lo espera y el jugador esta en la zona de entrega
    public bool Acepta(Grabbable objeto) => Espera(objeto) && JugadorEnZona();

    public bool JugadorEnZona()
    {
        if (zonaEntrega == null) return true;
        if (jugador == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm == null) return true;
            jugador = pm.transform;
        }
        return zonaEntrega.bounds.Contains(jugador.position);
    }

    // Lo llama PlayerInteractor cuando el jugador entrega el objeto.
    public void Recibir(Grabbable objeto)
    {
        if (!Acepta(objeto)) return;
        Recibido = true;

        // Se suelta (devuelve su escala real) y queda quieto, sin fisicas ni colision.
        objeto.Release();
        if (objeto.TryGetComponent(out Rigidbody rb)) { rb.isKinematic = true; rb.useGravity = false; }
        foreach (Collider c in objeto.GetComponentsInChildren<Collider>()) c.enabled = false;
        if (objeto.TryGetComponent(out Interactable inter)) inter.enabled = false;
        if (objeto.TryGetComponent(out Highlightable brillo)) brillo.Hide();

        if (ocultarAlRecibir)
        {
            objeto.gameObject.SetActive(false);
        }
        else
        {
            PegarAMano();
            Transform mano = puntoMano;
            if (mano == null)
            {
                var punto = new GameObject("Mano (objeto recibido)").transform;
                punto.SetParent(transform, false);
                punto.localPosition = new Vector3(0f, 1.1f, 0.35f);
                mano = punto;
            }
            objeto.transform.SetParent(mano, true);
            objeto.transform.localPosition = Vector3.zero;
            if (puntoMano != null && alinearConMano) objeto.transform.localRotation = Quaternion.identity;
            Achicar(objeto.transform);
        }

        alRecibir?.Invoke(objeto);
    }

    // El Punto Mano estaba suelto en el NPC (el cambio de modelo lo saco del hueso de la mano): se pone en el puño de la
    // mano del modelo que se ve (entre la muñeca y los nudillos, como lo armo CuchilloTutorialSetup) con su misma
    // rotacion, y queda pegado a ese hueso
    void PegarAMano()
    {
        if (puntoMano == null || string.IsNullOrEmpty(huesoMano)) return;
        AnimacionesModelo anim = GetComponentInChildren<AnimacionesModelo>();
        if (anim == null) anim = GetComponentInParent<AnimacionesModelo>();
        if (anim == null) return;
        Transform mano = anim.Hueso(huesoMano);
        if (mano == null) return;
        Transform medio = Hijo(mano, "Middle"), indice = Hijo(mano, "Index"), pulgar = Hijo(mano, "Thumb");
        Transform nudillo = medio != null ? medio : indice;
        Vector3 puno = mano.position;
        if (nudillo != null)
        {
            Transform falange = nudillo.childCount > 0 ? nudillo.GetChild(0) : null;
            puno = falange != null ? Vector3.Lerp(mano.position, falange.position, 0.75f) : Vector3.LerpUnclamped(mano.position, nudillo.position, 1.1f);
        }
        if (pulgar != null) puno = Vector3.Lerp(puno, pulgar.position, 0.25f);
        puntoMano.position = puno + puntoMano.forward * distanciaAgarre;
        anim.Adjuntar(puntoMano, huesoMano);
    }

    // Si el objeto es mas grande que tamanoMaximoEnMano, se achica (parejo) a ese tamaño y se vuelve a centrar en la mano.
    // Se mide en su propio espacio (no con la caja del mundo, que crece al girarlo)
    void Achicar(Transform t)
    {
        if (tamanoMaximoEnMano <= 0f) return;
        float largo = 0f;
        foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Vector3 s = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
            largo = Mathf.Max(largo, Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
        foreach (SkinnedMeshRenderer sk in t.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            Vector3 s = sk.bounds.size;
            largo = Mathf.Max(largo, s.x, s.y, s.z);
        }
        if (largo <= tamanoMaximoEnMano || largo <= 0.0001f) return;
        t.localScale *= tamanoMaximoEnMano / largo;
    }

    static Transform Hijo(Transform t, string parte)
    {
        foreach (Transform c in t) if (c.name.IndexOf(parte, System.StringComparison.OrdinalIgnoreCase) >= 0) return c;
        return null;
    }
}
