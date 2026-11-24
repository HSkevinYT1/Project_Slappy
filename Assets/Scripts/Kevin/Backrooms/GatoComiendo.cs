using UnityEngine;

// El gato de sombra comiendose el cadaver (el cliente) en la habitacion de los Backrooms, mientras espera encima
// del cuerpo. No hay FBX de "comer": se hace moviendo los huesos de su pose (cuello y cabeza hacia el cuerpo):
// - baja la cabeza, muerde y sacude (roe), tira hacia atras arrancando (el cadaver da un tiron y saltan pedacitos
//   de sangre) y mastica cabeceando. Cada mordida dura distinto, para que no se vea en bucle.
// - Alertar(): el jugador lo vio: deja de comer, levanta la cabeza de golpe y lo mira.
// - Se quita solo cuando el gato salta del cuerpo (su agente vuelve a moverlo) o cuando se activa su IA.
// Lo agrega BackroomsGuion al poner al gato encima del cadaver.
[DefaultExecutionOrder(500)]   // despues de AnimacionesModelo (que pone la pose del esqueleto en su LateUpdate)
public class GatoComiendo : MonoBehaviour
{
    public Transform cadaver;
    [Tooltip("Material para los pedacitos que salen al arrancar (ej. el de la mancha de sangre). Vacio = sin pedacitos")]
    public Material sangre;
    [Range(0f, 1f)] public float pedacitos = 1f;

    // Huesos (los del metarig: spine.002 = pecho, spine.004/005 = cuello, spine.006 = cabeza)
    Transform pecho, cuello, cabeza;
    Transform raizPose;
    // Para no acumular el giro si nadie vuelve a poner la pose (animacion apagada): lo que habia y lo que se puso
    Quaternion basePecho, baseCuello, baseCabeza, puestoPecho, puestoCuello, puestoCabeza;

    JefeIndigoAI ia;
    UnityEngine.AI.NavMeshAgent agente;
    float t, largo, alerta;
    Transform mirarA;
    bool arranco;
    Vector3 cadaverPos;
    Quaternion cadaverRot;
    float tiron;

    void Start()
    {
        ia = GetComponent<JefeIndigoAI>();
        agente = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (cadaver != null) { cadaverPos = cadaver.localPosition; cadaverRot = cadaver.localRotation; }
        NuevaMordida();
    }

    // El jugador lo vio: levanta la cabeza y lo mira
    public void Alertar(Transform jugador) => mirarA = jugador;

    void OnDisable() => SoltarCadaver();
    void OnDestroy() => SoltarCadaver();

    void SoltarCadaver()
    {
        if (cadaver != null) { cadaver.localPosition = cadaverPos; cadaver.localRotation = cadaverRot; }
    }

    void NuevaMordida()
    {
        t = 0f;
        largo = Random.Range(1.6f, 2.6f);
        arranco = false;
    }

    void LateUpdate()
    {
        // Se bajo del cuerpo (salto o salio a buscar al jugador): se acabo la comida
        if ((agente != null && agente.updatePosition) || (ia != null && ia.enabled)) { Destroy(this); return; }
        if (!Huesos()) return;

        float dt = Time.deltaTime;
        alerta = Mathf.MoveTowards(alerta, mirarA != null ? 1f : 0f, dt * 6f);

        // ---- La mordida: bajar, roer, arrancar, masticar
        t += dt;
        if (t >= largo) NuevaMordida();
        float f = t / largo;
        float baja;          // 0 = cabeza normal, 1 = metida en el cuerpo
        float roer = 0f;     // sacudidas rapidas de lado
        if (f < 0.18f) baja = Jugo.EntradaCubica(f / 0.18f);
        else if (f < 0.52f) { baja = 1f; roer = 1f; }
        else if (f < 0.62f)
        {
            // Arranca: tira para atras de golpe
            float k = (f - 0.52f) / 0.1f;
            baja = Mathf.Lerp(1f, -0.35f, Jugo.SalidaCubica(k));
            if (!arranco) { arranco = true; Arrancar(); }
        }
        else baja = Mathf.Lerp(-0.35f, 0.15f, (f - 0.62f) / 0.38f) + Mathf.Sin(t * 13f) * 0.12f;   // mastica

        float lado = roer * (Mathf.Sin(t * 31f) * 7f + Mathf.Sin(t * 19f) * 4f);
        float comer = 1f - alerta;

        // Hacia donde tiene la cabeza (del pecho a la cabeza): por si el modelo no mira hacia el frente del objeto
        Vector3 frente = pecho != null && cabeza != null ? cabeza.position - pecho.position : transform.forward;
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.0001f) { frente = transform.forward; frente.y = 0f; }
        if (frente.sqrMagnitude < 0.0001f) frente = Vector3.forward;
        frente.Normalize();
        Vector3 eje = Vector3.Cross(frente, Vector3.down).normalized;   // girar +grados sobre este eje = mirar hacia abajo

        Quaternion qPecho = Quaternion.AngleAxis(baja * 10f * comer, eje);
        Quaternion qCuello = Quaternion.AngleAxis(baja * 28f * comer, eje) * Quaternion.AngleAxis(lado * 0.5f * comer, Vector3.up);
        Quaternion qCabeza = Quaternion.AngleAxis(baja * 22f * comer, eje) * Quaternion.AngleAxis(lado * comer, frente);

        // ---- Alerta: la cabeza sube y gira hacia el jugador
        if (alerta > 0f && mirarA != null && cabeza != null)
        {
            Vector3 haciaJugador = mirarA.position + Vector3.up * 1.5f - cabeza.position;
            Vector3 plano = haciaJugador; plano.y = 0f;
            float giro = plano.sqrMagnitude > 0.001f ? Mathf.Clamp(Vector3.SignedAngle(frente, plano, Vector3.up), -70f, 70f) : 0f;
            float arriba = Mathf.Clamp(Vector3.SignedAngle(plano.sqrMagnitude > 0.001f ? plano : frente, haciaJugador, eje), -35f, 10f);
            float k = Jugo.SalidaAtras(alerta, 1.6f);
            qCuello = Quaternion.AngleAxis(giro * 0.5f * k, Vector3.up) * Quaternion.AngleAxis((arriba * 0.5f - 12f) * k, eje) * qCuello;
            qCabeza = Quaternion.AngleAxis(giro * 0.5f * k, Vector3.up) * Quaternion.AngleAxis(arriba * 0.5f * k, eje) * qCabeza;
        }

        Aplicar(pecho, ref basePecho, ref puestoPecho, qPecho);
        Aplicar(cuello, ref baseCuello, ref puestoCuello, qCuello);
        Aplicar(cabeza, ref baseCabeza, ref puestoCabeza, qCabeza);

        // ---- El cadaver da un tiron cuando arranca (y vuelve a su sitio)
        tiron = Mathf.MoveTowards(tiron, 0f, dt * 5f);
        if (cadaver != null)
        {
            float s = tiron * tiron;
            cadaver.localPosition = cadaverPos + cadaver.parent.InverseTransformVector(frente * 0.05f * s + Vector3.up * 0.025f * s);
            cadaver.localRotation = cadaverRot;
            cadaver.Rotate(eje, -3f * s, Space.World);
        }
    }

    // Giro en espacio de mundo encima de la pose (la que puso AnimacionesModelo este cuadro, o la ultima si no la puso)
    static void Aplicar(Transform hueso, ref Quaternion baseRot, ref Quaternion puesto, Quaternion giro)
    {
        if (hueso == null) return;
        if (hueso.localRotation != puesto) baseRot = hueso.localRotation;   // alguien puso la pose de nuevo
        hueso.localRotation = baseRot;
        hueso.rotation = giro * hueso.rotation;
        puesto = hueso.localRotation;
    }

    void Arrancar()
    {
        tiron = 1f;
        if (sangre == null || pedacitos <= 0f || cabeza == null) return;
        int n = Mathf.RoundToInt(Random.Range(3, 7) * pedacitos);
        for (int i = 0; i < n; i++)
        {
            var p = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
            p.name = "Pedacito";
            p.layer = 2;   // Ignore Raycast: no estorba al apuntar
            Object.Destroy(p.GetComponent<Collider>());
            p.transform.position = cabeza.position + Random.insideUnitSphere * 0.04f;
            p.transform.rotation = Random.rotation;
            p.transform.localScale = Vector3.one * Random.Range(0.012f, 0.03f);
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = sangre;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Con fisica pero sin collider: sale volando, cae y desaparece sin chocar con nada
            var rb = p.AddComponent<Rigidbody>();
            rb.mass = 0.01f;
            rb.linearVelocity = Vector3.up * Random.Range(0.6f, 1.3f) + Random.insideUnitSphere * 0.7f;
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            Object.Destroy(p, Random.Range(0.9f, 1.4f));
        }
    }

    // Los huesos del esqueleto que se ve ahora (cada animacion es su propio FBX: se buscan en el visible)
    bool Huesos()
    {
        Transform visible = null;
        foreach (SkinnedMeshRenderer s in GetComponentsInChildren<SkinnedMeshRenderer>())
            if (s.enabled && s.rootBone != null) { visible = s.rootBone; break; }
        if (visible == null) return false;
        // Sube hasta la raiz del FBX (el que tiene el Animator) y busca ahi
        Transform raiz = visible;
        Animator a = visible.GetComponentInParent<Animator>();
        if (a != null) raiz = a.transform;
        if (raiz == raizPose && cabeza != null) return true;
        raizPose = raiz;
        pecho = Buscar(raiz, "spine.002");
        cuello = Buscar(raiz, "spine.004") ?? Buscar(raiz, "spine.005");
        cabeza = Buscar(raiz, "spine.006");
        if (cabeza == null) { cabeza = Buscar(raiz, "head") ?? Buscar(raiz, "Head"); cuello = cuello ?? Buscar(raiz, "neck") ?? Buscar(raiz, "Neck"); }
        puestoPecho = puestoCuello = puestoCabeza = new Quaternion(0f, 0f, 0f, 0f);   // fuerza a tomar la pose actual
        return cabeza != null || cuello != null;
    }

    static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == nombre) return t;
        return null;
    }
}
