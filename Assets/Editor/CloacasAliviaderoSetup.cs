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
// Todo se arma dentro de "Cloacas/Aliviadero": mallas combinadas por pieza y material (guardadas como assets en
// Assets/Cloacas/Aliviadero) y colisiones con BoxCollider en "Colisiones". El agua es caminable (como antes).
// Tools > Cloacas > Rehacer como Aliviadero. Despues guarda la escena.
public static partial class CloacasAliviaderoSetup
{
    const string CarpetaAssets = "Assets/Cloacas/Aliviadero";
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

    // ---------------------------------------------------------------- piezas: se acumulan y al final se combinan
    // Cada "pieza" (camara o tunel) junta sus mallas por material.
    class Pieza
    {
        public string nombre;
        public readonly Dictionary<Material, List<CombineInstance>> porMat = new Dictionary<Material, List<CombineInstance>>();
        public readonly List<CombineInstance> agua = new List<CombineInstance>();
    }
    static Pieza pieza;
    static readonly List<Pieza> piezas = new List<Pieza>();
    static Transform raiz, colisiones, colPiso, colAgua, colActual;
    static Mesh mallaCilindro;

    static void NuevaPieza(string nombre) { pieza = new Pieza { nombre = nombre }; piezas.Add(pieza); }

    static void Agregar(Mesh m, Matrix4x4 mat, Material mt)
    {
        var ci = new CombineInstance { mesh = m, transform = mat };
        if (mt == mAgua) { pieza.agua.Add(ci); return; }
        if (!pieza.porMat.TryGetValue(mt, out var l)) pieza.porMat[mt] = l = new List<CombineInstance>();
        l.Add(ci);
    }

    // Caja (local a la raiz). colision: BoxCollider del mismo tamano.
    static void Caja(Vector3 c, Vector3 s, Material m, float rotY = 0f, bool col = true, float rotX = 0f, float rotZ = 0f)
    {
        Quaternion q = Quaternion.Euler(rotX, rotY, rotZ);
        Agregar(MallaCaja(s), Matrix4x4.TRS(c, q, Vector3.one), m);
        if (col) Colision(c, q, s);
    }

    static void CajaQ(Vector3 c, Vector3 s, Material m, Quaternion q, bool col = true)
    {
        Agregar(MallaCaja(s), Matrix4x4.TRS(c, q, Vector3.one), m);
        if (col) Colision(c, q, s);
    }

    static void Colision(Vector3 c, Quaternion q, Vector3 s)
    {
        var go = new GameObject("Col");
        go.transform.SetParent(colActual ? colActual : colisiones, false);
        go.transform.localPosition = c; go.transform.localRotation = q;
        go.AddComponent<BoxCollider>().size = s;
        go.isStatic = true;
    }

    // Agua caminable: plano con el shader del agua + colision fina con la cara de arriba a la altura del agua
    static void Agua(Vector3 c, Vector3 s, float rotY = 0f, bool caminable = true)
    {
        Quaternion q = Quaternion.Euler(0, rotY, 0);
        Agregar(MallaCaja(new Vector3(s.x, 0.02f, s.z)), Matrix4x4.TRS(c, q, Vector3.one), mAgua);
        if (caminable) { var prev = colActual; colActual = colAgua; Colision(c + Vector3.down * 0.1f, q, new Vector3(s.x, 0.2f, s.z)); colActual = prev; }
    }

    static void AguaCirculo(Vector3 c, float r, bool caminable = true)
    {
        Agregar(mallaCilindro, Matrix4x4.TRS(c, Quaternion.identity, new Vector3(r * 2f, 0.01f, r * 2f)), mAgua);
        if (caminable)
        {
            var go = new GameObject("Col");
            go.transform.SetParent(colAgua, false);
            go.transform.localPosition = c + Vector3.down * 0.1f; go.transform.localScale = new Vector3(r * 2f, 0.1f, r * 2f);
            go.AddComponent<MeshCollider>().sharedMesh = mallaCilindro;
            go.isStatic = true;
        }
    }

    static void Cilindro(Vector3 c, float radio, float alto, Material m, bool col = false)
    {
        Agregar(mallaCilindro, Matrix4x4.TRS(c, Quaternion.identity, new Vector3(radio * 2f, alto * 0.5f, radio * 2f)), m);
        if (col)
        {
            var go = new GameObject("Col");
            go.transform.SetParent(colActual ? colActual : colisiones, false);
            go.transform.localPosition = c; go.transform.localScale = new Vector3(radio * 2f, alto * 0.5f, radio * 2f);
            go.AddComponent<MeshCollider>().sharedMesh = mallaCilindro;
            go.isStatic = true;
        }
    }

    static void Tubo(Vector3 a, Vector3 b, float radio, Material m)
    {
        Quaternion q = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        Agregar(mallaCilindro, Matrix4x4.TRS((a + b) * 0.5f, q, new Vector3(radio * 2f, Vector3.Distance(a, b) * 0.5f, radio * 2f)), m);
    }

    static void Baranda(Vector3 a, Vector3 b, float alto = 1.05f)
    {
        Vector3 d = b - a; float largo = d.magnitude; if (largo < 0.05f) return;
        float giro = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        Caja((a + b) * 0.5f + Vector3.up * alto, new Vector3(0.06f, 0.06f, largo), mReja, giro, false);
        Caja((a + b) * 0.5f + Vector3.up * alto * 0.5f, new Vector3(0.04f, 0.04f, largo), mReja, giro, false);
        Colision((a + b) * 0.5f + Vector3.up * (alto * 0.5f + 0.1f), Quaternion.Euler(0, giro, 0), new Vector3(0.12f, alto + 0.2f, largo));
        int postes = Mathf.Max(1, Mathf.RoundToInt(largo / 1.5f));
        for (int i = 0; i <= postes; i++) Caja(Vector3.Lerp(a, b, i / (float)postes) + Vector3.up * alto * 0.5f, new Vector3(0.06f, alto, 0.06f), mReja, 0, false);
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

    // Mallas de caja con UV en metros (se guardan dentro del asset de cada malla combinada, no hace falta guardarlas)
    static readonly Dictionary<Vector3, Mesh> cacheMallas = new Dictionary<Vector3, Mesh>();
    static Mesh MallaCaja(Vector3 s)
    {
        if (cacheMallas.TryGetValue(s, out Mesh mm) && mm) return mm;
        const float T = 2.5f;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var t = new List<int>();
        Vector3 h = s * 0.5f;
        void Cara(Vector3 nor, Vector3 u, Vector3 w, float su, float sw)
        {
            int i = v.Count;
            Vector3 c = Vector3.Scale(nor, h);
            Vector3 hu = u * (su * 0.5f), hw = w * (sw * 0.5f);
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
        var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        cacheMallas[s] = mesh;
        return mesh;
    }

    // Combina todas las piezas en mallas (assets) bajo la raiz
    static void Combinar(Transform aguaT)
    {
        foreach (string viejo in Directory.Exists(CarpetaAssets) ? Directory.GetFiles(CarpetaAssets, "*.asset") : new string[0])
            AssetDatabase.DeleteAsset(viejo.Replace('\\', '/'));
        var geo = new GameObject("Geometria").transform; geo.SetParent(raiz, false);
        int n = 0;
        foreach (var p in piezas)
        {
            var go = new GameObject(p.nombre); go.transform.SetParent(geo, false); go.isStatic = true;
            var mats = new List<Material>(); var subs = new List<CombineInstance>();
            foreach (var kv in p.porMat)
            {
                var sub = new Mesh { indexFormat = IndexFormat.UInt32 };
                sub.CombineMeshes(kv.Value.ToArray(), true, true);
                subs.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity });
                mats.Add(kv.Key);
            }
            var malla = new Mesh { name = "Aliviadero_" + p.nombre, indexFormat = IndexFormat.UInt32 };
            malla.CombineMeshes(subs.ToArray(), false, false);
            malla.RecalculateBounds();
            AssetDatabase.CreateAsset(malla, $"{CarpetaAssets}/{n++:00}_{p.nombre}.asset");
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
            foreach (var s in subs) Object.DestroyImmediate(s.mesh);
            if (p.agua.Count > 0)
            {
                var ag = new GameObject("agua " + p.nombre); ag.transform.SetParent(aguaT, false); ag.isStatic = true;
                var ma = new Mesh { name = "Aliviadero_agua_" + p.nombre, indexFormat = IndexFormat.UInt32 };
                ma.CombineMeshes(p.agua.ToArray(), true, true);
                AssetDatabase.CreateAsset(ma, $"{CarpetaAssets}/{n++:00}_agua_{p.nombre}.asset");
                ag.AddComponent<MeshFilter>().sharedMesh = ma;
                var mr = ag.AddComponent<MeshRenderer>(); mr.sharedMaterial = mAgua; mr.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        AssetDatabase.SaveAssets();
    }
}
