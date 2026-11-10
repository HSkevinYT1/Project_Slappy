using UnityEngine;
using UnityEngine.UI;

// Caramelo con un agujero: con el en la mano, manteniendo el clic derecho (accion "Mirar") el jugador se lo
// acerca al ojo y mira a traves del agujero. La pantalla se oscurece salvo el agujero y aparecen las
// indicaciones ocultas de las paredes (MarcaOculta). Se puede caminar mientras se mira.
[RequireComponent(typeof(Grabbable))]
public class DulceVidente : MonoBehaviour
{
    [Header("Mirar")]
    [Tooltip("Segundos que tarda en llevarselo al ojo (y en bajarlo).")]
    public float transicion = 0.25f;
    [Tooltip("Tamano del agujero en pantalla (radio, en proporcion al alto de la pantalla).")]
    [Range(0.1f, 0.6f)] public float radioAgujero = 0.36f;
    [Tooltip("Color del borde del caramelo alrededor del agujero.")]
    public Color colorBorde = new Color(0.25f, 0.6f, 0.35f, 0.75f);
    public AudioClip sonidoMirar;

    [Header("Ayuda")]
    [Tooltip("Texto en pantalla mientras se tiene en la mano y todavia no se ha usado.")]
    public string ayuda = "Mantén clic derecho para mirar a través";
    [Tooltip("Luz que late para que se vea en el suelo; se apaga al recogerlo.")]
    public Light brillo;

    // true mientras se esta mirando a traves (otros scripts pueden consultarlo)
    public static bool Mirando { get; private set; }

    private Grabbable grab;
    private PlayerInteractor jugador;
    private float intensidadBrillo = -1f;
    private float k;
    private bool usado;
    private bool moviendo;
    private Vector3 manoPos;
    private Quaternion manoRot;
    private Renderer[] modelo;

    private Canvas canvas;
    private RawImage mascara;
    private Texture2D textura;

    void Awake()
    {
        grab = GetComponent<Grabbable>();
        modelo = GetComponentsInChildren<Renderer>();
    }

    void OnDisable()
    {
        Mirando = false;
        MarcaOculta.SetVisibilidad(0f);
        if (canvas != null) canvas.enabled = false;
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (textura != null) Destroy(textura);
    }

    bool EnMano()
    {
        if (jugador == null) jugador = FindFirstObjectByType<PlayerInteractor>();
        return jugador != null && jugador.Held == grab;
    }

    void Update()
    {
        bool enMano = EnMano();
        if (brillo != null)
        {
            brillo.enabled = !enMano;
            if (brillo.enabled)
            {
                if (intensidadBrillo < 0f) intensidadBrillo = brillo.intensity;
                brillo.intensity = intensidadBrillo * (0.65f + 0.35f * Mathf.Sin(Time.time * 2.2f));
            }
        }

        bool bloqueado = MenuPrincipal.Abierto || Time.timeScale <= 0f
                         || (DialogueManager.Instance != null && DialogueManager.Instance.IsActive);
        bool quiere = enMano && !bloqueado && GameInput.LookThrough;

        if (quiere && k <= 0f)
        {
            // Pose en la mano, para devolverlo despues
            manoPos = transform.localPosition;
            manoRot = transform.localRotation;
            moviendo = true;
            usado = true;
            if (sonidoMirar != null) AudioSource.PlayClipAtPoint(sonidoMirar, transform.position, 0.6f);
        }

        k = Mathf.MoveTowards(k, quiere ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, transicion));
        float s = Mathf.SmoothStep(0f, 1f, k);

        if (moviendo && enMano)
        {
            Camera cam = Camera.main;
            Transform padre = transform.parent;
            if (cam != null && padre != null)
            {
                // De la mano hasta delante del ojo; al final el caramelo se "funde" con la mascara
                Vector3 desde = padre.TransformPoint(manoPos);
                Vector3 hasta = cam.transform.position + cam.transform.forward * 0.12f;
                transform.position = Vector3.Lerp(desde, hasta, s);
                transform.rotation = Quaternion.Slerp(padre.rotation * manoRot, cam.transform.rotation, s);
            }
            foreach (Renderer r in modelo) if (r != null) r.enabled = s < 0.6f;
            if (k <= 0f)
            {
                transform.localPosition = manoPos;
                transform.localRotation = manoRot;
                moviendo = false;
            }
        }
        else if (moviendo)
        {
            // Lo soltaron mientras miraba
            foreach (Renderer r in modelo) if (r != null) r.enabled = true;
            moviendo = false;
            k = 0f;
            s = 0f;
        }

        Mirando = s > 0.5f;
        MarcaOculta.SetVisibilidad(Mathf.InverseLerp(0.4f, 1f, s));
        Mascara(s);
    }

    // Pantalla negra con el agujero del caramelo en el centro
    void Mascara(float s)
    {
        if (s <= 0f)
        {
            if (canvas != null) canvas.enabled = false;
            return;
        }
        if (canvas == null) CrearMascara();
        canvas.enabled = true;

        // La imagen cubre toda la pantalla; con el uvRect el agujero queda redondo y de radioAgujero del alto
        // (fuera de la textura se repite su borde, que es negro). Al acercarlo al ojo el agujero se cierra.
        float radio = radioAgujero * Screen.height * Mathf.Lerp(3f, 1f, s);
        float lado = 2f * radio / AgujeroTextura;                       // pixeles que ocupa la textura entera
        float w = Screen.width / lado, h = Screen.height / lado;
        mascara.uvRect = new Rect(0.5f - w * 0.5f, 0.5f - h * 0.5f, w, h);
        Color c = Color.white;
        c.a = Mathf.Clamp01(s * 1.5f);
        mascara.color = c;
    }

    void CrearMascara()
    {
        var go = new GameObject("Mascara Caramelo", typeof(RectTransform));
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        var img = new GameObject("Agujero", typeof(RectTransform));
        img.transform.SetParent(go.transform, false);
        mascara = img.AddComponent<RawImage>();
        mascara.raycastTarget = false;
        var r = mascara.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
        mascara.texture = textura = TexturaAgujero(colorBorde);
    }

    const float AgujeroTextura = 0.35f;   // radio del agujero en la textura (1 = medio lado)

    // Transparente dentro del agujero (radio 0.35), borde del caramelo con brillo y negro por fuera
    static Texture2D TexturaAgujero(Color borde)
    {
        const int n = 512;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;              // 0 centro, 1 borde del cuadrado
                Color c;
                if (d < 0.35f) c = new Color(0f, 0f, 0f, 0f);
                else if (d < 0.6f)
                {
                    float k = Mathf.InverseLerp(0.35f, 0.6f, d);
                    // Brillo del caramelo arriba a la izquierda
                    float luz = Mathf.Clamp01(Vector2.Dot(new Vector2(dx, dy).normalized, new Vector2(-0.6f, 0.8f))) * (1f - k) * 0.5f;
                    c = Color.Lerp(borde, Color.black, k * k);
                    c.r += luz; c.g += luz; c.b += luz;
                    c.a = Mathf.Lerp(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.39f, d)) * borde.a, 1f, k);
                }
                else c = Color.black;
                px[y * n + x] = c;
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    void OnGUI()
    {
        if (usado || string.IsNullOrEmpty(ayuda) || !EnMano() || MenuPrincipal.Abierto) return;
        var estilo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height * 0.025f) };
        estilo.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        GUI.Label(new Rect(0f, Screen.height * 0.85f, Screen.width, Screen.height * 0.06f), ayuda, estilo);
    }
}
