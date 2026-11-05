using TMPro;
using UnityEngine;

// Laser del mando en VR: muestra a donde se apunta y el texto de lo que se puede hacer,
// porque el OnGUI no se ve dentro del visor.
// Se coloca en el mando con el que se apunta (el mismo del campo Pointer de PlayerInteractor).
[RequireComponent(typeof(LineRenderer))]
public class VRLaser : MonoBehaviour
{
    private const string MinigameHint = "Une los cables del mismo color\n[B] Salir";

    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private float width = 0.004f;
    [SerializeField] private float labelSize = 0.35f;
    [SerializeField] private float hintSize = 0.45f;
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.35f);
    // Color cuando apunta a algo con lo que se puede interactuar
    [SerializeField] private Color targetColor = new Color(0.4f, 1f, 0.6f, 1f);

    // Si tiene valor, el laser termina ahi y no donde choca (lo usa VRHud al apuntar a un boton)
    public Vector3? EndOverride { get; set; }

    private LineRenderer line;
    private TextMeshPro label;
    private TextMeshPro hint;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = width;
        line.endWidth = width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = new Material(Shader.Find("Sprites/Default"));

        label = CreateText("Texto_Laser", labelSize);
        label.transform.SetParent(transform, false);
    }

    void LateUpdate()
    {
        WiresMinigame minigame = WiresMinigame.Active;

        Vector3 start = transform.position;
        Vector3 end = EndOverride ?? (minigame != null ? minigame.PointerPoint : FindEnd(start));

        line.SetPosition(0, start);
        line.SetPosition(1, end);

        Color color = EndOverride != null || minigame != null || interactor.Current != null ? targetColor : idleColor;
        line.startColor = color;
        line.endColor = color;

        UpdateLabel(end);
        UpdateMinigameHint(minigame);
    }

    private void UpdateLabel(Vector3 end)
    {
        // Con el jugador bloqueado (minijuego, pausa) no hay nada que ofrecer
        string prompt = interactor.enabled ? interactor.CurrentPrompt : string.Empty;
        bool show = !string.IsNullOrEmpty(prompt);
        label.gameObject.SetActive(show);
        if (!show) return;

        label.text = prompt;
        Vector3 head = interactor.transform.position;
        label.transform.position = end + Vector3.up * 0.08f + (head - end).normalized * 0.05f;
        label.transform.rotation = Quaternion.LookRotation(label.transform.position - head);
    }

    // El aviso flota debajo de los cables, delante del auricular para que no lo tape
    private void UpdateMinigameHint(WiresMinigame minigame)
    {
        if (hint == null)
        {
            if (minigame == null) return;
            hint = CreateText("Aviso_VR", hintSize);
            hint.text = MinigameHint;
        }

        hint.gameObject.SetActive(minigame != null);
        if (minigame == null) return;

        Bounds cables = minigame.StubsBounds;
        Vector3 front = -minigame.transform.right;
        hint.transform.SetPositionAndRotation(cables.center + Vector3.down * cables.size.y * 0.85f + front * 0.25f,
                                              Quaternion.LookRotation(-front, Vector3.up));
    }

    private static TextMeshPro CreateText(string objectName, float size)
    {
        TextMeshPro text = new GameObject(objectName).AddComponent<TextMeshPro>();
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        return text;
    }

    private Vector3 FindEnd(Vector3 start)
    {
        float distance = interactor.interactDistance;
        bool hit = Physics.Raycast(start, transform.forward, out RaycastHit info, distance,
                                   interactor.raycastLayers, QueryTriggerInteraction.Ignore);
        return hit ? info.point : start + transform.forward * distance;
    }
}
