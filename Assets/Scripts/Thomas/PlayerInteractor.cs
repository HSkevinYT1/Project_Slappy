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
    // Ayuda para apuntar a objetos pequenos: si el rayo pega cerca de uno (ej: en el suelo
    // junto a la linterna), se detecta el objeto mas cercano dentro de este radio
    public float aimAssistRadius = 0.3f;

    [Header("Agarrar")]
    // Punto donde se coloca el objeto agarrado (ej: el HandPoint del Player)
    public Transform handPoint;
    // Hueso de la muneca de los brazos. Si existe, el HandPoint se coloca en el centro del puno
    public string wristBoneName = "wrist.R";

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

        // El HandPoint debe seguir a la camara (como los brazos) para que lo agarrado
        // no se quede flotando al mirar arriba o abajo
        if (handPoint != null && !handPoint.IsChildOf(cam.transform))
            handPoint.SetParent(cam.transform, true);

        // Si los brazos tienen huesos, lo agarrado va al centro del puno
        Transform grip = CreateGripPoint();
        if (grip != null) handPoint = grip;
    }

    // Crea un punto en el centro del puno (promedio de las falanges del medio: F12.R, F22.R...).
    // Cuelga de la camara y no del hueso, para no heredar la escala rara del modelo.
    Transform CreateGripPoint()
    {
        Transform wrist = null;
        foreach (Transform t in transform.root.GetComponentsInChildren<Transform>(true))
            if (t.name == wristBoneName) { wrist = t; break; }
        if (wrist == null) return null;

        Vector3 center = Vector3.zero;
        int count = 0;
        foreach (Transform t in wrist.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Length == 5 && t.name[0] == 'F' && t.name[2] == '2')
            {
                center += t.position;
                count++;
            }
        }
        if (count == 0) return null;

        Transform grip = new GameObject("Agarre_Mano").transform;
        grip.SetParent(cam.transform, false);
        grip.position = center / count;
        return grip;
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
            IInteractable direct = hit.collider.GetComponentInParent<IInteractable>();
            if (direct != null) return direct;

            return FindNear(hit.point);
        }

        return null;
    }

    IInteractable FindNear(Vector3 point)
    {
        if (aimAssistRadius <= 0f) return null;

        IInteractable closest = null;
        float closestDistance = float.MaxValue;

        foreach (Collider c in Physics.OverlapSphere(point, aimAssistRadius, raycastLayers, QueryTriggerInteraction.Ignore))
        {
            IInteractable interactable = c.GetComponentInParent<IInteractable>();
            if (interactable == null) continue;

            float distance = (c.ClosestPoint(point) - point).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = interactable;
            }
        }

        return closest;
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
