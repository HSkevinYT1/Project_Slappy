using UnityEngine;

// Detecta con un raycast desde el centro de la camara el objeto interactuable
// al que apunta el jugador y lo activa al presionar la tecla de interaccion.
// Se coloca en la Main Camera (junto a MouseLook).
public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaccion")]
    // Distancia maxima a la que se puede interactuar
    public float interactDistance = 3f;
    // Capas que bloquean/permiten el rayo. Se deja en "Everything" a proposito:
    // asi una pared entre el jugador y el objeto impide la interaccion.
    public LayerMask raycastLayers = ~0;
    public KeyCode interactKey = KeyCode.F;

    [Header("Agarrar")]
    // Punto donde se coloca el objeto agarrado (ej: el HandPoint del Player)
    public Transform handPoint;

    // Objeto al que se esta apuntando ahora mismo (null si no hay ninguno)
    public IInteractable Current { get; private set; }
    public string CurrentPrompt => Held != null ? "Soltar" : Current != null ? Current.Prompt : string.Empty;

    // Objeto que el jugador tiene en la mano (null si no tiene ninguno)
    public GrabInteractable Held { get; private set; }

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        // Mientras hay un dialogo abierto, no se detecta ni se interactua con nada
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive)
        {
            Current = null;
            return;
        }

        // Con un objeto en la mano, la tecla de interaccion lo suelta
        if (Held != null)
        {
            Current = null;
            if (Input.GetKeyDown(interactKey)) Drop();
            return;
        }

        // Busca el objeto al que apunta la camara
        Current = FindInteractable();

        // Si hay uno y se presiona la tecla, se interactua con el
        if (Current != null && Input.GetKeyDown(interactKey))
            Current.Interact(this);
    }

    IInteractable FindInteractable()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance,
                            raycastLayers, QueryTriggerInteraction.Ignore))
        {
            // GetComponentInParent permite que el collider este en un hijo del objeto
            return hit.collider.GetComponentInParent<IInteractable>();
        }

        return null;
    }

    public void Hold(GrabInteractable obj)
    {
        if (handPoint == null)
        {
            Debug.LogWarning("PlayerInteractor: falta asignar el Hand Point para poder agarrar objetos.", this);
            return;
        }

        Held = obj;
        Held.Grab(handPoint);
    }

    public void Drop()
    {
        if (Held == null) return;

        Held.Release();
        Held = null;
    }

    // Texto temporal en pantalla para probar. Mas adelante se reemplaza por un Canvas.
    void OnGUI()
    {
        if (string.IsNullOrEmpty(CurrentPrompt)) return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20
        };

        Rect rect = new Rect(Screen.width / 2f - 150f, Screen.height / 2f + 40f, 300f, 40f);
        GUI.Label(rect, "[" + interactKey + "] " + CurrentPrompt, style);
    }
}
