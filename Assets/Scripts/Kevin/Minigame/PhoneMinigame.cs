using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

//script que hace que se caiga el fakin panel y que se pueda jugar al minijuego de cables del telefono
[RequireComponent(typeof(WiresMinigame), typeof(Interactable))]
public class PhoneMinigame : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Tapa que se cae. Si se deja vacio se busca un hijo cuyo nombre empiece por 'panel'.")]
    [SerializeField] private Transform panel;
    [SerializeField] private float panelMass = 2f;
    [SerializeField] private float panelPush = 0.5f;
    [SerializeField] private float panelTumble = 2f;
    [SerializeField] private float fallTime = 1f;
    [SerializeField] private float panelRestTime = 5f;
    [SerializeField] private AudioClip panelFallClip;

    [Header("Camara")]
    [SerializeField, Range(0.1f, 1f)] private float viewFill = 0.6f;
    [SerializeField] private float cameraMoveTime = 0.6f;

    [Header("Luz")]
    [SerializeField] private float lightIntensity = 0.2f;
    [SerializeField] private float lightRange = 1.5f;

    [Header("Al terminar")]
    // Segundos para ver el ultimo cable conectado antes de devolver la camara
    [SerializeField] private float endDelay = 0.8f;
    [SerializeField] private AudioClip repairedClip;
    public UnityEvent onRepaired;

    public bool IsRepaired { get; private set; }

    private Vector3 Front => -transform.right;

    private bool UsesPointer => player.pointer != null;

    private WiresMinigame minigame;
    private PlayerInteractor player;
    private Camera cam;
    private GameObject workLight;

    private bool busy;
    private bool playing;
    private bool panelDropped;

    private Vector3 cameraLocalPosition;
    private Quaternion cameraLocalRotation;

    // scripts del jugador que se apagan mientras dura el minijuego
    private readonly List<Behaviour> disabledWhilePlaying = new List<Behaviour>();
    // brazos y cuerpo del jugador, que taparian la camara al acercarla
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

    void Awake()
    {
        minigame = GetComponent<WiresMinigame>();
        minigame.OnCompleted += HandleCompleted;

        if (panel == null) panel = FindPanel();
        if (panel == null)
            Debug.LogWarning(name + ": falta asignar el panel del telefono.", this);
    }

    void OnDestroy()
    {
        if (minigame != null) minigame.OnCompleted -= HandleCompleted;
    }

    void Update()
    {
        if (!playing || PauseMenu.IsPaused) return;

        if (GameInput.ExitMinigame)
            StartCoroutine(CloseRoutine(false));
    }

    public void Open(PlayerInteractor interactor)
    {
        if (busy || IsRepaired) return;

        player = interactor;
        cam = interactor.GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        busy = true;
        LockPlayer();

        if (!panelDropped)
        {
            DropPanel();
            yield return new WaitForSeconds(fallTime);
        }

        Bounds bounds = minigame.StubsBounds;
        float distance = bounds.size.y / viewFill / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
        Vector3 viewPosition = bounds.center + Front * distance;

        SetWorkLight(viewPosition, true);
        if (UsesPointer)
        {
            yield return null;
        }
        else
        {
            cameraLocalPosition = cam.transform.localPosition;
            cameraLocalRotation = cam.transform.localRotation;
            HidePlayer();
            yield return MoveCamera(viewPosition, Quaternion.LookRotation(-Front, Vector3.up));

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        minigame.Begin(cam, player.pointer);
        playing = true;
    }

    private void HandleCompleted()
    {
        StartCoroutine(CloseRoutine(true));
    }

    private IEnumerator CloseRoutine(bool repaired)
    {
        playing = false;
        minigame.End();

        if (!UsesPointer)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (repaired)
        {
            IsRepaired = true;
            GetComponent<Interactable>().enabled = false;
            if (repairedClip != null) AudioSource.PlayClipAtPoint(repairedClip, transform.position);
            yield return new WaitForSeconds(endDelay);
        }

        if (!UsesPointer)
        {
            Transform parent = cam.transform.parent;
            yield return MoveCamera(parent.TransformPoint(cameraLocalPosition), parent.rotation * cameraLocalRotation);
            cam.transform.localPosition = cameraLocalPosition;
            cam.transform.localRotation = cameraLocalRotation;
        }

        SetWorkLight(Vector3.zero, false);
        ShowPlayer();
        UnlockPlayer();
        busy = false;

        if (repaired) onRepaired.Invoke();
    }

    private Transform FindPanel()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("panel", System.StringComparison.OrdinalIgnoreCase)) return t;
        return null;
    }

    private void DropPanel()
    {
        panelDropped = true;
        if (panel == null) return;

        // Sigue colgando del telefono para que, si el telefono se mueve (ej: FaintTeleport), la tapa vaya con el
        panel.SetParent(transform, true);
        BoxCollider box = AddBoxCollider(panel);

        Rigidbody rb = panel.gameObject.AddComponent<Rigidbody>();
        rb.mass = panelMass;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        IgnoreCollisions(box, GetComponentsInChildren<Collider>());
        IgnoreCollisions(box, player.transform.root.GetComponentsInChildren<Collider>());

        rb.AddForce(Front * panelPush, ForceMode.VelocityChange);
        rb.AddTorque(Vector3.Cross(Vector3.up, Front) * panelTumble, ForceMode.VelocityChange);

        if (panelFallClip != null) AudioSource.PlayClipAtPoint(panelFallClip, panel.position);

        StartCoroutine(RestPanel(rb, box));
    }

    private IEnumerator RestPanel(Rigidbody rb, Collider box)
    {
        yield return new WaitForSeconds(panelRestTime);

        // Ya en el suelo queda solo de decoracion, para no estorbar al jugador ni al rayo de interaccion
        Destroy(rb);
        Destroy(box);
    }

    private static void IgnoreCollisions(Collider collider, Collider[] others)
    {
        foreach (Collider other in others)
            if (other != collider) Physics.IgnoreCollision(collider, other);
    }

    private static BoxCollider AddBoxCollider(Transform root)
    {
        Bounds local = new Bounds();
        bool first = true;

        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
        {
            Bounds mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f));
                Vector3 point = root.InverseTransformPoint(filter.transform.TransformPoint(corner));

                if (first) local = new Bounds(point, Vector3.zero);
                else local.Encapsulate(point);
                first = false;
            }
        }

        BoxCollider box = root.gameObject.AddComponent<BoxCollider>();
        box.center = local.center;
        box.size = local.size;
        return box;
    }

    private IEnumerator MoveCamera(Vector3 position, Quaternion rotation)
    {
        Transform t = cam.transform;
        Vector3 startPosition = t.position;
        Quaternion startRotation = t.rotation;

        for (float time = 0f; time < cameraMoveTime; time += Time.deltaTime)
        {
            float blend = Mathf.SmoothStep(0f, 1f, time / cameraMoveTime);
            t.SetPositionAndRotation(Vector3.Lerp(startPosition, position, blend),
                                     Quaternion.Slerp(startRotation, rotation, blend));
            yield return null;
        }

        t.SetPositionAndRotation(position, rotation);
    }

    private void SetWorkLight(Vector3 position, bool on)
    {
        if (lightIntensity <= 0f) return;

        if (workLight == null)
        {
            workLight = new GameObject("Luz_Telefono");
            workLight.transform.SetParent(transform, false);

            Light pointLight = workLight.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.intensity = lightIntensity;
            pointLight.range = lightRange;
            pointLight.shadows = LightShadows.None;
        }

        if (on) workLight.transform.position = position;
        workLight.SetActive(on);
    }

    private void LockPlayer()
    {
        disabledWhilePlaying.Clear();
        DisableAll<MouseLook>();
        DisableAll<HeadBob>();
        DisableAll<PlayerMovement>();
        DisableAll<PlayerInteractor>();
        DisableAll<GraspObject>();

       
        Rigidbody playerRb = player.GetComponentInParent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.linearVelocity = new Vector3(0f, playerRb.linearVelocity.y, 0f);
            playerRb.angularVelocity = Vector3.zero;
        }
    }

    private void UnlockPlayer()
    {
        foreach (Behaviour b in disabledWhilePlaying)
        {
            if (b != null) b.enabled = true;
        }
        disabledWhilePlaying.Clear();
    }

    private void DisableAll<T>() where T : Behaviour
    {
        foreach (T b in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            if (!b.enabled) continue;
            b.enabled = false;
            disabledWhilePlaying.Add(b);
        }
    }

    private void HidePlayer()
    {
        hiddenRenderers.Clear();
        foreach (Renderer r in cam.transform.root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            r.enabled = false;
            hiddenRenderers.Add(r);
        }
    }

    private void ShowPlayer()
    {
        foreach (Renderer r in hiddenRenderers)
        {
            if (r != null) r.enabled = true;
        }
        hiddenRenderers.Clear();
    }

    // Texto temporal por si acaso
    void OnGUI()
    {
        if (!playing || PauseMenu.IsPaused || UsesPointer) return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20
        };

        Rect rect = new Rect(0f, Screen.height - 60f, Screen.width, 40f);
        GUI.Label(rect, "Une los cables del mismo color   [" + GameInput.InteractKeyName + "] Salir", style);
    }
}
