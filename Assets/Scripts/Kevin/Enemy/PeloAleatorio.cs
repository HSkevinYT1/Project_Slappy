using System.Collections.Generic;
using UnityEngine;

// El pelo de cada muñeca de las Cloacas es distinto: al aparecer se le elige un peinado (deformando la malla del pelo)
// y un color. Se reparten "en bolsa" (sin repetir hasta usar todos), asi dos muñecas seguidas nunca tienen el mismo
// peinado ni el mismo color.
// Va en el hijo "Pelo" del modelo de la muñeca (lo pone Tools > Cloacas > Modelo de la muñeca, MunecaModeloSetup).
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PeloAleatorio : MonoBehaviour
{
    public enum Estilo { Original, Largo, Corto, Esponjado, Despeinado, Alto, Ladeado }

    [Tooltip("Hacia arriba de la muñeca, en el espacio local de este objeto (lo calcula el setup)")]
    public Vector3 arriba = Vector3.up;
    [Tooltip("Hacia el frente de la muñeca (donde tiene la cara), en el espacio local de este objeto")]
    public Vector3 frente = Vector3.forward;
    [Tooltip("Colores posibles del pelo. Apagados a proposito: con la linterna (y el bloom de las Cloacas) lo claro brilla como un sol")]
    public Color[] colores =
    {
        new Color(0.05f, 0.04f, 0.04f),   // negro
        new Color(0.18f, 0.10f, 0.06f),   // castaño oscuro
        new Color(0.40f, 0.27f, 0.18f),   // castaño claro (el original)
        new Color(0.45f, 0.36f, 0.19f),   // rubio
        new Color(0.45f, 0.42f, 0.35f),   // rubio platino
        new Color(0.40f, 0.13f, 0.05f),   // pelirrojo
        new Color(0.42f, 0.42f, 0.41f),   // canoso
        new Color(0.25f, 0.24f, 0.23f),   // gris ceniza
        new Color(0.28f, 0.03f, 0.06f),   // rojo vino
        new Color(0.06f, 0.07f, 0.14f),   // negro azulado
    };
    [Tooltip("Brillo (V de HSV) maximo del color: por encima, la linterna lo quema")]
    [Range(0f, 1f)] public float brilloMaximo = 0.45f;

    public Estilo EstiloElegido { get; private set; }
    public Color ColorElegido { get; private set; }

    static List<int> bolsaEstilos, bolsaColores;
    static int iEstilo, iColor;
    static int ultimoEstilo = -1, ultimoColor = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar() { bolsaEstilos = bolsaColores = null; iEstilo = iColor = 0; ultimoEstilo = ultimoColor = -1; }

    Mesh copia;
    Material material;

    void Awake()
    {
        int n = System.Enum.GetValues(typeof(Estilo)).Length;
        EstiloElegido = (Estilo)Siguiente(ref bolsaEstilos, ref iEstilo, ref ultimoEstilo, n);
        ColorElegido = colores != null && colores.Length > 0
            ? colores[Siguiente(ref bolsaColores, ref iColor, ref ultimoColor, colores.Length)]
            : new Color(0.8f, 0.54f, 0.37f);
        // Un poco de variacion para que ni dos del mismo color sean identicas
        Color.RGBToHSV(ColorElegido, out float h, out float s, out float v);
        ColorElegido = Color.HSVToRGB(Mathf.Repeat(h + Random.Range(-0.015f, 0.015f), 1f), s, Mathf.Min(brilloMaximo, v * Random.Range(0.9f, 1.08f)));

        var mf = GetComponent<MeshFilter>();
        if (EstiloElegido != Estilo.Original && mf.sharedMesh != null && mf.sharedMesh.isReadable)
        {
            copia = Deformar(mf.sharedMesh, EstiloElegido);
            mf.sharedMesh = copia;
        }

        // Material propio (no un MaterialPropertyBlock): MunecaLuz lee el color del material para ponerla al rojo
        var mr = GetComponent<MeshRenderer>();
        if (mr.sharedMaterial != null)
        {
            material = new Material(mr.sharedMaterial) { name = mr.sharedMaterial.name + " (" + EstiloElegido + ")" };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", ColorElegido);
            if (material.HasProperty("_Color")) material.SetColor("_Color", ColorElegido);
            mr.sharedMaterial = material;
        }
    }

    void OnDestroy()
    {
        if (copia != null) Destroy(copia);
        // Con espera: los trozos de la muñeca rota (PedazoMuneca) siguen usando el material unos segundos
        if (material != null) Destroy(material, 8f);
    }

    // Saca el siguiente de una bolsa barajada; al rellenarla, el primero nunca es el ultimo que salio
    static int Siguiente(ref List<int> bolsa, ref int i, ref int ultimo, int n)
    {
        if (n <= 1) return 0;
        if (bolsa == null || bolsa.Count != n || i >= bolsa.Count)
        {
            bolsa = new List<int>(n);
            for (int k = 0; k < n; k++) bolsa.Insert(Random.Range(0, bolsa.Count + 1), k);
            if (bolsa[0] == ultimo) { bolsa[0] = bolsa[n - 1]; bolsa[n - 1] = ultimo; }
            i = 0;
        }
        ultimo = bolsa[i++];
        return ultimo;
    }

    Mesh Deformar(Mesh origen, Estilo estilo)
    {
        var m = Instantiate(origen);
        m.name = origen.name + " " + estilo;
        Vector3[] v = m.vertices;

        Vector3 up = arriba.sqrMagnitude > 0f ? arriba.normalized : Vector3.up;
        Vector3 fw = Vector3.ProjectOnPlane(frente, up);
        fw = fw.sqrMagnitude > 0f ? fw.normalized : Vector3.Cross(up, Vector3.right).normalized;
        Vector3 der = Vector3.Cross(up, fw);
        Vector3 c = origen.bounds.center;

        float bajo = float.MaxValue, alto = float.MinValue, radio = 0f;
        foreach (Vector3 p in v)
        {
            Vector3 d = p - c;
            float hgt = Vector3.Dot(d, up);
            bajo = Mathf.Min(bajo, hgt);
            alto = Mathf.Max(alto, hgt);
            radio = Mathf.Max(radio, (d - up * hgt).magnitude);
        }
        float largo = Mathf.Max(0.0001f, alto - bajo);
        float lado = Random.value < 0.5f ? -1f : 1f;
        float semilla = Random.Range(0f, 100f);

        for (int i = 0; i < v.Length; i++)
        {
            Vector3 d = v[i] - c;
            float hgt = Vector3.Dot(d, up);
            float t = (hgt - bajo) / largo;            // 0 abajo, 1 arriba
            Vector3 hor = d - up * hgt;
            Vector3 dirHor = hor.sqrMagnitude > 1e-10f ? hor.normalized : Vector3.zero;
            float alFrente = Vector3.Dot(dirHor, fw);  // 1 la cara, -1 la nuca
            float atras = Mathf.InverseLerp(0.4f, -0.2f, alFrente);   // el flequillo no se toca

            switch (estilo)
            {
                case Estilo.Largo:
                    // Lo de abajo (nuca y costados) cae hasta la espalda
                    if (t < 0.65f) d -= up * (largo * 1.1f * Mathf.Pow((0.65f - t) / 0.65f, 0.8f) * atras);
                    d += dirHor * (radio * 0.06f * (1f - t));
                    break;
                case Estilo.Corto:
                    // Melena corta: la parte de abajo sube y se pega a la cabeza
                    if (t < 0.55f) d += up * (largo * 0.5f * (0.55f - t) / 0.55f * Mathf.Lerp(0.5f, 1f, atras));
                    d -= dirHor * (radio * 0.04f);
                    break;
                case Estilo.Esponjado:
                    d = up * (hgt * 1.12f + largo * 0.04f) + hor * (1.18f + 0.22f * t);
                    break;
                case Estilo.Despeinado:
                {
                    // Mechones: el ruido depende de la posicion (los vertices repetidos de las costuras se mueven igual)
                    Vector3 p = v[i] / Mathf.Max(0.0001f, radio);
                    float n = Mathf.Sin(p.x * 9.1f + semilla) * Mathf.Sin(p.y * 7.3f - semilla * 0.7f) * Mathf.Sin(p.z * 8.2f + semilla * 1.3f);
                    float r = Mathf.PerlinNoise(p.x * 3.1f + semilla, p.z * 3.1f + p.y * 1.7f);
                    d += (dirHor * 0.8f + up * 0.35f) * (radio * (0.16f * n + 0.08f * r) * (0.4f + 0.6f * (1f - t)));
                    break;
                }
                case Estilo.Alto:
                    // Peinado hacia arriba (mono o tupe)
                    if (t > 0.45f) d += up * (largo * 0.55f * Mathf.Pow((t - 0.45f) / 0.55f, 1.5f));
                    d -= hor * (0.12f * Mathf.Clamp01((t - 0.6f) / 0.4f));
                    break;
                case Estilo.Ladeado:
                {
                    // Raya al costado: todo se va hacia un lado y ese lado cae mas largo
                    float deEseLado = Mathf.Clamp01(Vector3.Dot(dirHor, der) * lado + 0.3f);
                    d += der * (lado * radio * 0.22f * (1f - t));
                    if (t < 0.6f) d -= up * (largo * 0.6f * (0.6f - t) / 0.6f * deEseLado * atras);
                    break;
                }
            }
            v[i] = c + d;
        }

        m.vertices = v;
        m.RecalculateNormals();
        if (origen.tangents != null && origen.tangents.Length == v.Length) m.RecalculateTangents();
        m.RecalculateBounds();
        return m;
    }
}
