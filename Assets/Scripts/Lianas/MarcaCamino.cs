using System.Collections.Generic;
using UnityEngine;

// Marca brillante en el piso que sale desde la cabina hacia una zona.
// Forma (vista desde arriba, ver dibujo): dos arcos alrededor de la cabina que se abren
// en una salida y siguen en linea recta hacia afuera, como un "embudo":
//
//          |   |        <- tramos rectos hacia la zona
//       __/     \__     <- arcos alrededor de la cabina
//
// Cada color es un objeto independiente: usar Encender() / Apagar() / SetEncendida(bool).
[ExecuteAlways]
public class MarcaCamino : MonoBehaviour
{
    [Header("Destino")]
    [Tooltip("Zona a la que lleva este camino (solo referencia, puede quedar vacio).")]
    public Transform zonaDestino;
    [Tooltip("Nombre de la zona (para identificarla, ej: Laberinto, Backrooms...).")]
    public string nombreDestino;

    [Header("Color")]
    [ColorUsage(false, true)] public Color color = new Color(0.294f, 0f, 0.51f);
    [Range(0f, 20f)] public float intensidad = 4f;

    [Header("Estado")]
    public bool encendidaAlIniciar = true;
    [Range(0.01f, 5f)] public float duracionFade = 0.6f;

    [Header("Forma")]
    [Tooltip("Direccion de salida en grados (0 = +X local, 90 = +Z local).")]
    public float anguloSalida;
    [Tooltip("Radio del anillo alrededor de la cabina.")]
    [Range(1f, 15f)] public float radioAnillo = 5f;
    [Tooltip("Cuanto recorre cada arco hacia los lados (grados). 45 = se une con el color vecino.")]
    [Range(5f, 90f)] public float arcoGrados = 44f;
    [Tooltip("Mitad del ancho de la salida (separacion entre las dos lineas rectas).")]
    [Range(0.3f, 5f)] public float mitadPasillo = 1.5f;
    [Tooltip("Distancia desde la cabina hasta donde terminan las lineas rectas.")]
    [Range(2f, 40f)] public float largo = 11.5f;
    [Range(0.05f, 2f)] public float anchoLinea = 0.45f;
    [Tooltip("Que tan redondeada es la curva entre el arco y la recta.")]
    [Range(0, 5)] public int suavizado = 3;

    public Material material;

    [Tooltip("Otros renderers que brillan con esta marca (ej: bordes de la pasarela hacia la zona).")]
    public List<Renderer> extras = new List<Renderer>();

    static readonly int IdColor = Shader.PropertyToID("_Color");
    static readonly int IdIntensidad = Shader.PropertyToID("_Intensidad");
    static readonly int IdEncendido = Shader.PropertyToID("_Encendido");
    static readonly int IdBorde = Shader.PropertyToID("_Borde");

    const string NombreVisual = "Marca (generada)";

    bool encendida;
    float valor;
    Mesh malla;
    MeshRenderer rend;
    MaterialPropertyBlock bloque;

    public bool Encendida => encendida;

    public void Encender() => SetEncendida(true);
    public void Apagar() => SetEncendida(false);
    public void Alternar() => SetEncendida(!encendida);

    public void SetEncendida(bool valorNuevo)
    {
        encendida = valorNuevo;
        if (!Application.isPlaying) { valor = encendida ? 1f : 0f; AplicarPropiedades(); }
    }

    void OnEnable()
    {
        encendida = encendidaAlIniciar;
        valor = encendida ? 1f : 0f;
        Construir();
    }

    void OnDisable()
    {
        if (rend) rend.enabled = false;
    }

    // Para que se vean bien al abrir la escena en el editor.
    void Start() => AplicarPropiedades();

    void OnValidate()
    {
#if UNITY_EDITOR
        // Reconstruir en el siguiente frame del editor (no se puede crear objetos dentro de OnValidate).
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!this || !isActiveAndEnabled) return;
            if (!Application.isPlaying) { encendida = encendidaAlIniciar; valor = encendida ? 1f : 0f; }
            Construir();
        };
#endif
    }

    void Update()
    {
        if (!rend) return;
        float objetivo = encendida ? 1f : 0f;
        if (!Mathf.Approximately(valor, objetivo))
        {
            valor = Mathf.MoveTowards(valor, objetivo, Time.deltaTime / Mathf.Max(0.01f, duracionFade));
            AplicarPropiedades();
        }
    }

    void OnDestroy()
    {
        if (malla)
        {
            if (Application.isPlaying) Destroy(malla);
            else DestroyImmediate(malla);
        }
    }

    // ------------------------------------------------------------------

    void Construir()
    {
        if (!material) material = Resources.Load<Material>("MarcaPiso");

        Transform visual = transform.Find(NombreVisual);
        if (!visual)
        {
            var go = new GameObject(NombreVisual, typeof(MeshFilter), typeof(MeshRenderer));
            go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            visual = go.transform;
            visual.SetParent(transform, false);
        }

        if (!malla)
        {
            malla = new Mesh { name = "Marca " + name };
            malla.hideFlags = HideFlags.DontSave;
        }
        GenerarMalla(malla);

        visual.GetComponent<MeshFilter>().sharedMesh = malla;
        rend = visual.GetComponent<MeshRenderer>();
        rend.enabled = true;
        rend.sharedMaterial = material;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        AplicarPropiedades();
    }

    void AplicarPropiedades()
    {
        if (!rend) return;
        if (bloque == null) bloque = new MaterialPropertyBlock();
        rend.GetPropertyBlock(bloque);
        bloque.SetColor(IdColor, color);
        bloque.SetFloat(IdIntensidad, intensidad);
        bloque.SetFloat(IdEncendido, valor);
        rend.SetPropertyBlock(bloque);

        foreach (Renderer r in extras)
        {
            if (!r) continue;
            r.GetPropertyBlock(bloque);
            bloque.SetColor(IdColor, color);
            bloque.SetFloat(IdIntensidad, intensidad);
            bloque.SetFloat(IdEncendido, valor);
            bloque.SetFloat(IdBorde, 0.01f); // los cubos no necesitan bordes difuminados
            r.SetPropertyBlock(bloque);
        }
    }

    void GenerarMalla(Mesh m)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float a = anguloSalida * Mathf.Deg2Rad;
        float w = Mathf.Min(mitadPasillo, radioAnillo * 0.95f);
        float phi = Mathf.Asin(w / radioAnillo); // angulo donde el arco llega a la salida
        float arco = Mathf.Max(arcoGrados * Mathf.Deg2Rad, phi + 0.05f);

        for (int lado = -1; lado <= 1; lado += 2)
        {
            var pts = new List<Vector3>();

            // Arco: desde el extremo (lejos de la salida) hasta el borde de la salida.
            int pasosArco = 24;
            for (int i = 0; i <= pasosArco; i++)
            {
                float t = Mathf.Lerp(arco, phi, i / (float)pasosArco);
                float ang = a + lado * t;
                pts.Add(new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radioAnillo);
            }

            // Recta hacia afuera, paralela a la direccion de salida.
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 perp = new Vector3(Mathf.Cos(a + lado * Mathf.PI * 0.5f), 0f, Mathf.Sin(a + lado * Mathf.PI * 0.5f));
            float inicio = radioAnillo * Mathf.Cos(phi);
            int pasosRecta = Mathf.Max(2, Mathf.CeilToInt((largo - inicio) / 0.4f));
            for (int i = 1; i <= pasosRecta; i++)
            {
                float t = Mathf.Lerp(inicio, largo, i / (float)pasosRecta);
                pts.Add(d * t + perp * w);
            }

            // Redondear la esquina entre el arco y la recta (Chaikin).
            for (int s = 0; s < suavizado; s++) pts = Chaikin(pts);

            AgregarCinta(pts, verts, uvs, tris);
        }

        m.Clear();
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
    }

    static List<Vector3> Chaikin(List<Vector3> p)
    {
        var r = new List<Vector3>(p.Count * 2) { p[0] };
        for (int i = 0; i < p.Count - 1; i++)
        {
            r.Add(Vector3.Lerp(p[i], p[i + 1], 0.25f));
            r.Add(Vector3.Lerp(p[i], p[i + 1], 0.75f));
        }
        r.Add(p[p.Count - 1]);
        return r;
    }

    void AgregarCinta(List<Vector3> pts, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
    {
        int baseIdx = verts.Count;
        float dist = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            if (i > 0) dist += Vector3.Distance(pts[i - 1], pts[i]);
            Vector3 tan = (pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]).normalized;
            Vector3 lado = Vector3.Cross(Vector3.up, tan).normalized * (anchoLinea * 0.5f);

            // Puntas redondeadas: se adelgaza un poco en los extremos.
            float f = i / (float)(pts.Count - 1);
            float puntas = Mathf.Clamp01(Mathf.Min(f, 1f - f) * 12f);
            lado *= Mathf.Lerp(0.5f, 1f, puntas);

            verts.Add(pts[i] - lado);
            verts.Add(pts[i] + lado);
            uvs.Add(new Vector2(dist, 0f));
            uvs.Add(new Vector2(dist, 1f));

            if (i > 0)
            {
                int v = baseIdx + i * 2;
                tris.Add(v - 2); tris.Add(v); tris.Add(v - 1);
                tris.Add(v - 1); tris.Add(v); tris.Add(v + 1);
            }
        }
    }
}
