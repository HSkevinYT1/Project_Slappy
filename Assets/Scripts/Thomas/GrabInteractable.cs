using UnityEngine;

// Se coloca en cualquier objeto con Rigidbody y Collider para que el jugador
// pueda agarrarlo mirandolo y presionando la tecla de interaccion (F).
// Con el objeto en la mano, la misma tecla lo suelta.
[RequireComponent(typeof(Rigidbody))]
public class GrabInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Agarrar";

    public string Prompt => prompt;

    private Rigidbody rb;
    private Collider[] colliders;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
    }

    public void Interact(PlayerInteractor interactor)
    {
        interactor.Hold(this);
    }

    // Lo llama PlayerInteractor: coloca el objeto en la mano y desactiva sus fisicas
    public void Grab(Transform hand)
    {
        rb.useGravity = false;
        rb.isKinematic = true;
        SetColliders(false);

        transform.position = hand.position;
        transform.SetParent(hand);

        // Si el tutorial lo tenia resaltado, ya se encontro: se apaga el contorno
        Highlightable highlight = GetComponent<Highlightable>();
        if (highlight != null) highlight.Hide();
    }

    // Lo llama PlayerInteractor: suelta el objeto y le devuelve sus fisicas
    public void Release()
    {
        transform.SetParent(null);

        rb.isKinematic = false;
        rb.useGravity = true;
        SetColliders(true);
    }

    private void SetColliders(bool enabled)
    {
        foreach (Collider c in colliders) c.enabled = enabled;
    }
}
