using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

//efecto de desmayo y tp en las cabinas
public class FaintTeleport : MonoBehaviour
{
    [Header("Lugares")]
    [Tooltip("Lugar donde esta el jugador (ej: la cabina del sotano).")]
    [SerializeField] private Transform origin;
    [Tooltip("Lugar equivalente donde despierta (ej: la otra cabina).")]
    [SerializeField] private Transform destination;
    [Tooltip("Objetos que viajan con el jugador conservando su posicion relativa (ej: el telefono ya reparado).")]
    [SerializeField] private Transform[] carryAlong;
    [Tooltip("Objetos del destino que se ocultan porque los reemplazan los que viajan (ej: el telefono de la otra cabina).")]
    [SerializeField] private GameObject[] hideAtDestination;

    [Header("Tiempos")]
    [SerializeField] private float startDelay = 0.4f;
    // Mareo con parpadeos hasta cerrar los ojos del todo
    [SerializeField] private float faintTime = 3.2f;
    // Tiempo a oscuras y en silencio
    [SerializeField] private float blackTime = 2f;
    // Despertar: los ojos se abren poco a poco
    [SerializeField] private float wakeTime = 3f;

    [Header("Camara")]
    [SerializeField] private float swayAngle = 4f;
    [SerializeField] private float collapsePitch = 35f;
    [SerializeField] private float collapseRoll = 18f;
    [SerializeField] private float collapseDrop = 0.35f;

    [Header("Sonido")]
    [Tooltip("Ej: latidos o un pitido al marearse.")]
    [SerializeField] private AudioClip faintClip;
    [Tooltip("Ej: una respiracion al despertar.")]
    [SerializeField] private AudioClip wakeClip;

    public UnityEvent onArrived;

    public bool IsRunning { get; private set; }
    // Donde despierta el jugador (la otra cabina)
    public Transform Destino => destination;

    private Camera cam;
    private Transform body;
    private Rigidbody bodyRb;
    private CharacterController bodyController;
    private bool moveHead;

    private Vector3 headPosition;
    private Quaternion headRotation;

    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform topLid, topEdge, bottomLid, bottomEdge;
    private Image topEdgeImage, bottomEdgeImage, vignette, black;
    private AudioSource audioSource;
    private float savedVolume;

    // scripts del jugador que se apagan mientras dura la transicion
    private readonly List<Behaviour> disabled = new List<Behaviour>();

    public void Teleport()
    {
        if (IsRunning) return;
        if (origin == null || destination == null)
        {
            Debug.LogWarning(name + ": faltan el origen o el destino del teletransporte.", this);
            return;
        }

        PlayerInteractor interactor = FindFirstObjectByType<PlayerInteractor>();
        cam = interactor != null ? interactor.GetComponent<Camera>() : null;
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning(name + ": no se encontro la camara del jugador.", this);
            return;
        }

        bodyRb = cam.GetComponentInParent<Rigidbody>();
        bodyController = cam.GetComponentInParent<CharacterController>();
        body = bodyRb != null ? bodyRb.transform : bodyController != null ? bodyController.transform : cam.transform.root;
        moveHead = true;

        StartCoroutine(Run());
    }

    // Punto del destino que corresponde a uno del origen (lo usa PanelDebug)
    public Vector3 Equivalente(Vector3 enOrigen) => origin != null && destination != null ? Relocate(enOrigen) : enOrigen;

    // Sin desmayo (lo usa PanelDebug para saltarse el tutorial): el cuerpo del jugador se pone en "enOrigen" (dentro de
    // la cabina de origen) y de ahi pasa al sitio equivalente del destino, con lo que viaja con el; se esconde lo del
    // destino y se avisa la llegada (onArrived), todo ya
    public void TeleportInstant(Vector3 enOrigen)
    {
        if (IsRunning || origin == null || destination == null) return;
        PlayerInteractor interactor = FindFirstObjectByType<PlayerInteractor>();
        cam = interactor != null ? interactor.GetComponent<Camera>() : null;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        bodyRb = cam.GetComponentInParent<Rigidbody>();
        bodyController = cam.GetComponentInParent<CharacterController>();
        body = bodyRb != null ? bodyRb.transform : bodyController != null ? bodyController.transform : cam.transform.root;

        body.position = enOrigen;
        MoveEverything();
        onArrived.Invoke();
    }

    private IEnumerator Run()
    {
        IsRunning = true;
        LockPlayer();
        BuildOverlay();

        headPosition = cam.transform.localPosition;
        headRotation = cam.transform.localRotation;
        savedVolume = AudioListener.volume;

        yield return new WaitForSeconds(startDelay);
        if (faintClip != null) audioSource.PlayOneShot(faintClip);

        // Mareo: la vista se balancea y los ojos se cierran cada vez mas hasta desmayarse
        for (float t = 0f; t < faintTime; t += Time.deltaTime)
        {
            float k = t / faintTime;
            float lids = Mathf.Max(Blinks(k), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, k)));
            float collapse = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, k));

            SetLids(lids);
            SetVignette(Mathf.Lerp(0.1f, 0.9f, k));
            SetBlack(Mathf.InverseLerp(0.85f, 1f, k));
            Head(collapse, k, t);
            AudioListener.volume = savedVolume * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, k)));
            yield return null;
        }

        SetLids(1f);
        SetBlack(1f);
        Head(1f, 1f, faintTime);
        AudioListener.volume = 0f;

        // A oscuras: aqui ocurre el cambio de sitio
        yield return new WaitForFixedUpdate();
        MoveEverything();
        yield return new WaitForSeconds(blackTime);

        if (wakeClip != null) audioSource.PlayOneShot(wakeClip);

        // Despertar: los ojos se abren poco a poco, con un par de parpadeos pesados
        for (float t = 0f; t < wakeTime; t += Time.deltaTime)
        {
            float k = t / wakeTime;
            float open = Mathf.SmoothStep(0f, 1f, k);
            float lids = Mathf.Clamp01(1f - open + WakeFlutter(k));

            SetLids(lids);
            SetVignette(Mathf.Lerp(0.9f, 0f, k));
            SetBlack(1f - Mathf.InverseLerp(0f, 0.25f, k));
            Head(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1f, k)), 1f - k, t);
            AudioListener.volume = savedVolume * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.6f, k));
            yield return null;
        }

        AudioListener.volume = savedVolume;
        cam.transform.localPosition = headPosition;
        cam.transform.localRotation = headRotation;
        Destroy(canvas.gameObject);
        UnlockPlayer();
        IsRunning = false;

        onArrived.Invoke();
    }

    // Tres parpadeos cada vez mas largos durante el mareo (0 = abiertos, 1 = cerrados)
    private static float Blinks(float k)
    {
        return Blink(k, 0.12f, 0.06f, 0.55f) + Blink(k, 0.38f, 0.09f, 0.8f) + Blink(k, 0.62f, 0.13f, 0.95f);
    }

    // Al despertar los parpados todavia pesan y vuelven a caer un par de veces
    private static float WakeFlutter(float k)
    {
        return Blink(k, 0.35f, 0.1f, 0.55f) + Blink(k, 0.6f, 0.08f, 0.35f);
    }

    private static float Blink(float k, float at, float width, float depth)
    {
        float x = (k - at) / width;
        return x <= -1f || x >= 1f ? 0f : depth * (0.5f + 0.5f * Mathf.Cos(x * Mathf.PI));
    }

    // collapse: 0 de pie, 1 desplomado. sway: fuerza del balanceo
    private void Head(float collapse, float sway, float time)
    {
        if (!moveHead) return;

        float swayPitch = Mathf.Sin(time * 1.3f) * swayAngle * sway;
        float swayRoll = Mathf.Sin(time * 0.9f + 1f) * swayAngle * 1.5f * sway;
        Quaternion offset = Quaternion.Euler(collapse * collapsePitch + swayPitch, 0f, collapse * collapseRoll + swayRoll);

        cam.transform.localRotation = headRotation * offset;
        cam.transform.localPosition = headPosition + Vector3.down * (collapse * collapseDrop);
    }

    private void MoveEverything()
    {
        Vector3 position = Relocate(body.position);
        Quaternion rotation = Upright(Reorient(body.rotation));

        if (bodyController != null && bodyController.enabled)
        {
            bodyController.enabled = false;
            body.SetPositionAndRotation(position, rotation);
            bodyController.enabled = true;
        }
        else
        {
            body.SetPositionAndRotation(position, rotation);
        }

        if (bodyRb != null)
        {
            bodyRb.position = position;
            bodyRb.rotation = rotation;
            bodyRb.linearVelocity = Vector3.zero;
            bodyRb.angularVelocity = Vector3.zero;
        }

        foreach (Transform t in carryAlong)
        {
            if (t == null) continue;
            t.SetPositionAndRotation(Relocate(t.position), Reorient(t.rotation));
            foreach (Rigidbody rb in t.GetComponentsInChildren<Rigidbody>())
            {
                rb.position = rb.transform.position;
                rb.rotation = rb.transform.rotation;
                rb.linearVelocity = Vector3.zero;
            }
        }

        foreach (GameObject go in hideAtDestination)
            if (go != null) go.SetActive(false);

        Physics.SyncTransforms();
    }

    private Vector3 Relocate(Vector3 worldPosition)
    {
        return destination.TransformPoint(origin.InverseTransformPoint(worldPosition));
    }

    private Quaternion Reorient(Quaternion worldRotation)
    {
        return destination.rotation * (Quaternion.Inverse(origin.rotation) * worldRotation);
    }

    private static Quaternion Upright(Quaternion rotation)
    {
        Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
        return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up) : rotation;
    }

    private void LockPlayer()
    {
        disabled.Clear();
        DisableAll<MouseLook>();
        DisableAll<HeadBob>();
        DisableAll<PlayerMovement>();
        DisableAll<PlayerInteractor>();
        DisableAll<GraspObject>();

        if (bodyRb != null)
        {
            bodyRb.linearVelocity = new Vector3(0f, bodyRb.linearVelocity.y, 0f);
            bodyRb.angularVelocity = Vector3.zero;
        }
    }

    private void UnlockPlayer()
    {
        foreach (Behaviour b in disabled)
            if (b != null) b.enabled = true;
        disabled.Clear();
    }

    private void DisableAll<T>() where T : Behaviour
    {
        foreach (T b in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            if (!b.enabled) continue;
            b.enabled = false;
            disabled.Add(b);
        }
    }

    // Parpados, vineta y negro, pegados delante de la camara
    private void BuildOverlay()
    {
        GameObject root = new GameObject("Desmayo_Overlay", typeof(RectTransform));
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = cam.nearClipPlane + 0.01f;
        canvas.sortingOrder = 1000;
        canvasRect = (RectTransform)root.transform;

        vignette = CreateImage("Vineta", VignetteSprite());
        Stretch(vignette.rectTransform);

        topLid = CreateImage("Parpado_Arriba", null).rectTransform;
        topEdgeImage = CreateImage("Parpado_Arriba_Borde", GradientSprite(true));
        topEdge = topEdgeImage.rectTransform;
        bottomLid = CreateImage("Parpado_Abajo", null).rectTransform;
        bottomEdgeImage = CreateImage("Parpado_Abajo_Borde", GradientSprite(false));
        bottomEdge = bottomEdgeImage.rectTransform;

        PinTo(topLid, 1f);
        PinTo(topEdge, 1f);
        PinTo(bottomLid, 0f);
        PinTo(bottomEdge, 0f);

        black = CreateImage("Negro", null);
        Stretch(black.rectTransform);

        SetLids(0f);
        SetVignette(0f);
        SetBlack(0f);

        audioSource = root.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
    }

    private Image CreateImage(string imageName, Sprite sprite)
    {
        GameObject go = new GameObject(imageName, typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.black;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Ancla un parpado al borde de arriba (1) o de abajo (0) de la pantalla, a lo ancho
    private static void PinTo(RectTransform rect, float edgeY)
    {
        rect.anchorMin = new Vector2(0f, edgeY);
        rect.anchorMax = new Vector2(1f, edgeY);
        rect.pivot = new Vector2(0.5f, edgeY);
        rect.sizeDelta = Vector2.zero;
    }

    // closed: 0 ojos abiertos, 1 cerrados
    private void SetLids(float closed)
    {
        float height = canvasRect.rect.height;
        float edge = height * 0.18f;
        float solid = closed * (height * 0.5f + edge * 0.5f);
        float edgeAlpha = Mathf.Clamp01(closed * 5f);

        topLid.sizeDelta = new Vector2(0f, solid);
        topEdge.sizeDelta = new Vector2(0f, edge);
        topEdge.anchoredPosition = new Vector2(0f, -solid);

        bottomLid.sizeDelta = new Vector2(0f, solid);
        bottomEdge.sizeDelta = new Vector2(0f, edge);
        bottomEdge.anchoredPosition = new Vector2(0f, solid);

        topEdgeImage.color = new Color(0f, 0f, 0f, edgeAlpha);
        bottomEdgeImage.color = new Color(0f, 0f, 0f, edgeAlpha);
    }

    private void SetVignette(float amount)
    {
        vignette.color = new Color(0f, 0f, 0f, Mathf.Clamp01(amount));
    }

    private void SetBlack(float amount)
    {
        black.color = new Color(0f, 0f, 0f, Mathf.Clamp01(amount));
    }

    // Degradado vertical, el borde blando del parpado: opaco del lado del parpado y transparente hacia el centro
    private static Sprite GradientSprite(bool opaqueTop)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(1, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        {
            float v = y / (size - 1f);
            float a = Mathf.SmoothStep(0f, 1f, opaqueTop ? v : 1f - v);
            texture.SetPixel(0, y, new Color(1f, 1f, 1f, a));
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 1, size), new Vector2(0.5f, 0.5f));
    }

    // Oscuro en las esquinas y transparente en el centro
    private static Sprite VignetteSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x / (size - 1f), y / (size - 1f)) * 2f - Vector2.one;
                float a = Mathf.SmoothStep(0.35f, 1.35f, p.magnitude);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
