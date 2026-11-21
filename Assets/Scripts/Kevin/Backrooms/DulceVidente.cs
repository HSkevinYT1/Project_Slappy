using UnityEngine;
using UnityEngine.UI;

// La piedra de los Backrooms (antes un caramelo con agujero): con ella en la mano, manteniendo el clic derecho
// (accion "Mirar") el jugador se la acerca al ojo y mira a traves. La pantalla se oscurece salvo el agujero y
// aparecen las inscripciones ocultas de las paredes (MarcaOculta), como el mapa de las palancas.
// Se gasta mientras se mira (durabilidad): se agrieta y al final se apaga (se queda en el inventario).
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

    [Header("Desgaste")]
    [Tooltip("Segundos en total que se puede mirar a traves antes de que se deshaga (0 = no se gasta)")]
    public float durabilidad = 30f;
    [Tooltip("Grietas que van apareciendo sobre el agujero mientras se gasta")]
    [Range(0, 20)] public int grietas = 10;
    [TextArea] public string avisoGrietas = "Se está agrietando... no va a durar para siempre.";
    [TextArea] public string avisoCasiRota = "Se está deshaciendo en mis manos. Tengo que usarla con cuidado.";
    [TextArea] public string avisoRota = "Se apagó... ya no muestra nada.";

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

    // Desgaste: 0 nueva, 1 rota
    private float usoTotal;
    public float Desgaste => durabilidad > 0f ? Mathf.Clamp01(usoTotal / durabilidad) : 0f;
    private bool rota;
    private MaterialPropertyBlock bloque;
    private Color colorBase = Color.white, emisionBase = Color.black;
    private RectTransform contenedorGrietas;

    // Cada grieta es una linea quebrada (en radios del agujero, desde el borde hacia adentro) que se va abriendo
    struct Segmento { public Vector2 a, b; public float umbral; public Image img; }
    private Segmento[] segmentos;
    private int visiblesAntes;

    void Awake()
    {
        grab = GetComponent<Grabbable>();
        modelo = GetComponentsInChildren<Renderer>();
        bloque = new MaterialPropertyBlock();
        if (modelo.Length > 0 && modelo[0] != null && modelo[0].sharedMaterial != null)
        {
            Material m = modelo[0].sharedMaterial;
            if (m.HasProperty("_BaseColor")) colorBase = m.GetColor("_BaseColor");
            if (m.HasProperty("_EmissionColor")) emisionBase = m.GetColor("_EmissionColor");
        }
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
        if (rota) return;   // gastada (o apagandose): ya no deja mirar
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

        // Se gasta mientras se mira a traves
        if (Mirando && durabilidad > 0f && !rota)
        {
            float antes = Desgaste;
            usoTotal += Time.deltaTime;
            float ahora = Desgaste;
            if (antes < 0.45f && ahora >= 0.45f) Pensamiento.Decir("piedra.grietas", 0.2f, avisoGrietas);
            if (antes < 0.85f && ahora >= 0.85f) Pensamiento.Decir("piedra.casiRota", 0.2f, avisoCasiRota);
        }
        AspectoGastado();
        Mascara(s);
        if (Desgaste >= 1f && !rota) StartCoroutine(Romperse());
    }

    // En la mano se ve mas oscura y mas chica cuanto mas gastada esta
    void AspectoGastado()
    {
        float d = Desgaste;
        if (d <= 0f || rota) return;
        float k = d * d;
        foreach (Renderer r in modelo)
        {
            if (r == null) continue;
            r.GetPropertyBlock(bloque);
            bloque.SetColor("_BaseColor", Color.Lerp(colorBase, new Color(0.22f, 0.2f, 0.17f), k * 0.85f));
            bloque.SetColor("_EmissionColor", emisionBase * Mathf.Lerp(1f, 0.1f, k));
            r.SetPropertyBlock(bloque);
        }
    }

    // Se acabo: se cierra el agujero, se le caen unos pedazos y queda apagada (agrietada y oscura, ya no deja mirar).
    // Se queda en el inventario: en el guion el protagonista la guarda con las otras piedras.
    System.Collections.IEnumerator Romperse()
    {
        rota = true;
        Pensamiento.Decir("piedra.rota", 0.4f, avisoRota);
        // Se cierra lo que se estaba mirando
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            MarcaOculta.SetVisibilidad(1f - t / 0.35f);
            Mascara(1f - t / 0.35f);
            yield return null;
        }
        Mirando = false;
        MarcaOculta.SetVisibilidad(0f);
        if (canvas != null) canvas.enabled = false;
        Jugo.Sacudir(0.12f);

        // Vuelve a la mano (si estaba delante del ojo) y se le caen unos pedazos
        if (moviendo && transform.parent != null)
        {
            transform.localPosition = manoPos;
            transform.localRotation = manoRot;
        }
        moviendo = false;
        k = 0f;
        foreach (Renderer r in modelo) if (r != null) r.enabled = true;
        Pedazos(transform.position);

        // Apagada: oscura del todo y sin brillo
        foreach (Renderer r in modelo)
        {
            if (r == null) continue;
            r.GetPropertyBlock(bloque);
            bloque.SetColor("_BaseColor", new Color(0.17f, 0.16f, 0.14f));
            bloque.SetColor("_EmissionColor", Color.black);
            r.SetPropertyBlock(bloque);
        }
        if (brillo != null) brillo.enabled = false;
        usado = true;
    }

    public bool Agotada => rota;

    // Unos pedacitos con fisica que caen y desaparecen
    void Pedazos(Vector3 centro)
    {
        Material mat = modelo.Length > 0 && modelo[0] != null ? modelo[0].sharedMaterial : null;
        for (int i = 0; i < 7; i++)
        {
            var p = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
            p.name = "Pedazo de piedra";
            p.layer = 2;   // Ignore Raycast: no estorba al apuntar
            p.transform.position = centro + Random.insideUnitSphere * 0.05f;
            p.transform.rotation = Random.rotation;
            p.transform.localScale = Vector3.one * Random.Range(0.008f, 0.022f);
            var r = p.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.GetPropertyBlock(bloque);
            bloque.SetColor("_BaseColor", new Color(0.24f, 0.22f, 0.18f));
            bloque.SetColor("_EmissionColor", Color.black);
            r.SetPropertyBlock(bloque);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var rb = p.AddComponent<Rigidbody>();
            rb.mass = 0.01f;
            rb.linearVelocity = Random.insideUnitSphere * 0.8f + Vector3.down * 0.5f;
            rb.angularVelocity = Random.insideUnitSphere * 12f;
            Destroy(p, Random.Range(1.8f, 2.6f));
        }
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
        // Gastada, el agujero se va cerrando (se desmorona hacia adentro) y tiembla un poco
        float d = Desgaste;
        float radio = radioAgujero * Screen.height * Mathf.Lerp(3f, 1f, s) * Mathf.Lerp(1f, 0.72f, d * d);
        if (d > 0.6f) radio *= 1f + (Mathf.PerlinNoise(Time.time * 9f, 0.7f) - 0.5f) * 0.05f * (d - 0.6f) / 0.4f;
        Grietas(radio, s, d);
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

        // Grietas encima del agujero (centradas en la pantalla, en pixeles)
        var cont = new GameObject("Grietas", typeof(RectTransform));
        cont.transform.SetParent(go.transform, false);
        contenedorGrietas = (RectTransform)cont.transform;
        contenedorGrietas.anchorMin = contenedorGrietas.anchorMax = new Vector2(0.5f, 0.5f);
        contenedorGrietas.sizeDelta = Vector2.zero;
        CrearGrietas();
    }

    // Lineas quebradas que salen del borde del agujero hacia adentro; cada tramo tiene un "umbral" de desgaste
    // a partir del cual se ve, asi las grietas van creciendo a medida que se gasta
    void CrearGrietas()
    {
        var lista = new System.Collections.Generic.List<Segmento>();
        var rnd = new System.Random(GetInstanceID());
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        for (int g = 0; g < grietas; g++)
        {
            float umbral = Mathf.Lerp(0.12f, 0.9f, grietas > 1 ? g / (float)(grietas - 1) : 0f) + R(-0.04f, 0.04f);
            float ang = R(0f, Mathf.PI * 2f);
            Vector2 p = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R(0.98f, 1.08f);
            // Hacia el centro, con quiebres
            float dir = ang + Mathf.PI + R(-0.5f, 0.5f);
            int tramos = rnd.Next(4, 8);
            for (int t = 0; t < tramos; t++)
            {
                float largo = R(0.06f, 0.13f);
                dir += R(-0.6f, 0.6f);
                Vector2 q = p + new Vector2(Mathf.Cos(dir), Mathf.Sin(dir)) * largo;
                if (q.magnitude < 0.35f) break;   // no llegan hasta el centro
                lista.Add(new Segmento { a = p, b = q, umbral = umbral + t * 0.035f, img = Linea() });
                p = q;
            }
        }
        segmentos = lista.ToArray();
    }

    Image Linea()
    {
        var go = new GameObject("Grieta", typeof(RectTransform));
        go.transform.SetParent(contenedorGrietas, false);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.color = new Color(0.04f, 0.035f, 0.03f, 0.92f);
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        go.SetActive(false);
        return img;
    }

    void Grietas(float radio, float s, float d)
    {
        if (segmentos == null) return;
        float grosor = Mathf.Max(2f, Screen.height / 360f);
        int visibles = 0;
        foreach (Segmento g in segmentos)
        {
            bool ver = d > g.umbral;
            if (g.img.gameObject.activeSelf != ver) g.img.gameObject.SetActive(ver);
            if (!ver) continue;
            visibles++;
            // El tramo mas nuevo crece desde su punto de inicio
            float crecer = Mathf.Clamp01((d - g.umbral) / 0.03f);
            Vector2 a = g.a * radio, b = Vector2.Lerp(g.a, g.b, crecer) * radio;
            Vector2 v = b - a;
            RectTransform r = g.img.rectTransform;
            r.anchoredPosition = a;
            r.sizeDelta = new Vector2(v.magnitude, grosor);
            r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
            Color c = g.img.color;
            c.a = 0.92f * Mathf.Clamp01(s * 1.5f);
            g.img.color = c;
        }
        // Una grieta nueva: un tironcito
        if (visibles > visiblesAntes && Mirando) Jugo.Sacudir(0.05f);
        visiblesAntes = visibles;
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
        var estilo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height * 0.025f * HUD.Escala) };
        estilo.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        GUI.Label(new Rect(0f, Screen.height * 0.85f, Screen.width, Screen.height * 0.06f), ayuda, estilo);
    }
}
