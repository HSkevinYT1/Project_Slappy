using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

// Minijuego de los Backrooms: 4 palancas repartidas por el mapa. Cada vez que se baja una, la imagen corta a una
// camara de seguridad (con filtro CCTV, ver VistaSeguridad) que mira el tablero de energia: se ve como se prende
// un foco mas. Una palanca esta rota (le falta la manija): la manija esta donde llevan las indicaciones ocultas
// de la piedra. Con las 4 bajadas se completa la zona (ProgresoCaminos) y se abre el camino siguiente.
// Lo arma Tools > Backrooms > Palancas (BackroomsPalancasSetup).
public class PuzzlePalancas : MonoBehaviour
{
    public static PuzzlePalancas Instance { get; private set; }

    public Palanca[] palancas;
    [Tooltip("Focos del tablero, en orden (se prende uno por cada palanca bajada)")]
    public Renderer[] focos;
    public Light[] lucesFocos;
    [Tooltip("Texto del tablero (ej: 2 / 4)")]
    public TextMeshPro contador;
    [Tooltip("Donde esta la camara de seguridad que mira el tablero (su eje azul apunta al tablero)")]
    public Transform camaraSeguridad;
    public string nombreCamara = "CAM 04  -  SALA DE ENERGIA";
    [Tooltip("Material con el shader Slappy/CamaraSeguridad")]
    public Material filtroCamara;
    [Tooltip("La manija que le falta a la palanca rota (si hay una rota)")]
    public Grabbable pieza;
    [Tooltip("Tramo de ProgresoCaminos que se completa")]
    public string zona = "Backrooms";

    [Header("Boton y portal (al bajar todas)")]
    [Tooltip("Boton debajo de los focos: se desbloquea (y brilla) al bajar la ultima palanca")]
    public Interactable boton;
    public Transform botonPulsador;
    public Renderer luzBoton;
    public Highlightable brilloBoton;
    [Tooltip("Donde deja el portal al jugador (la salida de la zona, mirando hacia la estacion)")]
    public Transform salida;
    public Material materialPortal;
    [Tooltip("Lo que desaparece al salir por el portal (la puerta de la entrada de los Backrooms, que quedaba abierta " +
             "pegada al muro). Vacio = se busca \"Puerta Entrada\" en los Backrooms")]
    public GameObject[] quitarAlSalir;

    public bool BotonListo { get; private set; }
    public bool PortalAbierto { get; private set; }

    [Header("Focos")]
    [ColorUsage(false, true)] public Color colorApagado = new Color(0.25f, 0.03f, 0.02f);
    [ColorUsage(false, true)] public Color colorEncendido = new Color(3.2f, 2.6f, 0.9f);
    public float intensidadLuz = 1.6f;

    [Header("Cinematica")]
    public float esperaAntes = 0.35f;
    [Tooltip("Segundos mirando el tablero")]
    public float duracionCamara = 2.8f;
    [Tooltip("Cuando se prende el foco (segundos despues de cortar a la camara)")]
    public float prenderTras = 0.8f;
    [Tooltip("Sonido de camaras (Assets/Sounds/SFX/sonido camaras): suena SOLO mientras se ve la camara (el audio es " +
             "largo: se corta con un fundido al cerrar)")]
    public AudioClip sonidoCamaras;
    [Range(0f, 1f)] public float volumenCamaras = 0.8f;
    [Tooltip("Segundo del audio desde el que empieza (se salta el silencio del principio)")]
    public float inicioSonidoCamaras;
    AudioSource fuenteCamaras;

    [Header("Pensamientos")]
    [TextArea] public string trasPrimera = "Hay más de una palanca, al parecer.";
    [TextArea] public string trasTodas = "";

    public int Activadas { get; private set; }
    public int Total => palancas != null ? palancas.Length : 0;
    public bool Completo => Total > 0 && Activadas >= Total;
    public bool Ocupado { get; private set; }

    // La rota ya se vio (y todavia no se arreglo)
    public bool FaltaManija
    {
        get
        {
            if (palancas == null) return false;
            foreach (Palanca p in palancas) if (p != null && p.rota && p.RotaDescubierta) return true;
            return false;
        }
    }

    MaterialPropertyBlock bloque;

    void Awake()
    {
        Instance = this;
        bloque = new MaterialPropertyBlock();
        foreach (Palanca p in palancas) if (p != null) p.puzzle = this;
        for (int i = 0; i < Cuantos(); i++) Foco(i, 0f);
        Contador();
        if (boton != null)
        {
            boton.onInteract.AddListener(_ => Presionar());
            boton.enabled = false;
        }
        LuzBoton(0f);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    int Cuantos() => focos != null ? focos.Length : 0;

    // La llama cada palanca al terminar de bajarse
    public void AlBajar(Palanca p)
    {
        if (Ocupado) return;
        StartCoroutine(Cinematica());
    }

    IEnumerator Cinematica()
    {
        Ocupado = true;
        yield return new WaitForSeconds(esperaAntes);

        var bloqueados = Bloquear();
        if (camaraSeguridad != null) VistaSeguridad.Instancia.Abrir(camaraSeguridad, nombreCamara, filtroCamara);
        SonidoCamaras(true);
        yield return new WaitForSeconds(prenderTras);

        int i = Activadas;
        Activadas++;
        StartCoroutine(Prender(i));
        Contador();
        // La ultima: tambien se ve como se desbloquea el boton de abajo (brilla)
        if (Completo)
        {
            yield return new WaitForSeconds(0.7f);
            DesbloquearBoton();
            yield return new WaitForSeconds(0.9f);
        }
        yield return new WaitForSeconds(Mathf.Max(0.5f, duracionCamara - prenderTras));

        VistaSeguridad.Instancia.Cerrar();
        SonidoCamaras(false);
        yield return new WaitForSeconds(0.28f);
        Desbloquear(bloqueados);
        Ocupado = false;

        if (Completo) Completar();
        else if (Activadas == 1) Pensamiento.Decir("palancas.primera", 0.3f, trasPrimera);
    }

    // Empieza al cortar a la camara y se apaga (fundido corto) al cerrarla: no suena el audio entero
    void SonidoCamaras(bool on)
    {
        if (sonidoCamaras == null) return;
        if (on)
        {
            if (fuenteCamaras == null)
            {
                fuenteCamaras = gameObject.AddComponent<AudioSource>();
                fuenteCamaras.playOnAwake = false;
                fuenteCamaras.spatialBlend = 0f;
                fuenteCamaras.loop = false;
            }
            StopCoroutine(nameof(ApagarCamaras));
            fuenteCamaras.clip = sonidoCamaras;
            fuenteCamaras.volume = volumenCamaras;
            fuenteCamaras.time = Mathf.Clamp(inicioSonidoCamaras, 0f, Mathf.Max(0f, sonidoCamaras.length - 0.05f));
            fuenteCamaras.Play();
        }
        else if (fuenteCamaras != null && fuenteCamaras.isPlaying) StartCoroutine(nameof(ApagarCamaras));
    }

    IEnumerator ApagarCamaras()
    {
        float v0 = fuenteCamaras.volume;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime) { fuenteCamaras.volume = v0 * (1f - t / 0.3f); yield return null; }
        fuenteCamaras.Stop();
        fuenteCamaras.volume = volumenCamaras;
    }

    void Completar()
    {
        Pensamiento.Decir("palancas.todas", 0.3f, trasTodas);
        // Sin boton (version vieja): se completa ya. Con boton: al cruzar el portal
        if (boton == null)
        {
            ProgresoCaminos p = ProgresoCaminos.Instance;
            if (p != null && p.TramoActual != null && p.TramoActual.nombre == zona) p.CompletarZonaActual();
        }
    }

    // ------------------------------------------------------------------ Boton y portal

    void DesbloquearBoton()
    {
        if (boton == null || BotonListo) return;
        BotonListo = true;
        boton.enabled = true;
        LuzBoton(1f);
        if (brilloBoton != null) brilloBoton.Show();
    }

    void LuzBoton(float k)
    {
        if (luzBoton == null) return;
        luzBoton.GetPropertyBlock(bloque);
        bloque.SetColor("_EmissionColor", Color.Lerp(new Color(0.05f, 0.01f, 0.01f), new Color(0.4f, 2.6f, 0.9f), k));
        bloque.SetColor("_BaseColor", Color.Lerp(new Color(0.15f, 0.05f, 0.05f), new Color(0.5f, 1f, 0.6f), k));
        luzBoton.SetPropertyBlock(bloque);
    }

    void Presionar()
    {
        if (!BotonListo || PortalAbierto) return;
        PortalAbierto = true;
        boton.enabled = false;
        if (brilloBoton != null) brilloBoton.Hide();
        StartCoroutine(AbrirPortal());
    }

    // El pulsador se hunde y aparece el portal detras del jugador
    IEnumerator AbrirPortal()
    {
        if (botonPulsador != null)
        {
            Vector3 p0 = botonPulsador.localPosition;
            for (float t = 0f; t < 0.12f; t += Time.deltaTime) { botonPulsador.localPosition = p0 - Vector3.forward * 0.02f * (t / 0.12f); yield return null; }
            botonPulsador.localPosition = p0 - Vector3.forward * 0.02f;
        }
        LuzBoton(0.25f);
        yield return new WaitForSeconds(0.5f);

        Transform j = HUD.Jugador();
        if (j == null) yield break;
        Vector3 atras = -j.forward; atras.y = 0f; atras.Normalize();
        float dist = 2.2f;
        if (Physics.Raycast(j.position + Vector3.up * 1f, atras, out RaycastHit h, dist + 0.6f, ~0, QueryTriggerInteraction.Ignore))
            dist = Mathf.Max(1.1f, h.distance - 0.6f);
        Vector3 pos = j.position + atras * dist;
        if (Physics.Raycast(pos + Vector3.up * 1f, Vector3.down, out RaycastHit hs, 3f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hs.point.y;
        PortalSalida portal = PortalSalida.Crear(pos, Quaternion.LookRotation(-atras, Vector3.up), salida, zona, materialPortal);
        portal.alCruzar += AlSalir;
    }

    // Ya afuera: la puerta de la entrada desaparece (la entrada quedo tapada por el muro)
    void AlSalir()
    {
        bool alguno = false;
        if (quitarAlSalir != null)
            foreach (GameObject g in quitarAlSalir) if (g != null) { g.SetActive(false); alguno = true; }
        if (alguno) return;
        Transform puerta = transform.root.Find("Puerta Entrada");
        if (puerta != null) puerta.gameObject.SetActive(false);
    }

    // El foco titila y se queda prendido
    IEnumerator Prender(int i)
    {
        float[] niveles = { 0.4f, 0f, 0.8f, 0.2f, 1f };
        foreach (float n in niveles)
        {
            Foco(i, n);
            yield return new WaitForSeconds(Random.Range(0.04f, 0.09f));
        }
        Foco(i, 1f);
    }

    void Foco(int i, float k)
    {
        if (focos != null && i < focos.Length && focos[i] != null)
        {
            focos[i].GetPropertyBlock(bloque);
            Color c = Color.Lerp(colorApagado, colorEncendido, k);
            bloque.SetColor("_EmissionColor", c);
            bloque.SetColor("_BaseColor", Color.Lerp(new Color(0.18f, 0.05f, 0.04f), new Color(1f, 0.92f, 0.65f), k));
            focos[i].SetPropertyBlock(bloque);
        }
        if (lucesFocos != null && i < lucesFocos.Length && lucesFocos[i] != null)
        {
            lucesFocos[i].intensity = intensidadLuz * k;
            lucesFocos[i].enabled = k > 0.01f;
        }
    }

    void Contador()
    {
        if (contador != null) contador.text = Activadas + " / " + Mathf.Max(Total, Cuantos());
    }

    // ------------------------------------------------------------------ Durante la cinematica

    // El jugador queda quieto y los enemigos se paran (para que no lo atrapen mientras mira la camara)
    List<Behaviour> Bloquear()
    {
        var lista = new List<Behaviour>();
        void Apagar(Behaviour b) { if (b != null && b.enabled) { b.enabled = false; lista.Add(b); } }
        Transform j = HUD.Jugador();
        if (j != null)
        {
            Apagar(j.GetComponent<PlayerMovement>());
            foreach (MouseLook m in j.GetComponentsInChildren<MouseLook>()) Apagar(m);
            foreach (HeadBob h in j.GetComponentsInChildren<HeadBob>()) Apagar(h);
            foreach (PlayerInteractor p in j.GetComponentsInChildren<PlayerInteractor>()) Apagar(p);
            if (j.TryGetComponent(out Rigidbody rb) && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
        foreach (JefeIndigoAI e in FindObjectsByType<JefeIndigoAI>(FindObjectsSortMode.None))
        {
            Apagar(e);
            if (e.TryGetComponent(out NavMeshAgent a) && a.isOnNavMesh) a.isStopped = true;
        }
        foreach (EstatuaAI e in FindObjectsByType<EstatuaAI>(FindObjectsSortMode.None)) Apagar(e);
        return lista;
    }

    void Desbloquear(List<Behaviour> lista)
    {
        foreach (Behaviour b in lista)
        {
            if (b == null) continue;
            b.enabled = true;
            if (b is JefeIndigoAI && b.TryGetComponent(out NavMeshAgent a) && a.isOnNavMesh) a.isStopped = false;
        }
    }
}
