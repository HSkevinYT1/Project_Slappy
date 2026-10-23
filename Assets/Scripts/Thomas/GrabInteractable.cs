using UnityEngine;

// Se coloca en cualquier objeto con Rigidbody y Collider para que el jugador
// pueda agarrarlo mirandolo y presionando la tecla de interaccion (F).
// Con el objeto en la mano, la misma tecla lo suelta.
[RequireComponent(typeof(Rigidbody))]
public class GrabInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Agarrar";

    [Header("En la mano")]
    // Punto propio donde se sostiene (ej: un hijo del hueso de la muneca). Vacio = HandPoint del jugador
    [SerializeField] private Transform holdPoint;
    // Si no hay Hold Point asignado (ej: en un prefab), se busca por nombre dentro del jugador
    [SerializeField] private string holdPointName = "";
    // Posicion y rotacion del objeto respecto a ese punto mientras se sostiene.
    // Se pueden ajustar en Play y copiar despues los valores.
    [SerializeField] private Vector3 holdPosition = Vector3.zero;
    [SerializeField] private Vector3 holdRotation = Vector3.zero;
    // Tamano maximo (en metros) en la mano: si el objeto es mas grande se achica mientras se
    // sostiene, para que no tape la camara. Al soltarlo recupera su tamano. 0 = sin limite
    [SerializeField] private float maxHeldSize = 0.25f;

    public string Prompt => prompt;

    private Rigidbody rb;
    private Collider[] colliders;
    private RigidbodyInterpolation interpolation;
    // Tamano real del objeto, para devolverselo al soltarlo
    private Vector3 worldScale;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        interpolation = rb.interpolation;
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
        // Con interpolacion el Rigidbody sigue moviendo el objeto y no deja que siga a la mano
        rb.interpolation = RigidbodyInterpolation.None;
        SetColliders(false);

        if (holdPoint == null && !string.IsNullOrEmpty(holdPointName))
            holdPoint = FindChild(hand.root, holdPointName);

        worldScale = transform.lossyScale;
        transform.SetParent(holdPoint != null ? holdPoint : hand);
        transform.localPosition = holdPosition;
        transform.localRotation = Quaternion.Euler(holdRotation);

        // En la mano comun, el centro del modelo (no su pivote) queda en el puno
        if (holdPoint == null)
        {
            FitInHand();
            CenterOn(hand.position + hand.rotation * holdPosition);
        }

        // Si el tutorial lo tenia resaltado, ya se encontro: se apaga el contorno
        Highlightable highlight = GetComponent<Highlightable>();
        if (highlight != null) highlight.Hide();
    }

    // Lo llama PlayerInteractor: suelta el objeto y le devuelve sus fisicas
    public void Release()
    {
        transform.SetParent(null);
        // Al salir de un padre con escala (ej: el hueso de la muneca) Unity la recalcula mal
        transform.localScale = worldScale;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.interpolation = interpolation;
        SetColliders(true);
    }

    private void FitInHand()
    {
        if (maxHeldSize <= 0f || !TryGetBounds(out Bounds bounds)) return;

        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (size > maxHeldSize) transform.localScale *= maxHeldSize / size;
    }

    private void CenterOn(Vector3 point)
    {
        if (TryGetBounds(out Bounds bounds))
            transform.position += point - bounds.center;
    }

    private bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;

        bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        return true;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    private void SetColliders(bool enabled)
    {
        foreach (Collider c in colliders) c.enabled = enabled;
    }
}
