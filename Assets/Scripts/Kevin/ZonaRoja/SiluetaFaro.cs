using System.Collections.Generic;
using UnityEngine;

// La aparicion de la Zona Roja (como Herobrine): una copia QUIETA del enemigo 4 (Slappy) que se ve solo de reojo,
// en los BORDES de la pantalla, cuando la luz del faro pasa por donde esta. Nunca ataca.
// - Cada tanto busca un sitio a 25-60 m que este en el borde izquierdo o derecho de la vista y por donde este pasando
//   el haz del faro en ese momento: aparece ahi, mirando fijamente al jugador.
// - Cuando la luz se va de ese sitio, desaparece (en la siguiente pasada ya no esta). Tambien si el jugador se acerca,
//   si la alumbra con la linterna o si se la queda mirando demasiado.
// - Solo con el jugador en la Zona Roja (en la isla o despues de entrar al faro) y hasta que se rellena el faro.
// - Si no encuentra al enemigo 4, usa una silueta altisima y negra hecha con primitivas.
// La crea MisionFaro (MisionFaro.silueta); no hace falta ponerla en la escena.
public class SiluetaFaro : MonoBehaviour
{
    [Header("Apariciones")]
    public float distanciaMin = 25f, distanciaMax = 60f;
    [Tooltip("Mas cerca que esto desaparece")]
    public float desaparecerCerca = 18f;
    [Tooltip("Segundos (al azar) entre una aparicion y la siguiente")]
    public Vector2 entreApariciones = new Vector2(12f, 28f);
    [Tooltip("Segundos hasta la primera (despues de salir del faro a la isla)")]
    public float primera = 6f;
    [Tooltip("Segundos que aguanta que la miren antes de irse")]
    public float mirarMax = 2.5f;
    [Tooltip("Borde de la pantalla donde aparece (0.05-0.2 = el 5% al 20% desde el borde izquierdo o derecho)")]
    public Vector2 borde = new Vector2(0.04f, 0.2f);
    [Tooltip("Segundos que sigue despues de que la luz se fue de su sitio (enseguida desaparece)")]
    public float graciaLuz = 0.15f;

    public static SiluetaFaro Instance { get; private set; }

    [Header("Aspecto (si no encuentra al enemigo 4)")]
    public float altura = 3.1f;
    public Color colorCuerpo = new Color(0.035f, 0.035f, 0.04f);
    public Color colorOjos = new Color(0.9f, 0.9f, 0.85f);
    public float brilloOjos = 3f;

    [Header("Textos")]
    [TextArea] public string alVerla = "¿Qué... qué era eso? Estaba ahí, en la luz, mirándome.";
    [TextArea] public string otraVez = "Otra vez... Me está siguiendo.";

    MisionFaro mision;
    Transform bosque, cuerpo, cabeza;
    Collider suelo;
    float ySuelo, altoCuerpo;
    float proxima, vista, sinLuz, proximoBuscar;
    bool visible, fueVista, salioDelFaro, iluminada;
    int vecesVista, fallos;
    Transform jugador;
    VolumenZonas volumen;
    AudioSource fuente;
    static AudioClip sonido;

    public bool Visible => visible;

    public static SiluetaFaro Crear(MisionFaro m)
    {
        var go = new GameObject("Aparicion (enemigo 4, a lo lejos)");
        go.transform.SetParent(m.transform.parent, false);
        var s = go.AddComponent<SiluetaFaro>();
        s.mision = m;
        return s;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // PanelDebug: que aparezca ya en el borde de la vista (aunque la luz no este pasando por ahi)
    public bool Forzar()
    {
        Camera cam = Camera.main;
        if (cam == null || cuerpo == null) return false;
        if (jugador == null) jugador = HUD.Jugador();
        if (jugador == null) return false;
        salioDelFaro = true;
        if (visible) Ocultar(false);
        return Aparecer(cam, false);
    }

    void Start()
    {
        Instance = this;
        Transform zr = mision != null ? mision.transform.parent : null;
        if (zr != null)
        {
            bosque = zr.Find("Bosque");
            Transform isla = zr.Find("Isla");
            if (isla != null) { suelo = isla.GetComponent<Collider>(); ySuelo = isla.position.y; }
        }
        if (!CopiarEnemigo(zr)) Construir();
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

        // En la isla (abajo, en el suelo) cuenta aunque no haya entrado por el faro (ej: llego con el menu debug)
        bool enLaIsla = mision.centroFaro != null && jugador.position.y < mision.centroFaro.position.y - 8f;
        bool activa = EnZonaRoja() && (mision.Entro || enLaIsla) && !mision.Relleno && !PantallaMuerte.Muerto;
        if (activa && !salioDelFaro && mision.centroFaro != null &&
            (Plano(jugador.position - mision.centroFaro.position) > 12f || (enLaIsla && Plano(jugador.position - mision.centroFaro.position) > 9.5f)))
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
            // Busca (varias veces por segundo) un sitio en el borde de la vista por donde este pasando la luz
            if (Time.time >= proxima && Time.time >= proximoBuscar && !AgarreMuneca.Activo)
            {
                proximoBuscar = Time.time + 0.2f;
                Aparecer(cam, true);
            }
            return;
        }

        // Mirando fijamente al jugador
        Vector3 haciaJ = jugador.position - cuerpo.position; haciaJ.y = 0f;
        if (haciaJ.sqrMagnitude > 0.01f) cuerpo.rotation = Quaternion.LookRotation(haciaJ.normalized);

        Vector3 c = cabeza != null ? cabeza.position : cuerpo.position + Vector3.up * altoCuerpo * 0.9f;
        Vector3 pecho = cuerpo.position + Vector3.up * altoCuerpo * 0.55f;
        float d = Plano(cuerpo.position - jugador.position);
        bool enPantalla = EnPantalla(cam, c, 0.02f) && (SeVe(cam.transform.position, c) || SeVe(cam.transform.position, pecho));
        bool luz = Alumbrada(c) || Alumbrada(pecho);
        if (iluminada) sinLuz = luz ? 0f : sinLuz + Time.deltaTime;
        if (enPantalla && (luz || !iluminada)) vista += Time.deltaTime;
        if (!fueVista && vista > 0.3f)
        {
            fueVista = true;
            if (sonido == null) sonido = Sonido();
            fuente.PlayOneShot(sonido);
        }

        // Se va cuando la luz deja su sitio (o si se acerca, la alumbra o la mira demasiado)
        bool irse = (iluminada && sinLuz > graciaLuz) || d < desaparecerCerca || Linterna(c) || vista > mirarMax;
        if (irse) Ocultar(fueVista);
    }

    // conLuz: solo donde el haz del faro esta pasando ahora (si no, en cualquier sitio del borde: el menu debug)
    bool Aparecer(Camera cam, bool conLuz)
    {
        Vector3 ojosJ = cam.transform.position;
        int sinSuelo = 0, sinFaro = 0, tapada = 0, cerca = 0, probados = 0;
        for (int i = 0; i < 24; i++)
        {
            probados++;
            // Un punto en el borde izquierdo o derecho de la vista, a la altura del horizonte
            float x = Random.value < 0.5f ? Random.Range(borde.x, borde.y) : 1f - Random.Range(borde.x, borde.y);
            Ray rayo = cam.ViewportPointToRay(new Vector3(x, Random.Range(0.4f, 0.6f), 0f));
            Vector3 dir = rayo.direction; dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) continue;
            Vector3 p = jugador.position + dir.normalized * Random.Range(distanciaMin, distanciaMax);
            if (!Suelo(p, out Vector3 s)) { sinSuelo++; continue; }
            if (mision.centroFaro != null && Plano(s - mision.centroFaro.position) < 15f) { cerca++; continue; }
            Vector3 cab = s + Vector3.up * altoCuerpo * 0.9f, pecho = s + Vector3.up * altoCuerpo * 0.55f;
            if (conLuz && !Alumbrada(cab) && !Alumbrada(pecho)) { sinFaro++; continue; }
            if (Linterna(cab)) continue;
            if (!SeVe(ojosJ, cab) && !SeVe(ojosJ, pecho)) { tapada++; continue; }

            cuerpo.position = s;
            Vector3 mira = jugador.position - s; mira.y = 0f;
            cuerpo.rotation = Quaternion.LookRotation(mira.normalized);
            cuerpo.gameObject.SetActive(true);
            visible = true;
            iluminada = conLuz;
            fueVista = false;
            vista = 0f;
            sinLuz = 0f;
            fallos = 0;
            Debug.Log($"[Silueta] Aparece en el borde de la vista, a {Plano(s - jugador.position):0} m{(conLuz ? ", en la luz del faro" : "")}.", this);
            return true;
        }
        // Sin luz por ahi es lo normal (la luz pasa cada tanto); avisa solo cuando falla por otras cosas
        if (++fallos % 150 == 0)
            Debug.Log($"[Silueta] Todavia no encontro donde aparecer ({probados} sitios: sin luz del faro {sinFaro}, sin suelo {sinSuelo}, " +
                      $"cerca del faro {cerca}, tapada {tapada}). Sigue buscando.", this);
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

    // Nada en medio (la aparicion no tiene colisiones; el jugador tampoco cuenta)
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

    // La barre el haz del faro ahora mismo (prendido y no en el sector oscuro)
    bool Alumbrada(Vector3 p)
    {
        Light f = mision.foco;
        if (f == null || !f.enabled || f.intensity <= 0f) return false;
        Vector3 d = p - f.transform.position;
        if (d.magnitude > f.range) return false;
        float medio = f.type == LightType.Spot ? f.spotAngle * 0.5f : 180f;
        return Vector3.Angle(f.transform.forward, d) < medio * 0.9f;
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

    // ------------------------------------------------------------------ Modelo

    // Una copia quieta del enemigo 4 (la pose que se ve al caminar, con sus ojos), del mismo tamaño
    bool CopiarEnemigo(Transform zr)
    {
        if (zr == null) return false;
        JefeIndigoAI enemigo = null;
        foreach (JefeIndigoAI e in zr.GetComponentsInChildren<JefeIndigoAI>(true)) { enemigo = e; break; }
        if (enemigo == null) return false;
        Transform modelo = enemigo.transform.Find("Modelo");
        Transform pose = modelo != null ? modelo.Find(AnimacionesModelo.Caminar) : null;
        if (pose == null && modelo != null && modelo.childCount > 0) pose = modelo.GetChild(0);
        if (pose == null) return false;

        cuerpo = new GameObject("Cuerpo").transform;
        cuerpo.SetParent(transform, false);
        // La copia, en el mismo sitio respecto a la raiz del enemigo (los pies en el piso)
        float pies = enemigo.TryGetComponent(out UnityEngine.AI.NavMeshAgent ag) ? ag.baseOffset : 0f;
        var copia = Instantiate(pose.gameObject, cuerpo);
        copia.name = "Enemigo 4 (quieto)";
        // (la raiz del enemigo esta "baseOffset" por encima de los pies; la del cuerpo, en el piso)
        copia.transform.localPosition = enemigo.transform.InverseTransformPoint(pose.position) + Vector3.up * pies;
        copia.transform.localRotation = Quaternion.Inverse(enemigo.transform.rotation) * pose.rotation;
        copia.transform.localScale = pose.lossyScale;
        // Quieta: sin animaciones ni scripts, sin colisiones ni sombras raras
        foreach (Animator a in copia.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        foreach (MonoBehaviour m in copia.GetComponentsInChildren<MonoBehaviour>(true)) m.enabled = false;
        foreach (Collider c in copia.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        var rs = new List<Renderer>();
        foreach (Renderer r in copia.GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = true;
            if (r is SkinnedMeshRenderer sk) sk.updateWhenOffscreen = true;
            rs.Add(r);
        }
        if (rs.Count == 0) { Destroy(cuerpo.gameObject); cuerpo = null; return false; }
        // Los ojos de Slappy, si los tiene (los mas brillantes posibles: es lo que se ve en la oscuridad)
        foreach (Transform t in copia.GetComponentsInChildren<Transform>(true))
            if (t.name == OjosBrillantes.NombreOjo && t.TryGetComponent(out Renderer ro))
            {
                var b = new MaterialPropertyBlock();
                b.SetColor("_EmissionColor", new Color(0.82f, 0.92f, 1f) * 2.6f);
                ro.SetPropertyBlock(b);
                if (cabeza == null) cabeza = t;
            }
        // Alto (para saber donde tiene la cabeza)
        Bounds bb = rs[0].bounds;
        foreach (Renderer r in rs) bb.Encapsulate(r.bounds);
        altoCuerpo = Mathf.Max(1f, bb.size.y);
        cuerpo.gameObject.SetActive(false);
        return true;
    }

    // Sin enemigo 4: altisima y flaca, un poco encorvada, con los brazos colgando casi hasta el suelo y la cabeza ladeada
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
        altoCuerpo = altura;
        cuerpo = new GameObject("Cuerpo").transform;
        cuerpo.SetParent(transform, false);
        foreach (float x in new[] { -0.1f, 0.1f })
            Pieza("Pierna", PrimitiveType.Cylinder, new Vector3(x, 0.73f, 0f), new Vector3(0.12f, 0.73f, 0.12f), Vector3.zero, negro);
        Pieza("Cadera", PrimitiveType.Cube, new Vector3(0f, 1.47f, 0f), new Vector3(0.3f, 0.14f, 0.16f), Vector3.zero, negro);
        Pieza("Torso", PrimitiveType.Capsule, new Vector3(0f, 1.97f, 0.03f), new Vector3(0.3f, 0.55f, 0.17f), new Vector3(9f, 0f, 0f), negro);
        Pieza("Hombros", PrimitiveType.Cube, new Vector3(0f, 2.44f, 0.06f), new Vector3(0.46f, 0.08f, 0.13f), Vector3.zero, negro);
        Pieza("Cuello", PrimitiveType.Cylinder, new Vector3(0f, 2.6f, 0.1f), new Vector3(0.07f, 0.11f, 0.07f), new Vector3(18f, 0f, 0f), negro);
        cabeza = Pieza("Cabeza", PrimitiveType.Sphere, new Vector3(0f, 2.82f, 0.15f), new Vector3(0.21f, 0.3f, 0.23f), new Vector3(12f, 0f, 11f), negro).transform;
        foreach (float x in new[] { -0.2f, 0.2f })
        {
            var o = Pieza("Ojo", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.2f, Vector3.zero, ojos).transform;
            o.SetParent(cabeza, false);
            o.localPosition = new Vector3(x * 1.1f, 0.05f, 0.46f);
            o.localScale = new Vector3(0.34f, 0.17f, 0.14f);
        }
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

    // Un zumbido grave y desafinado, muy bajo, que sube y se apaga (cuando el jugador la ve)
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
