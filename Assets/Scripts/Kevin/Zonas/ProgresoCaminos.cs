using UnityEngine;
using UnityEngine.Events;

// Orden de las zonas desde la Zona Principal. Solo esta encendido el color del camino que toca
// y solo esta abierta su puerta; los demas caminos quedan sin color y con la puerta cerrada.
// Para avanzar: CompletarZonaActual() (ej: desde un Interactable o un UnityEvent al terminar una zona).
public class ProgresoCaminos : MonoBehaviour
{
    [System.Serializable]
    public class Tramo
    {
        public string nombre;
        public MarcaCamino marca;
        public PuertaDeslizante puerta;
    }

    public static ProgresoCaminos Instance { get; private set; }

    // En orden: el primero es la primera zona a la que hay que ir
    public Tramo[] tramos;
    // Zona a la que hay que ir ahora (0 = la primera). Igual o mayor que la cantidad = todas completadas
    public int zonaActual;

    // Recibe el numero de la nueva zona
    public UnityEvent<int> alCambiarZona;

    public Tramo TramoActual => zonaActual >= 0 && zonaActual < tramos.Length ? tramos[zonaActual] : null;

    [Tooltip("Al completar una zona su puerta sigue abierta para poder volver; se cierra cuando el jugador ya la cruzo " +
             "hacia la Zona Principal y esta a estos metros de ella")]
    public float metrosParaCerrar = 2f;

    [Header("Al volver a la estacion (como en el guion)")]
    [Tooltip("La puerta de la zona siguiente no se abre al terminar la zona, sino cuando el jugador vuelve a la " +
             "Zona Principal (se cierra la de la zona terminada y despues se abre la nueva)")]
    public bool abrirSiguienteAlVolver = true;
    [Tooltip("Segundos entre que se cierra la puerta de la zona terminada y se abre la nueva")]
    public float esperaAbrir = 1.2f;
    [Tooltip("Al abrirse la nueva, el jugador se gira a mirarla (mini cinematica)")]
    public bool mirarPuertaNueva = true;

    // El jugador volvio a la Zona Principal tras terminar la zona (numero de la zona terminada)
    public event System.Action<int> AlVolver;
    // Se abrio la puerta de la zona nueva (numero de la zona)
    public event System.Action<int> AlAbrir;
    // La puerta nueva esta esperando a que el jugador vuelva
    public bool SiguienteEsperando { get; private set; }

    // Tramo recien completado cuya puerta sigue abierta hasta que el jugador sale (-1 = ninguno)
    int pendienteCerrar = -1;
    // Lado de la puerta (segun su eje azul) en el que estaba el jugador al completar la zona = lado de la zona
    float ladoZona;
    Transform jugador;

    void Awake() => Instance = this;

    [Tooltip("Las puertas de los caminos desaparecen y aparecen parpadeando (en vez de hundirse en el piso)")]
    public bool puertasConParpadeo = true;

    void Start()
    {
        if (puertasConParpadeo)
            foreach (Tramo t in tramos)
                if (t != null && t.puerta != null) t.puerta.parpadeo = true;
        Aplicar(true);
    }

    public void CompletarZonaActual() => IrAZona(zonaActual + 1);

    public void IrAZona(int indice)
    {
        int anterior = zonaActual;
        zonaActual = Mathf.Clamp(indice, 0, tramos.Length);

        // Avanzar: la puerta de la zona que se acaba de terminar se queda abierta hasta que el jugador salga
        pendienteCerrar = -1;
        if (zonaActual > anterior && anterior >= 0 && anterior < tramos.Length && tramos[anterior].puerta != null && Jugador() != null)
        {
            ladoZona = Lado(tramos[anterior].puerta.transform);
            if (Mathf.Abs(ladoZona) > 0.01f) pendienteCerrar = anterior;
        }
        // La nueva se abre cuando vuelva (si no hay puerta que esperar, ya)
        SiguienteEsperando = abrirSiguienteAlVolver && pendienteCerrar >= 0 && zonaActual < tramos.Length;
        terminada = anterior;

        Aplicar(false);
        alCambiarZona?.Invoke(zonaActual);
    }

    int terminada = -1;

    void Update()
    {
        if (pendienteCerrar < 0 || Jugador() == null) return;
        PuertaDeslizante puerta = tramos[pendienteCerrar].puerta;
        if (puerta == null) { pendienteCerrar = -1; AbrirSiguiente(); return; }

        // Ya cruzo la puerta hacia el otro lado (la Zona Principal) y se alejo un poco: se cierra detras de el
        float lado = Lado(puerta.transform);
        if (Mathf.Sign(lado) != Mathf.Sign(ladoZona) && Mathf.Abs(lado) >= metrosParaCerrar)
        {
            pendienteCerrar = -1;
            puerta.Cerrar();
            AlVolver?.Invoke(terminada);
            AbrirSiguiente();
        }
    }

    void AbrirSiguiente()
    {
        if (!SiguienteEsperando) return;
        StartCoroutine(AbrirLuego());
    }

    // Se cerro la zona terminada: un momento despues se abre la nueva y el jugador se gira a mirarla
    System.Collections.IEnumerator AbrirLuego()
    {
        int v = saltos;
        yield return new WaitForSeconds(esperaAbrir);
        if (v != saltos) yield break;   // mientras, el PanelDebug cambio la zona
        SiguienteEsperando = false;
        Aplicar(false);
        AlAbrir?.Invoke(zonaActual);
        Tramo t = TramoActual;
        if (mirarPuertaNueva && t != null && t.puerta != null) yield return Mirar(t.puerta.transform.position);
    }

    // Gira el cuerpo del jugador hacia un punto (sin mover la camara arriba/abajo), lo mantiene un momento y lo suelta
    System.Collections.IEnumerator Mirar(Vector3 punto)
    {
        if (Jugador() == null || PantallaMuerte.Muerto) yield break;
        var apagados = new System.Collections.Generic.List<Behaviour>();
        void Apagar(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; apagados.Add(b); } }
        Apagar(jugador.GetComponent<PlayerMovement>());
        foreach (MouseLook m in jugador.GetComponentsInChildren<MouseLook>()) Apagar(m);
        foreach (HeadBob h in jugador.GetComponentsInChildren<HeadBob>()) Apagar(h);
        Rigidbody rb = jugador.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        Vector3 dir = punto - jugador.position; dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion desde = jugador.rotation, hasta = Quaternion.LookRotation(dir.normalized, Vector3.up);
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                Quaternion r = Quaternion.Slerp(desde, hasta, Mathf.SmoothStep(0f, 1f, t));
                if (rb != null) rb.MoveRotation(r); else jugador.rotation = r;
                yield return null;
            }
            if (rb != null) rb.rotation = hasta;
            jugador.rotation = hasta;
        }
        yield return new WaitForSeconds(1.4f);
        // MouseLook al reactivarse toma el nuevo giro del cuerpo
        foreach (Behaviour b in apagados) if (b != null) b.enabled = true;
    }

    // Distancia con signo del jugador al plano de la puerta (positivo = delante de su eje azul)
    float Lado(Transform puerta)
    {
        Vector3 d = jugador.position - puerta.position;
        return Vector3.Dot(d, puerta.forward);
    }

    Transform Jugador()
    {
        if (jugador == null) jugador = HUD.Jugador();
        return jugador;
    }

    int saltos;

    // Debug (PanelDebug): esa zona pasa a ser la actual ya (las anteriores quedan completadas), con su puerta abierta
    // al instante, sin esperar a que el jugador vuelva a la estacion y sin la cinematica de mirar la puerta
    public void SaltarA(int indice)
    {
        saltos++;
        zonaActual = Mathf.Clamp(indice, 0, tramos.Length);
        pendienteCerrar = -1;
        SiguienteEsperando = false;
        terminada = -1;
        Aplicar(true);
        alCambiarZona?.Invoke(zonaActual);
    }

    // Por nombre (ej: "Cloacas"), para usarlo desde un UnityEvent
    public void IrAZona(string nombre)
    {
        for (int i = 0; i < tramos.Length; i++)
            if (tramos[i].nombre == nombre) { IrAZona(i); return; }
        Debug.LogWarning(name + ": no hay ningun tramo llamado " + nombre, this);
    }

    void Aplicar(bool instantaneo)
    {
        for (int i = 0; i < tramos.Length; i++)
        {
            bool activo = i == zonaActual && !SiguienteEsperando;
            if (tramos[i].marca != null) tramos[i].marca.SetEncendida(activo);
            if (tramos[i].puerta != null) tramos[i].puerta.SetAbierta(activo || i == pendienteCerrar, instantaneo);
        }
    }
}
