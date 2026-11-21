using UnityEngine;
using UnityEngine.UI;

// Barra de estamina: fina y redondeada, abajo al centro pero DEBAJO de los subtitulos de los dialogos (estos
// empiezan a 70 del borde; la barra va a 26) y mas tenue mientras hay un dialogo. Lee PlayerMovement.Estamina.
// Aparece al correr (sube un poco al entrar) y se esconde sola un momento despues de llenarse. Con poca estamina se pone
// naranja; agotado, roja y latiendo. Lo que se acaba de gastar se vacia con retraso (rastro).
// El HUD la crea sola; no hace falta ponerla en la escena.
public class EstaminaUI : MonoBehaviour
{
    const float Ancho = 260f, Alto = 6f, Abajo = 26f;

    static readonly Color Normal = new Color(0.93f, 0.91f, 0.87f, 0.9f);
    static readonly Color Baja = new Color(1f, 0.6f, 0.28f, 0.95f);
    static readonly Color Agotada = new Color(0.88f, 0.16f, 0.12f, 0.95f);

    Canvas canvas;
    CanvasGroup grupo;
    RectTransform barra, relleno, rastro, minimo;
    Image rellenoImg;
    PlayerMovement jugador;
    float rastroValor = 1f, visibleHasta, entrada;

    void Awake()
    {
        canvas = HUD.CrearCanvas("HUD Estamina", 42);
        canvas.transform.SetParent(transform, false);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;
        grupo = canvas.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        Sprite pildora = Pildora();
        barra = HUD.Rect(canvas.transform, "Barra", new Vector2(0.5f, 0f), new Vector2(0f, Abajo), new Vector2(Ancho, Alto));
        // Fondo con la forma de pildora; tambien recorta (Mask) el relleno para que tenga las puntas redondas
        Image fondo = HUD.Imagen(barra, new Color(0f, 0f, 0f, 0.45f), pildora);
        fondo.type = Image.Type.Sliced;
        barra.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        rastro = HUD.Rect(barra, "Rastro", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        HUD.Imagen(rastro, new Color(1f, 1f, 1f, 0.22f));
        relleno = HUD.Rect(barra, "Relleno", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        rellenoImg = HUD.Imagen(relleno, Normal);

        // Marca del minimo para volver a correr tras agotarse
        minimo = HUD.Rect(barra, "Minimo", new Vector2(0.3f, 0f), new Vector2(0.3f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1.5f, 0f));
        HUD.Imagen(minimo, new Color(0f, 0f, 0f, 0.5f));
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    // Rectangulo con las puntas redondas (se estira con 9-slice sin deformar las puntas)
    static Sprite pildoraCache;
    static Sprite Pildora()
    {
        if (pildoraCache != null) return pildoraCache;
        const int w = 32, h = 16;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[w * h];
        float r = h * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, w - r), cy = r;
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                px[y * w + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        pildoraCache = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, 0f, r, 0f));
        return pildoraCache;
    }

    void Update()
    {
        if (jugador == null)
        {
            Transform t = HUD.Jugador();
            if (t != null) jugador = t.GetComponent<PlayerMovement>();
            if (jugador == null) return;
        }

        float e = jugador.Estamina;
        relleno.anchorMax = new Vector2(e, 1f);
        rastroValor = e >= rastroValor ? e : Mathf.MoveTowards(rastroValor, e, Time.deltaTime * 0.6f);
        rastro.anchorMax = new Vector2(rastroValor, 1f);
        // El marcador del minimo sigue al valor configurado (solo se ve cuando importa: agotado)
        minimo.anchorMin = new Vector2(jugador.minimoTrasAgotarse, 0f);
        minimo.anchorMax = new Vector2(jugador.minimoTrasAgotarse, 1f);
        minimo.gameObject.SetActive(jugador.Agotado);

        if (jugador.Agotado)
        {
            float p = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 6f));
            rellenoImg.color = new Color(Agotada.r, Agotada.g, Agotada.b, p);
        }
        else rellenoImg.color = Color.Lerp(Baja, Normal, Mathf.InverseLerp(0.15f, 0.5f, e));

        bool ocupado = e < 0.999f || jugador.Corriendo;
        if (ocupado) visibleHasta = Time.time + 1.2f;
        bool mostrar = Time.time < visibleHasta && jugador.enabled && !HUD.Oculto && !PantallaMuerte.Muerto && !PauseMenu.IsPaused;
        float dt = Time.unscaledDeltaTime;
        // Con un dialogo en pantalla queda tenue (que no compita con el texto)
        bool hablando = DialogueManager.Instance != null && DialogueManager.Instance.IsActive;
        float objetivo = mostrar ? (hablando ? 0.45f : 1f) : 0f;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, objetivo, dt * (mostrar ? 5f : 2f));
        // Al aparecer sube un poquito (y vuelve a hacerlo si se escondio del todo)
        entrada = mostrar ? Mathf.MoveTowards(entrada, 1f, dt / 0.35f) : (grupo.alpha <= 0f ? 0f : entrada);
        barra.anchoredPosition = new Vector2(0f, Abajo + Mathf.LerpUnclamped(-10f, 0f, Jugo.SalidaCubica(entrada)));
    }
}
