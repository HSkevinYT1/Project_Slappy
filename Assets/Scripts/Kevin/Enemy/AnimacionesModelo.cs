using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Modelo animado por "poses": cada animacion (Idle, Caminar, Correr, Jumpscare) es su propio FBX
// (cada archivo trae su propio esqueleto, asi que los clips no se pueden compartir entre archivos).
// Solo se ve la pose que toca; las demas quedan ocultas y sin evaluar.
// - Quieto -> Idle (si no hay, la caminata congelada).
// - Moviendose -> Caminar; persiguiendo (JefeIndigoAI en Chase) -> Correr. La velocidad de la animacion
//   sigue a la velocidad real (funciona igual con el NavMeshAgent que en cinematicas o el tutorial).
// - Atrapa al jugador (Caught) -> Jumpscare una vez, mirando al jugador (sin Jumpscare: el Grito).
// - Grita (JefeIndigoAI.Gritando) -> Grito, estirado para durar lo que dura el sonido.
// - pegarAlSuelo: los pies siempre tocan el piso (la altura se corrige cuadro a cuadro con los vertices de los pies).
// Lo monta Tools > Modelos > Intercambiar modelos y animaciones.
public class AnimacionesModelo : MonoBehaviour
{
    public const string Idle = "Idle", Caminar = "Caminar", Correr = "Correr", Jumpscare = "Jumpscare", Grito = "Grito";

    [Serializable]
    public class Pose
    {
        public string nombre;
        public Animator animator;
        public AnimationClip[] clips;
        public bool bucle = true;

        [NonSerialized] public PlayableGraph grafo;
        [NonSerialized] public AnimationClipPlayable[] playables;
        [NonSerialized] public Renderer[] renders;
        [NonSerialized] public float tiempo;
        // Lo que no se tiene que mover (ver Fijar): el contenedor del esqueleto, y la cadera solo de arriba a abajo
        [NonSerialized] public Transform[] fijos;
        [NonSerialized] public Vector3[] fijosPos, fijosEsc;
        [NonSerialized] public Quaternion[] fijosRot;
        [NonSerialized] public Transform[] caderas;
        [NonSerialized] public Vector3[] caderasPos;
        // false = la animacion no sirve (se descontrola): la pose se queda en su primer cuadro
        [NonSerialized] public bool valida = true;
        // Si queda quieta (no valida): en que momento del clip (el de los pies mas juntos, para que se vea parado)
        [NonSerialized] public float tiempoQuieto;
        // true = no tiene nada que se vea (FBX borrado, o exportado sin malla): no se usa, asi el personaje no desaparece
        [NonSerialized] public bool vacia;
        // Luces que cuelgan de esta pose (ej. el brillo de los ojos de Fede): se prenden solo con la pose visible
        [NonSerialized] public Light[] luces;
        // pegarAlSuelo: cuanto subir/bajar la envoltura en cada momento del clip para que el pie mas bajo toque el piso
        [NonSerialized] public float[] correccion;
        [NonSerialized] public Transform env;
        [NonSerialized] public Vector3 envBase;
        [NonSerialized] public System.Collections.Generic.List<Pies> pies;
        // pasosIK: las dos piernas (con en que parte del clip apoya cada pie) y el paso mas largo que alcanzan (m)
        [NonSerialized] public Pierna[] piernas;
        [NonSerialized] public float pasoMax, apoyoMedio;
    }

    // Vertices de los pies de una malla con huesos (se deforman a mano para saber donde esta la planta)
    public class Pies
    {
        public Transform[] huesos;
        public Matrix4x4[] bind, m;
        public Vector3[] v;
        public BoneWeight[] w;
    }

    [Tooltip("Pies siempre en el piso: la altura del modelo se corrige en cada cuadro de la animacion para que el pie " +
             "mas bajo toque el suelo (las animaciones que agachan el cuerpo, como el grito de Fede, no lo hunden ni lo dejan flotando)")]
    public bool pegarAlSuelo = false;
    [Tooltip("Con pegarAlSuelo: ademas busca el piso real debajo (raycast) por si el NavMesh queda unos cm por encima o por debajo")]
    public bool usarPisoReal = true;

    [Tooltip("Las animaciones que avanzan (root motion, ej. las de Mixamo) se quedan en el sitio: si no, el modelo se iba alejando del personaje")]
    public bool enElSitio = true;
    [Tooltip("Apagado = el modelo nuevo se ve quieto (la pose del primer cuadro de Idle, o de Caminar), sin animaciones")]
    public bool reproducir = true;   // (antes "animar": nombre nuevo para que no quede el "apagado" que se guardo antes)
    [Tooltip("Al empezar se prueba cada animacion: si el modelo se aleja del personaje o cambia mucho de tamano, esa " +
             "animacion queda quieta (en su primer cuadro). Asi solo se mueven las que funcionan")]
    public bool soloLasQueFuncionan = true;

    public Pose[] poses = new Pose[0];
    [Tooltip("Velocidad (m/s) a la que la caminata se ve natural (con JefeIndigoAI se usa su patrolSpeed)")]
    public float velocidadCaminar = 1.8f;
    [Tooltip("Ritmo de los pasos al caminar (1 = normal). Menos de 1 = pasos mas pausados, mas de 1 = mas seguidos")]
    [Range(0.3f, 2f)] public float ritmoCaminar = 1f;
    [Tooltip("Para caminatas 'en el sitio' (los pies no van hacia atras, como la de Fede): las piernas se mueven con IK " +
             "para que el pie apoyado se quede clavado en el piso mientras el cuerpo avanza y el otro de el paso hacia " +
             "adelante. El ritmo de los pasos se ajusta solo. Solo con pegarAlSuelo")]
    public bool pasosIK = true;
    [Tooltip("Largo de cada paso (m) con pasosIK. 0 = automatico (lo que alcanzan las piernas)")]
    public float largoPaso = 0f;
    float cicloSuave;   // metros que avanza el cuerpo en un ciclo de la caminata (suavizado)

    // Una pierna para el IK: muslo -> rodilla -> punta (el pie), y en que fase del clip (0..1) apoya el pie
    public class Pierna
    {
        public Transform muslo, rodilla, punta;
        public float inicio, fin;
        public float Apoyo => Mathf.Repeat(fin - inicio, 1f) is float d && d > 0.1f && d < 0.9f ? d : 0.5f;
    }
    [Tooltip("Velocidad (m/s) a la que la carrera se ve natural (con JefeIndigoAI se usa su chaseSpeed)")]
    public float velocidadCorrer = 4.5f;
    [Tooltip("Sin IA: por encima de esta velocidad corre")]
    public float umbralCorrer = 3.2f;
    [Tooltip("Por debajo de esta velocidad se considera quieto")]
    public float umbralQuieto = 0.08f;

    JefeIndigoAI ia;
    Pose actual;
    Vector3 ultima;
    float velocidad;
    bool moviendose;
    bool preparado;
    // Altura del piso en el espacio del personaje (con NavMeshAgent: -baseOffset) y ajuste por el piso real
    float suelo, ajustePiso;
    readonly RaycastHit[] golpes = new RaycastHit[8];

    void Awake() => Preparar();

    // (Lo llama Awake; desde el editor lo usan las capturas para ver una pose sin dar Play)
    public void Preparar()
    {
        if (preparado) return;
        preparado = true;
        ia = GetComponentInParent<JefeIndigoAI>();
        var agente = GetComponent<UnityEngine.AI.NavMeshAgent>();
        suelo = agente != null ? -agente.baseOffset / Mathf.Max(0.0001f, transform.lossyScale.y) : 0f;
        if (ia != null) { velocidadCaminar = ia.patrolSpeed; velocidadCorrer = ia.chaseSpeed; }
        if (!reproducir)
        {
            // Quieto: solo se ve una pose (la que dejo el setup en su primer cuadro) y no se evalua nada
            foreach (Pose p in poses)
            {
                if (p == null || p.animator == null) continue;
                p.renders = p.animator.GetComponentsInChildren<Renderer>(true);
                p.vacia = !TieneMalla(p);
                p.animator.runtimeAnimatorController = null;
                p.animator.enabled = false;
            }
            foreach (Pose p in poses) Mostrar(p, false);
            actual = Primera(Idle, Caminar, Correr);
            Mostrar(actual, true);
            if (actual != null && ia == null && GetComponent<UnityEngine.AI.NavMeshAgent>() == null) Asentar(actual);
            return;
        }
        foreach (Pose p in poses)
        {
            if (p == null || p.animator == null) continue;
            p.renders = p.animator.GetComponentsInChildren<Renderer>(true);
            p.vacia = !TieneMalla(p);
            if (p.vacia) Debug.LogWarning("[Animaciones] " + name + " / " + p.nombre + ": no tiene malla (su FBX no existe o no trae el modelo): se usa otra pose.", this);
            p.animator.runtimeAnimatorController = null;
            p.animator.applyRootMotion = false;
            if (p.clips == null || p.clips.Length == 0) continue;

            p.grafo = PlayableGraph.Create(name + " " + p.nombre);
            p.grafo.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var salida = AnimationPlayableOutput.Create(p.grafo, "Salida", p.animator);
            var mezcla = AnimationLayerMixerPlayable.Create(p.grafo, p.clips.Length);
            p.playables = new AnimationClipPlayable[p.clips.Length];
            for (int i = 0; i < p.clips.Length; i++)
            {
                if (p.clips[i] == null) continue;
                var c = AnimationClipPlayable.Create(p.grafo, p.clips[i]);
                c.SetApplyFootIK(false);
                p.grafo.Connect(c, 0, mezcla, i);
                mezcla.SetInputWeight(i, 1f);
                p.playables[i] = c;
            }
            salida.SetSourcePlayable(mezcla);
            p.grafo.Play();
            // La pose en el primer cuadro es la de referencia (asi la midio el setup)
            p.tiempo = 0f;
            Evaluar(p);
            Capturar(p);
            if (ia == null && GetComponent<UnityEngine.AI.NavMeshAgent>() == null) Asentar(p);
            // Con pegarAlSuelo las animaciones ya vienen elegidas a mano (y el suelo se corrige): no se descartan
            if (soloLasQueFuncionan && !pegarAlSuelo) Validar(p);
            PrepararSuelo(p);
        }
        foreach (Pose p in poses) if (p != null && p.animator != null) p.luces = p.animator.GetComponentsInChildren<Light>(true);
        if (ia != null)
        {
            Pose g = Buscar(Grito);
            ia.duracionAnimGrito = g != null && g.valida && g.playables != null ? Largo(g) : 0f;
        }
        IdleQueCamina();
        foreach (Pose p in poses) Mostrar(p, false);
        Cambiar(Elegir());
    }

    void OnEnable()
    {
        ultima = transform.position;
        velocidad = 0f;
    }

    void OnDestroy()
    {
        foreach (Pose p in poses) if (p != null && p.grafo.IsValid()) p.grafo.Destroy();
    }

    static bool Usable(Pose p) => p != null && p.animator != null && !p.vacia;

    // Algo que se pueda ver: una malla asignada
    static bool TieneMalla(Pose p)
    {
        if (p.renders == null) return false;
        foreach (Renderer r in p.renders)
        {
            if (r == null) continue;
            if (r is SkinnedMeshRenderer s) { if (s.sharedMesh != null) return true; }
            else if (r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null) return true;
        }
        return false;
    }

    public Pose Buscar(string nombre)
    {
        foreach (Pose p in poses) if (Usable(p) && p.nombre == nombre) return p;
        return null;
    }

    Pose Primera(params string[] nombres)
    {
        foreach (string n in nombres) { Pose p = Buscar(n); if (p != null) return p; }
        foreach (Pose p in poses) if (Usable(p)) return p;
        return null;
    }

    bool Atrapado => ia != null && ia.CurrentState == JefeIndigoAI.State.Caught;

    Pose Elegir()
    {
        if (Atrapado && Buscar(Jumpscare) != null) return Buscar(Jumpscare);
        if (Atrapado && Buscar(Grito) != null) return Buscar(Grito);
        if (ia != null && ia.Gritando && Buscar(Grito) != null) return Buscar(Grito);
        if (!moviendose) return Primera(Idle, Caminar, Correr);
        bool correr = ia != null ? ia.CurrentState == JefeIndigoAI.State.Chase : velocidad > umbralCorrer;
        return correr ? Primera(Correr, Caminar, Idle) : Primera(Caminar, Correr, Idle);
    }

    void Mostrar(Pose p, bool ver)
    {
        if (p == null || p.renders == null) return;
        foreach (Renderer r in p.renders) if (r != null) r.enabled = ver;
        if (p.luces != null) foreach (Light l in p.luces) if (l != null) l.enabled = ver;
    }

    void Cambiar(Pose p)
    {
        if (p == actual) return;
        Mostrar(actual, false);
        actual = p;
        if (actual == null) return;
        if (!actual.bucle) actual.tiempo = 0f;
        Mostrar(actual, true);
    }

    void LateUpdate()
    {
        if (!reproducir) return;
        Paso(Time.deltaTime);
    }

    // Un cuadro (lo llama LateUpdate; desde el editor se usa para simular la caminata sin dar Play)
    public void Paso(float dt)
    {
        if (dt > 0f)
        {
            Vector3 delta = transform.position - ultima;
            delta.y = 0f;
            float v = delta.magnitude / dt;
            if (v > 20f) v = 0f;   // teletransporte, no cuenta como caminar
            velocidad = Mathf.Lerp(velocidad, v, 1f - Mathf.Exp(-10f * dt));
        }
        ultima = transform.position;
        // Histeresis para no parpadear entre idle y caminar
        moviendose = moviendose ? velocidad > umbralQuieto : velocidad > umbralQuieto * 2f;

        Cambiar(Elegir());
        if (actual == null) return;

        float k = 1f;
        bool ik = actual.nombre == Caminar && pasosIK && actual.piernas != null;
        if (ik)
        {
            // Ritmo para que cada paso mida 'paso': mientras un pie esta apoyado (apoyoMedio del ciclo) el cuerpo avanza eso
            float paso = largoPaso > 0f ? largoPaso : actual.pasoMax * 0.8f;
            k = moviendose ? Mathf.Clamp(velocidad * actual.apoyoMedio * Largo(actual) / Mathf.Max(0.05f, paso) * ritmoCaminar, 0.3f, 4f) : 0f;
        }
        else if (actual.nombre == Caminar) k = moviendose ? Mathf.Clamp(velocidad / Mathf.Max(0.1f, velocidadCaminar) * ritmoCaminar, 0.2f, 2.5f) : 0f;
        else if (actual.nombre == Correr) k = moviendose ? Mathf.Clamp(velocidad / Mathf.Max(0.1f, velocidadCorrer), 0.6f, 2.2f) : 0f;
        // Grito: va al ritmo del grito de la IA (dura lo que dura el sonido); si un grito empieza encima de otro, arranca de nuevo
        if (actual.nombre == Grito && ia != null && ia.Gritando) { actual.tiempo = ia.ProgresoGrito * Largo(actual); k = 0f; }
        actual.tiempo += dt * k;

        if (pegarAlSuelo && usarPisoReal && Application.isPlaying) PisoReal(dt);

        // Jumpscare (o el grito al atraparlo): de frente al jugador
        if ((actual.nombre == Jumpscare || (actual.nombre == Grito && Atrapado)) && ia != null && ia.player != null)
        {
            Vector3 dir = ia.player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-20f * dt));
        }

        Evaluar(actual);
        if (ik) PasosIK(actual, k, dt);
    }

    // ------------------------------------------------------------------ Pasos con IK (caminata en el sitio)

    // Pie apoyado: va de adelante (+0.5) hacia atras (-0.5) a ritmo constante (asi, con el cuerpo avanzando, queda quieto en
    // el piso). Pie en el aire: de atras hacia adelante, suave
    static float Desfase(Pierna l, float fase)
    {
        float dur = l.Apoyo;
        float t = Mathf.Repeat(fase - l.inicio, 1f);
        if (t < dur) return 0.5f - t / dur;
        float s = (t - dur) / (1f - dur);
        return -0.5f + s * s * (3f - 2f * s);
    }

    // 0..1: cuanto se levanta el pie (solo en el aire)
    static float Elevacion(Pierna l, float fase)
    {
        float dur = l.Apoyo;
        float t = Mathf.Repeat(fase - l.inicio, 1f);
        return t < dur ? 0f : Mathf.Sin((t - dur) / (1f - dur) * Mathf.PI);
    }

    void PasosIK(Pose p, float k, float dt)
    {
        float largo = Largo(p);
        float fase = TiempoPose(p, largo) / largo;
        // Lo que avanza el cuerpo en un ciclo (a este ritmo); quieto = 0 (los pies vuelven a su sitio poco a poco)
        float objetivo = moviendose && k > 0.01f ? velocidad * largo / k : 0f;
        cicloSuave = Mathf.Lerp(cicloSuave, objetivo, 1f - Mathf.Exp(-8f * dt));
        if (cicloSuave < 0.005f) return;
        Vector3 adelante = transform.forward, arriba = transform.up;
        foreach (Pierna l in p.piernas)
        {
            if (l.muslo == null || l.rodilla == null || l.punta == null) continue;
            float paso = Mathf.Min(cicloSuave * l.Apoyo, p.pasoMax);
            float alto = Elevacion(l, fase) * Mathf.Min(0.18f, paso * 0.3f);
            ResolverIK(l, l.punta.position + adelante * (Desfase(l, fase) * paso) + arriba * alto);
        }
    }

    // IK de dos huesos: muslo y rodilla giran para que la punta llegue al objetivo, doblando la rodilla hacia el mismo
    // lado que en la animacion
    static void ResolverIK(Pierna l, Vector3 objetivo)
    {
        Vector3 A = l.muslo.position, B = l.rodilla.position, C = l.punta.position;
        float a = (B - A).magnitude, b = (C - B).magnitude;
        if (a < 1e-4f || b < 1e-4f) return;
        Vector3 at = objetivo - A;
        if (at.sqrMagnitude < 1e-8f) return;
        float d = Mathf.Clamp(at.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
        Vector3 dir = at.normalized;
        Vector3 ac = C - A;
        Vector3 haciaRodilla = (B - A) - Vector3.Project(B - A, ac.sqrMagnitude > 1e-8f ? ac.normalized : dir);
        Vector3 polo = haciaRodilla - Vector3.Project(haciaRodilla, dir);
        if (polo.sqrMagnitude < 1e-8f) polo = Vector3.Cross(dir, l.muslo.right);
        polo.Normalize();
        float x = (a * a - b * b + d * d) / (2f * d);
        float y = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
        Vector3 rodillaNueva = A + dir * x + polo * y;
        l.muslo.rotation = Quaternion.FromToRotation(B - A, rodillaNueva - A) * l.muslo.rotation;
        Vector3 B2 = l.rodilla.position, C2 = l.punta.position;
        l.rodilla.rotation = Quaternion.FromToRotation(C2 - B2, A + dir * d - B2) * l.rodilla.rotation;
    }

    static bool EsIzquierda(string n) { n = n.ToLowerInvariant(); return n.EndsWith(".l") || n.EndsWith("_l") || n.Contains("left"); }
    static bool EsDerecha(string n) { n = n.ToLowerInvariant(); return n.EndsWith(".r") || n.EndsWith("_r") || n.Contains("right"); }

    // De que pierna es un vertice de los pies: +1 izquierda, -1 derecha, 0 no se sabe
    static int LadoPie(Pose p, int zi, int vi)
    {
        if (zi < 0) return 0;
        Pies z = p.pies[zi];
        Transform h = z.huesos[z.w[vi].boneIndex0];
        if (h == null) return 0;
        return EsIzquierda(h.name) ? 1 : EsDerecha(h.name) ? -1 : 0;
    }

    // La racha mas larga (circular) del clip en que el pie mas bajo es de esa pierna = cuando esta apoyada
    static bool Apoyo(int[] lado, int quien, out float inicio, out float fin)
    {
        int n = lado.Length, mejorIni = -1, mejorLen = 0;
        for (int s = 0; s < n; s++)
        {
            if (lado[s] != quien || lado[(s - 1 + n) % n] == quien) continue;
            int len = 0;
            while (len < n && lado[(s + len) % n] == quien) len++;
            if (len > mejorLen) { mejorLen = len; mejorIni = s; }
        }
        inicio = fin = 0f;
        if (mejorIni < 0) return false;
        inicio = (float)mejorIni / n;
        fin = (float)((mejorIni + mejorLen) % n) / n;
        return true;
    }

    Pierna[] PrepararPiernas(Pose p, int[] lado)
    {
        var piernas = new Pierna[2];
        for (int k = 0; k < 2; k++)
        {
            bool izq = k == 0;
            Transform muslo = null;
            foreach (Transform t in p.animator.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if ((n.Contains("thigh") || n.Contains("upleg") || n.Contains("muslo")) && !n.EndsWith("_end") && (izq ? EsIzquierda(t.name) : EsDerecha(t.name))) { muslo = t; break; }
            }
            if (muslo == null || muslo.childCount == 0) return null;
            Transform rodilla = null;
            foreach (Transform c in muslo) { string n = c.name.ToLowerInvariant(); if (n.Contains("shin") || n.Contains("calf") || n.Contains("leg") || n.Contains("knee")) { rodilla = c; break; } }
            if (rodilla == null) rodilla = muslo.GetChild(0);
            if (rodilla.childCount == 0) return null;
            Transform punta = null;
            foreach (Transform c in rodilla) if (c.name.ToLowerInvariant().EndsWith("_end") || c.name.ToLowerInvariant().Contains("foot")) { punta = c; break; }
            if (punta == null) punta = rodilla.GetChild(0);
            var l = new Pierna { muslo = muslo, rodilla = rodilla, punta = punta };
            if (!Apoyo(lado, izq ? 1 : -1, out l.inicio, out l.fin)) { l.inicio = izq ? 0f : 0.5f; l.fin = izq ? 0.5f : 0f; }
            piernas[k] = l;
        }
        p.apoyoMedio = (piernas[0].Apoyo + piernas[1].Apoyo) * 0.5f;

        // Paso mas largo: hasta donde llega la pierna hacia adelante/atras con la cadera a la altura del primer cuadro
        p.tiempo = 0f;
        Evaluar(p);
        float alcance = float.MaxValue;
        foreach (Pierna l in piernas)
        {
            float largoPierna = 0.96f * ((l.rodilla.position - l.muslo.position).magnitude + (l.punta.position - l.rodilla.position).magnitude);
            float h = Vector3.Dot(l.muslo.position - l.punta.position, transform.up);
            alcance = Mathf.Min(alcance, Mathf.Sqrt(Mathf.Max(0f, largoPierna * largoPierna - h * h)));
        }
        p.pasoMax = Mathf.Max(0.1f, 2f * alcance * 0.9f);
        Debug.Log($"[Animaciones] {name} / {p.nombre}: pasos con IK (paso max {p.pasoMax:0.00} m; apoyo izq {piernas[0].inicio:0.00}-{piernas[0].fin:0.00}, der {piernas[1].inicio:0.00}-{piernas[1].fin:0.00})", this);
        return piernas;
    }

    void Evaluar(Pose p)
    {
        if (!p.grafo.IsValid() || p.playables == null) return;
        for (int i = 0; i < p.playables.Length; i++)
        {
            if (!p.playables[i].IsValid()) continue;
            float largo = Mathf.Max(0.0001f, p.clips[i].length);
            float t = !p.valida ? Mathf.Min(p.tiempoQuieto, largo) : p.bucle ? Mathf.Repeat(p.tiempo, largo) : Mathf.Min(p.tiempo, largo);
            p.playables[i].SetTime(t);
        }
        p.grafo.Evaluate(0f);
        Fijar(p);
        AplicarSuelo(p);
    }

    // ------------------------------------------------------------------ Pies en el suelo

    static float Largo(Pose p)
    {
        float l = 0f;
        if (p.clips != null) foreach (AnimationClip c in p.clips) if (c != null) l = Mathf.Max(l, c.length);
        return Mathf.Max(0.0001f, l);
    }

    // El momento del clip que se esta viendo (igual que en Evaluar)
    static float TiempoPose(Pose p, float largo) =>
        !p.valida ? Mathf.Min(p.tiempoQuieto, largo) : p.bucle ? Mathf.Repeat(p.tiempo, largo) : Mathf.Clamp(p.tiempo, 0f, largo);

    static bool EsHuesoPie(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("foot") || n.Contains("toe") || n.Contains("shin") || n.Contains("calf") || n.Contains("heel")
            || n.Contains("ankle") || n.Contains("leg") || n.Contains("pie") || n.Contains("tobillo");
    }

    // Vertices de los pies (los que mueve sobre todo un hueso de pierna/pie). Si ningun hueso lo parece: todo el cuerpo
    System.Collections.Generic.List<Pies> PuntosPies(Pose p)
    {
        var pies = new System.Collections.Generic.List<Pies>();
        var todo = new System.Collections.Generic.List<Pies>();
        foreach (SkinnedMeshRenderer s in p.animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh m = s.sharedMesh;
            if (m == null || !m.isReadable || s.bones == null || s.bones.Length == 0) continue;
            Vector3[] vs = m.vertices;
            BoneWeight[] bw = m.boneWeights;
            Matrix4x4[] bp = m.bindposes;
            if (bw.Length != vs.Length || bp.Length != s.bones.Length) continue;
            var esPie = new bool[s.bones.Length];
            for (int b = 0; b < esPie.Length; b++) esPie[b] = s.bones[b] != null && EsHuesoPie(s.bones[b].name);
            var v = new System.Collections.Generic.List<Vector3>();
            var w = new System.Collections.Generic.List<BoneWeight>();
            for (int i = 0; i < vs.Length; i++)
                if (bw[i].boneIndex0 < esPie.Length && esPie[bw[i].boneIndex0]) { v.Add(vs[i]); w.Add(bw[i]); }
            var huesos = s.bones;
            if (v.Count > 0) pies.Add(new Pies { huesos = huesos, bind = bp, m = new Matrix4x4[huesos.Length], v = v.ToArray(), w = w.ToArray() });
            todo.Add(new Pies { huesos = huesos, bind = bp, m = new Matrix4x4[huesos.Length], v = vs, w = bw });
        }
        return pies.Count > 0 ? pies : todo;
    }

    // Altura (en el espacio del personaje) del vertice de los pies mas bajo, con la pose actual de los huesos
    float MinimoPies(Pose p) => MinimoPies(p, out _, out _);

    // (cualMalla/cualVertice: cual es ese vertice, para seguirlo entre cuadros)
    float MinimoPies(Pose p, out int cualMalla, out int cualVertice)
    {
        float min = float.MaxValue;
        cualMalla = -1; cualVertice = -1;
        Matrix4x4 aLocal = transform.worldToLocalMatrix;
        for (int zi = 0; zi < p.pies.Count; zi++)
        {
            Pies z = p.pies[zi];
            for (int b = 0; b < z.huesos.Length; b++)
                z.m[b] = z.huesos[b] != null ? aLocal * z.huesos[b].localToWorldMatrix * z.bind[b] : Matrix4x4.zero;
            for (int i = 0; i < z.v.Length; i++)
            {
                BoneWeight w = z.w[i];
                Vector3 v = z.v[i];
                float y = Y(z.m[w.boneIndex0], v) * w.weight0 + Y(z.m[w.boneIndex1], v) * w.weight1
                        + Y(z.m[w.boneIndex2], v) * w.weight2 + Y(z.m[w.boneIndex3], v) * w.weight3;
                if (y < min) { min = y; cualMalla = zi; cualVertice = i; }
            }
        }
        return min;
    }


    static float Y(Matrix4x4 m, Vector3 v) => m.m10 * v.x + m.m11 * v.y + m.m12 * v.z + m.m13;

    // Recorre el clip y apunta, para cada momento, cuanto hay que subir/bajar el modelo para que el pie mas bajo
    // quede justo en el piso. Asi ni se hunde (el grito agacha el cuerpo) ni flota
    void PrepararSuelo(Pose p)
    {
        p.correccion = null;
        if (!pegarAlSuelo || p.playables == null || p.animator == null) return;
        p.env = p.animator.transform.parent;
        if (p.env == null || p.env == transform || p.env.parent == null) { p.env = null; return; }
        p.envBase = p.env.localPosition;
        p.pies = PuntosPies(p);
        if (p.pies.Count == 0)
        {
            Debug.LogWarning("[Animaciones] " + name + " / " + p.nombre + ": la malla no es legible (Read/Write): no se pueden pegar los pies al suelo.", this);
            p.env = null;
            return;
        }
        float largo = Largo(p);
        int n = Mathf.Clamp(Mathf.CeilToInt(largo * 30f), 8, 240);
        var tabla = new float[n + 1];
        float tAntes = p.tiempo;
        bool vAntes = p.valida;
        p.valida = true;
        var lado = new int[n];   // de que pierna es el pie mas bajo en cada momento (= cual esta apoyada)
        for (int i = 0; i <= n; i++)
        {
            p.tiempo = largo * i / n;
            Evaluar(p);   // (la envoltura sigue en su sitio: la tabla aun no existe)
            tabla[i] = suelo - MinimoPies(p, out int zi, out int vi);
            if (i < n) lado[i] = LadoPie(p, zi, vi);
        }
        p.piernas = pasosIK && p.bucle && p.nombre == Caminar ? PrepararPiernas(p, lado) : null;
        p.valida = vAntes;
        p.tiempo = tAntes;
        p.correccion = tabla;
        Evaluar(p);
    }

    void AplicarSuelo(Pose p)
    {
        if (p.correccion == null || p.env == null) return;
        float largo = Largo(p);
        float f = TiempoPose(p, largo) / largo * (p.correccion.Length - 1);
        int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, p.correccion.Length - 1);
        int j = Mathf.Min(i + 1, p.correccion.Length - 1);
        float c = Mathf.Lerp(p.correccion[i], p.correccion[j], f - i) + ajustePiso;
        p.env.localPosition = p.envBase + p.env.parent.InverseTransformVector(transform.TransformVector(new Vector3(0f, c, 0f)));
    }

    // El piso de verdad debajo del personaje (el NavMesh puede quedar unos cm por encima del suelo): si esta cerca,
    // los pies se apoyan en el. Si no hay piso cerca (o es otra cosa), se queda la altura del NavMesh
    void PisoReal(float dt)
    {
        Vector3 arriba = transform.up;
        Vector3 piso = transform.TransformPoint(new Vector3(0f, suelo, 0f));
        int n = Physics.RaycastNonAlloc(piso + arriba * 0.5f, -arriba, golpes, 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float objetivo = 0f, mejor = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (golpes[i].collider == null || golpes[i].collider.transform.IsChildOf(transform)) continue;
            if (golpes[i].collider.attachedRigidbody != null && !golpes[i].collider.attachedRigidbody.isKinematic) continue;   // el jugador, cosas sueltas
            float d = Vector3.Dot(golpes[i].point - piso, arriba);
            if (Mathf.Abs(d) > 0.3f) continue;
            if (golpes[i].distance < mejor) { mejor = golpes[i].distance; objetivo = d / Mathf.Max(0.0001f, transform.lossyScale.y); }
        }
        ajustePiso = Mathf.Lerp(ajustePiso, objetivo, 1f - Mathf.Exp(-12f * dt));
    }

    // ------------------------------------------------------------------ Vista previa (editor)

    // Deja visible la pose 'nombre' en el momento t (0..1 del clip), con los pies en el suelo, sin dar Play
    public void Previsualizar(string nombre, float t01)
    {
        Preparar();
        Pose p = Buscar(nombre) ?? Primera(Caminar, Idle, Correr);
        if (p == null) return;
        foreach (Pose q in poses) Mostrar(q, false);
        actual = p;
        Mostrar(p, true);
        p.tiempo = Largo(p) * Mathf.Clamp01(t01);
        Evaluar(p);
    }

    public void Liberar()
    {
        foreach (Pose p in poses) if (p != null && p.grafo.IsValid()) p.grafo.Destroy();
        preparado = false;
    }

    [Tooltip("NPCs (sin IA): al empezar, cada pose se centra en el personaje y se apoya en el piso (y si mide algo absurdo, se lleva a 'altoNPC')")]
    public bool asentarNPC = true;
    public float altoNPC = 1.8f;

    // NPC: la pose (su envoltura) queda con los pies en el piso que hay debajo del personaje y centrada en el.
    // El setup la media con la pose sin animar; con la animacion puesta quedaba flotando y corrida (atravesaba paredes)
    void Asentar(Pose p)
    {
        if (!asentarNPC) return;
        Transform env = p.animator.transform.parent;
        if (env == null || env == transform) return;
        if (!Limites(p.animator.transform, out Bounds b)) return;
        if (b.size.y > 3.5f || b.size.y < 0.6f)
        {
            env.localScale *= altoNPC / Mathf.Max(0.01f, b.size.y);
            if (!Limites(p.animator.transform, out b)) return;
        }
        float suelo = transform.position.y;
        foreach (RaycastHit h in Physics.RaycastAll(transform.position + Vector3.up * 1.2f, Vector3.down, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.point.y > suelo + 1f) continue;
            suelo = h.point.y;
            break;
        }
        env.position += new Vector3(transform.position.x - b.center.x, suelo - b.min.y, transform.position.z - b.center.z);
    }

    // Si la animacion de quieto es en realidad una caminata (el FBX de Idle trae el mismo clip que el de Caminar, como
    // el del cliente), parado seguiria caminando en el sitio: esa pose queda congelada en el cuadro de los pies mas
    // juntos, que es lo mas parecido a estar parado
    void IdleQueCamina()
    {
        Pose idle = Buscar(Idle), caminar = Buscar(Caminar);
        if (idle == null || idle.clips == null || idle.clips.Length == 0) return;
        bool camina = false;
        if (caminar != null && caminar.clips != null)
            foreach (AnimationClip a in idle.clips)
                foreach (AnimationClip b in caminar.clips)
                    if (a != null && b != null && a.name == b.name && Mathf.Abs(a.length - b.length) < 0.05f) camina = true;
        if (!camina) return;
        idle.valida = false;
        idle.tiempoQuieto = PiesJuntos(idle);
        Debug.Log("[Animaciones] " + name + ": su animacion de quieto es una caminata: parado queda quieto (sin caminar).", this);
    }

    // Momento del clip con los pies mas juntos (en horizontal)
    float PiesJuntos(Pose p)
    {
        var pies = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in p.animator.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if ((n.Contains("foot") || n.Contains("pie")) && !n.Contains("end") && !n.Contains("toe")) pies.Add(t);
        }
        if (pies.Count < 2 || p.playables == null) return 0f;
        float largo = 0f;
        foreach (AnimationClip c in p.clips) if (c != null) largo = Mathf.Max(largo, c.length);
        float mejor = 0f, menor = float.MaxValue;
        bool antes = p.valida;
        p.valida = true;
        for (int i = 0; i < 24; i++)
        {
            p.tiempo = largo * i / 24f;
            Evaluar(p);
            Vector3 d = pies[0].position - pies[1].position;
            Vector3 arriba = p.animator.transform.up;
            float h = (d - Vector3.Project(d, arriba)).magnitude;
            if (h < menor) { menor = h; mejor = p.tiempo; }
        }
        p.valida = antes;
        p.tiempo = 0f;
        p.tiempoQuieto = mejor;
        Evaluar(p);
        return mejor;
    }

    // Prueba la animacion en varios momentos: si el modelo se aleja (mas de medio cuerpo) o cambia mucho de tamano
    // respecto al primer cuadro, no sirve y queda quieta. Al final vuelve al primer cuadro
    void Validar(Pose p)
    {
        if (p.playables == null || !Limites(p.animator.transform, out Bounds referencia)) return;
        float alto = Mathf.Max(0.1f, referencia.size.y);
        float largoMax = 0f;
        foreach (AnimationClip c in p.clips) if (c != null) largoMax = Mathf.Max(largoMax, c.length);
        bool mal = false;
        foreach (float k in new[] { 0.2f, 0.45f, 0.7f, 0.95f })
        {
            p.tiempo = largoMax * k;
            Evaluar(p);
            if (!Limites(p.animator.transform, out Bounds b)) continue;
            Vector2 corrido = new Vector2(b.center.x - referencia.center.x, b.center.z - referencia.center.z);
            if (corrido.magnitude > Mathf.Max(0.6f, alto * 0.5f) || b.size.y > alto * 1.7f || b.size.y < alto * 0.45f
                || Mathf.Abs(b.min.y - referencia.min.y) > alto * 0.6f)
            { mal = true; break; }
        }
        p.tiempo = 0f;
        Evaluar(p);
        if (!mal) return;
        p.valida = false;
        Debug.LogWarning("[Animaciones] " + name + " / " + p.nombre + ": la animacion se descontrola (el modelo se aleja o cambia de tamano): queda quieta.", this);
    }

    // Limites reales de las mallas (con la pose actual del esqueleto)
    static bool Limites(Transform t, out Bounds b)
    {
        b = default;
        bool hay = false;
        var malla = new Mesh();
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
        {
            Mesh m = null;
            if (r is SkinnedMeshRenderer s && s.sharedMesh != null && s.sharedMesh.isReadable) { s.BakeMesh(malla, true); m = malla; }
            else if (!(r is SkinnedMeshRenderer) && r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null && mf.sharedMesh.isReadable) m = mf.sharedMesh;
            if (m == null)
            {
                // Malla no legible (en la build): los limites del renderer
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
                continue;
            }
            Matrix4x4 a = r.transform.localToWorldMatrix;
            Vector3[] vs = m.vertices;
            int paso = Mathf.Max(1, vs.Length / 3000);
            for (int i = 0; i < vs.Length; i += paso)
            {
                Vector3 w = a.MultiplyPoint3x4(vs[i]);
                if (!hay) { b = new Bounds(w, Vector3.zero); hay = true; } else b.Encapsulate(w);
            }
        }
        if (Application.isPlaying) Destroy(malla); else DestroyImmediate(malla);
        return hay;
    }

    // Guarda como estan (en el primer cuadro) el FBX, los objetos entre el y la cadera (ej. "Armature") y la cadera
    void Capturar(Pose p)
    {
        var fijos = new System.Collections.Generic.List<Transform>();
        var caderas = new System.Collections.Generic.List<Transform>();
        Transform raiz = p.animator.transform;
        fijos.Add(raiz);
        foreach (SkinnedMeshRenderer s in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Transform cadera = s.rootBone != null ? s.rootBone : (s.bones != null && s.bones.Length > 0 ? s.bones[0] : null);
            if (cadera == null || !cadera.IsChildOf(raiz)) continue;
            if (!caderas.Contains(cadera)) caderas.Add(cadera);
            for (Transform t = cadera.parent; t != null && t != raiz; t = t.parent) if (!fijos.Contains(t)) fijos.Add(t);
        }
        p.fijos = fijos.ToArray();
        p.fijosPos = new Vector3[p.fijos.Length]; p.fijosEsc = new Vector3[p.fijos.Length]; p.fijosRot = new Quaternion[p.fijos.Length];
        for (int i = 0; i < p.fijos.Length; i++) { p.fijosPos[i] = p.fijos[i].localPosition; p.fijosRot[i] = p.fijos[i].localRotation; p.fijosEsc[i] = p.fijos[i].localScale; }
        p.caderas = caderas.ToArray();
        p.caderasPos = new Vector3[p.caderas.Length];
        for (int i = 0; i < p.caderas.Length; i++) p.caderasPos[i] = p.caderas[i].localPosition;
    }

    // Despues de cada cuadro: el contenedor vuelve a su sitio y la cadera no avanza (solo sube y baja con el paso)
    void Fijar(Pose p)
    {
        if (!enElSitio || p.fijos == null) return;
        for (int i = 0; i < p.fijos.Length; i++)
        {
            if (p.fijos[i] == null) continue;
            p.fijos[i].localPosition = p.fijosPos[i];
            p.fijos[i].localRotation = p.fijosRot[i];
            p.fijos[i].localScale = p.fijosEsc[i];
        }
        for (int i = 0; i < p.caderas.Length; i++)
        {
            if (p.caderas[i] == null) continue;
            Vector3 c = p.caderas[i].localPosition, r = p.caderasPos[i];
            // En el espacio del padre de la cadera: se quita el avance horizontal (respecto al FBX)
            Transform padre = p.caderas[i].parent;
            Vector3 ahora = padre != null ? padre.TransformVector(c - r) : c - r;
            Vector3 vertical = Vector3.Project(ahora, p.animator.transform.up);
            p.caderas[i].localPosition = r + (padre != null ? padre.InverseTransformVector(vertical) : vertical);
        }
    }
}
