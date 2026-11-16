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

    [Tooltip("Si se marca, el objeto recibido se oculta en lugar de quedar en la mano.")]
    public bool ocultarAlRecibir = false;

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
        }

        alRecibir?.Invoke(objeto);
    }
}
