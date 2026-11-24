using System.Collections.Generic;
using UnityEngine;

// Una muñeca de las Cloacas que se rompe como porcelana (la llama MunecaLuz al destruirla con la linterna):
// - Romper(): su propia malla (cuerpo y pelo) se corta en trozos con fisica que salen despedidos, todavia al rojo,
//   mas astillas chiquitas y el sonido de la porcelana haciendose añicos.
// - Cada trozo (este componente) se va enfriando, queda un rato en el piso, se encoge y desaparece.
// Los trozos estan en la capa "Ignore Raycast" y no chocan con el jugador (no estorban ni para interactuar).
public class PedazoMuneca : MonoBehaviour
{
    [Tooltip("Segundos hasta que desaparece")]
    public float vida = 4f;
    [Tooltip("Segundos en que se le va el rojo")]
    public float enfriar = 1.2f;

    Mesh malla;
    Renderer rend;
    MaterialPropertyBlock bloque;
    Vector3 escala;
    float t;

    static readonly Color Rojo = new Color(0.75f, 0.12f, 0.05f);

    void Start()
    {
        rend = GetComponent<Renderer>();
        bloque = new MaterialPropertyBlock();
        escala = transform.localScale;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (rend != null)
        {
            float k = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, enfriar));
            Material[] mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (k <= 0f) { rend.SetPropertyBlock(null, i); continue; }
                Color c = mats[i] != null && mats[i].HasProperty("_BaseColor") ? mats[i].GetColor("_BaseColor") : Color.white;
                bloque.Clear();
                bloque.SetColor("_BaseColor", Color.Lerp(c, Rojo, 0.75f * k));
                rend.SetPropertyBlock(bloque, i);
            }
        }
        // Al final se encoge y desaparece
        float fin = Mathf.Clamp01((vida - t) / 0.6f);
        transform.localScale = escala * fin;
        if (t >= vida) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (malla != null) Destroy(malla);
    }

    // ------------------------------------------------------------------ Romper

    class Trozo
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly Dictionary<Material, List<int>> tris = new Dictionary<Material, List<int>>();
        public Vector3 suma;
        public int cuenta;
    }

    static AudioClip sonidoRotura;

    // partes: los renderers de la muñeca (sin la llave). centro: de donde salen despedidos
    public static void Romper(Renderer[] partes, Vector3 centro, float celda = 0.2f)
    {
        Collider[] delJugador = ColisionesJugador();
        Material primero = null;
        // La cuadricula de cortes se corre al azar: cada muñeca se rompe distinto
        Vector3 corrimiento = new Vector3(Random.value, Random.value, Random.value) * celda;
        var trozos = new Dictionary<Vector3Int, Trozo>();

        foreach (Renderer r in partes)
        {
            if (r == null || !r.enabled || !(r is MeshRenderer)) continue;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            Mesh m = mf != null ? mf.sharedMesh : null;
            if (m == null || !m.isReadable) continue;
            Matrix4x4 aMundo = r.transform.localToWorldMatrix;
            Vector3[] v = m.vertices, n = m.normals;
            Vector2[] uv = m.uv;
            bool hayN = n != null && n.Length == v.Length, hayUV = uv != null && uv.Length == v.Length;
            Material[] mats = r.sharedMaterials;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                Material mat = s < mats.Length ? mats[s] : (mats.Length > 0 ? mats[0] : null);
                if (mat == null) continue;
                if (primero == null) primero = mat;
                int[] t = m.GetTriangles(s);
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Vector3 a = aMundo.MultiplyPoint3x4(v[t[i]]), b = aMundo.MultiplyPoint3x4(v[t[i + 1]]), c = aMundo.MultiplyPoint3x4(v[t[i + 2]]);
                    Vector3 cen = (a + b + c) / 3f;
                    Vector3Int clave = Vector3Int.FloorToInt((cen - centro + corrimiento) / celda);
                    if (!trozos.TryGetValue(clave, out Trozo tr)) trozos[clave] = tr = new Trozo();
                    if (!tr.tris.TryGetValue(mat, out List<int> lista)) tr.tris[mat] = lista = new List<int>();
                    for (int k = 0; k < 3; k++)
                    {
                        int o = t[i + k];
                        lista.Add(tr.v.Count);
                        tr.v.Add(k == 0 ? a : k == 1 ? b : c);
                        tr.n.Add(hayN ? aMundo.MultiplyVector(n[o]).normalized : Vector3.up);
                        tr.uv.Add(hayUV ? uv[o] : Vector2.zero);
                    }
                    tr.suma += cen;
                    tr.cuenta++;
                }
            }
        }

        foreach (Trozo tr in trozos.Values) CrearTrozo(tr, centro, delJugador);
        Astillas(centro, primero, delJugador);
        Sonido(centro);
    }

    static void CrearTrozo(Trozo tr, Vector3 centro, Collider[] delJugador)
    {
        if (tr.cuenta == 0) return;
        Vector3 pc = tr.suma / tr.cuenta;
        for (int i = 0; i < tr.v.Count; i++) tr.v[i] -= pc;

        var m = new Mesh { name = "Pedazo de muñeca" };
        if (tr.v.Count > 65535) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(tr.v);
        m.SetNormals(tr.n);
        m.SetUVs(0, tr.uv);
        m.subMeshCount = tr.tris.Count;
        var mats = new Material[tr.tris.Count];
        int s = 0;
        foreach (var kv in tr.tris) { mats[s] = kv.Key; m.SetTriangles(kv.Value, s); s++; }
        m.RecalculateBounds();

        var go = new GameObject("Pedazo de muñeca");
        go.layer = 2;
        go.transform.position = pc;
        go.AddComponent<MeshFilter>().sharedMesh = m;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        var col = go.AddComponent<BoxCollider>();
        col.center = m.bounds.center;
        col.size = Vector3.Max(m.bounds.size, Vector3.one * 0.03f);
        Ignorar(col, delJugador);

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.08f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        Vector3 fuera = pc - centro; fuera.y *= 0.4f;
        if (fuera.sqrMagnitude < 1e-4f) fuera = Random.onUnitSphere;
        rb.linearVelocity = fuera.normalized * Random.Range(1.2f, 3.2f) + Vector3.up * Random.Range(0.6f, 2.2f);
        rb.angularVelocity = Random.insideUnitSphere * 9f;

        var p = go.AddComponent<PedazoMuneca>();
        p.malla = m;
        p.vida = Random.Range(3.5f, 5f);
    }

    // Astillas: polvo de porcelana que sale disparado mas rapido y dura poco
    static void Astillas(Vector3 centro, Material mat, Collider[] delJugador)
    {
        for (int i = 0; i < 16; i++)
        {
            var a = GameObject.CreatePrimitive(PrimitiveType.Cube);
            a.name = "Astilla de muñeca";
            a.layer = 2;
            a.transform.position = centro + Random.insideUnitSphere * 0.35f;
            a.transform.rotation = Random.rotation;
            a.transform.localScale = new Vector3(Random.Range(0.012f, 0.04f), Random.Range(0.004f, 0.012f), Random.Range(0.012f, 0.035f));
            if (mat != null) a.GetComponent<Renderer>().sharedMaterial = mat;
            Ignorar(a.GetComponent<Collider>(), delJugador);
            var rb = a.AddComponent<Rigidbody>();
            rb.mass = 0.01f;
            rb.linearVelocity = (Random.onUnitSphere + Vector3.up * 0.6f) * Random.Range(2.5f, 5.5f);
            rb.angularVelocity = Random.insideUnitSphere * 20f;
            var p = a.AddComponent<PedazoMuneca>();
            p.vida = Random.Range(1.4f, 2.6f);
            p.enfriar = 0.6f;
        }
    }

    static Collider[] ColisionesJugador()
    {
        Transform j = HUD.Jugador();
        return j != null ? j.root.GetComponentsInChildren<Collider>() : new Collider[0];
    }

    static void Ignorar(Collider c, Collider[] delJugador)
    {
        if (c == null) return;
        foreach (Collider o in delJugador) if (o != null) Physics.IgnoreCollision(c, o, true);
    }

    // ------------------------------------------------------------------ Sonidos (generados por codigo)

    static void Sonido(Vector3 donde)
    {
        if (sonidoRotura == null) sonidoRotura = CrearRotura();
        var go = new GameObject("Sonido muñeca rota");
        go.transform.position = donde;
        var a = go.AddComponent<AudioSource>();
        a.clip = sonidoRotura;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 2f;
        a.maxDistance = 30f;
        a.dopplerLevel = 0f;
        a.pitch = Random.Range(0.92f, 1.08f);
        a.volume = 1f;
        a.Play();
        Destroy(go, sonidoRotura.length / a.pitch + 0.2f);
    }

    // Porcelana haciendose añicos: un golpe seco con ruido y muchos tintineos agudos que caen despues
    static AudioClip CrearRotura()
    {
        const int rate = 44100;
        const float largo = 1.3f;
        int n = Mathf.CeilToInt(rate * largo);
        var d = new float[n];
        var rnd = new System.Random(31);
        float R() => (float)rnd.NextDouble();

        // Crack inicial: ruido filtrado muy corto
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float ruido = R() * 2f - 1f;
            lp += (ruido - lp) * 0.35f;
            d[i] += (ruido - lp) * Mathf.Exp(-t * 28f) * 0.9f + lp * Mathf.Exp(-t * 14f) * 0.5f;
        }
        // Tintineos: trocitos que chocan y rebotan (cada vez mas espaciados y suaves)
        for (int g = 0; g < 46; g++)
        {
            float inicio = Mathf.Pow(R(), 1.8f) * (largo - 0.2f);
            float f = Mathf.Lerp(2400f, 7800f, R());
            float amp = Mathf.Lerp(0.35f, 0.08f, inicio / largo) * Mathf.Lerp(0.5f, 1f, R());
            float caida = Mathf.Lerp(40f, 90f, R());
            int i0 = Mathf.RoundToInt(inicio * rate);
            for (int i = i0; i < n; i++)
            {
                float t = (i - i0) / (float)rate;
                float e = Mathf.Exp(-t * caida);
                if (e < 0.001f) break;
                d[i] += amp * e * (Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * f * 1.53f * t)) * Mathf.Clamp01(t * 2000f);
            }
        }
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i] * 0.8f, -1f, 1f);
        var clip = AudioClip.Create("Muñeca rota", n, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }

    // Crujido de la porcelana rajandose (MunecaLuz lo usa mientras la linterna la alumbra)
    static AudioClip crujido;
    public static AudioClip Crujido()
    {
        if (crujido != null) return crujido;
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * 0.28f);
        var d = new float[n];
        var rnd = new System.Random(77);
        // Varios chasquidos muy seguidos (una raja que avanza)
        float[] chasquidos = { 0f, 0.025f, 0.04f, 0.075f, 0.12f };
        foreach (float c in chasquidos)
        {
            int i0 = Mathf.RoundToInt(c * rate);
            float amp = 1f - c * 4f;
            for (int i = i0; i < n; i++)
            {
                float t = (i - i0) / (float)rate;
                float e = Mathf.Exp(-t * 160f);
                if (e < 0.001f) break;
                d[i] += amp * e * ((float)rnd.NextDouble() * 2f - 1f) * 0.8f + amp * e * Mathf.Sin(2f * Mathf.PI * 3100f * t) * 0.3f;
            }
        }
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f);
        crujido = AudioClip.Create("Crujido muñeca", n, 1, rate, false);
        crujido.SetData(d, 0);
        return crujido;
    }
}
