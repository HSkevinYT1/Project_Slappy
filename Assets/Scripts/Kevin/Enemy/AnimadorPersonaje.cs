using UnityEngine;
using UnityEngine.AI;

// Mueve las animaciones de un personaje (su Animator, en el "Modelo Animado" que pone AnimacionesSetup):
// - "Velocidad" (m/s) y "Corriendo": quieto / caminar / correr. "Ritmo" acelera o frena el paso segun lo rapido que va.
// - "Gritar" al ver al jugador (Fede) y "Atacar" al atraparlo (el jumpscare de Slappy), si el Animator los tiene.
// Sirve para enemigos (con NavMeshAgent y JefeIndigoAI) y para NPCs que se mueven por codigo (el cliente, el cultista).
public class AnimadorPersonaje : MonoBehaviour
{
    [Tooltip("Velocidad (m/s) a la que la animacion de caminar va a su ritmo normal")]
    public float velocidadCaminar = 1.6f;
    [Tooltip("Velocidad (m/s) a la que la animacion de correr va a su ritmo normal")]
    public float velocidadCorrer = 4.5f;
    [Tooltip("Sin IA: a partir de esta velocidad corre")]
    public float umbralCorrer = 3f;

    Animator anim;
    NavMeshAgent agent;
    JefeIndigoAI ia;
    Vector3 antes;
    float suave;
    bool pVel, pCorre, pRitmo, pGritar, pAtacar;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        ia = GetComponent<JefeIndigoAI>();
        antes = transform.position;
        if (anim == null) { enabled = false; return; }
        anim.applyRootMotion = false;
        foreach (AnimatorControllerParameter p in anim.parameters)
            switch (p.name)
            {
                case "Velocidad": pVel = true; break;
                case "Corriendo": pCorre = true; break;
                case "Ritmo": pRitmo = true; break;
                case "Gritar": pGritar = true; break;
                case "Atacar": pAtacar = true; break;
            }
        if (ia != null)
        {
            if (pGritar && ia.onPlayerSpotted != null) ia.onPlayerSpotted.AddListener(() => anim.SetTrigger("Gritar"));
            if (pAtacar && ia.onPlayerCaught != null) ia.onPlayerCaught.AddListener(() => anim.SetTrigger("Atacar"));
        }
    }

    void Update()
    {
        if (anim == null || Time.deltaTime <= 0f) return;
        float v;
        if (agent != null && agent.enabled && agent.updatePosition && agent.isOnNavMesh) { Vector3 vel = agent.velocity; vel.y = 0f; v = vel.magnitude; }
        else { Vector3 d = transform.position - antes; d.y = 0f; v = d.magnitude / Time.deltaTime; }
        antes = transform.position;
        suave = Mathf.Lerp(suave, v, 1f - Mathf.Exp(-10f * Time.deltaTime));

        bool corre = ia != null ? ia.CurrentState == JefeIndigoAI.State.Chase && suave > 1.2f : suave > umbralCorrer;
        if (pVel) anim.SetFloat("Velocidad", suave);
        if (pCorre) anim.SetBool("Corriendo", corre);
        if (pRitmo)
        {
            float referencia = corre ? velocidadCorrer : velocidadCaminar;
            anim.SetFloat("Ritmo", suave < 0.1f ? 1f : Mathf.Clamp(suave / Mathf.Max(0.1f, referencia), 0.6f, 1.8f));
        }
    }
}
