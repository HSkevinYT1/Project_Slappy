using UnityEngine;
using UnityEngine.Events;

// Se coloca en un NPC (o cualquier objeto con collider) que espera que el jugador le entregue algo.
// Si el jugador tiene en la mano el objeto esperado y apunta a este receptor, la tecla de interaccion
// lo ENTREGA en lugar de soltarlo (ver PlayerInteractor). El objeto queda en la mano del receptor.
public class ReceptorObjeto : MonoBehaviour
{
    [Tooltip("Objeto que acepta. Vacio = acepta cualquier objeto agarrable.")]
    public Grabbable objetoEsperado;

    [Tooltip("Texto que se muestra al apuntarle con el objeto en la mano.")]
    public string prompt = "Entregar";

    [Tooltip("Donde queda el objeto recibido (ej: la mano del NPC). Vacio = frente al pecho.")]
    public Transform puntoMano;

    [Tooltip("Si se marca, el objeto recibido se oculta en lugar de quedar en la mano.")]
    public bool ocultarAlRecibir = false;

    public UnityEvent<Grabbable> alRecibir;

    public bool Recibido { get; private set; }
    public string Prompt => prompt;

    public bool Acepta(Grabbable objeto)
    {
        if (!enabled || Recibido || objeto == null) return false;
        return objetoEsperado == null || objeto == objetoEsperado;
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
        }

        alRecibir?.Invoke(objeto);
    }
}
