using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Contador de las piedras coleccionables (PiedraColeccionable) arriba a la derecha: un rombo y "2/4".
// Da un saltito y brilla al recoger una. Se esconde en el menu, al morir, leyendo o con la pausa abierta.
// Se crea solo al jugar.
public class ContadorPiedras : MonoBehaviour
{
    public Color colorGema = new Color(0.55f, 0.88f, 1f);

    CanvasGroup grupo;
    RectTransform caja, gema;
    Image imgGema, brillo;
    TextMeshProUGUI texto;
    float salto, alfa;
    int mostradas = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AlCargar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
        Crear();
    }

    static void AlCargarEscena(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Crear();

    static void Crear()
    {
        if (FindFirstObjectByType<ContadorPiedras>()) return;
        if (!FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include)) return;
        new GameObject("Contador de piedras").AddComponent<ContadorPiedras>();
    }

    void Start()
    {
        Canvas c = HUD.CrearCanvas("Contador de piedras (UI)", 60, transform);
        grupo = c.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        caja = HUD.Rect(c.transform, "Caja", new Vector2(1f, 1f), new Vector2(-24f, -22f), new Vector2(120f, 40f));
        var fondo = HUD.Imagen(caja, new Color(0f, 0f, 0f, 0.35f));
        fondo.raycastTarget = false;

        gema = HUD.Rect(caja, "Gema", new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(18f, 18f));
        gema.pivot = new Vector2(0.5f, 0.5f);
        gema.localRotation = Quaternion.Euler(0f, 0f, 45f);
        imgGema = HUD.Imagen(gema, colorGema);
        RectTransform b = HUD.Rect(gema, "Brillo", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
        brillo = HUD.Imagen(b, new Color(colorGema.r, colorGema.g, colorGema.b, 0f), HUD.Circulo());

        RectTransform zt = HUD.Rect(caja, "Texto", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(18f, 0f), new Vector2(-36f, 0f));
        texto = zt.gameObject.AddComponent<TextMeshProUGUI>();
        texto.fontSize = 24f;
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = new Color(0.92f, 0.95f, 1f);
        texto.raycastTarget = false;

        PiedraColeccionable.AlRecoger += AlRecoger;
    }

    void OnDestroy() => PiedraColeccionable.AlRecoger -= AlRecoger;

    void AlRecoger(int n) => salto = 1f;

    void Update()
    {
        if (grupo == null) return;
        int n = PiedraColeccionable.Recogidas;
        if (n != mostradas) { mostradas = n; texto.text = n + "/" + PiedraColeccionable.Total; }

        bool visible = !HUD.Oculto && !PantallaMuerte.Muerto && !Lectura.Abierta && !PauseMenu.IsPaused;
        alfa = Mathf.MoveTowards(alfa, visible ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        grupo.alpha = alfa;

        // Saltito y destello al recoger una; con las 4, la gema queda brillando
        salto = Mathf.MoveTowards(salto, 0f, Time.unscaledDeltaTime * 1.6f);
        float k = Mathf.Sin(salto * Mathf.PI);
        caja.localScale = Vector3.one * (1f + 0.25f * k);
        bool completas = n >= PiedraColeccionable.Total;
        float pulso = completas ? 0.35f + 0.2f * Mathf.Sin(Time.unscaledTime * 3f) : 0f;
        brillo.color = new Color(colorGema.r, colorGema.g, colorGema.b, Mathf.Max(salto * 0.9f, pulso));
        imgGema.color = Color.Lerp(colorGema * 0.75f + new Color(0f, 0f, 0f, 0.25f), Color.white, salto * 0.6f);
    }
}
