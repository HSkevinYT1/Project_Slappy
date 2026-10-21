using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Genera un blocking estilo Backrooms: la zona se parte en habitaciones (BSP) conectadas por puertas,
// con algunas salas grandes con pilares, techo bajo con paneles de luz y una puerta de entrada.
// Menu: Tools > Backrooms > Construir. Cambia Semilla para obtener otra distribucion.
[InitializeOnLoad]
public static class BackroomsBuilder
{
    const int Semilla = 1987;
    const int Celdas = 20;                 // celdas por lado
    const float TamCelda = 3f;             // metros por celda -> zona de 60 x 60 m
    const float AltoPared = 2.8f;          // techo bajo, personaje de 1.75 m
    const float GrosorPared = 0.2f;
    const float AltoPuerta = 2.1f, AnchoPuerta = 1.1f;
    const int MinHabitacion = 2, MaxHabitacion = 5;   // en celdas
    const float ProbPuertaExtra = 0.3f;    // puertas de mas entre habitaciones vecinas (crea vueltas)
    const float ProbAbertura = 0.15f;      // conexiones sin pared en vez de puerta
    const float ProbSalaPilares = 0.3f;    // habitaciones grandes que quedan abiertas con pilares
    const float ProbPanel = 0.85f;         // paneles de luz que faltan, como en los Backrooms
    static readonly Vector3 SeparacionDelLaberinto = new Vector3(0, 0, -100);
    const string FlagPath = "Library/ConstruirBackrooms.flag";

    struct Hab { public int x, z, w, d; }

    static BackroomsBuilder()
    {
        if (System.IO.File.Exists(FlagPath))
        {
            System.IO.File.Delete(FlagPath);
            EditorApplication.delayCall += Construir;
        }
    }

    [MenuItem("Tools/Backrooms/Construir")]
    public static void Construir()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var viejo = GameObject.Find("Backrooms");
        if (viejo != null) Undo.DestroyObjectImmediate(viejo);

        var root = new GameObject("Backrooms");
        Undo.RegisterCreatedObjectUndo(root, "Construir Backrooms");
        var laberinto = GameObject.Find("Laberinto");
        root.transform.position = (laberinto != null ? laberinto.transform.position : Vector3.zero) + SeparacionDelLaberinto;

        var rnd = new System.Random(Semilla);
        int n = Celdas;

        // Habitaciones por particion binaria del espacio.
        var habs = new List<Hab>();
        Partir(rnd, new Hab { x = 0, z = 0, w = n, d = n }, habs);
        var id = new int[n, n];
        for (int k = 0; k < habs.Count; k++)
            for (int x = habs[k].x; x < habs[k].x + habs[k].w; x++)
                for (int z = habs[k].z; z < habs[k].z + habs[k].d; z++) id[x, z] = k;

        // h[x, z]: pared sobre la linea z entre los nodos x y x+1. v[x, z]: pared sobre la linea x entre z y z+1.
        var h = new bool[n, n + 1];
        var v = new bool[n + 1, n];
        var ph = new bool[n, n + 1];
        var pv = new bool[n + 1, n];
        var vecinos = new Dictionary<(int, int), List<(bool hor, int x, int z)>>();
        for (int x = 0; x < n; x++)
            for (int z = 0; z <= n; z++)
            {
                h[x, z] = z == 0 || z == n || id[x, z - 1] != id[x, z];
                if (h[x, z] && z > 0 && z < n) Vecino(vecinos, id[x, z - 1], id[x, z], (true, x, z));
            }
        for (int x = 0; x <= n; x++)
            for (int z = 0; z < n; z++)
            {
                v[x, z] = x == 0 || x == n || id[x - 1, z] != id[x, z];
                if (v[x, z] && x > 0 && x < n) Vecino(vecinos, id[x - 1, z], id[x, z], (false, x, z));
            }

        // Arbol de expansion (Kruskal) para que todas las habitaciones se conecten, mas puertas extra.
        var padre = new int[habs.Count];
        for (int k = 0; k < padre.Length; k++) padre[k] = k;
        var pares = new List<(int, int)>(vecinos.Keys);
        Barajar(rnd, pares);
        foreach (var par in pares)
        {
            int ra = Raiz(padre, par.Item1), rb = Raiz(padre, par.Item2);
            bool necesaria = ra != rb;
            if (!necesaria && rnd.NextDouble() > ProbPuertaExtra) continue;
            padre[ra] = rb;
            var bordes = vecinos[par];
            var e = bordes[rnd.Next(bordes.Count)];
            bool abierta = rnd.NextDouble() < ProbAbertura;
            if (e.hor) { if (abierta) h[e.x, e.z] = false; else ph[e.x, e.z] = true; }
            else { if (abierta) v[e.x, e.z] = false; else pv[e.x, e.z] = true; }
        }

        // Entrada: puerta en el centro de la pared sur.
        int ex = n / 2;
        ph[ex, 0] = true;

        // Algunas habitaciones grandes se llenan de pilares, como salas abiertas.
        var pilares = new List<Vector3>();
        foreach (var hb in habs)
        {
            if (hb.w < 4 || hb.d < 4 || rnd.NextDouble() > ProbSalaPilares) continue;
            for (int x = hb.x + 1; x < hb.x + hb.w; x += 2)
                for (int z = hb.z + 1; z < hb.z + hb.d; z += 2)
                    pilares.Add(new Vector3(Nodo(x), AltoPared / 2, Nodo(z)));
        }

        // Paneles de luz en cada celda; cada habitacion tiene al menos una luz real justo bajo un panel.
        var panel = new bool[n, n];
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++) panel[x, z] = rnd.NextDouble() < ProbPanel;
        var luces = new List<Vector3>();
        var rangos = new List<float>();
        foreach (var hb in habs)
        {
            int kx = Mathf.Max(1, Mathf.RoundToInt(hb.w / 3f)), kz = Mathf.Max(1, Mathf.RoundToInt(hb.d / 3f));
            for (int i = 0; i < kx; i++)
                for (int j = 0; j < kz; j++)
                {
                    int cx = hb.x + (int)((i + 0.5f) * hb.w / kx), cz = hb.z + (int)((j + 0.5f) * hb.d / kz);
                    panel[cx, cz] = true;
                    luces.Add(CentroCelda(cx, cz, AltoPared - 0.15f));
                    rangos.Add(TamCelda * Mathf.Max(hb.w / (float)kx, hb.d / (float)kz) + 2f);
                }
        }

        var matPared = BlockingUtils.CrearMaterial("BackroomsPared", new Color(0.79f, 0.72f, 0.42f), false);
        var matPiso = BlockingUtils.CrearMaterial("BackroomsAlfombra", new Color(0.55f, 0.5f, 0.3f), false);
        var matTecho = BlockingUtils.CrearMaterial("BackroomsTecho", new Color(0.85f, 0.82f, 0.68f), false);
        var matLuz = BlockingUtils.CrearMaterial("BackroomsLuz", new Color(1f, 0.98f, 0.88f), true);
        var matPuerta = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DoorMaterial.mat");

        float tam = n * TamCelda;
        var paredes = new List<Bounds>();
        for (int z = 0; z <= n; z++)
            Linea(paredes, z, x => h[x, z], x => ph[x, z], true);
        for (int x = 0; x <= n; x++)
            Linea(paredes, x, z => v[x, z], z => pv[x, z], false);
        foreach (var p in pilares) paredes.Add(new Bounds(p, new Vector3(0.6f, AltoPared, 0.6f)));

        var pisos = new List<Bounds> { new Bounds(new Vector3(0, -0.05f, 0), new Vector3(tam, 0.1f, tam)) };
        // Descanso afuera de la entrada para que la puerta no de al vacio.
        float sur = Nodo(0);
        pisos.Add(new Bounds(new Vector3(Nodo(ex) + TamCelda / 2, -0.05f, sur - TamCelda / 2), new Vector3(TamCelda, 0.1f, TamCelda)));
        var techos = new List<Bounds> { new Bounds(new Vector3(0, AltoPared + 0.05f, 0), new Vector3(tam, 0.1f, tam)) };
        var paneles = new List<Bounds>();
        for (int x = 0; x < n; x++)
            for (int z = 0; z < n; z++)
                if (panel[x, z]) paneles.Add(new Bounds(CentroCelda(x, z, AltoPared - 0.02f), new Vector3(1.2f, 0.04f, 0.6f)));

        BlockingUtils.Grupo(root.transform, "Paredes", matPared, paredes, true, true);
        BlockingUtils.Grupo(root.transform, "Piso", matPiso, pisos, true, false);
        BlockingUtils.Grupo(root.transform, "Techo", matTecho, techos, true, true);
        BlockingUtils.Grupo(root.transform, "Paneles Luz", matLuz, paneles, false, false);

        // Puerta de entrada abierta hacia afuera, con bisagra en el lado izquierdo del hueco.
        float cx0 = Nodo(ex) + TamCelda / 2;
        var puerta = GameObject.CreatePrimitive(PrimitiveType.Cube);
        puerta.name = "Puerta Entrada";
        puerta.transform.SetParent(root.transform, false);
        puerta.transform.localPosition = new Vector3(cx0 - AnchoPuerta / 2 + 0.03f, AltoPuerta / 2, sur - GrosorPared / 2 - AnchoPuerta / 2);
        puerta.transform.localScale = new Vector3(0.05f, AltoPuerta, AnchoPuerta);
        if (matPuerta != null) puerta.GetComponent<MeshRenderer>().sharedMaterial = matPuerta;
        var inicio = new GameObject("Inicio");
        inicio.transform.SetParent(root.transform, false);
        inicio.transform.localPosition = new Vector3(cx0, 0, sur + 1.5f);

        var gLuces = new GameObject("Luces").transform;
        gLuces.SetParent(root.transform, false);
        for (int k = 0; k < luces.Count; k++)
        {
            BlockingUtils.Luz(gLuces, "Luz " + k, luces[k], new Color(1f, 0.95f, 0.78f), rangos[k], 1.2f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log($"Backrooms construido en {scene.name}: {habs.Count} habitaciones, {paredes.Count} bloques de pared, " +
                  $"{paneles.Count} paneles, {luces.Count} luces.");
    }

    // Parte el rectangulo hasta que las habitaciones queden entre MinHabitacion y MaxHabitacion celdas.
    static void Partir(System.Random rnd, Hab r, List<Hab> salida)
    {
        bool puedeX = r.w >= MinHabitacion * 2, puedeZ = r.d >= MinHabitacion * 2;
        bool cabe = r.w <= MaxHabitacion && r.d <= MaxHabitacion;
        if ((!puedeX && !puedeZ) || (cabe && rnd.NextDouble() < 0.35))
        {
            salida.Add(r);
            return;
        }
        bool enX = puedeX && (!puedeZ || r.w > r.d || (r.w == r.d && rnd.Next(2) == 0));
        int largo = enX ? r.w : r.d;
        int corte = rnd.Next(MinHabitacion, largo - MinHabitacion + 1);
        if (enX)
        {
            Partir(rnd, new Hab { x = r.x, z = r.z, w = corte, d = r.d }, salida);
            Partir(rnd, new Hab { x = r.x + corte, z = r.z, w = r.w - corte, d = r.d }, salida);
        }
        else
        {
            Partir(rnd, new Hab { x = r.x, z = r.z, w = r.w, d = corte }, salida);
            Partir(rnd, new Hab { x = r.x, z = r.z + corte, w = r.w, d = r.d - corte }, salida);
        }
    }

    static void Vecino(Dictionary<(int, int), List<(bool, int, int)>> vecinos, int a, int b, (bool, int, int) borde)
    {
        var k = a < b ? (a, b) : (b, a);
        if (!vecinos.TryGetValue(k, out var l)) vecinos[k] = l = new List<(bool, int, int)>();
        l.Add(borde);
    }

    static int Raiz(int[] p, int a)
    {
        while (p[a] != a) a = p[a] = p[p[a]];
        return a;
    }

    static void Barajar<T>(System.Random rnd, List<T> l)
    {
        for (int i = l.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (l[i], l[j]) = (l[j], l[i]);
        }
    }

    static float Nodo(int i) => (i - Celdas / 2f) * TamCelda;
    static Vector3 CentroCelda(int x, int z, float y) => new Vector3(Nodo(x) + TamCelda / 2, y, Nodo(z) + TamCelda / 2);

    // Recorre una linea de la cuadricula; une los tramos solidos seguidos y recorta las puertas.
    static void Linea(List<Bounds> salida, int linea, System.Func<int, bool> hay, System.Func<int, bool> puerta, bool alongX)
    {
        float w = Nodo(linea);
        int i = 0;
        while (i < Celdas)
        {
            if (!hay(i)) { i++; continue; }
            if (puerta(i))
            {
                float c = Nodo(i) + TamCelda / 2;
                float lado = (TamCelda - AnchoPuerta) / 2;
                salida.Add(Tramo(alongX, w, Nodo(i) - GrosorPared / 2, Nodo(i) + lado, 0, AltoPared));
                salida.Add(Tramo(alongX, w, Nodo(i + 1) - lado, Nodo(i + 1) + GrosorPared / 2, 0, AltoPared));
                salida.Add(Tramo(alongX, w, c - AnchoPuerta / 2, c + AnchoPuerta / 2, AltoPuerta, AltoPared));
                i++;
                continue;
            }
            int j = i;
            while (j + 1 < Celdas && hay(j + 1) && !puerta(j + 1)) j++;
            salida.Add(Tramo(alongX, w, Nodo(i) - GrosorPared / 2, Nodo(j + 1) + GrosorPared / 2, 0, AltoPared));
            i = j + 1;
        }
    }

    static Bounds Tramo(bool alongX, float w, float u0, float u1, float y0, float y1)
    {
        float u = (u0 + u1) / 2, largo = u1 - u0, alto = y1 - y0, y = (y0 + y1) / 2;
        var pos = alongX ? new Vector3(u, y, w) : new Vector3(w, y, u);
        var size = alongX ? new Vector3(largo, alto, GrosorPared) : new Vector3(GrosorPared, alto, largo);
        return new Bounds(pos, size);
    }
}
