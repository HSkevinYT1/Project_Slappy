using System;
using System.Collections.Generic;
using UnityEngine;

//minijuego de unir cables
public class WiresMinigame : MonoBehaviour
{
    private const string StubNamePart = "cable_";
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private class Stub
    {
        public Renderer renderer;
        // Punta libre, donde se engancha el cable nuevo
        public Vector3 tip;
        public float thickness;
        public bool connected;

        public Material Material => renderer.sharedMaterial;
    }

    [Header("Cables")]
    // Grosor del cable nuevo respecto al grosor de los trozos del modelo
    [SerializeField, Range(0.1f, 1f)] private float wireThickness = 0.4f;
 
    [SerializeField] private float pickMargin = 0.015f;
    // Cuanto se aclara el trozo al pasar el mouse por encima
    [SerializeField, Range(0f, 1f)] private float hoverBrighten = 0.45f;

    [Header("Sonidos")]
    [SerializeField] private AudioClip connectClip;
    [Tooltip("Si hay, al conectar suena uno de estos al azar (en lugar de Connect Clip)")]
    [SerializeField] private AudioClip[] connectClips;

    // Caja que envuelve todos los trozos: la camara la usa para encuadrar el minijuego
    public Bounds StubsBounds { get; private set; }
    // Minijuego en curso (null si no hay ninguno) y punto al que apunta el jugador
    public static WiresMinigame Active { get; private set; }
    public Vector3 PointerPoint { get; private set; }
    public bool IsComplete { get; private set; }

    // Se lanza al conectar el ultimo cable
    public event Action OnCompleted;

    private readonly List<Stub> left = new List<Stub>();
    private readonly List<Stub> right = new List<Stub>();
    private readonly List<Stub> stubs = new List<Stub>();

    // El frente del modelo mira hacia -X local
    private Vector3 Front => -transform.right;

    private MaterialPropertyBlock block;
    private Camera cam;
    private Transform pointer;
    private bool playing;
    private int connectedPairs;

    private Stub dragFrom;
    private Transform dragWire;
    private Stub highlighted;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        FindStubs();
        ShuffleColors();
    }

    void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    void Update()
    {
        if (!playing || PauseMenu.IsPaused) return;

        Ray ray = PointerRay();
        PointerPoint = PointOnPlane(ray, StubsBounds.center);

        Stub hovered = StubUnderPointer(ray);

        if (dragFrom == null)
        {
            SetHighlighted(hovered);

            if (hovered != null && GameInput.DragStarted)
                StartDrag(hovered);
            return;
        }

        // Arrastrando, solo se resalta el trozo donde de verdad se puede conectar
        SetHighlighted(CanConnect(hovered) ? hovered : null);
        Vector3 from = dragFrom.tip + LayerOffset(dragFrom);
        StretchWire(dragWire, from, PointOnPlane(ray, from), dragFrom.thickness);

        if (GameInput.DragReleased)
        {
            if (CanConnect(hovered)) Connect(hovered);
            else CancelDrag();
        }
    }

    // playerPointer: con que se apunta si no es el cursor del mouse. Vacio = el cursor
    public void Begin(Camera playerCamera, Transform playerPointer)
    {
        cam = playerCamera;
        pointer = playerPointer;
        playing = !IsComplete;
        if (playing) Active = this;
    }

    public void End()
    {
        playing = false;
        CancelDrag();
        SetHighlighted(null);
        if (Active == this) Active = null;
    }

    private void FindStubs()
    {
        List<Renderer> renderers = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
            if (r.name.Contains(StubNamePart)) renderers.Add(r);

        if (renderers.Count == 0)
        {
            Debug.LogWarning(name + ": no se encontraron cables (hijos con '" + StubNamePart + "' en el nombre).", this);
            return;
        }

        Bounds all = renderers[0].bounds;
        foreach (Renderer r in renderers) all.Encapsulate(r.bounds);
        StubsBounds = all;

        foreach (Renderer r in renderers)
        {
            Bounds mesh = r.GetComponent<MeshFilter>().sharedMesh.bounds;

            // Cada trozo es un cilindro
            Vector3 axis = LongestAxis(mesh.size);
            Vector3 endA = r.transform.TransformPoint(mesh.center + Vector3.Scale(axis, mesh.extents));
            Vector3 endB = r.transform.TransformPoint(mesh.center - Vector3.Scale(axis, mesh.extents));
            bool aIsTip = (endA - all.center).sqrMagnitude < (endB - all.center).sqrMagnitude;

            Stub stub = new Stub
            {
                renderer = r,
                tip = aIsTip ? endA : endB,
                thickness = Mathf.Min(mesh.size.x, mesh.size.y, mesh.size.z) * r.transform.lossyScale.x
            };

            bool isLeft = Vector3.Dot(r.bounds.center - all.center, r.transform.TransformDirection(axis)) > 0f;
            (isLeft ? left : right).Add(stub);
            stubs.Add(stub);
        }

        left.Sort((a, b) => b.tip.y.CompareTo(a.tip.y));
        right.Sort((a, b) => b.tip.y.CompareTo(a.tip.y));
    }

    private static Vector3 LongestAxis(Vector3 size)
    {
        if (size.x >= size.y && size.x >= size.z) return Vector3.right;
        return size.y >= size.z ? Vector3.up : Vector3.forward;
    }

    // Reparte los colores del modelo al azar para que cada partida sea distinta
    private void ShuffleColors()
    {
        List<Material> colors = new List<Material>();
        foreach (Stub stub in stubs)
            if (!colors.Contains(stub.Material)) colors.Add(stub.Material);

        if (left.Count != right.Count || colors.Count != left.Count)
        {
            Debug.LogWarning(name + ": se esperaba un color por cada par de cables (" + left.Count + " y " + right.Count
                             + " cables, " + colors.Count + " colores). Se dejan como estan.", this);
            return;
        }

        int[] order = new int[colors.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;

        // Se repite hasta que no quede ningun cable justo enfrente del suyo... si hay con que cruzarlos
        int attempts = 0;
        do
        {
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            attempts++;
        }
        while (attempts < 50 && HasStraightPair(order));

        for (int i = 0; i < order.Length; i++)
        {
            left[i].renderer.sharedMaterial = colors[i];
            right[i].renderer.sharedMaterial = colors[order[i]];
        }
    }

    private static bool HasStraightPair(int[] order)
    {
        if (order.Length < 2) return false;

        for (int i = 0; i < order.Length; i++)
            if (order[i] == i) return true;
        return false;
    }

    private Ray PointerRay()
    {
        if (pointer != null) return new Ray(pointer.position, pointer.forward);
        return cam.ScreenPointToRay(GameInput.MousePosition);
    }

    private Stub StubUnderPointer(Ray ray)
    {
        Stub closest = null;
        float closestDistance = float.MaxValue;

        foreach (Stub stub in stubs)
        {
            if (stub.connected) continue;

            Bounds bounds = stub.renderer.bounds;
            bounds.Expand(pickMargin * 2f);
            if (!bounds.IntersectRay(ray)) continue;

            // Distancia entre el rayo y el centro del trozo
            float distance = Vector3.Cross(ray.direction, bounds.center - ray.origin).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = stub;
            }
        }

        return closest;
    }

    // Punto al que apunta el rayo a la misma profundidad que la punta del cable
    private Vector3 PointOnPlane(Ray ray, Vector3 through)
    {
        Plane plane = new Plane(Front, through);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : through;
    }

    private bool CanConnect(Stub target)
    {
        return target != null && target != dragFrom && target.Material == dragFrom.Material;
    }

    private void StartDrag(Stub from)
    {
        dragFrom = from;
        dragWire = CreateWire(from.Material);
    }

    private void CancelDrag()
    {
        if (dragWire != null) Destroy(dragWire.gameObject);
        dragWire = null;
        dragFrom = null;
    }

    private void Connect(Stub target)
    {
        Vector3 offset = LayerOffset(dragFrom);
        StretchWire(dragWire, dragFrom.tip + offset, target.tip + offset, dragFrom.thickness);
        dragFrom.connected = true;
        target.connected = true;
        dragWire = null;
        dragFrom = null;
        SetHighlighted(null);

        AudioClip clipCable = connectClips != null && connectClips.Length > 0 ? connectClips[UnityEngine.Random.Range(0, connectClips.Length)] : connectClip;
        if (clipCable != null) AudioSource.PlayClipAtPoint(clipCable, target.tip);

        connectedPairs++;
        if (connectedPairs < left.Count) return;

        IsComplete = true;
        playing = false;
        OnCompleted?.Invoke();
    }

    private Vector3 LayerOffset(Stub stub)
    {
        float layer = connectedPairs - (left.Count - 1) * 0.5f;
        return Front * (layer * stub.thickness * wireThickness * 1.05f);
    }

    private Transform CreateWire(Material material)
    {
        GameObject wire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wire.name = "Cable_Unido";
        Destroy(wire.GetComponent<Collider>());
        wire.GetComponent<Renderer>().sharedMaterial = material;
        wire.transform.SetParent(transform, false);
        return wire.transform;
    }

    private void StretchWire(Transform wire, Vector3 from, Vector3 to, float thickness)
    {
        Vector3 delta = to - from;
        wire.position = (from + to) * 0.5f;
        wire.up = delta;

        // El cilindro de Unity mide 2 de alto y 1 de diametro. El Telefono tiene escala uniforme
        float parentScale = transform.lossyScale.x;
        float width = thickness * wireThickness / parentScale;
        wire.localScale = new Vector3(width, delta.magnitude * 0.5f / parentScale, width);
    }

    private void SetHighlighted(Stub stub)
    {
        if (stub == highlighted) return;

        if (highlighted != null) highlighted.renderer.SetPropertyBlock(null);
        highlighted = stub;
        if (stub == null || !stub.Material.HasProperty(BaseColor)) return;

        // Con un MaterialPropertyBlock no se crean copias del material
        block.SetColor(BaseColor, Color.Lerp(stub.Material.GetColor(BaseColor), Color.white, hoverBrighten));
        stub.renderer.SetPropertyBlock(block);
    }
}
