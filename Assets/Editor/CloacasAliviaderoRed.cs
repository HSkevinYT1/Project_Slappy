using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Segunda parte de CloacasAliviaderoSetup: el trazado (camaras + tuneles) y la integracion con el resto del juego
// (trigger de la zona, NavMesh, salida con llave, munecas, estatuas, gema y baterias).
public static partial class CloacasAliviaderoSetup
{
    const float ParedTunel = 2.3f, LodoY = -0.6f, AguaY = -0.1f;

    class Nodo
    {
        public string nombre; public Vector3 c; public bool redondo; public float r; public Vector2 tam; public float alto;
        public int tipo; // 0 = acera perimetral y agua al centro, 1 = inundada, 2 = cisterna con pozo e isla, 3 = piso seco
        public float puertaSur; // ancho de una puerta en el muro sur (solo la entrada)
    }
    struct Tunel { public int a, b; public float w; public int estilo; } // 0 = canal central y 2 aceras, 1 = acera a un lado, 2 = inundado
    static readonly List<Nodo> nodos = new List<Nodo>();
    static readonly List<Tunel> tuneles = new List<Tunel>();

    static int Redonda(string n, float x, float z, float r, float alto, int tipo = 0) { nodos.Add(new Nodo { nombre = n, c = new Vector3(x, 0, z), redondo = true, r = r, alto = alto, tipo = tipo }); return nodos.Count - 1; }
    static int Rect(string n, float x, float z, float sx, float sz, float alto, int tipo = 0) { nodos.Add(new Nodo { nombre = n, c = new Vector3(x, 0, z), tam = new Vector2(sx, sz), alto = alto, tipo = tipo }); return nodos.Count - 1; }
    static void Tun(int a, int b, float w = 5f, int estilo = 0) => tuneles.Add(new Tunel { a = a, b = b, w = w, estilo = estilo });

    // ================================================================ menu
    [MenuItem("Tools/Cloacas/Rehacer como Aliviadero")]
    static void Menu()
    {
        if (!EditorUtility.DisplayDialog("Cloacas", "Esto borra la geometria actual de las Cloacas (Concreto, Piso, Metal, agua, Puerta Entrada) y arma el Aliviadero de tormenta. Ctrl+Z deshace la escena (los assets de mallas quedan en Assets/Cloacas/Aliviadero). ¿Seguir?", "Rehacer", "Cancelar")) return;
        Aplicar();
    }


    public static void Aplicar()
    {
        GameObject cl = GameObject.Find("Cloacas");
        if (!cl) { Debug.LogError("[Cloacas] No encontre \"Cloacas\"."); return; }
        Transform c = cl.transform;
        Undo.SetCurrentGroupName("Cloacas Aliviadero");
        int grupo = Undo.GetCurrentGroup();

        // 1) fuera la geometria vieja
        foreach (string n in new[] { "Concreto", "Piso", "Metal", "agua", "Puerta Entrada", NombreRaiz })
        {
            Transform t = c.Find(n);
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }

        // 2) grupos: Cloacas/Piso y Cloacas/agua (los marca EnemigosZonasSetup como caminables) y Cloacas/Aliviadero
        GameObject Hijo(string n, Transform padre)
        {
            var g = new GameObject(n); Undo.RegisterCreatedObjectUndo(g, "Cloacas");
            g.transform.SetParent(padre, false); g.transform.position = Base; g.isStatic = true; return g;
        }
        var piso = new GameObject("Piso"); Undo.RegisterCreatedObjectUndo(piso, "Cloacas"); piso.transform.SetParent(c, false);
        var agua = new GameObject("agua"); Undo.RegisterCreatedObjectUndo(agua, "Cloacas"); agua.transform.SetParent(c, false);
        raiz = Hijo(NombreRaiz, c).transform;
        colisiones = new GameObject("Colisiones").transform; colisiones.SetParent(raiz, false);
        colPiso = Hijo("Pisos Aliviadero", piso.transform).transform;
        colAgua = Hijo("Agua Aliviadero", agua.transform).transform;
        colActual = colisiones;

        // 3) armar
        Materiales();
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mallaCilindro = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);
        piezas.Clear(); nodos.Clear(); tuneles.Clear(); cacheMallas.Clear();
        Trazado();
        for (int i = 0; i < nodos.Count; i++) { NuevaPieza(nodos[i].nombre); if (nodos[i].redondo) CamaraRedonda(i); else CamaraRect(i); }
        for (int i = 0; i < tuneles.Count; i++) { NuevaPieza("Tunel " + nodos[tuneles[i].a].nombre + " - " + nodos[tuneles[i].b].nombre); ConstruirTunel(tuneles[i]); }
        Combinar(colAgua);
        foreach (var col in raiz.GetComponentsInChildren<Transform>()) col.gameObject.isStatic = true;
        foreach (var col in colPiso.GetComponentsInChildren<Transform>()) col.gameObject.isStatic = true;
        foreach (var col in colAgua.GetComponentsInChildren<Transform>()) col.gameObject.isStatic = true;
        Physics.SyncTransforms();

        // 4) integracion
        Integrar(c);
        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(cl.scene);
        Debug.Log("[Cloacas] Aliviadero armado. Revisa el Console y guarda la escena (Ctrl+S).", raiz);
    }

    // ================================================================ trazado (coordenadas locales: (0,0,0) = centro de la sala de entrada)
    static int nE, nH, nN, nNO, nNE, nSO, nSE;
    static void Trazado()
    {
        nE = Rect("Entrada", 0, 0, 16, 8, 5.8f, 3); nodos[nE].puertaSur = 3f;
        nH = Rect("Nave", 0, 42, 46, 32, 9, 1);
        nN = Redonda("Cisterna", 0, 96, 11, 9, 2);
        nNO = Redonda("Camara NO", -44, 78, 7, 7);
        nNE = Rect("Camara NE", 52, 78, 16, 16, 6);
        nSO = Rect("Camara SO", -44, 18, 14, 18, 6, 1);
        nSE = Redonda("Camara SE", 48, 14, 8, 7);
        Tun(nE, nH, 5);
        Tun(nH, nN, 6);
        Tun(nH, nSO, 4.5f);
        Tun(nH, nSE, 4.5f, 1);
        Tun(nSO, nNO, 4, 1);
        Tun(nNO, nN, 4, 0);
        Tun(nSE, nNE, 4, 2);
        Tun(nNE, nN, 4, 1);
        Tun(nE, nSO, 3.5f, 1);
    }

    static float Borde(Nodo n, Vector3 dir)
    {
        if (n.redondo) return n.r;
        float tx = Mathf.Abs(dir.x) > 1e-4f ? n.tam.x * 0.5f / Mathf.Abs(dir.x) : 1e9f;
        float tz = Mathf.Abs(dir.z) > 1e-4f ? n.tam.y * 0.5f / Mathf.Abs(dir.z) : 1e9f;
        return Mathf.Min(tx, tz);
    }

    // ---------------------------------------------------------------- bocas de tunel en los muros de las camaras
    // Que tunel (o la puerta sur) abre el muro en el punto p: -1 = ninguno, -2 = puerta de la entrada, >= 0 = indice del tunel
    static int Boca(int idx, Vector3 p, out float perp)
    {
        Nodo n = nodos[idx];
        perp = 0f;
        if (n.puertaSur > 0 && p.z < n.c.z - n.tam.y * 0.5f && Mathf.Abs(p.x - n.c.x) < n.puertaSur * 0.5f) return -2;
        for (int i = 0; i < tuneles.Count; i++)
        {
            var t = tuneles[i];
            if (t.a != idx && t.b != idx) continue;
            if (Perp(idx, i, p, out perp) < t.w * 0.5f + 0.05f) return i;
        }
        return -1;
    }

    // Distancia lateral de p al eje del tunel i (y si p esta del lado del tunel)
    static float Perp(int idx, int i, Vector3 p, out float perp)
    {
        var t = tuneles[i]; Nodo n = nodos[idx];
        Nodo o = nodos[t.a == idx ? t.b : t.a];
        Vector3 dir = o.c - n.c; dir.y = 0; dir.Normalize();
        Vector3 d = p - n.c; d.y = 0;
        perp = Mathf.Abs(dir.x * d.z - dir.z * d.x);
        return Vector3.Dot(d, dir) <= 0 ? 1e9f : perp;
    }

    // Altura minima del muro encima de una boca: sigue el arco de la boveda del tunel (asi no queda hueco entre el
    // muro y la boveda por donde se vea el exterior). Se usa el punto mas alejado del eje (el mas bajo del arco).
    static float AlturaDintel(int idx, int boca, Vector3[] esquinas)
    {
        if (boca == -2) return 3.2f;
        var t = tuneles[boca];
        float pm = 0f;
        foreach (var e in esquinas) { Perp(idx, boca, e, out float pe); pm = Mathf.Max(pm, pe); }
        float R = t.w * 0.5f + 0.2f;
        return pm >= R ? ParedTunel : ParedTunel + Mathf.Sqrt(R * R - pm * pm) - 0.05f;
    }

    // Muro recto (camaras rectangulares): en las bocas se parte en trozos de 0.2 m que siguen el arco
    static void Muro(int idx, Vector3 centro, float largo, float rotY, float alto)
    {
        const float Fondo = -3f;
        Quaternion q = Quaternion.Euler(0, rotY, 0);
        Vector3 eje = q * Vector3.right, nor = q * Vector3.forward;
        int boca = Boca(idx, centro, out _);
        if (boca == -1) { Caja(centro + Vector3.up * ((alto + Fondo) * 0.5f), new Vector3(largo, alto - Fondo, 0.6f), mLadrillo, rotY); return; }
        int k = Mathf.Max(1, Mathf.CeilToInt(largo / 0.2f));
        for (int i = 0; i < k; i++)
        {
            Vector3 c = centro + eje * (-largo * 0.5f + (i + 0.5f) * largo / k);
            float l = largo / k;
            int b = Boca(idx, c, out _);
            float y0 = b == -1 ? Fondo : AlturaDintel(idx, b, new[] { c + eje * l * 0.5f + nor * 0.3f, c - eje * l * 0.5f + nor * 0.3f, c + eje * l * 0.5f - nor * 0.3f, c - eje * l * 0.5f - nor * 0.3f });
            if (y0 < alto) Caja(c + Vector3.up * ((alto + y0) * 0.5f), new Vector3(l + 0.01f, alto - y0, 0.6f), mLadrillo, rotY);
        }
    }

    // ---------------------------------------------------------------- sectores de anillo (muros redondos, aceras y cupulas sin rendijas)
    // Malla de un sector de anillo macizo: radios rin..rout, angulos a0..a1 (grados, 0 = +Z, como Quaternion.Euler(0,a,0)),
    // de y = 0 a y = h. UV en metros.
    static Mesh MallaSector(float rin, float rout, float a0, float a1, float h, int sub)
    {
        const float T = 2.5f;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        Vector3 P(float r, float a, float y) { float rad = a * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(rad) * r, y, Mathf.Cos(rad) * r); }
        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector2 u0, Vector2 u1, Vector2 u2, Vector2 u3)
        {
            int i = v.Count; v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
            uv.Add(u0 / T); uv.Add(u1 / T); uv.Add(u2 / T); uv.Add(u3 / T);
            tr.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }
        for (int j = 0; j < sub; j++)
        {
            float b0 = Mathf.Lerp(a0, a1, j / (float)sub), b1 = Mathf.Lerp(a0, a1, (j + 1) / (float)sub);
            float s0i = b0 * Mathf.Deg2Rad * rin, s1i = b1 * Mathf.Deg2Rad * rin, s0o = b0 * Mathf.Deg2Rad * rout, s1o = b1 * Mathf.Deg2Rad * rout;
            Vector2 XZ(Vector3 p) => new Vector2(p.x, p.z);
            // arriba (mirando hacia +Y) y abajo
            Quad(P(rin, b0, h), P(rin, b1, h), P(rout, b1, h), P(rout, b0, h), XZ(P(rin, b0, h)), XZ(P(rin, b1, h)), XZ(P(rout, b1, h)), XZ(P(rout, b0, h)));
            Quad(P(rout, b0, 0), P(rout, b1, 0), P(rin, b1, 0), P(rin, b0, 0), XZ(P(rout, b0, 0)), XZ(P(rout, b1, 0)), XZ(P(rin, b1, 0)), XZ(P(rin, b0, 0)));
            // cara interior (mira hacia el centro) y exterior
            if (rin > 0.01f) Quad(P(rin, b1, 0), P(rin, b1, h), P(rin, b0, h), P(rin, b0, 0), new Vector2(s1i, 0), new Vector2(s1i, h), new Vector2(s0i, h), new Vector2(s0i, 0));
            Quad(P(rout, b0, 0), P(rout, b0, h), P(rout, b1, h), P(rout, b1, 0), new Vector2(s0o, 0), new Vector2(s0o, h), new Vector2(s1o, h), new Vector2(s1o, 0));
        }
        // tapas de los extremos (se ven dentro de las bocas)
        if (a1 - a0 < 359.9f)
        {
            Quad(P(rin, a0, 0), P(rin, a0, h), P(rout, a0, h), P(rout, a0, 0), new Vector2(rin, 0), new Vector2(rin, h), new Vector2(rout, h), new Vector2(rout, 0));
            Quad(P(rout, a1, 0), P(rout, a1, h), P(rin, a1, h), P(rin, a1, 0), new Vector2(rout, 0), new Vector2(rout, h), new Vector2(rin, h), new Vector2(rin, 0));
        }
        var m = new Mesh { hideFlags = HideFlags.HideAndDontSave };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
        m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }

    // Sector visible + (opcional) colision con una caja tangente
    static void Sector(Vector3 c, float rin, float rout, float a0, float a1, float y0, float y1, Material mat, bool col)
    {
        if (y1 - y0 < 0.01f) return;
        Agregar(MallaSector(rin, rout, a0, a1, y1 - y0, Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / 7.5f))), Matrix4x4.Translate(c + Vector3.up * y0), mat);
        if (!col) return;
        float am = (a0 + a1) * 0.5f, rm = (rin + rout) * 0.5f;
        float ancho = 2f * rout * Mathf.Sin((a1 - a0) * 0.5f * Mathf.Deg2Rad) + 0.05f;
        Colision(c + Quaternion.Euler(0, am, 0) * Vector3.forward * rm + Vector3.up * ((y0 + y1) * 0.5f), Quaternion.Euler(0, am, 0), new Vector3(ancho, y1 - y0, rout - rin));
    }

    static void EnPiso(System.Action a) { var p = colActual; colActual = colPiso; a(); colActual = p; }

    static void CamaraRedonda(int idx)
    {
        Nodo n = nodos[idx];
        float r = n.r, acera = n.tipo == 1 ? 0f : Mathf.Min(2.6f, r * 0.45f);
        const float Fondo = -3f, Grosor = 0.6f;
        // muro: sectores de ~0.4 m; en las bocas el muro arranca en el arco de la boveda
        int segs = Mathf.Max(24, Mathf.RoundToInt(2f * Mathf.PI * r / 0.4f));
        float paso = 360f / segs;
        int cadaPilastra = Mathf.Max(1, Mathf.RoundToInt(4f / (2f * Mathf.PI * r / segs)));
        Vector3 D(float a) => Quaternion.Euler(0, a, 0) * Vector3.forward;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i * paso, a1 = a0 + paso, a = a0 + paso * 0.5f;
            int boca = Boca(idx, n.c + D(a) * (r + Grosor * 0.5f), out _);
            float y0 = boca == -1 ? Fondo : AlturaDintel(idx, boca, new[] { n.c + D(a0) * r, n.c + D(a1) * r, n.c + D(a0) * (r + Grosor), n.c + D(a1) * (r + Grosor) });
            Sector(n.c, r, r + Grosor, a0, a1, y0, n.alto, mLadrillo, true);
            if (i % cadaPilastra == 0 && boca == -1 && Boca(idx, n.c + D(a0) * r, out _) == -1 && Boca(idx, n.c + D(a1 + paso) * r, out _) == -1)
                Caja(n.c + D(a) * (r - 0.15f) + Vector3.up * ((n.alto + LodoY) * 0.5f), new Vector3(0.7f, n.alto - LodoY, 0.3f), mLadrillo, a);
        }
        if (n.tipo == 2)
        {
            Cilindro(n.c + Vector3.up * -2.4f, r + 0.1f, 0.2f, mLodo, true);
            AguaCirculo(n.c + Vector3.up * -1.4f, r - acera + 0.2f, false);
        }
        else
        {
            Cilindro(n.c + Vector3.up * (LodoY - 0.1f), r + 0.1f, 0.2f, mLodo);
            AguaCirculo(n.c + Vector3.up * AguaY, n.tipo == 1 ? r : r - acera + 0.2f);
        }
        if (acera > 0)
        {
            float hondo = n.tipo == 2 ? -2.4f : LodoY;
            int sa = Mathf.Max(16, Mathf.RoundToInt(2f * Mathf.PI * r / 1.5f));
            float pa = 360f / sa;
            for (int i = 0; i < sa; i++)
            {
                float a0 = i * pa, a1 = a0 + pa, a = a0 + pa * 0.5f;
                EnPiso(() => Sector(n.c, r - acera, r + 0.05f, a0, a1, hondo, 0f, mAcera, true));
                bool puente = n.tipo == 2 && Mathf.Abs(Mathf.DeltaAngle(a, 180f)) < pa * 0.9f;
                if (n.tipo == 2 && !puente)
                    Baranda(n.c + D(a0) * (r - acera + 0.08f), n.c + D(a1) * (r - acera + 0.08f));
            }
        }
        if (n.tipo == 2)
        {
            float ri = r * 0.25f, largoP = r - acera - ri + 0.2f;
            Cilindro(n.c + Vector3.up * -1.2f, ri, 2.4f, mLadrillo, true);
            var prev = colActual; colActual = colPiso; Cilindro(n.c + Vector3.up * -0.03f, ri + 0.1f, 0.06f, mAcera, true); colActual = prev;
            EnPiso(() => Caja(n.c + Vector3.back * (ri + largoP * 0.5f - 0.1f) + Vector3.down * 0.06f, new Vector3(1.6f, 0.12f, largoP), mMetal));
            Baranda(n.c + new Vector3(0.8f, 0, -ri), n.c + new Vector3(0.8f, 0, -(r - acera)));
            Baranda(n.c + new Vector3(-0.8f, 0, -ri), n.c + new Vector3(-0.8f, 0, -(r - acera)));
            for (int i = 0; i < 12; i++)
            {
                float a0 = i * 30f, a1 = a0 + 30f;
                if (Mathf.Abs(Mathf.DeltaAngle(a0 + 15f, 180f)) < 20f) continue;
                Baranda(n.c + D(a0) * (ri - 0.05f), n.c + D(a1) * (ri - 0.05f));
            }
        }
        // cupula escalonada maciza: cada anillo llega hasta el muro y se solapa con el de arriba; tapa al centro
        const int anillos = 5;
        float paso2 = r * 0.12f;
        for (int k = 0; k < anillos - 1; k++)
            Sector(n.c, r * (1f - (k + 1f) / anillos), r + Grosor, 0f, 360f, n.alto + k * paso2, n.alto + (k + 1) * paso2 + 0.6f, mLadrillo, false);
        Sector(n.c, 0f, r / anillos + 0.4f, 0f, 360f, n.alto + (anillos - 1) * paso2, n.alto + anillos * paso2 + 0.6f, mLadrillo, false);
        // colision del techo (no se ve)
        Colision(n.c + Vector3.up * (n.alto + 0.3f), Quaternion.identity, new Vector3(r * 2.2f, 0.3f, r * 2.2f));
    }

    static void CamaraRect(int idx)
    {
        Nodo n = nodos[idx];
        float hx = n.tam.x * 0.5f, hz = n.tam.y * 0.5f, acera = n.tipo == 0 ? 2.2f : 0f;
        for (int lado = 0; lado < 4; lado++)
        {
            bool ejeX = lado < 2; float s = (lado % 2 == 0) ? 1f : -1f;
            float largo = ejeX ? n.tam.x + 1.2f : n.tam.y;
            int k = Mathf.CeilToInt(largo / 0.5f);
            for (int i = 0; i < k; i++)
            {
                float u = -largo * 0.5f + (i + 0.5f) * largo / k;
                Vector3 p = n.c + (ejeX ? new Vector3(u, 0, s * (hz + 0.3f)) : new Vector3(s * (hx + 0.3f), 0, u));
                Muro(idx, p, largo / k + 0.01f, ejeX ? 0 : 90, n.alto);
            }
        }
        if (n.tipo == 3) EnPiso(() => Caja(n.c + Vector3.up * -0.15f, new Vector3(n.tam.x, 0.3f, n.tam.y), mPiso));
        else
        {
            Caja(n.c + Vector3.up * (LodoY - 0.1f), new Vector3(n.tam.x + 0.6f, 0.2f, n.tam.y + 0.6f), mLodo, 0, false);
            Agua(n.c + Vector3.up * AguaY, new Vector3(n.tam.x, 0.02f, n.tam.y));
        }
        if (acera > 0) EnPiso(() =>
        {
            Caja(n.c + new Vector3(0, LodoY * 0.5f, hz - acera * 0.5f), new Vector3(n.tam.x, -LodoY, acera), mAcera);
            Caja(n.c + new Vector3(0, LodoY * 0.5f, -hz + acera * 0.5f), new Vector3(n.tam.x, -LodoY, acera), mAcera);
            Caja(n.c + new Vector3(hx - acera * 0.5f, LodoY * 0.5f, 0), new Vector3(acera, -LodoY, n.tam.y - 2 * acera), mAcera);
            Caja(n.c + new Vector3(-hx + acera * 0.5f, LodoY * 0.5f, 0), new Vector3(acera, -LodoY, n.tam.y - 2 * acera), mAcera);
        });
        if (n.tipo != 3)
        {
            int nx = Mathf.Max(1, Mathf.RoundToInt((n.tam.x - 2 * acera) / 6f)), nz = Mathf.Max(1, Mathf.RoundToInt((n.tam.y - 2 * acera) / 6f));
            float px = (n.tam.x - 2 * acera) / nx, pz = (n.tam.y - 2 * acera) / nz;
            for (int i = 1; i < nx; i++)
                for (int j = 1; j < nz; j++)
                {
                    Vector3 p = n.c + new Vector3(-hx + acera + i * px, 0, -hz + acera + j * pz);
                    Caja(p + Vector3.up * ((n.alto + LodoY) * 0.5f), new Vector3(1.2f, n.alto - LodoY, 1.2f), mLadrillo);
                    Caja(p + Vector3.up * (n.alto - 0.5f), new Vector3(px, 1.0f, 0.8f), mLadrillo, 0, false);
                    Caja(p + Vector3.up * (n.alto - 0.5f), new Vector3(0.8f, 1.0f, pz), mLadrillo, 0, false);
                }
            for (int i = 0; i <= nx; i++) Caja(n.c + new Vector3(-hx + acera + i * px, n.alto - 0.5f, 0), new Vector3(0.8f, 1.0f, n.tam.y), mLadrillo, 0, false);
        }
        Caja(n.c + Vector3.up * (n.alto + 0.25f), new Vector3(n.tam.x + 1.2f, 0.5f, n.tam.y + 1.2f), mConcreto);
        Tubo(n.c + new Vector3(-hx, n.alto * 0.6f, hz - 0.5f), n.c + new Vector3(hx, n.alto * 0.6f, hz - 0.5f), 0.35f, mTuberia);
    }

    // ---------------------------------------------------------------- tuneles
    // Distancia desde p0 (dentro de la camara) hasta su borde, avanzando en dir. NaN si la recta no corta la camara.
    static float DistBorde(Nodo n, Vector3 p0, Vector3 dir)
    {
        Vector3 q = p0 - n.c; q.y = 0;
        if (n.redondo)
        {
            float b = Vector3.Dot(q, dir), cc = q.sqrMagnitude - n.r * n.r, disc = b * b - cc;
            return disc < 0 ? float.NaN : -b + Mathf.Sqrt(disc);
        }
        float hx = n.tam.x * 0.5f, hz = n.tam.y * 0.5f;
        float tx = Mathf.Abs(dir.x) > 1e-4f ? ((dir.x > 0 ? hx : -hx) - q.x) / dir.x : 1e9f;
        float tz = Mathf.Abs(dir.z) > 1e-4f ? ((dir.z > 0 ? hz : -hz) - q.z) / dir.z : 1e9f;
        return Mathf.Min(tx, tz);
    }

    // Cada pieza del tunel (muros, boveda, aceras, fondo) llega justo hasta el muro de cada camara segun su posicion
    // lateral: asi los tuneles en diagonal o que entran en camaras redondas no dejan rendijas al exterior.
    static Tunel tu; static Nodo tA, tB; static Vector3 tDir, tLado; static float tTotal;

    static void Tramo(float lat0, float lat1, out float s, out float e)
    {
        float dA = float.PositiveInfinity, dB = float.PositiveInfinity;
        foreach (float l in new[] { lat0, lat1, (lat0 + lat1) * 0.5f })
        {
            float a = DistBorde(tA, tA.c + tLado * l, tDir), b = DistBorde(tB, tB.c + tLado * l, -tDir);
            if (!float.IsNaN(a)) dA = Mathf.Min(dA, a);
            if (!float.IsNaN(b)) dB = Mathf.Min(dB, b);
        }
        if (float.IsInfinity(dA)) dA = Borde(tA, tDir);
        if (float.IsInfinity(dB)) dB = Borde(tB, -tDir);
        s = dA - 0.05f; e = tTotal - (dB - 0.05f);
    }

    // Caja a lo largo del tunel entre lat0 y lat1 (lateral), de y0 a y1
    static void CajaTunel(float lat0, float lat1, float y0, float y1, Material m, bool col = true)
    {
        Tramo(lat0, lat1, out float s, out float e);
        Vector3 c = tA.c + tDir * ((s + e) * 0.5f) + tLado * ((lat0 + lat1) * 0.5f) + Vector3.up * ((y0 + y1) * 0.5f);
        Caja(c, new Vector3(Mathf.Abs(lat1 - lat0), y1 - y0, e - s), m, Mathf.Atan2(tDir.x, tDir.z) * Mathf.Rad2Deg, col);
    }

    static void ConstruirTunel(Tunel t)
    {
        tu = t; tA = nodos[t.a]; tB = nodos[t.b];
        tDir = tB.c - tA.c; tDir.y = 0; tTotal = tDir.magnitude; tDir /= tTotal;
        tLado = Vector3.Cross(Vector3.up, tDir);
        float w = t.w, rot = Mathf.Atan2(tDir.x, tDir.z) * Mathf.Rad2Deg, hw = w * 0.5f;
        Quaternion q = Quaternion.Euler(0, rot, 0);

        // muros laterales
        CajaTunel(hw, hw + 0.6f, -3f, ParedTunel, mLadrillo);
        CajaTunel(-hw - 0.6f, -hw, -3f, ParedTunel, mLadrillo);
        // boveda: cada dovela con su propio largo
        float R = (w + 0.4f) * 0.5f; const int segs = 9;
        for (int i = 0; i < segs; i++)
        {
            float a0 = Mathf.PI * i / segs, a1 = Mathf.PI * (i + 1) / segs, am = (a0 + a1) * 0.5f;
            float cuerda = 2f * R * Mathf.Sin((a1 - a0) * 0.5f) + 0.08f;
            float lat = Mathf.Cos(am) * R, mitad = cuerda * 0.5f * Mathf.Abs(Mathf.Sin(am)) + 0.2f;
            Tramo(lat - mitad, lat + mitad, out float s, out float e);
            Vector3 c = tA.c + tDir * ((s + e) * 0.5f) + tLado * lat + Vector3.up * (Mathf.Sin(am) * R + ParedTunel);
            CajaQ(c, new Vector3(cuerda, 0.35f, e - s), mLadrillo, q * Quaternion.Euler(0, 0, am * Mathf.Rad2Deg - 90f));
        }
        // fondo y agua
        CajaTunel(-hw - 0.1f, hw + 0.1f, LodoY - 0.21f, LodoY - 0.01f, mLodo, false);
        float d0 = Borde(tA, tDir), d1 = tTotal - Borde(tB, -tDir);
        Vector3 a = tA.c + tDir * (d0 + 0.6f), b = tA.c + tDir * (d1 - 0.6f);
        float L = d1 - d0 + 0.1f;
        Vector3 mAguaC = tA.c + tDir * ((d0 + d1) * 0.5f);
        if (t.estilo == 2) Agua(mAguaC + Vector3.up * AguaY, new Vector3(w, 0.02f, L), rot);
        else
        {
            float canal = t.estilo == 0 ? Mathf.Max(1.4f, w * 0.38f) : w * 0.45f;
            Agua(mAguaC + Vector3.up * AguaY + (t.estilo == 1 ? -tLado * (hw - canal * 0.5f) : Vector3.zero), new Vector3(canal + 0.1f, 0.02f, L), rot);
            EnPiso(() =>
            {
                if (t.estilo == 0)
                {
                    CajaTunel(canal * 0.5f, hw, LodoY, -0.005f, mAcera);
                    CajaTunel(-hw, -canal * 0.5f, LodoY, -0.005f, mAcera);
                }
                else CajaTunel(hw - (w - canal), hw, LodoY, -0.005f, mAcera);
            });
        }
        // costillas cada ~4 m (empiezan donde el tunel ya esta fuera de las dos camaras)
        float s0 = 0f, s1 = tTotal;
        foreach (float l in new[] { -hw, hw })
        {
            float da = DistBorde(tA, tA.c + tLado * l, tDir), db = DistBorde(tB, tB.c + tLado * l, -tDir);
            s0 = Mathf.Max(s0, (float.IsNaN(da) ? Borde(tA, tDir) : da) + 0.6f);
            s1 = Mathf.Min(s1, tTotal - (float.IsNaN(db) ? Borde(tB, -tDir) : db) - 0.6f);
        }
        if (s1 > s0)
        {
            int nc = Mathf.Max(1, Mathf.RoundToInt((s1 - s0) / 4f));
            for (int i = 0; i <= nc; i++)
            {
                Vector3 p = tA.c + tDir * Mathf.Lerp(s0, s1, i / (float)nc);
                Caja(p + tLado * (hw - 0.05f) + Vector3.up * ((ParedTunel + LodoY) * 0.5f), new Vector3(0.35f, ParedTunel - LodoY, 0.6f), mLadrillo, rot);
                Caja(p - tLado * (hw - 0.05f) + Vector3.up * ((ParedTunel + LodoY) * 0.5f), new Vector3(0.35f, ParedTunel - LodoY, 0.6f), mLadrillo, rot);
                Boveda(p, w - 0.1f, 0.6f, ParedTunel, rot, mLadrillo, 9);
            }
        }
        Tubo(a + tLado * (hw - 0.35f) + Vector3.up * 1.9f, b + tLado * (hw - 0.35f) + Vector3.up * 1.9f, 0.16f, mTuberia);
        // macizo encima de la boveda en las dos bocas: tapa cualquier rendija entre el muro de la camara y la boveda
        Collar(true); Collar(false);
    }

    // Franjas de ladrillo apoyadas sobre la boveda (por fuera), desde el muro de la camara hasta 2.5 m dentro del
    // tunel y hasta el techo de la camara. No se ven desde el tunel (estan encima de la boveda) y desde la camara solo
    // asoman por el hueco del muro encima del arco, que es justo lo que hay que tapar.
    static void Collar(bool enA)
    {
        Nodo n = enA ? tA : tB;
        float hw = tu.w * 0.5f, Rext = (tu.w + 0.4f) * 0.5f + 0.175f, lim = hw + 0.6f;
        const float paso = 0.15f, Largo = 2.5f;
        float rot = Mathf.Atan2(tDir.x, tDir.z) * Mathf.Rad2Deg;
        for (float l0 = -lim; l0 < lim - 1e-3f; l0 += paso)
        {
            float l1 = Mathf.Min(lim, l0 + paso);
            float lm = Mathf.Max(Mathf.Abs(l0), Mathf.Abs(l1));
            float y0 = lm >= Rext ? ParedTunel : ParedTunel + Mathf.Sqrt(Rext * Rext - lm * lm) - 0.03f;
            float y1 = n.alto + 0.3f;
            if (y1 <= y0) continue;
            // cruce con el muro (lado de la camara) en las dos orillas de la franja
            float d = float.PositiveInfinity;
            foreach (float l in new[] { l0, l1 })
            {
                float v = enA ? DistBorde(tA, tA.c + tLado * l, tDir) : DistBorde(tB, tB.c + tLado * l, -tDir);
                if (!float.IsNaN(v)) d = Mathf.Min(d, v);
            }
            if (float.IsInfinity(d)) d = enA ? Borde(tA, tDir) : Borde(tB, -tDir);
            float ax0 = enA ? d - 0.05f : tTotal - d + 0.05f - Largo;   // coordenada a lo largo del tunel (desde A)
            float ax1 = ax0 + Largo;
            Vector3 c = tA.c + tDir * ((ax0 + ax1) * 0.5f) + tLado * ((l0 + l1) * 0.5f) + Vector3.up * ((y0 + y1) * 0.5f);
            Caja(c, new Vector3(l1 - l0 + 0.01f, y1 - y0, Largo), mLadrillo, rot, false);
        }
    }

    // ================================================================ integracion
    static Vector3 Mundo(int nodo, float dx, float dz, float y = 0f) => Base + nodos[nodo].c + new Vector3(dx, y, dz);

    static void Integrar(Transform cl)
    {
        // Trigger de la zona: cubre todas las colisiones nuevas
        Bounds b = new Bounds(Base, Vector3.one); bool hay = false;
        foreach (var col in cl.GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger || !(col.transform.IsChildOf(raiz) || col.transform.IsChildOf(colPiso) || col.transform.IsChildOf(colAgua))) continue;
            if (!hay) { b = col.bounds; hay = true; } else b.Encapsulate(col.bounds);
        }
        b.Expand(new Vector3(1f, 2f, 0f));
        // que no se meta en el pasillo de la Zona Principal (el muro sur de la entrada es el limite)
        float zMin = Base.z + nodos[nE].c.z - nodos[nE].tam.y * 0.5f - 0.3f;
        b.SetMinMax(new Vector3(b.min.x, b.min.y, zMin), b.max);
        var trig = GameObject.Find("Enemigos y Zonas/Triggers Volumen/Trigger Cloacas");
        if (trig && trig.TryGetComponent(out BoxCollider bc))
        {
            Undo.RecordObject(trig.transform, "Cloacas"); Undo.RecordObject(bc, "Cloacas");
            trig.transform.position = b.center; trig.transform.rotation = Quaternion.identity;
            Vector3 s = trig.transform.lossyScale;
            bc.center = Vector3.zero;
            bc.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z);
            Debug.Log($"[Cloacas] Trigger Cloacas: centro {b.center} tamano {b.size}");
        }
        else Debug.LogWarning("[Cloacas] No encontre \"Trigger Cloacas\": hay que ajustarlo a mano.");

        // Gema en la isla de la cisterna; baterias en las camaras
        Transform gema = cl.Find("Piedra Coleccionable (Cloacas)");
        if (gema) { Undo.RecordObject(gema, "Cloacas"); gema.position = Mundo(nN, 0, 0.6f, 0.42f); }
        var bats = GameObject.Find("Menu y Progreso/Baterias");
        if (bats)
        {
            var sitios = new[] { Mundo(nNE, -6.8f, 6.8f, 0.2f), Mundo(nNO, -5.6f, 0f, 0.2f), Mundo(nSE, 0f, -6.6f, 0.2f) };
            int i = 0;
            foreach (Transform t in bats.transform)
                if (t.name.StartsWith("Bateria Cloacas") && i < sitios.Length) { Undo.RecordObject(t, "Cloacas"); t.position = sitios[i++]; }
        }
        Physics.SyncTransforms();

        // NavMesh, salida con llave (rehace la reja en la puerta nueva), munecas/pinturas y estatuas
        EnemigosZonasSetup.Hornear();
        EditorApplication.ExecuteMenuItem("Tools/Cloacas/Salida con llave");
        EditorApplication.ExecuteMenuItem("Tools/Cloacas/Muñecas y luz (rehacer)");
        EditorApplication.ExecuteMenuItem("Tools/Enemigos y Zonas/Recolocar estatuas de las Cloacas (dentro de las paredes)");
    }
}
