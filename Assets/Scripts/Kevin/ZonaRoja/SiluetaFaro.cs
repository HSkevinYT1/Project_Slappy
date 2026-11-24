using UnityEngine;

// La silueta de la Zona Roja (como Herobrine): la criatura de las notas ("muy alta, extremadamente delgada, con brazos
// que casi llegan al suelo"). Nunca ataca: solo se la ve A LO LEJOS, quieta entre los arboles, mirando al jugador.
// - Aparece cada tanto a 30-55 m, al costado de la vista (nunca justo delante), junto a un arbol y donde se la pueda ver.
// - Es negra: en la oscuridad solo se le ven los ojos; el cuerpo se ve cuando la barre la luz del faro.
// - Desaparece si el jugador se acerca, si la alumbra con la linterna, si se la queda mirando, o si aparta la vista
//   despues de haberla visto (al volver a mirar ya no esta). Despues vuelve a aparecer en otro lado.
// - Solo con el jugador en la Zona Roja, despues de haber entrado al faro, y hasta que se rellena el faro.
// La crea MisionFaro (MisionFaro.silueta); no hace falta ponerla en la escena.
public class SiluetaFaro : MonoBehaviour
{
    [Header("Apariciones")]
    public float distanciaMin = 30f, distanciaMax = 55f;
    [Tooltip("Mas cerca que esto desaparece")]
    public float desaparecerCerca = 22f;
    [Tooltip("Segundos (al azar) entre una aparicion y la siguiente")]
    public Vector2 entreApariciones = new Vector2(22f, 45f);
    [Tooltip("Segundos hasta la primera (despues de entrar al faro y volver a salir)")]
    public float primera = 14f;
    [Tooltip("Segundos maximos que se queda si nadie la ve")]
    public float duracionMax = 28f;
    [Tooltip("Segundos que aguanta que la miren antes de irse")]
    public float mirarMax = 2.2f;
    [Tooltip("Grados desde el centro de la vista en los que puede aparecer (min = nunca justo delante)")]
    public Vector2 anguloAparicion = new Vector2(18f, 70f);

    [Header("Aspecto")]
    public float altura = 3.1f;
    public Color colorCuerpo = new Color(0.035f, 0.035f, 0.04f);
    public Color colorOjos = new Color(0.9f, 0.9f, 0.85f);
    public float brilloOjos = 3f;

    [Header("Textos")]
    [TextArea] public string alVerla = "¿Qué... qué era eso? Era demasiado alto para ser una persona.";
    [TextArea] public string otraVez = "Otra vez... Me está siguiendo.";

    MisionFaro mision;
    Transform bosque, cuerpo, cabeza;
    Collider suelo;
    float ySuelo;
    float proxima, hasta, vista;
    bool visible, fueVista, salioDelFaro;
    int vecesVista;
    Transform jugador;
    VolumenZonas volumen;
    AudioSource fuente;
    static AudioClip sonido;

    public bool Visible => visible;

    public static SiluetaFaro Crear(MisionFaro m)
    {
        var go = new GameObject("Silueta (a lo lejos)");
        go.transform.SetParent(m.transform.parent, false);
        var s = go.AddComponent<SiluetaFaro>();
        s.mision = m;
        return s;
    }

    void Start()
    {
        Transform zr = mision != null ? mision.transform.parent : null;
        if (zr != null)
        {
            bosque = zr.Find("Bosque");
            Transform isla = zr.Find("Isla");
            if (isla != null) { suelo = isla.GetComponent<Collider>(); ySuelo = isla.position.y; }
        }
        Construir();
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.spatialBlend = 0f;
        fuente.playOnAwake = false;
        fuente.volume = 0.55f;
        Ocultar(false);
        proxima = float.MaxValue;
    }

    void Update()
    {
        if (mision == null || cuerpo == null) return;
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador == null) return; }
        Camera cam = Camera.main;
        if (cam == null) return;

        bool activa = EnZonaRoja() && mision.Entro && !mision.Relleno && !PantallaMuerte.Muerto;
        // La primera, un rato despues de salir del faro (no mientras sube las escaleras)
        if (activa && !salioDelFaro && mision.centroFaro != null && Plano(jugador.position - mision.centroFaro.position) > 14f)
        {
            salioDelFaro = true;
            proxima = Time.time + primera;
        }
        if (!activa || !salioDelFaro)
        {
            if (visible) Ocultar(false);
            return;
        }

        if (!visible)
        {
            if (Time.time >= proxima && !AgarreMuneca.Activo) { if (!Aparecer(cam)) proxima = Time.time + 3f; }
            return;
        }

        // Mirando siempre al jugador
        Vector3 haciaJ = jugador.position - cuerpo.position; haciaJ.y = 0f;
        if (haciaJ.sqrMagnitude > 0.01f) cuerpo.rotation = Quaternion.LookRotation(haciaJ.normalized);

        Vector3 c = cabeza.position;
        float d = Plano(cuerpo.position - jugador.position);
        bool enPantalla = EnPantalla(cam, c, 0.02f) && SeVe(cam.transform.position, c);
        // La "ve": en pantalla y alumbrada por el faro, o cerca del centro de la vista (los ojos)
        bool laVe = enPantalla && (Alumbrada(c) || Alumbrada(cuerpo.position + Vector3.up * altura * 0.5f) ||
                                   Vector3.Angle(cam.transform.forward, c - cam.transform.position) < 9f);
        if (laVe) vista += Time.deltaTime;
        if (!fueVista && vista > 0.45f)
        {
            fueVista = true;
            if (sonido == null) sonido = Sonido();
            fuente.PlayOneShot(sonido);
        }

        bool irse = d < desaparecerCerca || Linterna(c) || Time.time > hasta ||
                    (fueVista && (!enPantalla || vista > mirarMax));
        if (irse) Ocultar(fueVista);
    }

    bool Aparecer(Camera cam)
    {
        if (bosque == null || bosque.childCount == 0) return false;
        Vector3 fw = cam.transform.forward; fw.y = 0f;
        if (fw.sqrMagnitude < 0.001f) return false;
        fw.Normalize();
        Vector3 ojosJ = cam.transform.position;
        for (int intento = 0; intento < 40; intento++)
        {
            Transform arbol = bosque.GetChild(Random.Range(0, bosque.childCount));
            Vector3 dir = arbol.position - jugador.position; dir.y = 0f;
            float d = dir.magnitude;
            if (d < distanciaMin || d > distanciaMax) continue;
            float ang = Vector3.Angle(fw, dir);
            if (ang < anguloAparicion.x || ang > anguloAparicion.y) continue;
            // Al lado del arbol (asomandose), sobre el suelo de la isla y lejos del faro
            Vector3 lado = Vector3.Cross(Vector3.up, dir / d) * (Random.value < 0.5f ? -1.1f : 1.1f);
            Vector3 p = arbol.position + lado;
            if (!Suelo(p, out Vector3 s)) continue;
            if (mision.centroFaro != null && Plano(s - mision.centroFaro.position) < 15f) continue;
            // Que se le vea la cabeza desde donde esta el jugador
            if (!SeVe(ojosJ, s + Vector3.up * altura * 0.9f)) continue;

            cuerpo.position = s;
            Vector3 mira = jugador.position - s; mira.y = 0f;
            cuerpo.rotation = Quaternion.LookRotation(mira.normalized);
            cuerpo.gameObject.SetActive(true);
            visible = true;
            fueVista = false;
            vista = 0f;
            hasta = Time.time + duracionMax;
            return true;
        }
        return false;
    }

    void Ocultar(bool despuesDeVerla)
    {
        if (cuerpo != null) cuerpo.gameObject.SetActive(false);
        bool estaba = visible;
        visible = false;
        if (estaba && despuesDeVerla)
        {
            vecesVista++;
            if (vecesVista == 1 && !string.IsNullOrEmpty(alVerla)) Pensamiento.Decir("silueta.ver", 0.5f, alVerla);
            else if (vecesVista == 3 && !string.IsNullOrEmpty(otraVez)) Pensamiento.Decir("silueta.otra", 0.5f, otraVez);
        }
        proxima = Time.time + Random.Range(entreApariciones.x, entreApariciones.y);
    }

    // ------------------------------------------------------------------ Comprobaciones

    bool EnZonaRoja()
    {
        if (volumen == null) volumen = FindFirstObjectByType<VolumenZonas>();
        ZonaVolumen z = volumen != null ? volumen.ZonaActual : null;
        return z != null && mision != null && z.name == "Trigger " + mision.zona;
    }

    bool Suelo(Vector3 p, out Vector3 s)
    {
        s = p;
        RaycastHit h;
        bool hay = suelo != null
            ? suelo.Raycast(new Ray(p + Vector3.up * 40f, Vector3.down), out h, 100f)
            : Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out h, 100f, ~0, QueryTriggerInteraction.Ignore);
        if (!hay || h.normal.y < 0.7f) return false;
        if (suelo != null && h.point.y < ySuelo - 0.3f) return false;   // agua (lago, playa hundida)
        s = h.point;
        return true;
    }

    // Nada en medio (la silueta no tiene colisiones; el jugador tampoco cuenta)
    bool SeVe(Vector3 desde, Vector3 hasta)
    {
        Vector3 d = hasta - desde;
        float largo = d.magnitude;
        if (largo < 0.01f) return true;
        foreach (RaycastHit h in Physics.RaycastAll(desde, d / largo, largo - 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (jugador != null && h.collider.transform.IsChildOf(jugador.root)) continue;
            return false;
        }
        return true;
    }

    static bool EnPantalla(Camera cam, Vector3 p, float margen)
    {
        Vector3 v = cam.WorldToViewportPoint(p);
        return v.z > 0f && v.x > -margen && v.x < 1f + margen && v.y > -margen && v.y < 1f + margen;
    }

    // La barre el haz del faro
    bool Alumbrada(Vector3 p)
    {
        Light f = mision.foco;
        if (f == null || !f.enabled) return false;
        Vector3 d = p - f.transform.position;
        if (d.magnitude > f.range) return false;
        float medio = f.type == LightType.Spot ? f.spotAngle * 0.5f : 180f;
        return Vector3.Angle(f.transform.forward, d) < medio;
    }

    // La alumbra la linterna del jugador
    static bool Linterna(Vector3 p)
    {
        Flashlight f = Flashlight.EnMano;
        if (f == null || !f.Encendida) return false;
        Light haz = f.Haz;
        if (haz == null) return false;
        Vector3 d = p - haz.transform.position;
        if (d.magnitude > haz.range) return false;
        float medio = (haz.type == LightType.Spot ? haz.spotAngle : 60f) * 0.5f;
        return Vector3.Angle(haz.transform.forward, d) < medio * 0.8f;
    }

    static float Plano(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    // ------------------------------------------------------------------ Modelo (con primitivas)

    // Altisima y flaca, un poco encorvada, con los brazos colgando casi hasta el suelo y la cabeza ladeada
    void Construir()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var negro = new Material(lit) { name = "Silueta" };
        negro.SetColor("_BaseColor", colorCuerpo);
        negro.SetFloat("_Smoothness", 0f);
        negro.SetFloat("_Metallic", 0f);
        var ojos = new Material(lit) { name = "Silueta ojos" };
        ojos.SetColor("_BaseColor", colorOjos);
        ojos.SetFloat("_Smoothness", 0f);
        ojos.EnableKeyword("_EMISSION");
        ojos.SetColor("_EmissionColor", colorOjos * brilloOjos);
        ojos.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

        float k = altura / 3.1f;
        cuerpo = new GameObject("Cuerpo").transform;
        cuerpo.SetParent(transform, false);
        foreach (float x in new[] { -0.1f, 0.1f })
            Pieza("Pierna", PrimitiveType.Cylinder, new Vector3(x, 0.73f, 0f), new Vector3(0.12f, 0.73f, 0.12f), Vector3.zero, negro);
        Pieza("Cadera", PrimitiveType.Cube, new Vector3(0f, 1.47f, 0f), new Vector3(0.3f, 0.14f, 0.16f), Vector3.zero, negro);
        Pieza("Torso", PrimitiveType.Capsule, new Vector3(0f, 1.97f, 0.03f), new Vector3(0.3f, 0.55f, 0.17f), new Vector3(9f, 0f, 0f), negro);
        Pieza("Hombros", PrimitiveType.Cube, new Vector3(0f, 2.44f, 0.06f), new Vector3(0.46f, 0.08f, 0.13f), Vector3.zero, negro);
        Pieza("Cuello", PrimitiveType.Cylinder, new Vector3(0f, 2.6f, 0.1f), new Vector3(0.07f, 0.11f, 0.07f), new Vector3(18f, 0f, 0f), negro);
        cabeza = Pieza("Cabeza", PrimitiveType.Sphere, new Vector3(0f, 2.82f, 0.15f), new Vector3(0.21f, 0.3f, 0.23f), new Vector3(12f, 0f, 11f), negro).transform;
        // Los ojos, hijos de la cabeza (se ladean con ella)
        foreach (float x in new[] { -0.2f, 0.2f })
        {
            var o = Pieza("Ojo", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.2f, Vector3.zero, ojos).transform;
            o.SetParent(cabeza, false);
            // Un poco afuera de la cabeza (si no, quedan tapados) y grandes: a 40 m tienen que verse
            o.localPosition = new Vector3(x * 1.1f, 0.05f, 0.46f);
            o.localScale = new Vector3(0.34f, 0.17f, 0.14f);
        }
        // Brazos larguisimos, colgando casi hasta el piso, con dedos finos
        foreach (float lado in new[] { -1f, 1f })
        {
            Pieza("Brazo", PrimitiveType.Cylinder, new Vector3(0.24f * lado, 1.4f, 0.07f), new Vector3(0.075f, 1.05f, 0.075f), new Vector3(-3f, 0f, 4f * lado), negro);
            for (int i = 0; i < 3; i++)
                Pieza("Dedo", PrimitiveType.Cylinder, new Vector3((0.32f + 0.025f * (i - 1)) * lado, 0.2f, 0.06f + 0.02f * (i - 1)),
                      new Vector3(0.018f, 0.13f, 0.018f), new Vector3(-6f + 6f * (i - 1), 0f, 7f * lado), negro);
        }
        cuerpo.localScale = Vector3.one * k;
        cuerpo.gameObject.SetActive(false);
    }

    GameObject Pieza(string nombre, PrimitiveType tipo, Vector3 pos, Vector3 tam, Vector3 giro, Material m)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(cuerpo, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(giro);
        go.transform.localScale = tam;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    // Un zumbido grave y desafinado, muy bajo, que sube y se apaga (cuando el jugador la ve por primera vez)
    static AudioClip Sonido()
    {
        const int rate = 44100;
        const float largo = 3.2f;
        int n = Mathf.CeilToInt(rate * largo);
        var d = new float[n];
        var rnd = new System.Random(13);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Clamp01(t / 0.9f) * Mathf.Clamp01((largo - t) / 1.6f);
            float tono = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 58.3f * t) * 0.4f
                       + Mathf.Sin(2f * Mathf.PI * 82.4f * t + Mathf.Sin(t * 3f)) * 0.25f + Mathf.Sin(2f * Mathf.PI * 311f * t) * 0.05f * Mathf.Sin(t * 1.7f);
            lp += ((float)rnd.NextDouble() * 2f - 1f - lp) * 0.02f;
            d[i] = Mathf.Clamp((tono + lp * 1.5f) * env * 0.55f, -1f, 1f);
        }
        var c = AudioClip.Create("Silueta", n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }
}
