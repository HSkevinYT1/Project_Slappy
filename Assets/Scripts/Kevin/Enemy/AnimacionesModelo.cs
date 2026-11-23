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
// - Atrapa al jugador (Caught) -> Jumpscare una vez, mirando al jugador.
// Lo monta Tools > Modelos > Intercambiar modelos y animaciones.
public class AnimacionesModelo : MonoBehaviour
{
    public const string Idle = "Idle", Caminar = "Caminar", Correr = "Correr", Jumpscare = "Jumpscare";

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
    }

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

    void Awake()
    {
        ia = GetComponentInParent<JefeIndigoAI>();
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
            if (soloLasQueFuncionan) Validar(p);
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
        if (!moviendose) return Primera(Idle, Caminar, Correr);
        bool correr = ia != null ? ia.CurrentState == JefeIndigoAI.State.Chase : velocidad > umbralCorrer;
        return correr ? Primera(Correr, Caminar, Idle) : Primera(Caminar, Correr, Idle);
    }

    void Mostrar(Pose p, bool ver)
    {
        if (p == null || p.renders == null) return;
        foreach (Renderer r in p.renders) if (r != null) r.enabled = ver;
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
        float dt = Time.deltaTime;
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
        if (actual.nombre == Caminar) k = moviendose ? Mathf.Clamp(velocidad / Mathf.Max(0.1f, velocidadCaminar), 0.5f, 2.5f) : 0f;
        else if (actual.nombre == Correr) k = moviendose ? Mathf.Clamp(velocidad / Mathf.Max(0.1f, velocidadCorrer), 0.6f, 2.2f) : 0f;
        actual.tiempo += dt * k;

        // Jumpscare: de frente al jugador
        if (actual.nombre == Jumpscare && ia != null && ia.player != null)
        {
            Vector3 dir = ia.player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-20f * dt));
        }

        Evaluar(actual);
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
        Destroy(malla);
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
