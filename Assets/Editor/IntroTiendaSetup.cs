using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Arma la cinematica de inicio (IntroTienda): carro, puntos del cliente y del tendero, y la ruta de la camara
// que pasa por la ventana derecha de la Tienda (vista desde la calle). No modifica nada de la Tienda.
// Se crea sola si no existe "Intro Tienda" (o si es de una version anterior); rehacer: Tools > Menu > Cinematica de inicio.
[InitializeOnLoad]
public static class IntroTiendaSetup
{
    const string Nombre = "Intro Tienda";
    const string Version = "_intro v8";
    const string CarpetaCarro = "Assets/Tienda/Intro";
    const string ModeloCarro = "Assets/Models/Props/car (1).fbx";

    static IntroTiendaSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject ya = GameObject.Find(Nombre);
            if (GameObject.Find("Tienda/paredes/Cube (21)") && (!ya || !ya.transform.Find(Version))) Crear();
        };
    }

    [MenuItem("Tools/Menu/Cinematica de inicio")]
    static void Crear()
    {
        GameObject vieja = GameObject.Find(Nombre);
        if (vieja) Undo.DestroyObjectImmediate(vieja);
        var tut = Object.FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include);
        GameObject ventana = GameObject.Find("Tienda/paredes/Cube (21)");
        if (!tut || !ventana) { Debug.LogWarning("[Intro] Falta TutorialTienda o la ventana Tienda/paredes/Cube (21)."); return; }

        var raiz = new GameObject(Nombre);
        Undo.RegisterCreatedObjectUndo(raiz, Nombre);
        new GameObject(Version).transform.SetParent(raiz.transform, false);
        var intro = raiz.AddComponent<IntroTienda>();

        // Calle: la calzada esta a la altura del origen del material CalleFalsa; el cordon de la acera a 3 m del vidrio
        var calle = AssetDatabase.LoadAssetAtPath<Material>("Assets/Tienda/Materiales/CalleFalsa.mat");
        float yCalle = calle ? calle.GetVector("_Origen").y : 4.12f;
        float zCordon = (calle ? calle.GetVector("_Origen").z : 45.93f) + (calle ? calle.GetFloat("_Acera") : 3f);
        Vector3 puerta = tut.caminoEntrada != null && tut.caminoEntrada.Length > 0 && tut.caminoEntrada[0] ? tut.caminoEntrada[0].position : new Vector3(-15.26f, 4.27f, 48.5f);
        Bounds v = ventana.GetComponent<Renderer>().bounds;

        Transform Punto(string n, Vector3 p, Quaternion r = default)
        {
            var t = new GameObject(n).transform;
            t.SetParent(raiz.transform, false);
            t.SetPositionAndRotation(p, r == default ? Quaternion.identity : r);
            return t;
        }

        // ---- Carro: llega desde el oeste (la derecha de la camara del menu) y se estaciona frente a la ventana derecha
        Quaternion haciaEste = Quaternion.LookRotation(Vector3.right);
        float zCarro = zCordon + 0.2f + 0.95f;
        var fin = Punto("Carro Estacionado", new Vector3(v.center.x, yCalle, zCarro), haciaEste);
        var inicio = Punto("Carro Inicio", new Vector3(v.center.x - 22f, yCalle, zCarro), haciaEste);
        var carroGo = new GameObject("Carro");
        carroGo.transform.SetParent(raiz.transform, false);
        carroGo.transform.SetPositionAndRotation(inicio.position, haciaEste);
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloCarro);
        if (modelo)
        {
            var m = (GameObject)PrefabUtility.InstantiatePrefab(modelo, carroGo.transform);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            PrepararCarro(m, intro);
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>()) r.allowOcclusionWhenDynamic = false;
        }
        carroGo.SetActive(false); // aparece al darle a Jugar
        intro.carro = carroGo.transform;
        intro.carroInicio = inicio;
        intro.carroFin = fin;

        // ---- Cliente: baja por la puerta del lado de la acera y camina a la puerta de la tienda
        intro.bajadaCliente = Punto("Cliente Baja", new Vector3(fin.position.x + 0.15f, yCalle + 0.15f, zCordon - 0.25f));
        intro.puertaTienda = tut.caminoEntrada != null && tut.caminoEntrada.Length > 0 ? tut.caminoEntrada[0] : Punto("Puerta Tienda", puerta);

        // ---- Tendero: sale del sotano, sube la escalera y llega detras del mostrador (donde esta el jugador)
        var jugador = Object.FindFirstObjectByType<PlayerMovement>();
        Vector3 pies = jugador ? jugador.transform.position : new Vector3(-21.46f, 4.3f, 43.54f);
        if (jugador && jugador.TryGetComponent(out CapsuleCollider cap)) pies.y = cap.bounds.min.y;
        GameObject esc = GameObject.Find("escaleras/escalera");
        Bounds eb = esc ? esc.GetComponent<Renderer>().bounds : new Bounds(new Vector3(-24.5f, 2.5f, 43.64f), new Vector3(4.2f, 3.6f, 1.8f));
        float zEsc = eb.center.z;
        intro.caminoSotano = new[]
        {
            Punto("Sotano 1", new Vector3(eb.min.x - 1.6f, eb.min.y + 0.6f, zEsc)),
            Punto("Sotano 2 (pie de la escalera)", new Vector3(eb.min.x + 0.2f, eb.min.y + 0.7f, zEsc)),
            Punto("Sotano 3 (arriba)", new Vector3(eb.max.x + 0.1f, pies.y, zEsc)),
            Punto("Sotano 4 (mostrador)", pies),
        };

        Debug.Log($"[Intro] Tendero: sotano {intro.caminoSotano[0].position} -> pie {intro.caminoSotano[1].position} -> arriba {intro.caminoSotano[2].position} -> mostrador {intro.caminoSotano[3].position} (jugador={(jugador ? jugador.transform.position.ToString() : "-")}).");

        // ---- Camara: pasa cerca del cliente, va a la ventana derecha y la atraviesa
        float yCam = (v.min.y + v.max.y) * 0.5f + 0.05f;
        // Frente real de la fachada (la ventana esta metida en una pared gruesa): se mide con un rayo desde la calle
        float zFachada = v.max.z + 0.5f;
        float xPared = (puerta.x + v.max.x) * 0.5f;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(xPared, yCam, zCordon + 2f), Vector3.back, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(raiz.transform) || h.collider.GetComponentInParent<PlayerMovement>()) continue;
            if (h.point.z > zFachada && h.point.z < zCordon - 0.5f) zFachada = h.point.z;
        }
        float zAcera = zFachada + 1.6f;
        Debug.Log($"[Intro] Fachada en z={zFachada:0.00}, camara por la acera en z={zAcera:0.00} (cliente en z={puerta.z:0.00}).");
        intro.caminoCamara = new[]
        {
            // (siempre de frente a la tienda: de la toma del menu va derecho frente a la ventana derecha, un poco
            // alto para pasar sobre el carro, y de ahi entra por el vidrio)
            Punto("Camara 1 (frente a la ventana)", new Vector3(v.center.x, yCam + 0.35f, zAcera)),
            Punto("Camara 3 (tras el vidrio)", new Vector3(v.center.x, yCam - 0.05f, v.min.z - 0.8f)),
        };

        intro.mirarAdentro = Punto("Camara Mira (adentro)", new Vector3(v.center.x - 0.6f, yCam - 0.5f, v.min.z - 4.5f));
        EditorUtility.SetDirty(intro);
        EditorSceneManager.MarkSceneDirty(raiz.scene);
        Debug.Log("[Intro] Cinematica de inicio: carro, cliente, tendero del sotano y camara por la ventana derecha. Guarda la escena (Ctrl+S).", raiz);
    }

    // ------------------------------------------------------------------ Carro
    // El modelo es una sola malla (una submalla por material). Se separan las 4 llantas (con su rin) en piezas
    // propias que giran, se les pone material de caucho y metal, y se le arma una capota con ventanas de color
    // solido para que no se vea el interior (el modelo original es descapotable).
    static readonly string[] SubLlanta = { "standardSurface15", "standardSurface14" };
    const string SubRin = "standardSurface17";

    static void PrepararCarro(GameObject modelo, IntroTienda intro)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(modelo))
            PrefabUtility.UnpackPrefabInstance(modelo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var mf = modelo.GetComponentInChildren<MeshFilter>();
        var mr = mf ? mf.GetComponent<MeshRenderer>() : null;
        if (!mf || !mr || !mf.sharedMesh) return;
        Mesh src = mf.sharedMesh;
        Material[] mats = mr.sharedMaterials;
        Vector3[] V = src.vertices; Vector3[] N = src.normals; Vector2[] U = src.uv;
        int nSub = src.subMeshCount;

        System.IO.Directory.CreateDirectory(CarpetaCarro);
        Material caucho = Mat("Carro_Llanta", new Color(0.035f, 0.035f, 0.04f), 0.25f, 0f);
        Material rin = Mat("Carro_Rin", new Color(0.62f, 0.63f, 0.66f), 0.75f, 0.85f);
        Material vidrio = Mat("Carro_Vidrio", new Color(0.05f, 0.07f, 0.09f), 0.9f, 0f);
        Material carroceria = null;

        bool[] esLlanta = new bool[nSub], esRin = new bool[nSub];
        for (int s = 0; s < nSub && s < mats.Length; s++)
        {
            string n = mats[s] ? mats[s].name : "";
            esLlanta[s] = System.Array.IndexOf(SubLlanta, n) >= 0;
            esRin[s] = n == SubRin;
            if (n == "phongE2") carroceria = mats[s];
            // vidrios transparentes (celestes) -> color solido
            if (mats[s] && mats[s].HasProperty("_BaseColor") && mats[s].GetColor("_BaseColor").a < 0.5f) mats[s] = vidrio;
        }

        // Centro y radio de cada llanta (agrupando los vertices del caucho por cuadrante)
        var caja = new Bounds[4]; var hay = new bool[4];
        for (int s = 0; s < nSub; s++)
        {
            if (!esLlanta[s]) continue;
            foreach (int i in src.GetTriangles(s))
            {
                Vector3 p = V[i]; int g = (p.x > 0f ? 1 : 0) + (p.z > 0f ? 2 : 0);
                if (!hay[g]) { caja[g] = new Bounds(p, Vector3.zero); hay[g] = true; } else caja[g].Encapsulate(p);
            }
        }
        float radio = 0f; int cuantas = 0;
        for (int g = 0; g < 4; g++) if (hay[g]) { radio += Mathf.Max(caja[g].size.y, caja[g].size.z) * 0.5f; cuantas++; }
        if (cuantas == 0) { Debug.LogWarning("[Intro] No se encontraron las llantas del carro."); return; }
        radio /= cuantas;

        // Cada triangulo dentro del cilindro de una llanta pasa a esa llanta
        var cuerpo = new List<int>[nSub];
        var rueda = new List<int>[4, nSub];
        for (int s = 0; s < nSub; s++)
        {
            cuerpo[s] = new List<int>();
            for (int g = 0; g < 4; g++) rueda[g, s] = new List<int>();
            int[] t = src.GetTriangles(s);
            for (int k = 0; k < t.Length; k += 3)
            {
                int dueno = -1;
                for (int g = 0; g < 4 && dueno < 0; g++)
                {
                    if (!hay[g]) continue;
                    bool dentro = true;
                    for (int j = 0; j < 3 && dentro; j++)
                    {
                        Vector3 p = V[t[k + j]], c = caja[g].center;
                        float r = new Vector2(p.y - c.y, p.z - c.z).magnitude;
                        dentro = r <= radio * 1.02f && p.x >= caja[g].min.x - 0.03f && p.x <= caja[g].max.x + 0.03f;
                    }
                    if (dentro) dueno = g;
                }
                var lista = dueno < 0 ? cuerpo[s] : rueda[dueno, s];
                lista.Add(t[k]); lista.Add(t[k + 1]); lista.Add(t[k + 2]);
            }
        }

        // Cuerpo: la misma malla sin las llantas
        Mesh mCuerpo = Object.Instantiate(src);
        mCuerpo.name = "Carro_Cuerpo";
        for (int s = 0; s < nSub; s++) mCuerpo.SetTriangles(cuerpo[s], s);
        mCuerpo.RecalculateBounds();
        mf.sharedMesh = Guardar(mCuerpo);
        mr.sharedMaterials = mats;

        // Llantas: piezas con el pivote en su centro
        var llantas = new List<Transform>();
        string[] nombres = { "Llanta trasera izq", "Llanta trasera der", "Llanta delantera izq", "Llanta delantera der" };
        for (int g = 0; g < 4; g++)
        {
            if (!hay[g]) continue;
            Vector3 c = caja[g].center;
            var vs = new List<Vector3>(); var ns = new List<Vector3>(); var us = new List<Vector2>();
            var mapa = new Dictionary<int, int>();
            var subs = new List<List<int>>(); var matsL = new List<Material>();
            for (int s = 0; s < nSub; s++)
            {
                if (rueda[g, s].Count == 0) continue;
                var tris = new List<int>();
                foreach (int i in rueda[g, s])
                {
                    if (!mapa.TryGetValue(i, out int ni))
                    {
                        ni = vs.Count; mapa[i] = ni;
                        vs.Add(V[i] - c); ns.Add(N != null && N.Length > i ? N[i] : Vector3.up); us.Add(U != null && U.Length > i ? U[i] : Vector2.zero);
                    }
                    tris.Add(ni);
                }
                subs.Add(tris);
                matsL.Add(esLlanta[s] ? caucho : esRin[s] ? rin : mats[s]);
            }
            var m = new Mesh { name = "Carro_" + nombres[g].Replace(' ', '_') };
            if (vs.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(vs); m.SetNormals(ns); m.SetUVs(0, us);
            m.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s);
            m.RecalculateBounds();
            var go = new GameObject(nombres[g]);
            go.transform.SetParent(mf.transform, false);
            go.transform.localPosition = c;
            go.AddComponent<MeshFilter>().sharedMesh = Guardar(m);
            go.AddComponent<MeshRenderer>().sharedMaterials = matsL.ToArray();
            llantas.Add(go.transform);
        }
        intro.llantas = llantas.ToArray();
        intro.radioLlanta = radio * mf.transform.lossyScale.y;

        Capota(mf.transform, carroceria ? carroceria : rin, vidrio);
    }

    // Capota cerrada: cabina con ventanas de color solido, techo y parales del color de la carroceria
    static void Capota(Transform padre, Material pintura, Material vidrio)
    {
        // (coordenadas del modelo: x ancho, y alto desde el centro del carro, z hacia el frente)
        float yCintura = 0.29f, yTecho = 0.63f;
        float zAbajoFrente = 0.69f, zAbajoAtras = -1.42f, zArribaFrente = 0.17f, zArribaAtras = -0.95f;
        float xAbajo = 0.75f, xArriba = 0.65f;
        Vector3[] p =
        {
            new Vector3(-xAbajo, yCintura, zAbajoAtras), new Vector3(xAbajo, yCintura, zAbajoAtras),
            new Vector3(xAbajo, yCintura, zAbajoFrente), new Vector3(-xAbajo, yCintura, zAbajoFrente),
            new Vector3(-xArriba, yTecho, zArribaAtras), new Vector3(xArriba, yTecho, zArribaAtras),
            new Vector3(xArriba, yTecho, zArribaFrente), new Vector3(-xArriba, yTecho, zArribaFrente),
        };
        var vs = new List<Vector3>(); var vid = new List<int>(); var techo = new List<int>();
        void Cara(int a, int b, int c, int d, List<int> sub)
        {
            int i = vs.Count;
            vs.Add(p[a]); vs.Add(p[b]); vs.Add(p[c]); vs.Add(p[d]);
            sub.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        Cara(1, 0, 4, 5, vid);   // atras
        Cara(3, 2, 6, 7, vid);   // parabrisas (detras del original)
        Cara(0, 3, 7, 4, vid);   // lado izquierdo
        Cara(2, 1, 5, 6, vid);   // lado derecho
        Cara(4, 7, 6, 5, techo); // techo
        var m = new Mesh { name = "Carro_Capota" };
        m.SetVertices(vs);
        m.subMeshCount = 2; m.SetTriangles(vid, 0); m.SetTriangles(techo, 1);
        // cada cara tiene que mirar hacia afuera: si no, se invierte
        Vector3 centro = new Vector3(0f, (yCintura + yTecho) * 0.5f, (zAbajoFrente + zAbajoAtras) * 0.5f);
        for (int s = 0; s < 2; s++)
        {
            int[] t = m.GetTriangles(s);
            for (int k = 0; k < t.Length; k += 3)
            {
                Vector3 a = vs[t[k]], b = vs[t[k + 1]], c = vs[t[k + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - centro) < 0f) { (t[k + 1], t[k + 2]) = (t[k + 2], t[k + 1]); }
            }
            m.SetTriangles(t, s);
        }
        m.RecalculateNormals(); m.RecalculateBounds();
        var go = new GameObject("Capota");
        go.transform.SetParent(padre, false);
        go.AddComponent<MeshFilter>().sharedMesh = Guardar(m);
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { vidrio, pintura };

        // Techo un poco salido y parales (del color del carro) por las aristas de la cabina
        Barra(go.transform, "Techo", new Vector3(0f, yTecho + 0.02f, (zArribaFrente + zArribaAtras) * 0.5f), Quaternion.identity, new Vector3(xArriba * 2f + 0.08f, 0.05f, zArribaFrente - zArribaAtras + 0.08f), pintura);
        foreach (float sx in new[] { -1f, 1f })
        {
            Arista(go.transform, "Paral A", new Vector3(sx * xAbajo, yCintura, zAbajoFrente), new Vector3(sx * xArriba, yTecho, zArribaFrente), pintura);
            Arista(go.transform, "Paral C", new Vector3(sx * xAbajo, yCintura, zAbajoAtras), new Vector3(sx * xArriba, yTecho, zArribaAtras), pintura);
            float zb = -0.36f;
            Arista(go.transform, "Paral B", new Vector3(sx * (xAbajo + 0.005f), yCintura, zb), new Vector3(sx * (xArriba + 0.005f), yTecho, zb), pintura, 0.09f);
            Arista(go.transform, "Marco", new Vector3(sx * xAbajo, yCintura + 0.01f, zAbajoAtras), new Vector3(sx * xAbajo, yCintura + 0.01f, zAbajoFrente), pintura, 0.05f);
        }
    }

    static void Arista(Transform padre, string nombre, Vector3 a, Vector3 b, Material mat, float grosor = 0.07f)
    {
        Barra(padre, nombre, (a + b) * 0.5f, Quaternion.LookRotation(b - a), new Vector3(grosor, grosor * 0.8f, (b - a).magnitude + grosor * 0.5f), mat);
    }

    static void Barra(Transform padre, string nombre, Vector3 pos, Quaternion rot, Vector3 escala, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = nombre;
        g.transform.SetParent(padre, false);
        g.transform.SetLocalPositionAndRotation(pos, rot);
        g.transform.localScale = escala;
        g.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Mesh Guardar(Mesh m)
    {
        string ruta = $"{CarpetaCarro}/{m.name}.asset";
        AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    static Material Mat(string nombre, Color color, float brillo, float metal)
    {
        string ruta = $"{CarpetaCarro}/{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre }; AssetDatabase.CreateAsset(m, ruta); }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", brillo);
        m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }
}
