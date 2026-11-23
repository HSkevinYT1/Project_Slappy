using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Cloacas nuevas: "Aliviadero de tormenta" (propuesta 2 de PropuestasCloacas).
// Red de cloacas a oscuras: una nave grande inundada con bosque de pilares en el centro, un anillo de tuneles de ladrillo
// abovedados (canal de agua con el shader del nivel y aceras) que une 4 camaras y la cisterna del norte, y la entrada
// al sur, en la misma puerta de antes (frente a "Cloacas/Entrada Pasillo").
// Se arma en BLOCKING: cada muro, piso, pilar, baranda, etc. es un objeto suelto con su propio collider, para poder
// moverlo, escalarlo, duplicarlo o borrarlo a mano.
//   Cloacas/Aliviadero/<camara o tunel>             muros, techos, pilares, barandas, tuberias
//   Cloacas/Piso/Pisos Aliviadero/<camara o tunel>  pisos y aceras (caminables)
//   Cloacas/agua/Agua Aliviadero/<camara o tunel>   agua (con el shader del nivel; caminable salvo el pozo de la cisterna)
// Las cajas son cubos de 1 m escalados (UV en metros segun su tamano). Las mallas curvas (muros redondos, cupulas)
// quedan en Assets/Cloacas/Aliviadero/Mallas.asset.
// Tools > Cloacas > Rehacer como Aliviadero (OJO: rehacer borra los cambios hechos a mano). Despues guarda la escena.
public static partial class CloacasAliviaderoSetup
{
    const string CarpetaAssets = "Assets/Cloacas/Aliviadero";
    const string RutaMallas = CarpetaAssets + "/Mallas.asset";
    const string NombreRaiz = "Aliviadero";
    // Centro de la sala de entrada (su muro sur queda justo al norte de z = -35.58, donde termina el pasillo de la Zona Principal)
    static readonly Vector3 Base = new Vector3(99.92f, 0f, -30.88f);

    // ---------------------------------------------------------------- materiales (assets)
    static Material mConcreto, mMetal, mPiso, mAgua, mReja, mLodo, mOxido, mTuberia, mLadrillo, mAcera, mPiedra;

    static void Materiales()
    {
        mConcreto = AssetDatabase.LoadAssetAtPath<Material>("Assets/TexturasZonas/Materiales/TZ_CloacaConcreto.mat");
        mMetal = AssetDatabase.LoadAssetAtPath<Material>("Assets/TexturasZonas/Materiales/TZ_CloacaMetal.mat");
        mPiso = AssetDatabase.LoadAssetAtPath<Material>("Assets/TexturasZonas/Materiales/TZ_CloacaPiso.mat");
        mAgua = AssetDatabase.LoadAssetAtPath<Material>("Assets/Shaders/Agua/prueba/Simple Water/agua prueba.mat");
        mReja = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RejaHierro.mat");
        Directory.CreateDirectory(CarpetaAssets);
        mLodo = Tinte("Lodo", mPiso, new Color(0.35f, 0.33f, 0.26f));
        mOxido = Tinte("Oxido", mMetal, new Color(0.55f, 0.32f, 0.2f));
        mTuberia = Tinte("Tuberia", mMetal, new Color(0.45f, 0.5f, 0.42f));
        mLadrillo = Tinte("Ladrillo", mConcreto, new Color(0.62f, 0.42f, 0.34f));
        mAcera = Tinte("Acera", mConcreto, new Color(0.55f, 0.55f, 0.52f));
        mPiedra = Tinte("Piedra", mConcreto, new Color(0.8f, 0.78f, 0.74f));
    }

    static Material Tinte(string nombre, Material b, Color c)
    {
        string ruta = CarpetaAssets + "/Aliviadero" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (!m) { m = new Material(b); AssetDatabase.CreateAsset(m, ruta); }
        else m.CopyPropertiesFromMaterial(b);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------------------------------------------------------- piezas (una carpeta por camara o tunel)
    class Pieza
    {
        public string nombre;
        public Transform geo, piso, agua;
    }
    static Pieza pieza;
    static readonly List<Pieza> piezas = new List<Pieza>();
    static Transform raiz, colisiones, colPiso, colAgua, colActual;
    static Transform grupoPieza; // si no es null, las piezas nuevas van dentro (barandas)
    static Mesh mallaCilindro;
    static Object contenedorMallas;

    static void NuevaPieza(string nombre) { pieza = new Pieza { nombre = nombre }; piezas.Add(pieza); }

    static Transform Carpeta(ref Transform t, Transform padre)
    {
        if (!t) { t = new GameObject(pieza.nombre).transform; t.SetParent(padre, false); t.gameObject.isStatic = true; }
        return t;
    }

    // Donde va una pieza nueva: agua -> Cloacas/agua, piso -> Cloacas/Piso, el resto -> Cloacas/Aliviadero
    static Transform Padre(bool esAgua = false)
    {
        if (grupoPieza) return grupoPieza;
        if (esAgua) return Carpeta(ref pieza.agua, colAgua);
        if (colActual == colPiso) return Carpeta(ref pieza.piso, colPiso);
        return Carpeta(ref pieza.geo, raiz);
    }

    static GameObject Objeto(string nombre, Mesh malla, Vector3 pos, Quaternion rot, Vector3 escala, Material m, Transform padre)
    {
        var go = new GameObject(nombre);
        EnRaiz(go.transform, pos, rot, padre);
        go.transform.localScale = escala;
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m;
        if (m == mAgua) mr.shadowCastingMode = ShadowCastingMode.Off;
        go.isStatic = true;
        return go;
    }

    // Coloca t con pos/rot en coordenadas de la raiz y lo cuelga de padre (las carpetas comparten el marco de la raiz)
    static void EnRaiz(Transform t, Vector3 pos, Quaternion rot, Transform padre)
    {
        t.SetParent(raiz, false);
        t.localPosition = pos; t.localRotation = rot;
        if (padre != raiz) t.SetParent(padre, true);
    }

    static string Nombre(Material m)
    {
        if (m == mLadrillo) return "Ladrillo";
        if (m == mAcera) return "Acera";
        if (m == mLodo) return "Lodo";
        if (m == mReja) return "Reja";
        if (m == mTuberia) return "Tuberia";
        if (m == mOxido) return "Oxido";
        if (m == mPiedra) return "Piedra";
        if (m == mMetal) return "Metal";
        if (m == mPiso) return "Piso";
        if (m == mConcreto) return "Concreto";
        if (m == mAgua) return "Agua";
        return m ? m.name : "Pieza";
    }

    // Malla generica (tuberias): un objeto con la matriz descompuesta
    static GameObject Agregar(Mesh m, Matrix4x4 mat, Material mt)
    {
        return Objeto(Nombre(mt), m, mat.GetColumn(3), mat.rotation, mat.lossyScale, mt, Padre(mt == mAgua));
    }

    // Caja: cubo de 1 m escalado al tamano, con BoxCollider propio (si col)
    static void Caja(Vector3 c, Vector3 s, Material m, float rotY = 0f, bool col = true, float rotX = 0f, float rotZ = 0f)
        => CajaQ(c, s, m, Quaternion.Euler(rotX, rotY, rotZ), col);

    static void CajaQ(Vector3 c, Vector3 s, Material m, Quaternion q, bool col = true)
    {
        var go = Objeto(Nombre(m), MallaCaja(s), c, q, s, m, Padre());
        if (col) go.AddComponent<BoxCollider>();
    }

    // Colision suelta (sin malla): techos, barandas
    static void Colision(Vector3 c, Quaternion q, Vector3 s)
    {
        var go = new GameObject("Colision");
        EnRaiz(go.transform, c, q, Padre());
        go.AddComponent<BoxCollider>().size = s;
        go.isStatic = true;
    }

    // Agua: placa fina con el shader del agua; si es caminable, su BoxCollider llega 20 cm hacia abajo desde la superficie
    static void Agua(Vector3 c, Vector3 s, float rotY = 0f, bool caminable = true)
    {
        Vector3 e = new Vector3(s.x, 0.02f, s.z);
        var go = Objeto("Agua", MallaCaja(e), c, Quaternion.Euler(0, rotY, 0), e, mAgua, Padre(true));
        if (caminable) { var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(1f, 10f, 1f); bc.center = new Vector3(0f, -5f, 0f); }
    }

    static void AguaCirculo(Vector3 c, float r, bool caminable = true)
    {
        var go = Objeto("Agua", mallaCilindro, c, Quaternion.identity, new Vector3(r * 2f, 0.01f, r * 2f), mAgua, Padre(true));
        if (caminable)
        {
            var col = new GameObject("Colision");
            col.transform.SetParent(go.transform, false);
            col.transform.localPosition = new Vector3(0f, -10f, 0f); col.transform.localScale = new Vector3(1f, 10f, 1f);
            col.AddComponent<MeshCollider>().sharedMesh = mallaCilindro;
            col.isStatic = true;
        }
    }

    static void Cilindro(Vector3 c, float radio, float alto, Material m, bool col = false)
    {
        Vector3 e = new Vector3(radio * 2f, alto * 0.5f, radio * 2f);
        var go = Objeto(Nombre(m), MallaCilindro(e), c, Quaternion.identity, e, m, Padre());
        if (col) go.AddComponent<MeshCollider>().sharedMesh = mallaCilindro;
    }

    static void Tubo(Vector3 a, Vector3 b, float radio, Material m)
    {
        Quaternion q = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        Vector3 e = new Vector3(radio * 2f, Vector3.Distance(a, b) * 0.5f, radio * 2f);
        Objeto("Tuberia", MallaCilindro(e), (a + b) * 0.5f, q, e, m, Padre());
    }

    // Baranda: un objeto "Baranda" con sus barras, postes y una sola colision adentro
    static void Baranda(Vector3 a, Vector3 b, float alto = 1.05f)
    {
        Vector3 d = b - a; float largo = d.magnitude; if (largo < 0.05f) return;
        float giro = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        var g = new GameObject("Baranda").transform;
        EnRaiz(g, (a + b) * 0.5f, Quaternion.Euler(0, giro, 0), Padre());
        g.gameObject.isStatic = true;
        grupoPieza = g;
        Caja((a + b) * 0.5f + Vector3.up * alto, new Vector3(0.06f, 0.06f, largo), mReja, giro, false);
        Caja((a + b) * 0.5f + Vector3.up * alto * 0.5f, new Vector3(0.04f, 0.04f, largo), mReja, giro, false);
        Colision((a + b) * 0.5f + Vector3.up * (alto * 0.5f + 0.1f), Quaternion.Euler(0, giro, 0), new Vector3(0.12f, alto + 0.2f, largo));
        int postes = Mathf.Max(1, Mathf.RoundToInt(largo / 1.5f));
        for (int i = 0; i <= postes; i++) Caja(Vector3.Lerp(a, b, i / (float)postes) + Vector3.up * alto * 0.5f, new Vector3(0.06f, alto, 0.06f), mReja, 0, false);
        grupoPieza = null;
    }

    // Boveda de canon (eje = direccion rotY), ancho w, arranque en y0
    static void Boveda(Vector3 c, float w, float largo, float y0, float rotY, Material m, int segs = 9)
    {
        float r = w * 0.5f;
        Quaternion q = Quaternion.Euler(0, rotY, 0);
        for (int i = 0; i < segs; i++)
        {
            float a0 = Mathf.PI * i / segs, a1 = Mathf.PI * (i + 1) / segs, am = (a0 + a1) * 0.5f;
            Vector3 p = new Vector3(Mathf.Cos(am) * r, Mathf.Sin(am) * r + y0, 0f);
            float cuerda = 2f * r * Mathf.Sin((a1 - a0) * 0.5f) + 0.08f;
            CajaQ(c + q * p, new Vector3(cuerda, 0.35f, largo), m, q * Quaternion.Euler(0, 0, am * Mathf.Rad2Deg - 90f));
        }
    }

    static void AnilloMuro(Vector3 c, float r, float alto, float grosor, Material m, int segs, float y0, bool col)
    {
        float paso = 360f / segs;
        float largo = 2f * r * Mathf.Tan(paso * 0.5f * Mathf.Deg2Rad) + 0.05f;
        for (int i = 0; i < segs; i++)
        {
            float a = i * paso + paso * 0.5f;
            Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            Caja(c + dir * (r + grosor * 0.5f) + Vector3.up * (y0 + alto * 0.5f), new Vector3(largo, alto, grosor), m, a, col);
        }
    }

    // ---------------------------------------------------------------- mallas guardadas (todas en un solo asset)
    static void EmpezarMallas()
    {
        foreach (string viejo in Directory.Exists(CarpetaAssets) ? Directory.GetFiles(CarpetaAssets, "*.asset") : new string[0])
            AssetDatabase.DeleteAsset(viejo.Replace('\\', '/'));
        var indice = new Mesh { name = "Mallas Aliviadero" };
        AssetDatabase.CreateAsset(indice, RutaMallas);
        contenedorMallas = indice;
    }

    static Mesh Guardar(Mesh m)
    {
        m.hideFlags = HideFlags.None;
        AssetDatabase.AddObjectToAsset(m, contenedorMallas);
        return m;
    }

    // Cubo de 1 m con UV en metros para el tamano s (el objeto se escala a s). Nombre "UVM_" para que
    // TexturasZonasSetup no le haga otra copia.
    static readonly Dictionary<Vector3, Mesh> cacheMallas = new Dictionary<Vector3, Mesh>();
    static Mesh MallaCaja(Vector3 s)
    {
        s = new Vector3(Redondo(s.x), Redondo(s.y), Redondo(s.z));
        if (cacheMallas.TryGetValue(s, out Mesh mm) && mm) return mm;
        const float T = 2.5f;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var t = new List<int>();
        void Cara(Vector3 nor, Vector3 u, Vector3 w, float su, float sw)
        {
            int i = v.Count;
            Vector3 c = nor * 0.5f, hu = u * 0.5f, hw = w * 0.5f;
            v.Add(c - hu - hw); v.Add(c + hu - hw); v.Add(c + hu + hw); v.Add(c - hu + hw);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(su / T, 0)); uv.Add(new Vector2(su / T, sw / T)); uv.Add(new Vector2(0, sw / T));
            for (int k = 0; k < 4; k++) n.Add(nor);
            t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }
        Cara(Vector3.right, Vector3.forward, Vector3.up, s.z, s.y);
        Cara(Vector3.left, Vector3.back, Vector3.up, s.z, s.y);
        Cara(Vector3.up, Vector3.right, Vector3.forward, s.x, s.z);
        Cara(Vector3.down, Vector3.right, Vector3.back, s.x, s.z);
        Cara(Vector3.forward, Vector3.left, Vector3.up, s.x, s.y);
        Cara(Vector3.back, Vector3.right, Vector3.up, s.x, s.y);
        var mesh = new Mesh { name = string.Format(System.Globalization.CultureInfo.InvariantCulture, "UVM_Cubo_{0:0.00}x{1:0.00}x{2:0.00}", s.x, s.y, s.z) };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        cacheMallas[s] = Guardar(mesh);
        return mesh;
    }

    // Cilindro de Unity con UV en metros para la escala s (como las cajas)
    static readonly Dictionary<Vector3, Mesh> cacheCilindros = new Dictionary<Vector3, Mesh>();
    static Mesh MallaCilindro(Vector3 s)
    {
        s = new Vector3(Redondo(s.x), Redondo(s.y), Redondo(s.z));
        if (cacheCilindros.TryGetValue(s, out Mesh mm) && mm) return mm;
        Mesh m = Object.Instantiate(mallaCilindro);
        m.name = string.Format(System.Globalization.CultureInfo.InvariantCulture, "UVM_Cilindro_{0:0.00}x{1:0.00}x{2:0.00}", s.x, s.y, s.z);
        Vector3[] v = m.vertices, nr = m.normals;
        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 p = Vector3.Scale(v[i], s) / 2.5f;
            if (Mathf.Abs(nr[i].y) > 0.5f) uv[i] = new Vector2(p.x, p.z);
            else uv[i] = new Vector2(Mathf.Atan2(v[i].x, v[i].z) * s.x * 0.5f / 2.5f, p.y);
        }
        m.uv = uv; m.RecalculateTangents();
        cacheCilindros[s] = Guardar(m);
        return m;
    }

    static float Redondo(float x) => Mathf.Max(0.01f, Mathf.Round(Mathf.Abs(x) * 20f) / 20f);

    static void TerminarMallas()
    {
        EditorUtility.SetDirty(contenedorMallas);
        AssetDatabase.SaveAssets();
    }
}
