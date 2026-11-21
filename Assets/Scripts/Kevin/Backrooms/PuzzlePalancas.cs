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
    [Tooltip("La manija que le falta a la palanca rota")]
    public Grabbable pieza;
    [Tooltip("Tramo de ProgresoCaminos que se completa")]
    public string zona = "Backrooms";

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

    [Header("Pensamientos")]
    [TextArea] public string trasPrimera = "Algo se encendió en algún lado... Ese tablero cuenta las palancas.";
    [TextArea] public string trasTodas = "Las cuatro. Eso debería abrir algo.";

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
        yield return new WaitForSeconds(prenderTras);

        int i = Activadas;
        Activadas++;
        StartCoroutine(Prender(i));
        Contador();
        yield return new WaitForSeconds(Mathf.Max(0.5f, duracionCamara - prenderTras));

        VistaSeguridad.Instancia.Cerrar();
        yield return new WaitForSeconds(0.28f);
        Desbloquear(bloqueados);
        Ocupado = false;

        if (Completo) Completar();
        else if (Activadas == 1) Pensamiento.Decir("palancas.primera", 0.3f, trasPrimera);
    }

    void Completar()
    {
        Pensamiento.Decir("palancas.todas", 0.3f, trasTodas);
        ProgresoCaminos p = ProgresoCaminos.Instance;
        if (p != null && p.TramoActual != null && p.TramoActual.nombre == zona) p.CompletarZonaActual();
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
