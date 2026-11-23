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
    }

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
        foreach (Pose p in poses)
        {
            if (p == null || p.animator == null) continue;
            p.renders = p.animator.GetComponentsInChildren<Renderer>(true);
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
        }
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

    public Pose Buscar(string nombre)
    {
        foreach (Pose p in poses) if (p != null && p.animator != null && p.nombre == nombre) return p;
        return null;
    }

    Pose Primera(params string[] nombres)
    {
        foreach (string n in nombres) { Pose p = Buscar(n); if (p != null) return p; }
        foreach (Pose p in poses) if (p != null && p.animator != null) return p;
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
            float t = p.bucle ? Mathf.Repeat(p.tiempo, largo) : Mathf.Min(p.tiempo, largo);
            p.playables[i].SetTime(t);
        }
        p.grafo.Evaluate(0f);
    }
}
