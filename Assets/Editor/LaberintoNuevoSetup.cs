using System.Collections.Generic;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Nuevo trazado del Laberinto: "sentirse perdido sin estarlo".
// - Cuadricula de 21 x 21 celdas de 4 m (84 x 84 m). La entrada sigue donde termina el pasillo de la Zona Principal.
// - Habitacion central de 20 x 20 m con una puerta en cada lado (es la zona segura: las lianas guian hacia ella).
// - 4 salas con un panel (bloque blanco), una en el centro de cada cuadrante, lejos entre si.
// - 4 casilleros (prefab Armario, se puede esconder), uno en cada esquina.
// - Laberinto "trenzado": muchos bucles y casi sin callejones sin salida, asi que cualquier camino termina
//   volviendo a un pasillo conocido. Pasillo de entrada recto (para la presentacion del enemigo).
// - Paredes elevadas (pasar por debajo con F) como atajos entre zonas que por el camino normal estan lejos.
// Se conservan los contenedores (Paredes, Paredes Elevadas, casilleros, Piso, techo), asi las lianas, el NavMesh,
// el trigger de Volume, el enemigo, las baterias y la presentacion siguen funcionando (se reubican solos).
// Se aplica solo una vez; rehacer: Tools > Laberinto > Nuevo trazado.
[InitializeOnLoad]
public static class LaberintoNuevoSetup
{
    const string Version = "_laberinto v8";
    const int N = 21;              // celdas por lado
    const float Celda = 4f;        // metros por celda
    const float Alto = 4.25f;      // alto de las paredes (igual que antes)
    const float Grosor = 0.6f;
    const float BaseElevada = 1.42f;
    const int Semilla = 2026;

    static LaberintoNuevoSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject lab = GameObject.Find("Laberinto");
            if (lab && !lab.transform.Find(Version) && lab.transform.Find("Paredes")) Construir();
        };
    }

    // ------------------------------------------------------------------ Trazado

    // Muros: h[i,j] = muro vertical entre (i-1,j) y (i,j) (i = 0..N); v[i,j] = muro horizontal entre (i,j-1) y (i,j)
    static bool[,] muroH, muroV, fijoH, fijoV;
    static int[] padre;
    static int Buscar(int a) { while (padre[a] != a) a = padre[a] = padre[padre[a]]; return a; }
    static bool Unir(int a, int b) { a = Buscar(a); b = Buscar(b); if (a == b) return false; padre[a] = b; return true; }
    static int Id(int i, int j) => j * N + i;

    static readonly RectInt Central = new RectInt(8, 8, 5, 5);
    static readonly RectInt[] SalasPanel = { new RectInt(3, 15, 3, 3), new RectInt(15, 15, 3, 3), new RectInt(15, 3, 3, 3), new RectInt(3, 3, 3, 3) };
    static readonly RectInt[] Rincones = { new RectInt(0, 19, 2, 2), new RectInt(19, 19, 2, 2), new RectInt(19, 0, 2, 2), new RectInt(0, 0, 2, 2) };
    // Entrada: pasillo recto de 5 celdas, dobla hacia el norte 2 celdas y llega a un cruce este-oeste.
    // Al doblar la esquina se ve el cruce: Fede pasa por ahi de izquierda a derecha (sale de detras de una pared
    // y se mete detras de la otra).
    static readonly Vector2Int[] CeldasEntrada = { new Vector2Int(0, 10), new Vector2Int(1, 10), new Vector2Int(2, 10), new Vector2Int(3, 10), new Vector2Int(4, 10), new Vector2Int(4, 11), new Vector2Int(4, 12) };
    static readonly Vector2Int[] CeldasCruce = { new Vector2Int(1, 13), new Vector2Int(2, 13), new Vector2Int(3, 13), new Vector2Int(4, 13), new Vector2Int(5, 13), new Vector2Int(6, 13), new Vector2Int(7, 13) };

    static void Generar(out List<Vector2Int> atajos)
    {
        var rnd = new System.Random(Semilla);
        muroH = new bool[N + 1, N]; muroV = new bool[N, N + 1]; fijoH = new bool[N + 1, N]; fijoV = new bool[N, N + 1];
        for (int i = 0; i <= N; i++) for (int j = 0; j < N; j++) muroH[i, j] = true;
        for (int i = 0; i < N; i++) for (int j = 0; j <= N; j++) muroV[i, j] = true;
        // El borde exterior es fijo
        for (int j = 0; j < N; j++) { fijoH[0, j] = fijoH[N, j] = true; }
        for (int i = 0; i < N; i++) { fijoV[i, 0] = fijoV[i, N] = true; }
        padre = new int[N * N];
        for (int k = 0; k < padre.Length; k++) padre[k] = k;

        // Salas: interior abierto y el contorno fijo (salvo las puertas elegidas)
        void Sala(RectInt r, List<(Vector2Int celda, Vector2Int dir)> puertas)
        {
            for (int i = r.xMin; i < r.xMax; i++)
            for (int j = r.yMin; j < r.yMax; j++)
            {
                if (i + 1 < r.xMax) { muroH[i + 1, j] = false; Unir(Id(i, j), Id(i + 1, j)); }
                if (j + 1 < r.yMax) { muroV[i, j + 1] = false; Unir(Id(i, j), Id(i, j + 1)); }
            }
            for (int i = r.xMin; i < r.xMax; i++) { fijoV[i, r.yMin] = true; fijoV[i, r.yMax] = true; }
            for (int j = r.yMin; j < r.yMax; j++) { fijoH[r.xMin, j] = true; fijoH[r.xMax, j] = true; }
            foreach (var (c, d) in puertas) Abrir(c, d);
        }

        // Sala con forma libre (lista de celdas)
        void SalaCeldas(Vector2Int[] celdas, List<(Vector2Int celda, Vector2Int dir)> puertas)
        {
            var set = new HashSet<Vector2Int>(celdas);
            foreach (Vector2Int c in celdas)
            {
                foreach (Vector2Int d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    Vector2Int n = c + d;
                    bool dentro = set.Contains(n);
                    if (d.x != 0) { int i = d.x > 0 ? c.x + 1 : c.x; muroH[i, c.y] = !dentro; fijoH[i, c.y] = true; }
                    else { int j = d.y > 0 ? c.y + 1 : c.y; muroV[c.x, j] = !dentro; fijoV[c.x, j] = true; }
                    if (dentro) Unir(Id(c.x, c.y), Id(n.x, n.y));
                }
            }
            foreach (var (c, d) in puertas) Abrir(c, d);
        }

        // Central: una puerta en el medio de cada lado
        Sala(Central, new List<(Vector2Int, Vector2Int)> {
            (new Vector2Int(8, 10), Vector2Int.left), (new Vector2Int(12, 10), Vector2Int.right),
            (new Vector2Int(10, 8), Vector2Int.down), (new Vector2Int(10, 12), Vector2Int.up) });
        // Salas de panel: dos puertas, hacia afuera del cuadrante y hacia el centro (en esquinas opuestas de la sala)
        Sala(SalasPanel[0], new List<(Vector2Int, Vector2Int)> { (new Vector2Int(3, 17), Vector2Int.up), (new Vector2Int(5, 15), Vector2Int.right) });
        Sala(SalasPanel[1], new List<(Vector2Int, Vector2Int)> { (new Vector2Int(17, 17), Vector2Int.right), (new Vector2Int(15, 15), Vector2Int.down) });
        Sala(SalasPanel[2], new List<(Vector2Int, Vector2Int)> { (new Vector2Int(17, 3), Vector2Int.down), (new Vector2Int(15, 5), Vector2Int.left) });
        Sala(SalasPanel[3], new List<(Vector2Int, Vector2Int)> { (new Vector2Int(3, 3), Vector2Int.left), (new Vector2Int(5, 5), Vector2Int.up) });
        // Rincones de los casilleros: una sola entrada
        Sala(Rincones[0], new List<(Vector2Int, Vector2Int)> { (new Vector2Int(1, 19), Vector2Int.right) });
        Sala(Rincones[1], new List<(Vector2Int, Vector2Int)> { (new Vector2Int(19, 19), Vector2Int.down) });
        Sala(Rincones[2], new List<(Vector2Int, Vector2Int)> { (new Vector2Int(19, 1), Vector2Int.left) });
        Sala(Rincones[3], new List<(Vector2Int, Vector2Int)> { (new Vector2Int(1, 1), Vector2Int.up) });
        // Entrada en L (una sola salida: al cruce) y el cruce (sale por sus dos puntas)
        SalaCeldas(CeldasEntrada, new List<(Vector2Int, Vector2Int)> { (new Vector2Int(4, 12), Vector2Int.up) });
        SalaCeldas(CeldasCruce, new List<(Vector2Int, Vector2Int)> { (new Vector2Int(1, 13), Vector2Int.left), (new Vector2Int(7, 13), Vector2Int.right), (new Vector2Int(4, 13), Vector2Int.down) });
        fijoH[0, 10] = true; muroH[0, 10] = false; // boca de la entrada (hacia el pasillo de la Zona Principal)

        // Kruskal: arbol que une todo
        var muros = new List<(int, int, bool)>(); // (i, j, esH)
        for (int i = 1; i < N; i++) for (int j = 0; j < N; j++) if (!fijoH[i, j]) muros.Add((i, j, true));
        for (int i = 0; i < N; i++) for (int j = 1; j < N; j++) if (!fijoV[i, j]) muros.Add((i, j, false));
        for (int k = muros.Count - 1; k > 0; k--) { int r = rnd.Next(k + 1); (muros[k], muros[r]) = (muros[r], muros[k]); }
        foreach (var (i, j, esH) in muros)
        {
            int a = esH ? Id(i - 1, j) : Id(i, j - 1), b = Id(i, j);
            if (Unir(a, b)) { if (esH) muroH[i, j] = false; else muroV[i, j] = false; }
        }

        // Trenzado: casi sin callejones sin salida (fuera de las salas)
        for (int pasada = 0; pasada < 2; pasada++)
        for (int i = 0; i < N; i++)
        for (int j = 0; j < N; j++)
        {
            if (EnSala(i, j)) continue;
            var cerrados = MurosAbiertables(i, j);
            int abiertos = 4 - MurosCerrados(i, j);
            if (abiertos <= 1 && cerrados.Count > 0 && rnd.NextDouble() < 0.85)
            {
                var (mi, mj, esH) = cerrados[rnd.Next(cerrados.Count)];
                if (esH) muroH[mi, mj] = false; else muroV[mi, mj] = false;
            }
        }
        // Algunos bucles extra
        foreach (var (i, j, esH) in muros)
            if ((esH ? muroH[i, j] : muroV[i, j]) && rnd.NextDouble() < 0.06)
            { if (esH) muroH[i, j] = false; else muroV[i, j] = false; }

        // Atajos: muros cerrados entre celdas que por el camino estan lejos -> pared elevada (pasar por debajo)
        atajos = new List<Vector2Int>(); // x = indice de muro codificado, y = 1 si es H
        var candidatos = new List<(int i, int j, bool esH, int dist)>();
        foreach (var (i, j, esH) in muros)
        {
            if (!(esH ? muroH[i, j] : muroV[i, j])) continue;
            int a = esH ? Id(i - 1, j) : Id(i, j - 1), b = Id(i, j);
            int d = Distancia(a, b);
            if (d >= 7) candidatos.Add((i, j, esH, d));
        }
        candidatos.Sort((x, y) => y.dist.CompareTo(x.dist));
        var usados = new List<Vector2>();
        foreach (var c in candidatos)
        {
            Vector2 p = c.esH ? new Vector2(c.i, c.j + 0.5f) : new Vector2(c.i + 0.5f, c.j);
            bool lejos = true;
            foreach (Vector2 u in usados) if ((u - p).magnitude < 5f) lejos = false;
            if (!lejos) continue;
            usados.Add(p);
            atajos.Add(new Vector2Int(c.esH ? c.i * 1000 + c.j : c.i * 1000 + c.j, c.esH ? 1 : 0));
            if (c.esH) muroH[c.i, c.j] = false; else muroV[c.i, c.j] = false; // abierto para el trazado; la pared elevada lo tapa
            if (atajos.Count >= 8) break;
        }
    }

    // Abre (y deja fijo) el muro entre la celda c y su vecina en direccion d
    static void Abrir(Vector2Int c, Vector2Int d)
    {
        if (d.x != 0) { int i = d.x > 0 ? c.x + 1 : c.x; muroH[i, c.y] = false; fijoH[i, c.y] = true; }
        else { int j = d.y > 0 ? c.y + 1 : c.y; muroV[c.x, j] = false; fijoV[c.x, j] = true; }
        Vector2Int n = c + d;
        if (n.x >= 0 && n.y >= 0 && n.x < N && n.y < N) Unir(Id(c.x, c.y), Id(n.x, n.y));
    }

    static bool EnSala(int i, int j)
    {
        var p = new Vector2Int(i, j);
        if (Central.Contains(p) || System.Array.IndexOf(CeldasEntrada, p) >= 0 || System.Array.IndexOf(CeldasCruce, p) >= 0) return true;
        foreach (RectInt r in SalasPanel) if (r.Contains(p)) return true;
        foreach (RectInt r in Rincones) if (r.Contains(p)) return true;
        return false;
    }

    static int MurosCerrados(int i, int j) =>
        (muroH[i, j] ? 1 : 0) + (muroH[i + 1, j] ? 1 : 0) + (muroV[i, j] ? 1 : 0) + (muroV[i, j + 1] ? 1 : 0);

    static List<(int, int, bool)> MurosAbiertables(int i, int j)
    {
        var l = new List<(int, int, bool)>();
        if (muroH[i, j] && !fijoH[i, j] && i > 0 && !EnSala(i - 1, j)) l.Add((i, j, true));
        if (muroH[i + 1, j] && !fijoH[i + 1, j] && i + 1 < N && !EnSala(i + 1, j)) l.Add((i + 1, j, true));
        if (muroV[i, j] && !fijoV[i, j] && j > 0 && !EnSala(i, j - 1)) l.Add((i, j, false));
        if (muroV[i, j + 1] && !fijoV[i, j + 1] && j + 1 < N && !EnSala(i, j + 1)) l.Add((i, j + 1, false));
        return l;
    }

    static int Distancia(int a, int b)
    {
        var dist = new int[N * N];
        for (int k = 0; k < dist.Length; k++) dist[k] = -1;
        var cola = new Queue<int>();
        dist[a] = 0; cola.Enqueue(a);
        while (cola.Count > 0)
        {
            int c = cola.Dequeue(); int i = c % N, j = c / N;
            if (c == b) return dist[c];
            void Ir(int ni, int nj, bool muro) { if (muro || ni < 0 || nj < 0 || ni >= N || nj >= N) return; int k = Id(ni, nj); if (dist[k] < 0) { dist[k] = dist[c] + 1; cola.Enqueue(k); } }
            Ir(i - 1, j, muroH[i, j]); Ir(i + 1, j, muroH[i + 1, j]); Ir(i, j - 1, muroV[i, j]); Ir(i, j + 1, muroV[i, j + 1]);
        }
        return 999;
    }

    // ------------------------------------------------------------------ Construccion

    [MenuItem("Tools/Laberinto/Nuevo trazado")]
    static void Construir()
    {
        GameObject labGo = GameObject.Find("Laberinto");
        if (!labGo) return;
        Transform lab = labGo.transform;
        Transform paredes = lab.Find("Paredes"), elev = lab.Find("Paredes Elevadas"), cas = lab.Find("casilleros");
        Transform piso = lab.Find("Piso"), techo = lab.Find("techo");
        if (!paredes || !elev || !cas || !piso) { Debug.LogWarning("[Laberinto] Faltan contenedores (Paredes, Paredes Elevadas, casilleros o Piso)."); return; }

        Undo.IncrementCurrentGroup();
        int grupo = Undo.GetCurrentGroup();

        // Lo que se reusa de lo anterior
        Material matPared = PrimerMaterial(paredes), matElev = PrimerMaterial(elev);
        Vector3 escalaArmario = cas.childCount > 0 ? cas.GetChild(0).localScale : Vector3.one;
        var prefabArmario = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Armario.prefab");
        foreach (Transform c in new[] { paredes, elev, cas })
            for (int k = c.childCount - 1; k >= 0; k--) Undo.DestroyObjectImmediate(c.GetChild(k).gameObject);
        foreach (string viejo in new[] { "Paneles", "Luces Laberinto", "Zona Segura (centro)", "Puntos Laberinto" })
        {
            Transform t = lab.Find(viejo);
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }
        Transform marca = lab.Find(Version);
        if (marca) Undo.DestroyObjectImmediate(marca.gameObject);

        // Origen: borde oeste en la boca del pasillo, la fila central a la altura del pasillo
        Vector3 boca = BocaPasillo(lab);
        float x0 = boca.x + Grosor * 0.5f, z0 = boca.z - (N * Celda) * 0.5f;
        Vector3 W(float ci, float cj, float y = 0f) => new Vector3(x0 + ci * Celda, y, z0 + cj * Celda);

        Generar(out List<Vector2Int> atajos);

        // Muros verticales (lineas x = const): se unen los tramos seguidos
        int nPared = 0;
        for (int i = 0; i <= N; i++)
        {
            int j = 0;
            while (j < N)
            {
                if (!muroH[i, j]) { j++; continue; }
                int j1 = j; while (j1 < N && muroH[i, j1]) j1++;
                Caja(paredes, "Pared " + nPared++, W(i, j) + new Vector3(-Grosor * 0.5f, 0, -Grosor * 0.5f), W(i, j1) + new Vector3(Grosor * 0.5f, Alto, Grosor * 0.5f), matPared, true);
                j = j1;
            }
        }
        bool VerticalEn(int i, int j) => (j - 1 >= 0 && muroH[i, j - 1]) || (j < N && muroH[i, j]);
        for (int j = 0; j <= N; j++)
        {
            int i = 0;
            while (i < N)
            {
                if (!muroV[i, j]) { i++; continue; }
                int i1 = i + 1;
                while (i1 < N && muroV[i1, j] && !VerticalEn(i1, j)) i1++;
                float xa = W(i, j).x + (VerticalEn(i, j) ? Grosor * 0.5f : -Grosor * 0.5f);
                float xb = W(i1, j).x + (VerticalEn(i1, j) ? -Grosor * 0.5f : Grosor * 0.5f);
                if (xb - xa > 0.01f)
                    Caja(paredes, "Pared " + nPared++, new Vector3(xa, 0f, W(i, j).z - Grosor * 0.5f), new Vector3(xb, Alto, W(i, j).z + Grosor * 0.5f), matPared, true);
                i = i1;
            }
        }
        // Paredes elevadas (atajos)
        int nElev = 0;
        foreach (Vector2Int a in atajos)
        {
            int i = a.x / 1000, j = a.x % 1000;
            bool esH = a.y == 1;
            Vector3 min, max;
            if (esH) { min = W(i, j) + new Vector3(-Grosor * 0.5f - 0.2f, BaseElevada, Grosor * 0.5f); max = W(i, j + 1) + new Vector3(Grosor * 0.5f + 0.2f, Alto, -Grosor * 0.5f); }
            else { min = W(i, j) + new Vector3(Grosor * 0.5f, BaseElevada, -Grosor * 0.5f - 0.2f); max = W(i + 1, j) + new Vector3(-Grosor * 0.5f, Alto, Grosor * 0.5f + 0.2f); }
            Caja(elev, "Pared Elevada " + nElev++, min, max, matElev, true);
        }

        // Piso y techo cubriendo todo
        Ajustar(piso, new Vector3(x0 - Grosor * 0.5f, -0.28f, z0 - Grosor * 0.5f), new Vector3(x0 + N * Celda + Grosor * 0.5f, 0f, z0 + N * Celda + Grosor * 0.5f));
        // Techo: activo, tapando todo el laberinto apoyado sobre las paredes (al oeste empieza despues del bloque de la reja)
        if (techo)
        {
            Undo.RecordObject(techo.gameObject, "Techo");
            techo.gameObject.SetActive(true);
            Ajustar(techo, new Vector3(x0 + Grosor * 0.5f, Alto, z0 - Grosor * 0.5f), new Vector3(x0 + N * Celda + Grosor * 0.5f, Alto + 0.3f, z0 + N * Celda + Grosor * 0.5f));
        }

        // Paneles (bloques blancos) en el centro de cada sala de cuadrante
        Material blanco = MaterialPanel();
        Transform paneles = Hijo(lab, "Paneles");
        Transform luces = Hijo(lab, "Luces Laberinto");
        for (int k = 0; k < SalasPanel.Length; k++)
        {
            RectInt r = SalasPanel[k];
            Vector3 c = W(r.center.x, r.center.y);
            Caja(paneles, "Panel " + (k + 1), c + new Vector3(-0.8f, 0f, -0.3f), c + new Vector3(0.8f, 2.2f, 0.3f), blanco, true);
            Luz(luces, "Luz Panel " + (k + 1), c + new Vector3(0f, 3.6f, 0f), new Color(0.85f, 0.9f, 1f), 9f, 3.5f);
        }

        // Casilleros en las esquinas, mirando hacia adentro de su rincon
        if (prefabArmario)
            for (int k = 0; k < Rincones.Length; k++)
            {
                RectInt r = Rincones[k];
                bool este = r.xMin > N / 2, norte = r.yMin > N / 2;
                Vector3 esquina = W(este ? N : 0, norte ? N : 0);
                Vector3 haciaAdentro = new Vector3(este ? -1f : 1f, 0f, norte ? -1f : 1f).normalized;
                var arm = (GameObject)PrefabUtility.InstantiatePrefab(prefabArmario, cas);
                Undo.RegisterCreatedObjectUndo(arm, "Casillero");
                arm.name = "Armario " + (k + 1);
                arm.transform.localScale = escalaArmario;
                arm.transform.position = esquina + haciaAdentro * 1.9f;
                arm.transform.rotation = Quaternion.LookRotation(haciaAdentro, Vector3.up);
                // El frente del armario es hacia donde queda su "Punto Salida": se gira para que mire al rincon
                Transform salida = null;
                foreach (Transform t in arm.GetComponentsInChildren<Transform>(true)) if (t.name == "Punto Salida") { salida = t; break; }
                if (salida)
                {
                    Vector3 frente = salida.position - arm.transform.position; frente.y = 0f;
                    if (frente.sqrMagnitude > 0.0001f)
                        arm.transform.rotation = Quaternion.FromToRotation(frente.normalized, haciaAdentro) * arm.transform.rotation;
                    arm.transform.position = esquina + haciaAdentro * 1.9f;
                }
            }

        // Zona segura: la habitacion central (las lianas guian hasta aqui)
        Vector3 centro = W(Central.center.x, Central.center.y);
        var zona = new GameObject("Zona Segura (centro)");
        Undo.RegisterCreatedObjectUndo(zona, "Zona segura");
        zona.transform.SetParent(lab, false);
        zona.transform.position = centro + Vector3.up * 1.5f;
        zona.transform.rotation = Quaternion.identity;
        var col = zona.AddComponent<BoxCollider>();
        col.isTrigger = true;
        Vector3 ls = zona.transform.lossyScale;
        col.size = new Vector3((Central.width * Celda - 1f) / ls.x, 3f / ls.y, (Central.height * Celda - 1f) / ls.z);
        zona.AddComponent<ZonaSeguraLianas>();
        Luz(luces, "Luz Centro", centro + Vector3.up * 3.7f, new Color(1f, 0.82f, 0.6f), 14f, 4f);

        var inicio = new GameObject("Inicio Laberinto");
        inicio.transform.SetParent(lab, false);
        inicio.transform.position = W(0.5f, 10.5f);

        // Lianas: mismos contenedores, nueva zona segura y sin la ruta vieja (p1...)
        var lianas = labGo.GetComponent<LianasLaberinto>();
        if (lianas)
        {
            Undo.RecordObject(lianas, "Lianas");
            lianas.zonaSegura = zona.transform;
            lianas.puntosRuta.Clear();
            lianas.puntoInicio = inicio.transform;
            lianas.AutoConfigurar();
            lianas.zonaSegura = zona.transform;
            lianas.puntosRuta.Clear();
            EditorUtility.SetDirty(lianas);
        }
        ParedesBajoSetup.AutoAplicar();

        new GameObject(Version).transform.SetParent(lab, false);
        Physics.SyncTransforms();
        Rejas(lab, W(0, 10), W(0, 11), W(1.2f, 10f), W(3f, 11f), matPared);
        Texturas(lab, piso);
        TexturaTecho(techo);
        Dependientes(lab, W(N * 0.5f, N * 0.5f));
        ConfigurarPresentacion(W(4f, 11f), W(5f, 12f), W(1.5f, 13.5f), W(6.8f, 13.5f), W(4.5f, 11.4f, 1.6f), W(4.5f, 13.5f, 1.3f));
        if (lianas) lianas.RegenerarVistaPrevia();

        EditorSceneManager.MarkSceneDirty(labGo.scene);
        Undo.CollapseUndoOperations(grupo);
        Debug.Log($"[Laberinto] Nuevo trazado: {N * Celda} x {N * Celda} m, {nPared} paredes, {nElev} paredes elevadas, 4 paneles, 4 casilleros, habitacion central. Guarda la escena (Ctrl+S).", labGo);
    }

    // Donde termina el pasillo de la Zona Principal (borde este de su piso)
    static Vector3 BocaPasillo(Transform lab)
    {
        Bounds b = BoundsPasillo();
        if (b.size != Vector3.zero) return new Vector3(b.max.x, 0f, b.center.z);
        return lab.position;
    }

    static Bounds BoundsPasillo()
    {
        GameObject p = GameObject.Find("Zona_Principal/Pasillos/Pasillo - Laberinto/Piso");
        return p && p.TryGetComponent(out Renderer r) ? r.bounds : new Bounds();
    }

    // ------------------------------------------------------------------ Lo que depende del laberinto

    static void Dependientes(Transform lab, Vector3 centro)
    {
        // NavMesh solo del laberinto (la superficie y sus ajustes ya estan en el objeto)
        var surface = lab.GetComponent<NavMeshSurface>();
        if (surface)
        {
            var guardar = typeof(EnemigosZonasSetup).GetMethod("GuardarNavMesh", BindingFlags.NonPublic | BindingFlags.Static);
            if (guardar != null) guardar.Invoke(null, new object[] { surface }); else surface.BuildNavMesh();
        }

        // Trigger del Volume: exactamente el piso del laberinto (desde la boca hacia adentro) y hasta arriba de las paredes.
        // (Antes se calculaba con todos los colliders y se recortaba contra la Zona Principal, y quedaba corrido.)
        GameObject trig = GameObject.Find("Laberinto/Trigger Laberinto");
        if (!trig) trig = GameObject.Find("Enemigos y Zonas/Triggers Volumen/Trigger Laberinto");
        Transform pisoT = lab.Find("Piso");
        if (trig && pisoT && pisoT.TryGetComponent(out Renderer pr))
        {
            Bounds b = pr.bounds;
            b.SetMinMax(new Vector3(b.min.x, -1f, b.min.z), new Vector3(b.max.x, Alto + 4f, b.max.z));
            Undo.RecordObject(trig.transform, "Trigger");
            trig.transform.SetPositionAndRotation(b.center, Quaternion.identity);
            var box = trig.GetComponent<BoxCollider>();
            Undo.RecordObject(box, "Trigger");
            Vector3 s = trig.transform.lossyScale;
            box.center = Vector3.zero;
            box.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z);
        }

        // Enemigo: lejos de la entrada, sobre el NavMesh nuevo
        GameObject grupo = GameObject.Find("Laberinto/Enemigos Laberinto");
        if (!grupo) grupo = GameObject.Find("Enemigos y Zonas/Enemigos/Enemigos Laberinto");
        if (grupo)
        {
            int k = 0;
            Vector3[] puntos = { centro + new Vector3(24f, 0f, 24f), centro + new Vector3(24f, 0f, -24f), centro + new Vector3(0f, 0f, 30f) };
            foreach (Transform e in grupo.transform)
            {
                Vector3 p = puntos[k++ % puntos.Length];
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 8f, NavMesh.AllAreas)) continue;
                float alto = e.TryGetComponent(out NavMeshAgent ag) ? ag.baseOffset : 0f;
                Undo.RecordObject(e, "Enemigo");
                e.position = hit.position + Vector3.up * alto;
            }
        }

        // Baterias del laberinto: en pasillos, repartidas
        var listaBats = new List<Transform>();
        foreach (Bateria bt in Object.FindObjectsByType<Bateria>(FindObjectsInactive.Include, FindObjectsSortMode.None)) listaBats.Add(bt.transform);
        if (listaBats.Count > 0)
        {
            var rnd = new System.Random(5);
            var puestas = new List<Vector3>();
            foreach (Transform b in listaBats)
            {
                if (!b.name.StartsWith("Bateria Laberinto")) continue;
                for (int i = 0; i < 300; i++)
                {
                    Vector3 p = centro + new Vector3(((float)rnd.NextDouble() - 0.5f) * N * Celda * 0.9f, 0f, ((float)rnd.NextDouble() - 0.5f) * N * Celda * 0.9f);
                    if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                    bool lejos = true;
                    foreach (Vector3 o in puestas) if ((o - hit.position).magnitude < 20f) lejos = false;
                    if (!lejos || (hit.position - centro).magnitude < 12f) continue;
                    if (!Physics.Raycast(hit.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit suelo, 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    puestas.Add(hit.position);
                    Undo.RecordObject(b, "Bateria");
                    b.position = suelo.point + Vector3.up * 0.1f;
                    break;
                }
            }
        }

        // Presentacion del enemigo (busca sola un pasillo largo que se vea desde la entrada)
        var menuPres = typeof(PresentacionEnemigoSetup).GetMethod("Menu", BindingFlags.NonPublic | BindingFlags.Static);
        if (menuPres != null) menuPres.Invoke(null, null);
    }

    // ------------------------------------------------------------------ Rejas de la entrada

    // Reja (rastrillo) escondida en un bloque de piedra sobre la boca; cae cuando el jugador entra.
    static void Rejas(Transform lab, Vector3 bocaA, Vector3 bocaB, Vector3 zonaMin, Vector3 zonaMax, Material piedra)
    {
        Transform vieja = lab.Find("Rejas Entrada");
        if (vieja) Undo.DestroyObjectImmediate(vieja.gameObject);
        var raiz = new GameObject("Rejas Entrada");
        Undo.RegisterCreatedObjectUndo(raiz, "Rejas");
        raiz.transform.SetParent(lab, false);
        raiz.transform.position = (bocaA + bocaB) * 0.5f;
        raiz.transform.rotation = Quaternion.identity;

        float x = bocaA.x;
        float zA = bocaA.z + Grosor * 0.5f, zB = bocaB.z - Grosor * 0.5f;
        // Bloque de piedra encima de la boca (apoyado sobre el borde, del mismo grosor)
        Caja(raiz.transform, "Bloque de la reja", new Vector3(x - Grosor * 0.5f, Alto, bocaA.z - Grosor * 0.5f - 0.6f), new Vector3(x + Grosor * 0.5f, Alto + 4.4f, bocaB.z + Grosor * 0.5f + 0.6f), piedra, true);

        // La reja (barrotes verticales y travesanos), levantada dentro del bloque
        Material hierro = MaterialHierro();
        var reja = new GameObject("Reja");
        reja.transform.SetParent(raiz.transform, false);
        reja.transform.rotation = Quaternion.identity;
        reja.transform.position = new Vector3(x, Alto + 0.05f, (zA + zB) * 0.5f);
        float altoReja = Alto - 0.05f;
        for (float z = zA + 0.15f; z < zB - 0.05f; z += 0.32f)
            Caja(reja.transform, "Barrote", new Vector3(x - 0.05f, Alto + 0.05f, z - 0.045f), new Vector3(x + 0.05f, Alto + 0.05f + altoReja, z + 0.045f), hierro, false);
        foreach (float h in new[] { 0.35f, 1.6f, 2.9f, altoReja - 0.15f })
            Caja(reja.transform, "Travesano", new Vector3(x - 0.07f, Alto + 0.05f + h - 0.06f, zA), new Vector3(x + 0.07f, Alto + 0.05f + h + 0.06f, zB), hierro, false);
        // Puntas abajo
        for (float z = zA + 0.15f; z < zB - 0.05f; z += 0.32f)
            Caja(reja.transform, "Punta", new Vector3(x - 0.03f, Alto + 0.05f - 0.12f, z - 0.025f), new Vector3(x + 0.03f, Alto + 0.05f, z + 0.025f), hierro, false);
        // La reja se mueve en el juego: no puede ser estatica (el static batching la dejaria congelada arriba)
        foreach (Transform t in reja.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        var col = reja.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, altoReja * 0.5f, 0f);
        Vector3 ls = reja.transform.lossyScale;
        col.size = new Vector3(0.3f / ls.x, altoReja / ls.y, (zB - zA) / ls.z);

        // Zona: un poco adentro del pasillo de entrada
        var zonaGo = new GameObject("Zona Rejas");
        zonaGo.layer = 2;
        zonaGo.transform.SetParent(raiz.transform, false);
        zonaGo.transform.rotation = Quaternion.identity;
        zonaGo.transform.position = new Vector3((zonaMin.x + zonaMax.x) * 0.5f, 1.5f, (zonaMin.z + zonaMax.z) * 0.5f);
        var zona = zonaGo.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        Vector3 zs = zonaGo.transform.lossyScale;
        zona.size = new Vector3(Mathf.Abs(zonaMax.x - zonaMin.x) / zs.x, 3f / zs.y, Mathf.Abs(zonaMax.z - zonaMin.z) / zs.z);

        var r = raiz.AddComponent<RejasEntrada>();
        r.reja = reja.transform;
        r.zona = zona;
        r.caida = Alto + 0.05f;
        r.estruendo = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sonidos/SFX/RejaCae.wav");
    }

    static Material MaterialHierro()
    {
        const string ruta = "Assets/Materials/RejaHierro.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "RejaHierro" };
        m.SetColor("_BaseColor", new Color(0.12f, 0.1f, 0.09f));
        m.SetFloat("_Metallic", 0.85f);
        m.SetFloat("_Smoothness", 0.35f);
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ------------------------------------------------------------------ Presentacion de Fede en el cruce

    static void ConfigurarPresentacion(Vector3 zonaMin, Vector3 zonaMax, Vector3 aparicion, Vector3 destino, Vector3 vista, Vector3 mirar)
    {
        var pres = Object.FindFirstObjectByType<PresentacionEnemigo>(FindObjectsInactive.Include);
        if (!pres) return;
        Undo.RecordObject(pres, "Presentacion");
        Transform padre = pres.transform;
        Transform Punto(Transform actual, string nombre, Vector3 pos, Quaternion rot)
        {
            Transform t = actual ? actual : new GameObject(nombre).transform;
            if (!actual) { Undo.RegisterCreatedObjectUndo(t.gameObject, nombre); t.SetParent(padre, false); }
            Undo.RecordObject(t, nombre);
            t.SetPositionAndRotation(pos, rot);
            return t;
        }
        Quaternion haciaDerecha = Quaternion.LookRotation(destino - aparicion, Vector3.up);
        pres.puntoAparicion = Punto(pres.puntoAparicion, "Punto Aparicion", aparicion, haciaDerecha);
        pres.puntoDestino = Punto(pres.puntoDestino, "Punto Destino", destino, haciaDerecha);
        pres.puntoVista = Punto(pres.puntoVista, "Punto Vista", vista, Quaternion.LookRotation(mirar - vista));
        pres.mirarFijo = Punto(pres.mirarFijo, "Mirar Fijo", mirar, Quaternion.identity);
        pres.franjasCine = false; // (si no, su setup lo rehace)
        pres.velocidadEnemigo = 2.4f;
        pres.duracionIr = 1.1f;
        pres.duracionMirar = 7.5f;
        if (pres.zona)
        {
            Undo.RecordObject(pres.zona.transform, "Zona");
            Undo.RecordObject(pres.zona, "Zona");
            pres.zona.transform.rotation = Quaternion.identity;
            pres.zona.transform.position = new Vector3((zonaMin.x + zonaMax.x) * 0.5f, 1.5f, (zonaMin.z + zonaMax.z) * 0.5f);
            Vector3 s = pres.zona.transform.lossyScale;
            pres.zona.center = Vector3.zero;
            pres.zona.size = new Vector3(Mathf.Abs(zonaMax.x - zonaMin.x) / s.x, 3f / s.y, Mathf.Abs(zonaMax.z - zonaMin.z) / s.z);
        }
        EditorUtility.SetDirty(pres);
    }

    // ------------------------------------------------------------------ Texturas sin estirar

    // Cada bloque recibe una malla con UV en metros y los materiales repiten la textura cada tantos metros.
    static void Texturas(Transform lab, Transform piso)
    {
        var copias = new Dictionary<Material, Material>();
        Material Copia(Material m, float metros)
        {
            if (!m) return null;
            if (m.name.StartsWith("Lab_")) return m;
            if (copias.TryGetValue(m, out Material ya)) return ya;
            string ruta = $"Assets/Laberinto/Materiales/Lab_{m.name}.mat";
            var c = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (!c)
            {
                System.IO.Directory.CreateDirectory("Assets/Laberinto/Materiales");
                c = new Material(m) { name = "Lab_" + m.name };
                AssetDatabase.CreateAsset(c, ruta);
            }
            c.SetTextureScale("_BaseMap", Vector2.one / metros);
            c.SetTextureOffset("_BaseMap", Vector2.zero);
            EditorUtility.SetDirty(c);
            return copias[m] = c;
        }
        var conjuntos = new (string ruta, float metros)[] { ("Paredes", 2.5f), ("Paredes Elevadas", 2f), ("Rejas Entrada", 2.5f) };
        foreach (var (ruta, metros) in conjuntos)
        {
            Transform c = lab.Find(ruta);
            if (!c) continue;
            foreach (Renderer r in c.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.TryGetComponent(out MeshFilter mf) || !mf.sharedMesh || mf.sharedMesh.name != "Cube") continue;
                if (r.sharedMaterial && r.sharedMaterial.name == "RejaHierro") continue;
                Undo.RecordObject(mf, "UV"); Undo.RecordObject(r, "Material");
                mf.sharedMesh = CuboMetros(r.bounds.size);
                r.sharedMaterial = Copia(r.sharedMaterial, metros);
            }
        }
        if (piso && piso.TryGetComponent(out MeshFilter pf) && piso.TryGetComponent(out Renderer pr))
        {
            Undo.RecordObject(pf, "UV"); Undo.RecordObject(pr, "Material");
            // El piso esta girado: la malla se arma en sus ejes locales con su escala real
            Vector3 s = piso.lossyScale;
            pf.sharedMesh = CuboMetros(new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) * 1f);
            pr.sharedMaterial = Copia(pr.sharedMaterial, 3f);
        }
    }

    // Cubo unitario (como el de Unity) con UV en metros para un bloque de ese tamano
    static Mesh CuboMetros(Vector3 tam)
    {
        string clave = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00}x{1:0.00}x{2:0.00}", tam.x, tam.y, tam.z);
        string ruta = $"Assets/Laberinto/Mallas/Cubo_{clave}.asset";
        var ya = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (ya) return ya;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        Vector3[,] caras =
        {
            { Vector3.right, Vector3.up, Vector3.forward }, { Vector3.left, Vector3.forward, Vector3.up },
            { Vector3.up, Vector3.forward, Vector3.right }, { Vector3.down, Vector3.right, Vector3.forward },
            { Vector3.forward, Vector3.right, Vector3.up }, { Vector3.back, Vector3.up, Vector3.right },
        };
        for (int c = 0; c < 6; c++)
        {
            Vector3 nn = caras[c, 0], A = caras[c, 1], B = caras[c, 2];
            int b0 = v.Count;
            Vector3[] esq = { nn * 0.5f - A * 0.5f - B * 0.5f, nn * 0.5f + A * 0.5f - B * 0.5f, nn * 0.5f + A * 0.5f + B * 0.5f, nn * 0.5f - A * 0.5f + B * 0.5f };
            foreach (Vector3 p in esq)
            {
                v.Add(p); n.Add(nn);
                Vector3 q = Vector3.Scale(p, tam);
                uv.Add(Mathf.Abs(nn.y) > 0.5f ? new Vector2(q.x, q.z) : Mathf.Abs(nn.x) > 0.5f ? new Vector2(q.z, q.y) : new Vector2(q.x, q.y));
            }
            t.AddRange(new[] { b0, b0 + 1, b0 + 2, b0, b0 + 2, b0 + 3 });
        }
        var m = new Mesh { name = "Cubo_" + clave };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateTangents(); m.RecalculateBounds();
        System.IO.Directory.CreateDirectory("Assets/Laberinto/Mallas");
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ------------------------------------------------------------------ Techo (va con las venas brillantes)

    // Piedra oscura y humeda en losas irregulares con grietas profundas (de donde parecen salir las venas)
    // y un leve tinte indigo en las juntas.
    static void TexturaTecho(Transform techo)
    {
        if (!techo || !techo.TryGetComponent(out MeshFilter mf) || !techo.TryGetComponent(out Renderer r)) return;
        var a = SotanoAcabados.Generar("TechoLaberinto", MuestraTecho, 1.8f, 3f, 0.3f, "Assets/Laberinto/Texturas", "Lab_");
        const string ruta = "Assets/Laberinto/Materiales/Lab_Techo.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        bool nuevo = !m;
        if (nuevo) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Lab_Techo" };
        m.SetTexture("_BaseMap", a.color);
        m.SetTexture("_BumpMap", a.normal);
        m.SetTextureScale("_BaseMap", Vector2.one / 3f);
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat("_BumpScale", 1.2f);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Smoothness", 0.38f);
        if (nuevo) { System.IO.Directory.CreateDirectory("Assets/Laberinto/Materiales"); AssetDatabase.CreateAsset(m, ruta); }
        else EditorUtility.SetDirty(m);
        Undo.RecordObject(mf, "UV techo"); Undo.RecordObject(r, "Material techo");
        Vector3 s = techo.lossyScale;
        mf.sharedMesh = CuboMetros(new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
        r.sharedMaterial = m;
    }

    static float RT(float u, float v, float esc, float sem)
    {
        float a = Mathf.PerlinNoise(u * esc + sem, v * esc + sem * 1.7f), b = Mathf.PerlinNoise((u - 1f) * esc + sem, v * esc + sem * 1.7f);
        float c = Mathf.PerlinNoise(u * esc + sem, (v - 1f) * esc + sem * 1.7f), d = Mathf.PerlinNoise((u - 1f) * esc + sem, (v - 1f) * esc + sem * 1.7f);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static void MuestraTecho(float u, float v, out Color c, out float h)
    {
        // Losas irregulares: se deforma la cuadricula con ruido
        float du = (RT(u, v, 4f, 3f) - 0.5f) * 0.18f, dv = (RT(u, v, 4f, 9f) - 0.5f) * 0.18f;
        float fu = Mathf.Repeat((u + du) * 3f, 1f), fv = Mathf.Repeat((v + dv) * 3f, 1f);
        float borde = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv));
        float junta = Mathf.Clamp01((borde - 0.015f) / 0.04f);
        int id = Mathf.FloorToInt((u + du) * 3f) * 7 + Mathf.FloorToInt((v + dv) * 3f) * 13;
        float tono = 0.85f + Mathf.Repeat(Mathf.Sin(id * 12.9898f) * 43758.55f, 1f) * 0.3f;
        float grano = RT(u, v, 70f, 1f) * 0.6f + RT(u, v, 22f, 2f) * 0.4f;
        float humedad = Mathf.Clamp01((RT(u, v, 3f, 5f) - 0.5f) * 3f);
        // Grietas finas que cruzan las losas
        float g = RT(u, v, 5f, 21f) * 0.6f + RT(u, v, 15f, 6f) * 0.3f + RT(u, v, 40f, 13f) * 0.1f;
        float grieta = (1f - Mathf.Clamp01(Mathf.Abs(g - 0.5f) * 90f)) * Mathf.Clamp01((RT(u, v, 2.5f, 33f) - 0.45f) * 5f);
        Color piedra = new Color(0.13f, 0.125f, 0.15f) * tono * (0.8f + grano * 0.35f);
        piedra *= 1f - humedad * 0.35f;
        Color hueco = new Color(0.04f, 0.02f, 0.07f); // juntas y grietas con un leve tinte indigo
        c = Color.Lerp(hueco, piedra, junta * (1f - grieta));
        h = junta * (0.65f + grano * 0.3f) - grieta * 0.5f;
    }

    // ------------------------------------------------------------------ Utilidades

    static Material PrimerMaterial(Transform t)
    {
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) if (r.sharedMaterial) return r.sharedMaterial;
        return null;
    }

    // Cubo con medidas del mundo (min/max) dentro de un contenedor que puede estar girado/escalado
    static GameObject Caja(Transform padre, string nombre, Vector3 a, Vector3 b, Material mat, bool colision)
    {
        Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, "Bloque");
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.rotation = Quaternion.identity;
        go.transform.position = (min + max) * 0.5f;
        Vector3 tam = max - min;
        Vector3 ps = padre.lossyScale;
        // Padre girado solo en Y y con escala igual en X/Z: se divide eje por eje
        go.transform.localScale = new Vector3(tam.x / Mathf.Abs(ps.x), tam.y / Mathf.Abs(ps.y), tam.z / Mathf.Abs(ps.z));
        if (!colision) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    // Escala un bloque existente (piso/techo) para que ocupe esos limites del mundo
    static void Ajustar(Transform t, Vector3 min, Vector3 max)
    {
        var r = t.GetComponent<Renderer>();
        if (!r) return;
        Undo.RecordObject(t, "Ajustar");
        Vector3 objetivo = max - min;
        for (int it = 0; it < 2; it++)
        {
            Bounds b = r.bounds;
            Vector3 s = t.localScale;
            // Que eje local apunta a X y cual a Z del mundo
            Vector3[] ejes = { t.right, t.up, t.forward };
            for (int k = 0; k < 3; k++)
            {
                Vector3 e = ejes[k];
                float ax = Mathf.Abs(e.x), ay = Mathf.Abs(e.y), az = Mathf.Abs(e.z);
                float actual = ax > 0.5f ? b.size.x : ay > 0.5f ? b.size.y : b.size.z;
                float deseado = ax > 0.5f ? objetivo.x : ay > 0.5f ? objetivo.y : objetivo.z;
                if (actual > 0.0001f) s[k] *= deseado / actual;
            }
            t.localScale = s;
        }
        Bounds f = r.bounds;
        t.position += (min + max) * 0.5f - f.center;
        var c = t.GetComponent<BoxCollider>();
        if (c) EditorUtility.SetDirty(c);
    }

    static Transform Hijo(Transform padre, string nombre)
    {
        var go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }

    static void Luz(Transform padre, string nombre, Vector3 pos, Color color, float rango, float intensidad)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = rango;
        l.intensity = intensidad;
        l.shadows = LightShadows.None;
    }

    static Material MaterialPanel()
    {
        const string ruta = "Assets/Materials/PanelBlanco.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PanelBlanco" };
        m.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.9f));
        m.SetFloat("_Smoothness", 0.3f);
        m.SetColor("_EmissionColor", new Color(0.12f, 0.12f, 0.12f));
        m.EnableKeyword("_EMISSION");
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }
}
