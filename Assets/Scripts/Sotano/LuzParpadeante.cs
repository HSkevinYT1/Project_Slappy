using UnityEngine;

// Luz de foco medio danado: casi siempre encendida con un leve temblor, pero cada pocos segundos
// falla: parpadeos rapidos y, a veces, se apaga un momento antes de volver.
// Tambien apaga/enciende el brillo del vidrio del foco (emision del material) al mismo ritmo.
//
// SONIDO (sincronizado con la luz):
// - Zumbido de fondo suave y grave: sigue al brillo (se corta cuando la luz se apaga).
// - Chisporroteo electrico: SOLO mientras la luz falla.
// - Chasquidos: uno en cada salto brusco de la luz y uno mas fuerte cuando se apaga del todo.
// Si no se asignan clips, se generan por codigo al iniciar (no hacen falta archivos de audio).
[RequireComponent(typeof(Light))]
public class LuzParpadeante : MonoBehaviour
{
    [Tooltip("Vidrio del foco (su emision sigue a la luz). Opcional.")]
    public Renderer foco;
    [ColorUsage(false, true)] public Color brilloFoco = new Color(2.2f, 1.7f, 1.0f);

    [Header("Funcionamiento normal")]
    [Tooltip("Cuanto tiembla la luz cuando funciona bien (0 = nada).")]
    [Range(0f, 0.5f)] public float temblor = 0.08f;

    [Header("Fallas")]
    [Tooltip("Segundos entre fallas (minimo y maximo).")]
    public Vector2 entreFallas = new Vector2(1.5f, 6f);
    [Tooltip("Duracion de cada falla (minimo y maximo).")]
    public Vector2 duracionFalla = new Vector2(0.25f, 1.4f);
    [Tooltip("Probabilidad de que una falla termine con la luz apagada un rato.")]
    [Range(0f, 1f)] public float probApagon = 0.3f;
    public Vector2 duracionApagon = new Vector2(0.4f, 1.6f);

    [Header("Sonido")]
    [Tooltip("Desmarcado: la luz parpadea igual pero en silencio (ej: las luces de la estacion, para que el foco fallando solo se oiga en la tienda/sotano).")]
    public bool conSonido = true;
    [Tooltip("Zumbido suave de fondo, en bucle (suena siempre que la luz esta encendida). Vacio = se genera por codigo.")]
    public AudioClip zumbido;
    [Tooltip("Chisporroteo electrico en bucle que SOLO suena durante las fallas. Vacio = se genera por codigo.")]
    public AudioClip chisporroteo;
    [Tooltip("Chasquidos de chispa en cada salto de la luz (se elige uno al azar). Vacio = se generan por codigo.")]
    public AudioClip[] chispas;
    [Range(0f, 1f)] public float volumenFondo = 0.12f;
    [Range(0f, 1f)] public float volumenChisporroteo = 0.3f;
    [Range(0f, 1f)] public float volumenChasquidos = 0.4f;
    [Tooltip("Distancia (m) a la que el sonido se deja de oir. 9 m: se oye en todo el sotano pero no en la escalera ni en la tienda.")]
    public float radioSonido = 9f;

    Light luz;
    float intensidadBase;
    MaterialPropertyBlock bloque;
    static readonly int IdEmision = Shader.PropertyToID("_EmissionColor");

    float proximaFalla, finFalla, finApagon, proximoCambio;
    float nivelFalla = 1f;
    bool enFalla;

    AudioSource fuenteZumbido, fuenteChisporroteo, fuenteChispas;
    float nivelAnterior = 1f;

    void Awake()
    {
        luz = GetComponent<Light>();
        intensidadBase = luz.intensity;
        bloque = new MaterialPropertyBlock();
        proximaFalla = Time.time + Random.Range(entreFallas.x, entreFallas.y);
        if (conSonido) PrepararSonido();
    }

    void Update()
    {
        float t = Time.time;
        float nivel;

        if (t < finApagon)
        {
            nivel = 0f;
        }
        else if (enFalla)
        {
            if (t >= finFalla)
            {
                enFalla = false;
                proximaFalla = t + Random.Range(entreFallas.x, entreFallas.y);
                if (Random.value < probApagon) finApagon = t + Random.Range(duracionApagon.x, duracionApagon.y);
                nivel = finApagon > t ? 0f : 1f;
            }
            else
            {
                // Saltos bruscos entre apagado, medio y encendido.
                if (t >= proximoCambio)
                {
                    float r = Random.value;
                    nivelFalla = r < 0.35f ? 0f : r < 0.65f ? Random.Range(0.15f, 0.5f) : Random.Range(0.8f, 1.1f);
                    proximoCambio = t + Random.Range(0.025f, 0.12f);
                }
                nivel = nivelFalla;
            }
        }
        else
        {
            if (t >= proximaFalla)
            {
                enFalla = true;
                finFalla = t + Random.Range(duracionFalla.x, duracionFalla.y);
                proximoCambio = t;
            }
            // Temblor suave de corriente inestable.
            nivel = 1f - temblor + temblor * Mathf.PerlinNoise(t * 9f, 0.37f) * 2f;
        }

        Aplicar(nivel);
        Sonar(nivel);
    }

    void Aplicar(float nivel)
    {
        // Si el script se recompila en Play, los campos internos se pierden: se recrean.
        if (!luz) { luz = GetComponent<Light>(); intensidadBase = luz.intensity > 0f ? luz.intensity : 12f; }
        if (bloque == null) bloque = new MaterialPropertyBlock();
        luz.intensity = intensidadBase * nivel;
        luz.enabled = nivel > 0.01f;
        if (foco)
        {
            foco.GetPropertyBlock(bloque);
            bloque.SetColor(IdEmision, brilloFoco * Mathf.Max(0.02f, nivel));
            foco.SetPropertyBlock(bloque);
        }
    }

    // ------------------------------------------------------------------ Sonido

    void Sonar(float nivel)
    {
        if (!conSonido)
        {
            if (fuenteZumbido) fuenteZumbido.volume = 0f;
            if (fuenteChisporroteo) fuenteChisporroteo.volume = 0f;
            return;
        }
        // Tras recompilar en Play las fuentes de sonido se pierden: se vuelven a armar.
        if (!fuenteZumbido && !fuenteChispas)
        {
            foreach (Transform h in transform) if (h.GetComponent<AudioSource>()) Destroy(h.gameObject);
            PrepararSonido();
        }
        if (fuenteZumbido)
        {
            // Zumbido de fondo: sigue al brillo (se corta junto con la luz), sin sobresaltos.
            fuenteZumbido.volume = volumenFondo * Mathf.Clamp01(nivel);
        }
        if (fuenteChisporroteo)
        {
            // Chisporroteo: solo mientras la luz falla, siguiendo los saltos de brillo.
            float objetivo = enFalla ? volumenChisporroteo * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(nivel)) : 0f;
            fuenteChisporroteo.volume = Mathf.MoveTowards(fuenteChisporroteo.volume, objetivo, 6f * Time.deltaTime);
            if (enFalla) fuenteChisporroteo.pitch = Random.Range(0.92f, 1.08f);
        }

        if (fuenteChispas && chispas != null && chispas.Length > 0)
        {
            float salto = Mathf.Abs(nivel - nivelAnterior);
            bool seApaga = nivelAnterior > 0.05f && nivel <= 0.01f;
            bool vuelve = nivelAnterior <= 0.01f && nivel > 0.05f;
            if (salto > 0.3f || seApaga || vuelve)
            {
                float fuerza = seApaga && !enFalla ? 1f : Mathf.Clamp01(0.35f + salto * 0.6f);
                fuenteChispas.pitch = Random.Range(0.8f, 1.1f);
                fuenteChispas.PlayOneShot(chispas[Random.Range(0, chispas.Length)], volumenChasquidos * fuerza);
            }
        }
        nivelAnterior = nivel;
    }

    void PrepararSonido()
    {
        if (!zumbido) zumbido = GenerarZumbido();
        if (!chisporroteo) chisporroteo = GenerarChisporroteo();
        if (chispas == null || chispas.Length == 0)
            chispas = new[] { GenerarChispa(1), GenerarChispa(2), GenerarChispa(3), GenerarChispa(4) };

        fuenteZumbido = CrearFuente("Zumbido");
        fuenteZumbido.clip = zumbido;
        fuenteZumbido.loop = true;
        fuenteZumbido.volume = volumenFondo;
        fuenteZumbido.Play();

        fuenteChisporroteo = CrearFuente("Chisporroteo");
        fuenteChisporroteo.clip = chisporroteo;
        fuenteChisporroteo.loop = true;
        fuenteChisporroteo.volume = 0f;
        fuenteChisporroteo.Play();

        fuenteChispas = CrearFuente("Chispas");
    }

    AudioSource CrearFuente(string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 1f;                       // sonido 3D desde el foco
        s.rolloffMode = AudioRolloffMode.Linear;   // se deja de oir fuera del sotano
        s.minDistance = 1f;
        s.maxDistance = radioSonido;
        s.dopplerLevel = 0f;
        return s;
    }

    // Zumbido de fondo SUAVE: tono grave (60 Hz) con pocos armonicos, filtrado para quitar asperezas,
    // y una ondulacion lenta de volumen. Bucle de 4 s exacto.
    static AudioClip GenerarZumbido()
    {
        const int sr = 44100;
        const float dur = 4f;
        int n = (int)(sr * dur);
        var d = new float[n];
        float filtro = 0f;
        // Se recorre el bucle dos veces y se guarda la segunda: el filtro ya esta estable y no hay "clic" al repetir.
        for (int k = -n; k < n; k++)
        {
            int i = k < 0 ? k + n : k;
            float t = i / (float)sr;
            float w = 2f * Mathf.PI * 60f * t;
            float tono = Mathf.Sin(w) + 0.35f * Mathf.Sin(2f * w) + 0.1f * Mathf.Sin(3f * w);
            filtro = Mathf.Lerp(filtro, tono, 0.08f);                       // paso bajo: sonido redondo, no chillon
            float ondula = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.5f * t); // respira lento (2 ciclos en 4 s)
            if (k >= 0) d[i] = filtro * ondula;
        }
        float max = 0.0001f;
        foreach (float s in d) max = Mathf.Max(max, Mathf.Abs(s));
        for (int i = 0; i < n; i++) d[i] *= 0.5f / max;

        var clip = AudioClip.Create("ZumbidoSuave", n, 1, sr, false);
        clip.SetData(d, 0);
        return clip;
    }

    // Chisporroteo de filamento flojo (solo en las fallas): ruido electrico que "late" 100 veces por segundo
    // (corriente alterna arqueando). Bucle de 2 s exacto.
    static AudioClip GenerarChisporroteo()
    {
        const int sr = 44100;
        const float dur = 2f;
        int n = (int)(sr * dur);
        var d = new float[n];
        var azar = new System.Random(11);
        float pasa1 = 0f, pasa2 = 0f, prev = 0f;   // filtros para dejar el ruido en la banda media-aguda
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;

            // Ruido "chisporroteante": se quitan graves (paso alto) y los agudos mas filosos (paso bajo).
            float r = (float)azar.NextDouble() * 2f - 1f;
            float alto = r - prev; prev = r;
            pasa1 = Mathf.Lerp(pasa1, alto, 0.45f);
            pasa2 = Mathf.Lerp(pasa2, pasa1, 0.45f);

            // El arco se enciende en cada medio ciclo de la corriente (100 Hz) con pulsos cortos.
            float fase = Mathf.Repeat(t * 100f, 1f);
            float pulso = Mathf.Exp(-fase * 9f);
            // Intensidad que cambia lento (1 Hz) y un temblor a 7 Hz: suena inestable, no constante.
            float inestable = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 1f * t) * Mathf.Sin(2f * Mathf.PI * 7f * t + 1.3f);
            float chisporroteo = pasa2 * pulso * inestable * 2.4f;

            // Zumbido grave de fondo (50 Hz + armonicos impares), muy bajo.
            float w = 2f * Mathf.PI * 50f * t;
            float grave = (Mathf.Sin(w) + 0.33f * Mathf.Sin(3f * w) + 0.2f * Mathf.Sin(5f * w)) * 0.12f;

            // Micro chasquidos esporadicos dentro del bucle.
            float tic = azar.NextDouble() < 0.0006 ? ((float)azar.NextDouble() * 2f - 1f) * 0.9f : 0f;

            d[i] = Mathf.Clamp((chisporroteo + grave + tic) * 0.5f, -1f, 1f);
        }
        // Normalizar a un nivel comodo.
        float max = 0.0001f;
        foreach (float s in d) max = Mathf.Max(max, Mathf.Abs(s));
        for (int i = 0; i < n; i++) d[i] *= 0.7f / max;

        var clip = AudioClip.Create("Chisporroteo", n, 1, sr, false);
        clip.SetData(d, 0);
        return clip;
    }

    // Chasquido de chispa: clic seco + rafaga de ruido con caida rapida y un par de crepitaciones.
    static AudioClip GenerarChispa(int semilla)
    {
        const int sr = 44100;
        int n = (int)(sr * 0.16f);
        var d = new float[n];
        var azar = new System.Random(semilla * 31);
        float lp = 0f;
        float[] crepitas = { (float)azar.NextDouble() * 0.05f + 0.02f, (float)azar.NextDouble() * 0.07f + 0.05f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float ruido = (float)azar.NextDouble() * 2f - 1f;
            lp = Mathf.Lerp(lp, ruido, 0.3f);                                   // quita agudos: chasquido mas sordo
            float env = Mathf.Exp(-t * 45f);
            float s = lp * env;
            foreach (float c in crepitas)
            {
                float dt = t - c;
                if (dt >= 0f && dt < 0.012f) s += ((float)azar.NextDouble() * 2f - 1f) * Mathf.Exp(-dt * 400f) * 0.8f;
            }
            if (i < 40) s += (1f - i / 40f) * (i % 2 == 0 ? 1f : -1f) * 0.9f;  // clic inicial
            d[i] = Mathf.Clamp(s * 0.8f, -1f, 1f);
        }
        var clip = AudioClip.Create("Chispa" + semilla, n, 1, sr, false);
        clip.SetData(d, 0);
        return clip;
    }

    void OnDisable()
    {
        if (luz) { luz.intensity = intensidadBase; luz.enabled = true; }
        if (fuenteZumbido) fuenteZumbido.volume = 0f;
        if (fuenteChisporroteo) fuenteChisporroteo.volume = 0f;
    }

    void OnEnable()
    {
        if (fuenteZumbido && !fuenteZumbido.isPlaying) fuenteZumbido.Play();
    }
}
