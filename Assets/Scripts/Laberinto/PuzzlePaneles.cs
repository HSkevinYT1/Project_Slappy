using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// Puzzle del centro del laberinto: en cada sala del laberinto hay un panel con un numero. Hay que recogerlos
// (el inventario solo tiene 3 lugares, y uno suele ser la linterna) y colocarlos en las ranuras del tablero
// del centro, ordenados de menor a mayor (de izquierda a derecha).
// - Cada ranura es un ReceptorObjeto que solo acepta paneles. Un panel colocado se puede volver a tomar.
// - Con todas llenas: si el orden es correcto se resuelve (luces verdes, se abre la reja de la entrada,
//   avanza el progreso de las zonas y se dispara "Al Resolver"); si no, parpadea en rojo.
// Los numeros cambian en cada partida (si "Numeros Aleatorios" esta marcado).
public class PuzzlePaneles : MonoBehaviour
{
    [Tooltip("Ranuras del tablero, de izquierda a derecha")]
    public ReceptorObjeto[] ranuras;
    [Tooltip("Paneles repartidos por el laberinto")]
    public PanelNumero[] paneles;
    [Tooltip("Luces/indicadores de cada ranura (mismo orden que las ranuras)")]
    public Renderer[] indicadores;
    public TextMeshPro textoEstado;

    public bool numerosAleatorios = true;

    [Header("Al resolver")]
    [Tooltip("Sube otra vez la reja de la entrada del laberinto")]
    public bool abrirReja = true;
    [Tooltip("Marca el laberinto como completado (ProgresoCaminos)")]
    public bool completarZona = true;
    public UnityEvent alResolver;

    [Header("Sonidos (vacio = generados)")]
    public AudioClip sonidoColocar;
    public AudioClip sonidoError;
    public AudioClip sonidoResuelto;

    public bool Resuelto { get; private set; }

    PanelNumero[] colocados;
    MaterialPropertyBlock bloque;
    Color[] colores;
    AudioSource audioFuente;
    bool error;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    static readonly Color Apagado = new Color(0.08f, 0.08f, 0.08f);
    static readonly Color Ocupado = new Color(1f, 0.75f, 0.3f);
    static readonly Color Rojo = new Color(1f, 0.1f, 0.05f);
    static readonly Color Verde = new Color(0.2f, 1f, 0.35f);

    void Awake()
    {
        colocados = new PanelNumero[ranuras.Length];
        colores = new Color[ranuras.Length];
        bloque = new MaterialPropertyBlock();
        audioFuente = gameObject.AddComponent<AudioSource>();
        audioFuente.spatialBlend = 1f;
        audioFuente.minDistance = 3f;
        audioFuente.maxDistance = 30f;
        if (sonidoColocar == null) sonidoColocar = HUD.Clic("Colocar", 180f, 0.18f, 0.5f);
        if (sonidoError == null) sonidoError = Tono("Error", new[] { 220f, 160f }, 0.22f);
        if (sonidoResuelto == null) sonidoResuelto = Tono("Resuelto", new[] { 392f, 523f, 659f, 784f }, 0.16f);

        if (numerosAleatorios) Repartir();

        for (int i = 0; i < ranuras.Length; i++)
        {
            int k = i;
            ReceptorObjeto r = ranuras[i];
            if (r == null) continue;
            r.filtro = g => g != null && g.GetComponent<PanelNumero>() != null;
            r.alRecibir.AddListener(g => Colocado(k, g));
            colores[i] = Apagado;
        }
        ActualizarTexto();
    }

    // Numeros distintos del 1 al 9, mezclados entre los paneles
    void Repartir()
    {
        var disponibles = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        foreach (PanelNumero p in paneles)
        {
            if (p == null || disponibles.Count == 0) continue;
            int i = Random.Range(0, disponibles.Count);
            p.SetNumero(disponibles[i]);
            disponibles.RemoveAt(i);
        }
    }

    void Colocado(int i, Grabbable g)
    {
        PanelNumero p = g.GetComponent<PanelNumero>();
        colocados[i] = p;
        // Se puede volver a tomar (por si estaba mal)
        foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) c.enabled = true;
        if (g.TryGetComponent(out Interactable inter)) { inter.enabled = !Resuelto; inter.SetPrompt("Quitar panel"); }
        audioFuente.PlayOneShot(sonidoColocar, 0.9f);
        colores[i] = Ocupado;
        ActualizarTexto();
        Verificar();
    }

    void Update()
    {
        // Paneles que el jugador volvio a tomar
        for (int i = 0; i < colocados.Length; i++)
        {
            PanelNumero p = colocados[i];
            if (p == null) continue;
            Transform punto = ranuras[i].puntoMano != null ? ranuras[i].puntoMano : ranuras[i].transform;
            if (p.transform.IsChildOf(punto)) continue;
            colocados[i] = null;
            if (p.TryGetComponent(out Interactable inter)) inter.SetPrompt("Tomar panel");
            ranuras[i].Reabrir();
            colores[i] = Apagado;
            ActualizarTexto();
        }

        if (indicadores != null)
            for (int i = 0; i < indicadores.Length && i < colores.Length; i++)
            {
                Renderer r = indicadores[i];
                if (r == null) continue;
                Color c = colores[i];
                if (error) c = Color.Lerp(Apagado, Rojo, Mathf.PingPong(Time.time * 8f, 1f));
                r.GetPropertyBlock(bloque);
                bloque.SetColor(BaseColorId, c);
                bloque.SetColor(EmissionId, c * (c == Apagado ? 0f : 3f));
                r.SetPropertyBlock(bloque);
            }
    }

    void Verificar()
    {
        if (Resuelto) return;
        int ultimo = int.MinValue;
        foreach (PanelNumero p in colocados) if (p == null) return;
        foreach (PanelNumero p in colocados)
        {
            if (p.numero < ultimo) { StartCoroutine(Error()); return; }
            ultimo = p.numero;
        }
        Resolver();
    }

    IEnumerator Error()
    {
        error = true;
        audioFuente.PlayOneShot(sonidoError, 1f);
        if (textoEstado != null) textoEstado.text = "<color=#ff3a2a>ASÍ NO</color>";
        HUD.Aviso("Ese no es el orden...", 2.5f);
        yield return new WaitForSeconds(1.6f);
        error = false;
        ActualizarTexto();
    }

    void Resolver()
    {
        Resuelto = true;
        for (int i = 0; i < colocados.Length; i++)
        {
            colores[i] = Verde;
            if (colocados[i] != null && colocados[i].TryGetComponent(out Interactable inter)) inter.enabled = false;
            ranuras[i].enabled = false;
        }
        audioFuente.PlayOneShot(sonidoResuelto, 1f);
        if (textoEstado != null) textoEstado.text = "<color=#5cff7a>ABIERTO</color>";
        HUD.Aviso("Algo se abrió a lo lejos...", 4f);

        if (abrirReja)
            foreach (RejasEntrada r in FindObjectsByType<RejasEntrada>(FindObjectsSortMode.None)) r.Abrir();
        if (completarZona && ProgresoCaminos.Instance != null)
        {
            ProgresoCaminos.Tramo t = ProgresoCaminos.Instance.TramoActual;
            if (t != null && t.nombre == "Laberinto") ProgresoCaminos.Instance.CompletarZonaActual();
        }
        alResolver?.Invoke();
    }

    void ActualizarTexto()
    {
        if (textoEstado == null || Resuelto) return;
        int n = 0;
        foreach (PanelNumero p in colocados) if (p != null) n++;
        textoEstado.text = n + " / " + ranuras.Length;
    }

    static AudioClip Tono(string nombre, float[] notas, float duracionNota)
    {
        int porNota = Mathf.RoundToInt(44100 * duracionNota);
        var d = new float[porNota * notas.Length + 22050];
        for (int k = 0; k < notas.Length; k++)
            for (int i = 0; i < porNota + 22050 && k * porNota + i < d.Length; i++)
            {
                float t = i / 44100f;
                d[k * porNota + i] += Mathf.Sin(2f * Mathf.PI * notas[k] * t) * Mathf.Exp(-t * 4f) * 0.35f;
            }
        AudioClip c = AudioClip.Create(nombre, d.Length, 1, 44100, false);
        c.SetData(d, 0);
        return c;
    }
}
