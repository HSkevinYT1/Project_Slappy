using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Algo que se lee (el diario del faro, las notas): al interactuar se abre una hoja en pantalla con el texto.
// Mientras se lee el jugador no se mueve; se cierra con la tecla de interaccion, Esc o clic.
[RequireComponent(typeof(Interactable))]
public class Lectura : MonoBehaviour
{
    public string titulo = "Nota";
    [TextArea(4, 14)] public string texto = "";
    public string prompt = "Leer";
    [Tooltip("Color de la hoja")]
    public Color papel = new Color(0.86f, 0.8f, 0.66f);

    public UnityEvent alLeer;
    public bool Leida { get; private set; }
    public static bool Abierta { get; private set; }

    Interactable inter;

    void Awake()
    {
        inter = GetComponent<Interactable>();
        inter.SetPrompt(prompt);
        inter.onInteract.AddListener(_ => Leer());
    }

    public void Leer()
    {
        if (Abierta) return;
        StartCoroutine(Mostrar());
    }

    IEnumerator Mostrar()
    {
        Abierta = true;
        var apagados = new System.Collections.Generic.List<Behaviour>();
        void Apagar<T>() where T : Behaviour
        {
            foreach (T b in FindObjectsByType<T>(FindObjectsSortMode.None)) if (b.enabled) { b.enabled = false; apagados.Add(b); }
        }
        Apagar<PlayerMovement>();
        Apagar<MouseLook>();
        Apagar<HeadBob>();
        Apagar<PlayerInteractor>();
        Apagar<PauseMenu>();
        Transform j = HUD.Jugador();
        if (j != null && j.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        GameObject ui = CrearHoja(out CanvasGroup grupo, out RectTransform hoja);
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            float k = Jugo.SalidaCubica(t / 0.25f);
            grupo.alpha = k;
            hoja.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, k);
            hoja.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-4f, -1.2f, k));
            yield return null;
        }
        grupo.alpha = 1f;
        // Se espera a que suelte la tecla con la que la abrio
        yield return null;
        while (!(GameInput.Interact || GameInput.ExitMinigame || GameInput.Pause || Input.GetMouseButtonDown(0))) yield return null;

        if (!Leida) { Leida = true; alLeer?.Invoke(); }
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime) { grupo.alpha = 1f - t / 0.18f; yield return null; }
        Destroy(ui);
        // Un frame mas para que la misma tecla no vuelva a abrirla ni pause el juego
        yield return null;
        foreach (Behaviour b in apagados) if (b != null) b.enabled = true;
        Abierta = false;
    }

    GameObject CrearHoja(out CanvasGroup grupo, out RectTransform hoja)
    {
        var go = new GameObject("Lectura (" + titulo + ")");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 1f;
        grupo = go.AddComponent<CanvasGroup>();

        var fondo = Bloque(go.transform, "Fondo", new Color(0f, 0f, 0f, 0.62f));
        Estirar(fondo);

        hoja = Bloque(go.transform, "Hoja", papel);
        hoja.sizeDelta = new Vector2(760f, 860f);
        var sombra = hoja.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.5f);
        sombra.effectDistance = new Vector2(10f, -12f);

        Texto(hoja, titulo.ToUpperInvariant(), 38f, FontStyles.Bold, new Vector2(0f, 360f), new Vector2(640f, 70f), TextAlignmentOptions.Center);
        var cuerpo = Texto(hoja, texto, 28f, FontStyles.Italic, new Vector2(0f, -10f), new Vector2(640f, 640f), TextAlignmentOptions.TopLeft);
        cuerpo.lineSpacing = 12f;
        Texto(hoja, "[" + GameInput.InteractKeyName + "] cerrar", 20f, FontStyles.Normal, new Vector2(0f, -390f), new Vector2(640f, 40f), TextAlignmentOptions.Center)
            .color = new Color(0.25f, 0.2f, 0.15f, 0.7f);
        return go;
    }

    static RectTransform Bloque(Transform padre, string nombre, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        go.GetComponent<Image>().color = color;
        return (RectTransform)go.transform;
    }

    static void Estirar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI Texto(RectTransform padre, string s, float tam, FontStyles estilo, Vector2 pos, Vector2 tamano, TextAlignmentOptions alineado)
    {
        var go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = s;
        t.fontSize = tam;
        t.fontStyle = estilo;
        t.alignment = alineado;
        t.color = new Color(0.16f, 0.12f, 0.08f);
        t.textWrappingMode = TextWrappingModes.Normal;
        var r = (RectTransform)go.transform;
        r.anchoredPosition = pos;
        r.sizeDelta = tamano;
        return t;
    }
}
