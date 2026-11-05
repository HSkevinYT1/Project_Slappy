using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Muestra en el visor los Canvas pensados para pantalla (dialogos, pausa, bateria), que dentro
// de un visor no se ven. Los convierte al arrancar el nivel: las escenas siguen igual y la build
// normal no cambia. Flotan frente al jugador como una pantalla y sus botones se pulsan con el laser.
public class VRHud : MonoBehaviour
{
    private const float Distance = 1.2f;
    // Ancho en metros de la pantalla flotante a esa distancia
    private const float Width = 1.4f;
    // Lo mas cerca que puede quedar al acercarse a una pared
    private const float MinDistance = 0.4f;
    private const float FollowSpeed = 3f;

    private Transform head;
    private Transform pointer;
    private VRLaser laser;
    private Selectable hovered;

    public void Setup(Transform playerHead, Transform playerPointer)
    {
        head = playerHead;
        pointer = playerPointer;
        laser = pointer.GetComponent<VRLaser>();

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool isRoot = canvas.transform.parent == null || canvas.transform.parent.GetComponentInParent<Canvas>(true) == null;
            if (isRoot && canvas.renderMode == RenderMode.ScreenSpaceOverlay) Adopt(canvas);
        }
    }

    private void Adopt(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        bool scaled = scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize;
        Vector2 size = scaled ? scaler.referenceResolution : new Vector2(1920f, 1080f);

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = head.GetComponent<Camera>();

        RectTransform rect = (RectTransform)canvas.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition3D = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * (Width / size.x);
    }

    // Con unscaledDeltaTime y en LateUpdate para que siga funcionando en pausa, con timeScale en 0
    void LateUpdate()
    {
        float distance = Distance;
        if (Physics.Raycast(head.position, head.forward, out RaycastHit wall, Distance, ~0, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(wall.distance * 0.9f, MinDistance);

        float blend = 1f - Mathf.Exp(-FollowSpeed * Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, head.position + head.forward * distance, blend);
        transform.rotation = Quaternion.LookRotation(transform.position - head.position);

        // Al acercarse a una pared se achica para verse siempre del mismo tamano
        float scale = Vector3.Distance(transform.position, head.position) / Distance;
        transform.localScale = Vector3.one * scale;

        UpdatePointer();
    }

    private void UpdatePointer()
    {
        Ray ray = new Ray(pointer.position, pointer.forward);
        Selectable target = null;
        Vector3 point = Vector3.zero;

        foreach (Selectable selectable in Selectable.allSelectablesArray)
        {
            if (!selectable.transform.IsChildOf(transform) || !selectable.IsInteractable()) continue;

            RectTransform rect = (RectTransform)selectable.transform;
            if (!new Plane(rect.forward, rect.position).Raycast(ray, out float hit)) continue;

            Vector3 world = ray.GetPoint(hit);
            if (rect.rect.Contains(rect.InverseTransformPoint(world)))
            {
                target = selectable;
                point = world;
                break;
            }
        }

        if (laser != null) laser.EndOverride = target != null ? point : (Vector3?)null;
        if (target != hovered)
        {
            hovered = target;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(target != null ? target.gameObject : null);
        }

        if (target != null && GameInput.Interact)
            ExecuteEvents.Execute(target.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
    }
}
