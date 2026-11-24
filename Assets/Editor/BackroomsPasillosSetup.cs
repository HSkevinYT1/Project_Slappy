using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Backrooms nuevos: mas pasillos que habitaciones. Mismo terreno (60 x 60 m), misma entrada, mismo piso y techo.
// El plano es una cuadricula de 24 x 24 celdas de 2.5 m (ver "Plano" abajo):
//   '.' pasillo   ',' pasillo del sector de la entrada   '#' macizo (bloque de pared)   'p' zona abierta con pilares
//   otra letra = habitacion (c = la del cadaver y los focos, r = donde reaparece el gato, o/k/a = cuartos vacios)
// Entre pasillo y habitacion va una pared fina con las puertas de "Puertas"; los macizos son cajas con el papel tapiz.
// Se conserva: los materiales (BR_Pared, BR_Alfombra, techo y paneles de luz), el gato, el puzle de las palancas
// (tablero con los focos, camara, boton, portal), las pilas, la piedra bruja con sus marcas ocultas (mapa y X) y el
// cadaver en la sala de los focos, cerca del camino de siempre (pasillo recto desde la entrada).
// Se aplica sola una vez (borra las paredes, paneles y luces viejas); rehacer: Tools > Backrooms > Rehacer pasillos.
// Despues guarda la escena (Ctrl+S).
[InitializeOnLoad]
public static class BackroomsPasillosSetup
{
    const string Marca = "_pasillos v2";   // v2: sector de la entrada cerrado (1 palanca antes del gato) y occlusion rehorneado
    const int N = 24;
    const float Celda = 2.5f, Alto = 2.8f, Grosor = 0.2f, AnchoPuerta = 1.3f, AltoPuerta = 2.2f, Pilar = 0.5f;
    // Esquina noroeste del terreno (x minima, z maxima): j crece hacia el sur
    const float X0 = 70.9f, Z0 = -111.55f;
    // Hueco de la puerta de entrada en el muro norte (el pasillo de la estacion llega ahi)
    const float EntradaX0 = 98.85f, EntradaX1 = 99.95f, AltoEntrada = 2.1f;

    // ',' = pasillo del sector de la entrada: separado del resto ('.') por paredes; su unica salida es la sala del
    // cadaver (entra por la puerta este, sale por la oeste). Asi antes de la cinematica del gato solo se puede
    // encontrar UNA palanca (la del cuartito 'k') y la cinematica no se puede saltar.
    static readonly string[] Plano =
    {
        "###########,#######..###",
        "###########,#######.####",
        "........,,,,,,,.###.....",
        "##.####.,##,kk,.###.###.",
        "#######.,##,kk,.###.###.",
        "#######.,##,##,.###.#.#.",
        "........,,,,,,,.........",
        "aaa.###.,##,##,pppp.####",
        "aaa.###.,##,##,pppp.####",
        "aaa.###.,##,##,pppp.####",
        "........,,,,,,,.........",
        "###.###.ccc,###.ooo####.",
        "###..##.ccc,###.ooo####.",
        "###.###.ccc####.ooo####.",
        "....ppp.................",
        "ppp.ppp####.###.###.###.",
        "ppp.ppp####.###.###.##..",
        "ppp.ppp####.###.###.###.",
        "............###.........",
        "###.###.###.###.###.rrr.",
        "###.###.###.###.###.rrr.",
        "#.#.###.###.###.###.rrr.",
        "....###.................",
        "#######.#####.##########",
    };

    // Puertas: celda de un lado y celda del otro (i, j)
    static readonly int[][] Puertas =
    {
        new[] { 10, 12, 11, 12 }, new[] { 8, 13, 7, 13 },     // cadaver: entrada (pasillo principal) y salida trasera
        new[] { 21, 19, 21, 18 }, new[] { 20, 21, 19, 21 },   // reaparicion del gato
        new[] { 17, 11, 17, 10 }, new[] { 16, 13, 15, 13 },   // oficina
        new[] { 12, 4, 11, 4 },                               // cuartito
        new[] { 1, 9, 1, 10 }, new[] { 2, 8, 3, 8 },          // archivo
    };

    static readonly Color ColorLuz = new Color(1f, 0.95f, 0.78f);
    const float IntensidadLuz = 1.2f, AlcanceLuz = 9.5f, SeparacionLuces = 5.2f;

    static BackroomsPasillosSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject br = GameObject.Find("Backrooms");
        if (!br || !br.transform.Find("Inicio")) return;
        if (Hecho(br.transform))
        {
            // Ya armados: solo faltan las senales ocultas (sin tocar paredes, asi no hay que volver a hornear el occlusion)
            if (!br.transform.Find(NombreSenales) && br.transform.Find("Palancas"))
            {
                Senales(br.transform);
                EditorSceneManager.MarkSceneDirty(br.scene);
            }
            if (Ajustes(br.transform)) EditorSceneManager.MarkSceneDirty(br.scene);
            return;
        }
        Debug.Log("[Backrooms] Armando los Backrooms de pasillos (las paredes, paneles y luces viejas se borran; Ctrl+Z deshace).", br);
        Aplicar(br.transform);
    }

    internal static bool Hecho(Transform br)
    {
        Transform p = br.Find("Paredes");
        return p && p.Find(Marca);
    }

    [MenuItem("Tools/Backrooms/Rehacer pasillos")]
    static void Menu()
    {
        GameObject br = GameObject.Find("Backrooms");
        if (!br || !br.transform.Find("Inicio")) { EditorUtility.DisplayDialog("Backrooms", "No se encontro \"Backrooms/Inicio\".", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Backrooms", "Esto borra las paredes, paneles, luces y el puzle de los Backrooms y los arma de nuevo segun el plano. ¿Seguir?", "Rehacer", "Cancelar")) return;
        Aplicar(br.transform);
    }

    [MenuItem("Tools/Backrooms/Fotos de los pasillos")]
    static void MenuFotos() => Fotos();

    // ================================================================ plano

    static char T(int i, int j) => i < 0 || j < 0 || i >= N || j >= N ? '#' : Plano[j][i];
    static bool Libre(int i, int j) => T(i, j) != '#';
    static bool Abierto(char c) => c == '.' || c == 'p' || c == ',';

    // Se pasa de una celda a la vecina sin pared en medio (el sector de la entrada ',' solo se une consigo mismo)
    static bool Unidas(int i1, int j1, int i2, int j2)
    {
        char a = T(i1, j1), b = T(i2, j2);
        if (a == '#' || b == '#') return false;
        return a == b || ((a == '.' || a == 'p') && (b == '.' || b == 'p'));
    }

    static bool EsPuerta(int i1, int j1, int i2, int j2)
    {
        foreach (int[] p in Puertas)
            if ((p[0] == i1 && p[1] == j1 && p[2] == i2 && p[3] == j2) || (p[0] == i2 && p[1] == j2 && p[2] == i1 && p[3] == j1)) return true;
        return false;
    }

    internal static Vector3 Centro(float i, float j) => new Vector3(X0 + (i + 0.5f) * Celda, 0f, Z0 - (j + 0.5f) * Celda);
    static float BordeX(int i) => X0 + i * Celda;   // borde oeste de la columna i
    static float BordeZ(int j) => Z0 - j * Celda;   // borde norte de la fila j

    // ================================================================ armado

    static Transform br, paredes, paneles, luces;
    static Material mPared, mLuz;
    static Mesh cubo;

    static void Aplicar(Transform raiz)
    {
        br = raiz;
        Undo.SetCurrentGroupName("Backrooms pasillos");
        int grupo = Undo.GetCurrentGroup();

        mPared = AssetDatabase.LoadAssetAtPath<Material>("Assets/Backrooms/Texturas/BR_Pared.mat");
        mLuz = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BackroomsLuz.mat");
        if (!mPared) mPared = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BackroomsPared.mat");
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubo = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);

        // 1) fuera lo viejo (el piso, el techo, la puerta, la piedra, el gato y la piedra coleccionable se quedan)
        foreach (string n in new[] { "Paredes", "Paneles Luz", "Luces", "Indicaciones Ocultas", "Destino Indicaciones", "Palancas" })
        {
            Transform t = br.Find(n);
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }

        paredes = Grupo("Paredes");
        new GameObject(Marca).transform.SetParent(paredes, false);
        paneles = Grupo("Paneles Luz");
        luces = Grupo("Luces");

        // 2) geometria
        int nMacizos = Macizos();
        int nFinas = ParedesFinas();
        int nPilares = Pilares();
        var (nPaneles, nLuces) = Iluminacion();
        Physics.SyncTransforms();

        // 3) puzle (palancas, tablero, cadaver, piedra bruja, gato), pilas y piedra coleccionable
        RehacerPuzzle(br);
        Recolocar();
        Physics.SyncTransforms();

        // 4) NavMesh del gato
        var surface = br.GetComponent<NavMeshSurface>();
        if (surface) EnemigosZonasSetup.GuardarNavMesh(surface);
        else Debug.LogWarning("[Backrooms] \"Backrooms\" no tiene NavMeshSurface: Tools > Enemigos y Zonas > Hornear NavMesh.", br);

        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(br.gameObject.scene);

        // 5) Occlusion culling: el horneado viejo todavia tenia las paredes de antes y ocultaba (hacia "desaparecer")
        //    paredes nuevas al jugar. Se borra (asi se ve todo bien ya) y se vuelve a hornear en segundo plano
        StaticOcclusionCulling.Cancel();
        StaticOcclusionCulling.Clear();
        bool horneando = StaticOcclusionCulling.GenerateInBackground();
        Debug.Log(horneando
            ? "[Backrooms] Occlusion culling: borrado el horneado viejo y horneando de nuevo en segundo plano (Window > Rendering > Occlusion Culling). Cuando termine, guarda la escena."
            : "[Backrooms] Occlusion culling: borrado el horneado viejo (no se pudo rehornear solo: Window > Rendering > Occlusion Culling > Bake).", br);

        string resumen = $"{nMacizos} macizos, {nFinas} paredes finas, {nPilares} pilares, {nPaneles} paneles, {nLuces} luces";
        Debug.Log("[Backrooms] Pasillos listos: " + resumen + ". Guarda la escena (Ctrl+S).", paredes);
        Directory.CreateDirectory("Temp/Capturas");
        File.WriteAllText("Temp/Capturas/backrooms_pasillos_listo.txt", resumen + " " + System.DateTime.Now.ToString("HH:mm:ss"));
        Fotos();
    }

    static Transform Grupo(string nombre)
    {
        var g = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(g, "Backrooms");
        g.transform.SetParent(br, false);
        g.transform.position = br.position;
        g.isStatic = true;
        return g.transform;
    }

    // Caja (cubo escalado, rotacion de mundo 0) con el papel tapiz, UV en metros y colision
    static GameObject Caja(Transform padre, string nombre, Vector3 centro, Vector3 tam, Material m, bool colision = true)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(centro, Quaternion.identity);
        go.transform.localScale = tam;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = cubo;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = m;
        if (m == mPared)
        {
            Mesh uv = TexturasZonasSetup.MallaUVMetros(mf);
            if (uv) mf.sharedMesh = uv;
        }
        if (colision) go.AddComponent<BoxCollider>();
        go.isStatic = true;
        return go;
    }

    // ---------------------------------------------------------------- macizos: rectangulos de celdas '#'
    static int Macizos()
    {
        var hecho = new bool[N, N];
        int n = 0;
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                if (Libre(i, j) || hecho[i, j]) continue;
                int i1 = i;
                while (i1 + 1 < N && !Libre(i1 + 1, j) && !hecho[i1 + 1, j]) i1++;
                int j1 = j;
                while (j1 + 1 < N)
                {
                    bool fila = true;
                    for (int k = i; k <= i1; k++) if (Libre(k, j1 + 1) || hecho[k, j1 + 1]) { fila = false; break; }
                    if (!fila) break;
                    j1++;
                }
                for (int a = i; a <= i1; a++) for (int b = j; b <= j1; b++) hecho[a, b] = true;
                float x0 = BordeX(i), x1 = BordeX(i1 + 1), z0 = BordeZ(j), z1 = BordeZ(j1 + 1);
                Caja(paredes, "Macizo " + (++n), new Vector3((x0 + x1) * 0.5f, Alto * 0.5f, (z0 + z1) * 0.5f),
                     new Vector3(x1 - x0, Alto, z0 - z1), mPared);
            }
        return n;
    }

    // ---------------------------------------------------------------- paredes finas (borde, y entre pasillo y habitacion)
    static int nFinasCont;

    static int ParedesFinas()
    {
        nFinasCont = 0;
        // Lineas horizontales (bordes norte de cada fila): z = BordeZ(j)
        for (int j = 0; j <= N; j++)
        {
            var hay = new bool[N]; var puerta = new bool[N];
            for (int i = 0; i < N; i++)
            {
                if (j == 0 || j == N) { hay[i] = true; continue; }
                bool a = Libre(i, j - 1), b = Libre(i, j);
                hay[i] = a && b && !Unidas(i, j - 1, i, j);
                puerta[i] = hay[i] && EsPuerta(i, j - 1, i, j);
            }
            Linea(hay, puerta, true, BordeZ(j), j == 0);
        }
        // Lineas verticales (bordes oeste de cada columna): x = BordeX(i)
        for (int i = 0; i <= N; i++)
        {
            var hay = new bool[N]; var puerta = new bool[N];
            for (int j = 0; j < N; j++)
            {
                if (i == 0 || i == N) { hay[j] = true; continue; }
                bool a = Libre(i - 1, j), b = Libre(i, j);
                hay[j] = a && b && !Unidas(i - 1, j, i, j);
                puerta[j] = hay[j] && EsPuerta(i - 1, j, i, j);
            }
            Linea(hay, puerta, false, BordeX(i), false);
        }
        return nFinasCont;
    }

    // Una linea de bordes: tramos seguidos se unen en una sola pared, con huecos (y dintel) en las puertas
    static void Linea(bool[] hay, bool[] puerta, bool horizontal, float fijo, bool entrada)
    {
        for (int k = 0; k < N; k++)
        {
            if (!hay[k]) continue;
            int k1 = k;
            while (k1 + 1 < N && hay[k1 + 1]) k1++;
            // a lo largo de la linea: u (x si es horizontal, -z si es vertical)
            float u0 = k * Celda - Grosor * 0.5f, u1 = (k1 + 1) * Celda + Grosor * 0.5f;
            var huecos = new List<(float a, float b, float alto)>();
            for (int q = k; q <= k1; q++)
                if (puerta[q]) { float c = (q + 0.5f) * Celda; huecos.Add((c - AnchoPuerta * 0.5f, c + AnchoPuerta * 0.5f, AltoPuerta)); }
            if (entrada) huecos.Add((EntradaX0 - X0, EntradaX1 - X0, AltoEntrada));
            huecos.Sort((x, y) => x.a.CompareTo(y.a));
            float desde = u0;
            foreach (var h in huecos)
            {
                if (h.a > desde + 0.01f) Fina(horizontal, fijo, desde, h.a, 0f, Alto);
                Fina(horizontal, fijo, h.a, h.b, h.alto, Alto);   // dintel
                desde = h.b;
            }
            if (u1 > desde + 0.01f) Fina(horizontal, fijo, desde, u1, 0f, Alto);
            k = k1;
        }
    }

    static void Fina(bool horizontal, float fijo, float u0, float u1, float y0, float y1)
    {
        if (y1 - y0 < 0.01f || u1 - u0 < 0.01f) return;
        float um = (u0 + u1) * 0.5f, largo = u1 - u0;
        Vector3 c = horizontal ? new Vector3(X0 + um, (y0 + y1) * 0.5f, fijo) : new Vector3(fijo, (y0 + y1) * 0.5f, Z0 - um);
        Vector3 tam = horizontal ? new Vector3(largo, y1 - y0, Grosor) : new Vector3(Grosor, y1 - y0, largo);
        Caja(paredes, y0 > 0.01f ? "Dintel" : "Pared", c, tam, mPared);
        nFinasCont++;
    }

    // ---------------------------------------------------------------- pilares en las zonas abiertas
    static int Pilares()
    {
        int n = 0;
        for (int j = 1; j < N; j++)
            for (int i = 1; i < N; i++)
            {
                // vertice (i, j): esquina comun de (i-1,j-1), (i,j-1), (i-1,j), (i,j)
                char a = T(i - 1, j - 1), b = T(i, j - 1), c = T(i - 1, j), d = T(i, j);
                if (!Abierto(a) || !Abierto(b) || !Abierto(c) || !Abierto(d)) continue;
                int p = (a == 'p' ? 1 : 0) + (b == 'p' ? 1 : 0) + (c == 'p' ? 1 : 0) + (d == 'p' ? 1 : 0);
                if (p < 3) continue;
                Caja(paredes, "Pilar " + (++n), new Vector3(BordeX(i), Alto * 0.5f, BordeZ(j)), new Vector3(Pilar, Alto, Pilar), mPared);
            }
        return n;
    }

    // ---------------------------------------------------------------- paneles y luces
    static (int, int) Iluminacion()
    {
        var rnd = new System.Random(24);
        var celdasPanel = new List<(int i, int j, Renderer r)>();
        int nPan = 0;
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                if (!Libre(i, j)) continue;
                char t = T(i, j);
                bool sala = !Abierto(t);
                // Algunos paneles faltan (zonas mas oscuras), nunca en las habitaciones ni en la entrada
                if (!sala && !(i == 11 && j < 3) && rnd.NextDouble() < 0.09) continue;
                bool ew = Unidas(i, j, i - 1, j) || Unidas(i, j, i + 1, j);
                bool ns = Unidas(i, j, i, j - 1) || Unidas(i, j, i, j + 1);
                bool largoEnX = ew && !ns ? true : (ns && !ew ? false : ((i + j) % 2 == 0));
                Vector3 c = Centro(i, j);
                var go = Caja(paneles, "Panel " + (++nPan), new Vector3(c.x, 2.78f, c.z),
                              largoEnX ? new Vector3(1.2f, 0.04f, 0.6f) : new Vector3(0.6f, 0.04f, 1.2f), mLuz, false);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                celdasPanel.Add((i, j, go.GetComponent<Renderer>()));
            }

        // Luces: repartidas (una cada ~5 m) sobre paneles; primero las de las habitaciones
        celdasPanel.Sort((x, y) => (Abierto(T(x.i, x.j)) ? 1 : 0).CompareTo(Abierto(T(y.i, y.j)) ? 1 : 0));
        var puestas = new List<Vector3>();
        int nLuz = 0;
        Color emision = mLuz && mLuz.HasProperty("_EmissionColor") ? mLuz.GetColor("_EmissionColor") : new Color(2.2f, 1.7f, 1f);
        // Las que fallan (zumbido y parpadeo): unas pocas, lejos de la entrada
        var fallan = new HashSet<(int, int)> { (11, 9), (7, 14), (3, 18), (17, 8), (23, 12), (15, 22), (19, 18), (2, 6) };
        foreach (var (i, j, r) in celdasPanel)
        {
            Vector3 c = Centro(i, j);
            bool cerca = false;
            foreach (Vector3 p in puestas) if ((p - c).sqrMagnitude < SeparacionLuces * SeparacionLuces) { cerca = true; break; }
            if (cerca && !fallan.Contains((i, j))) continue;
            puestas.Add(c);
            var lg = new GameObject("Luz " + (++nLuz));
            lg.transform.SetParent(luces, false);
            lg.transform.SetPositionAndRotation(new Vector3(c.x, 2.65f, c.z), Quaternion.identity);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = ColorLuz;
            l.intensity = IntensidadLuz;
            l.range = AlcanceLuz;
            l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            if (fallan.Contains((i, j)))
            {
                var f = lg.AddComponent<LuzParpadeante>();
                f.foco = r;
                f.brilloFoco = emision;
                f.temblor = 0.05f;
                f.entreFallas = new Vector2(3f, 10f);
                f.radioSonido = 7f;
                f.volumenFondo = 0.08f;
                lg.name += " (falla)";
            }
        }
        return (nPan, nLuz);
    }

    // ================================================================ puzle

    // La sala de los focos ('c': columnas 8-10, filas 11-13). Pasillo principal al este (columna 11), salida trasera al oeste
    internal static void RehacerPuzzle(Transform raiz)
    {
        float xO = BordeX(8), xE = BordeX(11), zN = BordeZ(11), zS = BordeZ(14);
        float g = Grosor * 0.5f + 0.001f;
        var plan = new BackroomsPalancasSetup.Plan
        {
            // Tablero en la pared norte (la del pasillo), mirando al sur
            tablero = BackroomsPalancasSetup.ParedEn(new Vector3(Centro(9, 11).x, 0f, zN - g), Vector3.back),
            // Primera palanca: pared oeste, fila del medio
            primera = BackroomsPalancasSetup.ParedEn(new Vector3(xO + g, 0f, Centro(8, 11).z), Vector3.right),
            // Mapa oculto: pared sur, del lado oeste (la camara queda del lado este)
            mapa = BackroomsPalancasSetup.ParedEn(new Vector3(Centro(8, 13).x + 0.3f, 0f, zS + g), Vector3.forward),
            cuarto = new Bounds(new Vector3((xO + xE) * 0.5f, 1.4f, (zN + zS) * 0.5f), new Vector3(xE - xO - Grosor, 2.8f, zN - zS - Grosor)),
        };
        // Otras palancas. Antes del gato solo se llega a una: la del cuartito 'k' del sector de la entrada (en la pared
        // de enfrente de su puerta). Las otras dos, del otro lado de la sala del cadaver: recoveco del noreste y sala de
        // pilares del oeste
        plan.otras.Add(BackroomsPalancasSetup.ParedEn(new Vector3(BordeX(14) - g, 0f, BordeZ(4)), Vector3.left));
        plan.otras.Add(BackroomsPalancasSetup.ParedEn(new Vector3(Centro(21, 5).x, 0f, BordeZ(5)), Vector3.back));
        plan.otras.Add(BackroomsPalancasSetup.ParedEn(new Vector3(X0 + g, 0f, Centro(0, 16).z), Vector3.right));

        // El gato: sentado al lado del cadaver (del lado de la puerta), de tres cuartos hacia la entrada de la sala
        Vector3 cadaver = plan.tablero.punto + Vector3.back * 2.4f;
        plan.puntoGato = cadaver + new Vector3(0.35f, 0f, -0.85f);
        plan.giroGato = 120f;
        // Se va por la puerta trasera y dobla hacia el sur (fuera de la vista desde la entrada de la sala)
        plan.salidaGato = Centro(7, 14) + new Vector3(0f, 0f, -0.6f);
        // Si el jugador llega por la puerta trasera: sale por la de enfrente y dobla hacia el norte
        plan.salidaGatoAlterna = Centro(11, 10) + new Vector3(0f, 0f, 0.6f);
        // Reaparece en la habitacion 'r' (sureste), la mas lejos caminando
        plan.reaparicionGato = Centro(21, 20);

        BackroomsPalancasSetup.AplicarPlan(raiz, plan);
        Senales(raiz);
        Ajustes(raiz);
    }

    // ================================================================ sonidos y puerta de la sala (solo lo que falte)

    const string CarpetaSfx = "Assets/Sounds/SFX/";

    internal static bool Ajustes(Transform raiz)
    {
        bool cambio = false;
        AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>(CarpetaSfx + n);
        BackroomsGuion guion = raiz.GetComponentInChildren<BackroomsGuion>(true);
        if (guion)
        {
            Undo.RecordObject(guion, "Backrooms sonidos");
            if (!guion.sonidoGlitch && (guion.sonidoGlitch = Clip("glitch puerta.wav"))) cambio = true;
            if (!guion.suspiro && (guion.suspiro = Clip("suspiro rapido.wav"))) cambio = true;
            if (!guion.sonidoKill && (guion.sonidoKill = Clip("sonido kill enemigo 2 y 4.mp3")))
            {
                guion.inicioKill = Inicio(guion.sonidoKill);
                cambio = true;
            }
            if (!guion.puertaSala)
            {
                // Puerta este de la sala del cadaver (la del pasillo de la entrada), mirando hacia dentro
                var p = new GameObject("Puerta Sala").transform;
                Undo.RegisterCreatedObjectUndo(p.gameObject, "Puerta Sala");
                p.SetParent(guion.transform, false);
                p.SetPositionAndRotation(new Vector3(BordeX(11), 0f, Centro(10, 12).z), Quaternion.LookRotation(Vector3.left));
                guion.puertaSala = p;
                guion.anchoPuertaSala = AnchoPuerta;
                cambio = true;
            }
            if (cambio) EditorUtility.SetDirty(guion);
        }
        PuzzlePalancas puzzle = raiz.GetComponentInChildren<PuzzlePalancas>(true);
        if (puzzle && !puzzle.sonidoCamaras)
        {
            Undo.RecordObject(puzzle, "Backrooms sonidos");
            puzzle.sonidoCamaras = Clip("sonido camaras.mp3");
            if (puzzle.sonidoCamaras)
            {
                puzzle.inicioSonidoCamaras = Inicio(puzzle.sonidoCamaras);
                EditorUtility.SetDirty(puzzle);
                cambio = true;
            }
        }
        if (cambio)
            Debug.Log($"[Backrooms] Sonidos: glitch de la puerta, suspiro, kill del gato (desde {(guion ? guion.inicioKill : 0f):0.00} s) y camaras " +
                      $"(desde {(puzzle ? puzzle.inicioSonidoCamaras : 0f):0.00} s). Puerta de la sala para asomarse. Guarda la escena.", raiz);
        return cambio;
    }

    // Donde empieza a sonar de verdad (salta el silencio del principio): la primera ventana de 20 ms con mas del 12%
    // del volumen maximo (mirando los primeros 20 s)
    static float Inicio(AudioClip c)
    {
        if (!c) return 0f;
        try
        {
            if (c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
            int canales = Mathf.Max(1, c.channels);
            int n = Mathf.Min(c.samples, c.frequency * 20);
            var d = new float[n * canales];
            if (!c.GetData(d, 0)) return 0f;
            int ventana = Mathf.Max(1, c.frequency / 50);
            var rms = new List<float>();
            for (int w = 0; w + ventana <= n; w += ventana)
            {
                double s = 0;
                for (int i = w * canales; i < (w + ventana) * canales; i++) s += d[i] * d[i];
                rms.Add((float)System.Math.Sqrt(s / (ventana * canales)));
            }
            float pico = 0f;
            foreach (float r in rms) pico = Mathf.Max(pico, r);
            if (pico < 1e-4f) return 0f;
            for (int k = 0; k < rms.Count; k++)
                if (rms[k] > pico * 0.12f) return Mathf.Max(0f, k * ventana / (float)c.frequency - 0.03f);
        }
        catch (System.Exception e) { Debug.LogWarning("[Backrooms] No pude leer " + c.name + ": " + e.Message); }
        return 0f;
    }

    // ================================================================ senales ocultas (solo se ven con la piedra bruja)

    const string NombreSenales = "Senales Ocultas";
    const string CarpetaSenales = "Assets/Backrooms/Senales";
    // Un color por rastro (y la X de su palanca del mismo color)
    static readonly Color[] ColoresRastro =
    {
        new Color(0.35f, 1.6f, 1.25f, 0.95f),   // turquesa: palanca 2 (cuartito de la entrada)
        new Color(1.7f, 1.15f, 0.25f, 0.95f),   // ambar: palanca 3 (noreste)
        new Color(1.3f, 0.45f, 1.8f, 0.95f),    // violeta: palanca 4 (pilares del oeste)
    };
    static readonly Color ColorPeligro = new Color(1.8f, 0.22f, 0.18f, 0.95f);

    // - Rastros de flechas desde la sala del cadaver hasta cada palanca (en las esquinas y cruces); cada rastro se
    //   apaga cuando su palanca ya esta bajada.
    // - Huellas del gato desde la puerta trasera de la sala del cadaver hasta su guarida ('r').
    // - Un ojo pintado sobre cada puerta de la guarida (advertencia).
    internal static void Senales(Transform raiz)
    {
        Transform viejo = raiz.Find(NombreSenales);
        if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);
        Material flecha = AssetDatabase.LoadAssetAtPath<Material>("Assets/Backrooms/Vista/PinturaFlecha.mat");
        if (!flecha) { Debug.LogWarning("[Backrooms] No encuentro Assets/Backrooms/Vista/PinturaFlecha.mat: sin senales ocultas."); return; }
        Directory.CreateDirectory(CarpetaSenales);
        var g = new GameObject(NombreSenales);
        Undo.RegisterCreatedObjectUndo(g, "Senales ocultas");
        g.transform.SetParent(raiz, false);
        g.transform.position = raiz.position;
        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var rnd = new System.Random(31);

        Transform palancas = raiz.Find("Palancas");
        Material equis = AssetDatabase.LoadAssetAtPath<Material>("Assets/Backrooms/Palancas/PinturaMarca X.mat");
        var rastros = new[]
        {
            (n: 2, desde: new Vector2Int(11, 12), hasta: new Vector2Int(12, 4)),
            (n: 3, desde: new Vector2Int(7, 13), hasta: new Vector2Int(21, 5)),
            (n: 4, desde: new Vector2Int(7, 13), hasta: new Vector2Int(0, 16)),
        };
        int nFlechas = 0;
        for (int k = 0; k < rastros.Length; k++)
        {
            var (n, desde, hasta) = rastros[k];
            Palanca palanca = palancas ? palancas.Find("Palanca " + n)?.GetComponent<Palanca>() : null;
            Material mat = Pintura("Flecha Palanca " + n, flecha, null, ColoresRastro[k]);
            // La X oculta sobre la palanca, del color de su rastro
            Transform x = palancas ? palancas.Find("Marca Palanca " + n) : null;
            if (x && equis && x.TryGetComponent(out Renderer rx))
            {
                Undo.RecordObject(rx, "Senales ocultas");
                rx.sharedMaterial = Pintura("Marca X Palanca " + n, equis, null, ColoresRastro[k]);
            }
            List<Vector2Int> camino = Camino(desde, hasta);
            if (camino == null) { Debug.LogWarning($"[Backrooms] No hay camino para el rastro de la palanca {n}."); continue; }
            var t = new GameObject("Rastro Palanca " + n).transform;
            t.SetParent(g.transform, false);
            nFlechas += Flechas(t, quad, mat, camino, palanca, k, rnd);
        }

        // Huellas del gato: hasta su guarida
        Material huella = Pintura("Huella", flecha, Textura("Huella", 128, 128, PixelHuella), ColorPeligro);
        List<Vector2Int> ida = Camino(new Vector2Int(7, 13), new Vector2Int(21, 20));
        int nHuellas = 0;
        if (ida != null)
        {
            var t = new GameObject("Huellas del gato").transform;
            t.SetParent(g.transform, false);
            float paso = 0.8f, resto = 0.4f;
            bool izq = false;
            for (int k = 0; k + 1 < ida.Count; k++)
            {
                Vector3 a = Centro(ida[k].x, ida[k].y), b = Centro(ida[k + 1].x, ida[k + 1].y);
                Vector3 dir = (b - a).normalized, lado = Vector3.Cross(Vector3.up, dir);
                for (float s = resto; s < Celda; s += paso)
                {
                    izq = !izq;
                    Vector3 p = a + dir * s + lado * (izq ? 0.09f : -0.09f);
                    Vector3 giro = Quaternion.Euler(0f, (float)(rnd.NextDouble() * 16 - 8), 0f) * dir;
                    Pintar(t, quad, huella, "Huella", p, Vector3.up, giro, 0.17f, 0.17f, null, rnd);
                    nHuellas++;
                    resto = s + paso - Celda;
                }
            }
        }

        // Ojo (de gato) sobre cada puerta de la guarida, por fuera
        Material ojo = Pintura("Ojo", flecha, Textura("Ojo", 256, 128, PixelOjo), ColorPeligro);
        foreach (int[] p in Puertas)
        {
            bool dentroA = T(p[0], p[1]) == 'r', dentroB = T(p[2], p[3]) == 'r';
            if (dentroA == dentroB) continue;
            Vector2Int de = dentroA ? new Vector2Int(p[0], p[1]) : new Vector2Int(p[2], p[3]);
            Vector2Int fuera = dentroA ? new Vector2Int(p[2], p[3]) : new Vector2Int(p[0], p[1]);
            Vector3 n = W(fuera - de);
            Vector3 punto = Centro(de.x, de.y) + n * (Celda * 0.5f + Grosor * 0.5f) + Vector3.up * ((AltoPuerta + Alto) * 0.5f);
            Pintar(g.transform, quad, ojo, "Ojo (peligro)", punto, n, Vector3.Cross(n, Vector3.up), 0.56f, 0.28f, null, rnd, false);
        }
        Debug.Log($"[Backrooms] Senales ocultas (con la piedra bruja): {nFlechas} flechas en 3 rastros de colores hasta las palancas, " +
                  $"{nHuellas} huellas del gato hasta su guarida y ojos sobre sus puertas.", g);
    }

    static Vector3 W(Vector2Int d) => new Vector3(d.x, 0f, -d.y);   // direccion de la cuadricula -> mundo (j crece hacia el sur)

    static bool Pasa(Vector2Int a, Vector2Int b) =>
        Libre(a.x, a.y) && Libre(b.x, b.y) && (Unidas(a.x, a.y, b.x, b.y) || EsPuerta(a.x, a.y, b.x, b.y));

    static readonly Vector2Int[] Dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

    static List<Vector2Int> Camino(Vector2Int desde, Vector2Int hasta)
    {
        var previa = new Dictionary<Vector2Int, Vector2Int> { [desde] = desde };
        var cola = new Queue<Vector2Int>();
        cola.Enqueue(desde);
        while (cola.Count > 0)
        {
            Vector2Int c = cola.Dequeue();
            if (c == hasta) break;
            foreach (Vector2Int d in Dirs)
            {
                Vector2Int v = c + d;
                if (previa.ContainsKey(v) || !Pasa(c, v)) continue;
                previa[v] = c;
                cola.Enqueue(v);
            }
        }
        if (!previa.ContainsKey(hasta)) return null;
        var res = new List<Vector2Int>();
        for (Vector2Int c = hasta; ; c = previa[c]) { res.Add(c); if (c == desde) break; }
        res.Reverse();
        return res;
    }

    static int Salidas(Vector2Int c)
    {
        int n = 0;
        foreach (Vector2Int d in Dirs) if (Pasa(c, c + d)) n++;
        return n;
    }

    // Flechas en el arranque, en cada giro y en cada cruce: en la pared de enfrente (si gira y hay pared), en una pared
    // de al lado (si sigue de largo) o en el piso. Las de rastros distintos en el mismo sitio se apilan (capa)
    static int Flechas(Transform padre, Mesh quad, Material mat, List<Vector2Int> c, Palanca palanca, int capa, System.Random rnd)
    {
        int n = 0;
        for (int k = 0; k + 1 < c.Count; k++)
        {
            Vector2Int a = k == 0 ? c[1] - c[0] : c[k] - c[k - 1];
            Vector2Int b = c[k + 1] - c[k];
            if (k > 0 && a == b && Salidas(c[k]) < 3) continue;   // tramo recto sin cruce: nada
            Vector2Int lado;
            Vector2Int apunta = b;
            if (a != b) lado = !Pasa(c[k], c[k] + a) ? a : Vector2Int.zero;
            else
            {
                Vector2Int s = new Vector2Int(-a.y, a.x);
                lado = !Pasa(c[k], c[k] + s) ? s : (!Pasa(c[k], c[k] - s) ? -s : Vector2Int.zero);
            }
            Vector3 centro = Centro(c[k].x, c[k].y);
            if (lado != Vector2Int.zero)
            {
                Vector2Int v = c[k] + lado;
                bool fina = v.x < 0 || v.y < 0 || v.x >= N || v.y >= N || Libre(v.x, v.y);
                float dist = Celda * 0.5f - (fina ? Grosor * 0.5f : 0f);
                Vector3 punto = centro + W(lado) * dist + Vector3.up * (1.25f + 0.38f * capa);
                Pintar(padre, quad, mat, "Flecha", punto, -W(lado), W(apunta), 0.8f, 0.4f, palanca, rnd);
            }
            else
            {
                Vector3 perp = Vector3.Cross(Vector3.up, W(apunta));
                Pintar(padre, quad, mat, "Flecha (piso)", centro + perp * ((capa - 1) * 0.55f), Vector3.up, W(apunta), 0.95f, 0.48f, palanca, rnd);
            }
            n++;
        }
        return n;
    }

    // Quad pintado (MarcaOculta) sobre una superficie de normal "normal", con su +X (la punta) hacia "dir"
    static void Pintar(Transform padre, Mesh quad, Material mat, string nombre, Vector3 punto, Vector3 normal, Vector3 dir,
                       float ancho, float alto, Palanca palanca, System.Random rnd, bool torcer = true)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        Vector3 frente = -normal;   // el Quad se ve desde su -Z
        go.transform.SetPositionAndRotation(punto + normal * 0.016f, Quaternion.LookRotation(frente, Vector3.Cross(frente, dir)));
        if (torcer) go.transform.Rotate(0f, 0f, (float)(rnd.NextDouble() * 10 - 5), Space.Self);   // pintado a mano
        go.transform.localScale = new Vector3(ancho, alto, 1f);
        go.AddComponent<MarcaOculta>().seApagaCon = palanca;
    }

    // Copia de la pintura de las indicaciones (sin luz, transparente) con otro color y, si se da, otra imagen
    static Material Pintura(string nombre, Material baseMat, Texture2D tex, Color color)
    {
        string ruta = CarpetaSenales + "/Pintura" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (!m) { m = new Material(baseMat); AssetDatabase.CreateAsset(m, ruta); }
        if (tex) { m.SetTexture("_BaseMap", tex); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex); }
        m.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(m);
        return m;
    }

    delegate float Pixel(float u, float v);

    static Texture2D Textura(string nombre, int w, int h, Pixel alfa)
    {
        string ruta = CarpetaSenales + "/" + nombre + ".png";
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alfa((x + 0.5f) / w, (y + 0.5f) / h)) * 255f));
        t.SetPixels32(px);
        t.Apply();
        File.WriteAllBytes(ruta, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(ruta);
        if (AssetImporter.GetAtPath(ruta) is TextureImporter ti)
        {
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    static float Elipse(float u, float v, float cu, float cv, float ru, float rv)
    {
        float du = (u - cu) / ru, dv = (v - cv) / rv;
        return du * du + dv * dv <= 1f ? 1f : 0f;
    }

    // Huella de gato con los dedos hacia +X (la punta, como las flechas)
    static float PixelHuella(float u, float v)
    {
        float a = Elipse(u, v, 0.36f, 0.5f, 0.2f, 0.25f);
        a = Mathf.Max(a, Elipse(u, v, 0.66f, 0.2f, 0.085f, 0.1f));
        a = Mathf.Max(a, Elipse(u, v, 0.79f, 0.39f, 0.09f, 0.1f));
        a = Mathf.Max(a, Elipse(u, v, 0.79f, 0.61f, 0.09f, 0.1f));
        a = Mathf.Max(a, Elipse(u, v, 0.66f, 0.8f, 0.085f, 0.1f));
        return a * (0.75f + 0.25f * Mathf.PerlinNoise(u * 18f, v * 18f));
    }

    // Ojo de gato: contorno de almendra y pupila en ranura
    static float PixelOjo(float u, float v)
    {
        float x = u * 2f - 1f, y = v * 2f - 1f;
        float borde = 0.9f * (1f - x * x);
        if (Mathf.Abs(y) > borde) return 0f;
        bool contorno = Mathf.Abs(y) > borde - 0.2f || Mathf.Abs(x) > 0.93f;
        bool pupila = Mathf.Abs(x) < 0.11f * (1f - y * y * 0.6f);
        return contorno || pupila ? 0.85f + 0.15f * Mathf.PerlinNoise(u * 30f, v * 15f) : 0.08f;
    }

    // Pilas (Menu y Progreso/Baterias/Bateria Backrooms N) y la piedra coleccionable, en recovecos
    static void Recolocar()
    {
        var sitios = new[] { Centro(20, 0), Centro(2, 3), Centro(22, 16) };
        GameObject bats = GameObject.Find("Menu y Progreso/Baterias");
        int k = 0;
        if (bats)
            foreach (Transform t in bats.transform)
            {
                if (!t.name.StartsWith("Bateria Backrooms") || k >= sitios.Length) continue;
                Undo.RecordObject(t, "Backrooms pilas");
                t.position = new Vector3(sitios[k].x, t.position.y, sitios[k].z);
                k++;
            }
        if (k < sitios.Length) Debug.LogWarning("[Backrooms] Solo encontre " + k + " pilas de los Backrooms (Menu y Progreso/Baterias).");

        Transform gema = br.Find("Piedra Coleccionable (Backrooms)");
        if (gema)
        {
            Undo.RecordObject(gema, "Backrooms gema");
            Vector3 c = Centro(1, 21);
            gema.position = new Vector3(c.x, gema.position.y, c.z - 0.6f);
        }

        // El gato ya sentado al lado del cadaver tambien en el editor (al jugar lo pone ahi BackroomsGuion)
        BackroomsGuion guion = br.GetComponentInChildren<BackroomsGuion>(true);
        if (guion && guion.gato && guion.puntoGato)
        {
            var agente = guion.gato.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Undo.RecordObject(guion.gato.transform, "Backrooms gato");
            guion.gato.transform.SetPositionAndRotation(guion.puntoGato.position + Vector3.up * (agente ? agente.baseOffset : 0f),
                                                        Quaternion.Euler(0f, guion.puntoGato.eulerAngles.y, 0f));
        }
    }

    // ================================================================ revision

    // Planta (sin techo) y unas fotos a la altura de los ojos: Temp/Capturas/backrooms_*.png (no guarda nada)
    static void Fotos()
    {
        GameObject g = GameObject.Find("Backrooms");
        if (!g) return;
        Directory.CreateDirectory("Temp/Capturas");
        Transform techo = g.transform.Find("Techo 0");
        Renderer rt = techo ? techo.GetComponent<Renderer>() : null;
        bool antes = rt && rt.enabled;
        if (rt) rt.enabled = false;
        var go = new GameObject("_camPlanta") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 31f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 100f;
        go.transform.SetPositionAndRotation(new Vector3(X0 + 30f, 40f, Z0 - 30f), Quaternion.Euler(90f, 0f, 0f));
        var luzGo = new GameObject("_luzPlanta") { hideFlags = HideFlags.HideAndDontSave };
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Directional; luz.intensity = 1.2f;
        luzGo.transform.rotation = Quaternion.Euler(70f, 30f, 0f);
        Guardar(cam, "backrooms_planta", 1400, 1400);
        Object.DestroyImmediate(luzGo);
        Object.DestroyImmediate(go);
        if (rt) rt.enabled = antes;

        // A la altura de los ojos, con la iluminacion real
        Vector3 ojos = Vector3.up * 1.6f;
        CapturasEditor.Capturar("backrooms_entrada_real", Centro(11, 0) + ojos, Centro(11, 8) + Vector3.up * 1.2f, 70f);
        CapturasEditor.Capturar("backrooms_puerta_cadaver_real", Centro(11, 12) + ojos + Vector3.right * 0.6f, Centro(9, 12) + Vector3.up * 0.4f, 70f);
        CapturasEditor.Capturar("backrooms_pilares_real", Centro(3, 14) + ojos, Centro(1, 17) + Vector3.up * 1.2f, 75f);
        CapturasEditor.Capturar("backrooms_este_real", Centro(23, 2) + ojos, Centro(23, 14) + Vector3.up * 1.2f, 70f);
    }

    static void Guardar(Camera cam, string nombre, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24) { antiAliasing = 2 };
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var t = new Texture2D(w, h, TextureFormat.RGB24, false);
        t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        t.Apply();
        File.WriteAllBytes("Temp/Capturas/" + nombre + ".png", t.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(t);
    }
}
