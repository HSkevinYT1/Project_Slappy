using UnityEngine;

// Detecta con un raycast desde el centro de la camara (o desde su puntero, si tiene) el objeto interactuable
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
    // Ayuda para apuntar a objetos pequenos: si el rayo pega cerca de uno (ej: en el suelo
    // junto a la linterna), se detecta el objeto mas cercano dentro de este radio
    public float aimAssistRadius = 0.3f;

    [Header("Puntero")]
    [Tooltip("Con que se apunta y se agarra en lugar de la camara (ej: un mando). Vacio = la camara.")]
    public Transform pointer;

    [Header("Agarrar")]
    // Punto donde se coloca el objeto agarrado (ej: el HandPoint del Player)
    public Transform handPoint;
    // Hueso de la muneca de los brazos. Si existe, el HandPoint se coloca en el centro del puno
    public string wristBoneName = "wrist.R";

    // Objeto al que se esta apuntando ahora mismo (null si no hay ninguno)
    public Interactable Current { get; private set; }
    // Con algo en la mano no se muestra "Soltar": solo el receptor o el objeto al que se apunta
    public string CurrentPrompt => Receptor != null ? Receptor.Prompt
                                 : Current != null ? Current.Prompt : string.Empty;

    // Con un objeto en la mano: quien lo espera y al que se esta apuntando (ej: el cliente del tutorial)
    public ReceptorObjeto Receptor { get; private set; }

    // Objeto que el jugador tiene en la mano (null si no tiene ninguno)
    public Grabbable Held { get; private set; }

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        // El HandPoint debe seguir a la camara (como los brazos) o al puntero para que lo
        // agarrado no se quede flotando al mirar arriba o abajo
        Transform follow = pointer != null ? pointer : cam.transform;
        if (handPoint != null && !handPoint.IsChildOf(follow))
            handPoint.SetParent(follow, true);

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

        // Con un objeto en la mano (ej: la linterna) se sigue pudiendo interactuar con todo.
        // Si se le apunta a quien lo espera, se le entrega; si no se apunta a nada, se suelta.
        Receptor = Held != null ? FindReceptor() : null;
        Current = Receptor != null ? null : FindInteractable();

        if (!GameInput.Interact) return;
        if (Receptor != null) Give(Receptor);
        else if (Current != null) Current.Interact(this);
        else if (Held != null) Drop();
    }

    Interactable FindInteractable()
    {
        Transform aim = pointer != null ? pointer : cam.transform;
        Ray ray = new Ray(aim.position, aim.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance,
                            raycastLayers, QueryTriggerInteraction.Ignore))
        {
            // GetComponentInParent permite que el collider este en un hijo del objeto
            Interactable direct = hit.collider.GetComponentInParent<Interactable>();
            if (direct != null && direct.enabled) return direct;

            return FindNear(hit.point);
        }

        return null;
    }

    Interactable FindNear(Vector3 point)
    {
        if (aimAssistRadius <= 0f) return null;

        Interactable closest = null;
        float closestDistance = float.MaxValue;

        foreach (Collider c in Physics.OverlapSphere(point, aimAssistRadius, raycastLayers, QueryTriggerInteraction.Ignore))
        {
            Interactable interactable = c.GetComponentInParent<Interactable>();
            if (interactable == null || !interactable.enabled) continue;

            float distance = (c.ClosestPoint(point) - point).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = interactable;
            }
        }

        return closest;
    }

    public void Hold(Grabbable obj)
    {
        if (handPoint == null)
        {
            Debug.LogWarning("PlayerInteractor: falta asignar el Hand Point para poder agarrar objetos.", this);
            return;
        }

        // Solo cabe un objeto en la mano: el anterior se suelta
        if (Held != null && Held != obj) Drop();
        Held = obj;
        Held.Grab(handPoint);
    }

    public void Drop()
    {
        if (Held == null) return;

        Held.Release();
        Held = null;
    }

    // Entrega el objeto de la mano a un receptor (ej: un NPC que lo pidio)
    public void Give(ReceptorObjeto receptor)
    {
        if (Held == null || receptor == null || !receptor.Acepta(Held)) return;

        Grabbable obj = Held;
        Held = null;
        Receptor = null;
        receptor.Recibir(obj);
    }

    // Receptor al que apunta el jugador que acepta el objeto que tiene en la mano
    ReceptorObjeto FindReceptor()
    {
        Transform aim = pointer != null ? pointer : cam.transform;
        if (Physics.Raycast(new Ray(aim.position, aim.forward), out RaycastHit hit, interactDistance,
                            raycastLayers, QueryTriggerInteraction.Ignore))
        {
            ReceptorObjeto r = hit.collider.GetComponentInParent<ReceptorObjeto>();
            if (r != null && r.Acepta(Held)) return r;
        }
        return null;
    }

    // Texto temporal en pantalla para probar. Mas adelante se reemplaza por un Canvas.
    void OnGUI()
    {
        // Con puntero propio, el texto lo muestra quien maneje ese puntero
        if (pointer != null || string.IsNullOrEmpty(CurrentPrompt)) return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20
        };

        Rect rect = new Rect(Screen.width / 2f - 150f, Screen.height / 2f + 40f, 300f, 40f);
        GUI.Label(rect, "[" + GameInput.InteractKeyName + "] " + CurrentPrompt, style);
    }
}
