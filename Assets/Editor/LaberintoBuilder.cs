using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Construye el blocking del laberinto (generado a partir del dibujo de referencia).
// Menu: Tools > Laberinto > Construir. Escala en metros, personaje de 1.75 m.
[InitializeOnLoad]
public static class LaberintoBuilder
{
    const float MetrosPorPixel = 0.08f;   // pasillo tipico ~2.5 m, el mas angosto ~1.4 m
    const float AltoPared = 3f;
    const float HuecoParedElevada = 1.0f; // espacio libre entre el piso y las paredes elevadas (naranja)
    const float AltoBloqueAzul = 1f;
    const int AnchoPx = 1100, AltoPx = 616;
    const string FlagPath = "Library/ConstruirLaberinto.flag";

    // x, y, ancho, alto en pixeles de la imagen (origen arriba-izquierda); una pared = un bloque
    static readonly float[] Paredes = {
            447, 21, 17, 92,
            549.5f, 21, 14.5f, 131,
            1064.5f, 64, 36.5f, 553,
            548, 66, 553, 14,
            65, 72, 21, 545,
            67, 72, 397, 11,
            117, 100, 12, 172,
            116, 101.5f, 246.5f, 8,
            384.5f, 101.5f, 78.5f, 8,
            606, 102, 429, 10,
            227, 120, 14, 262,
            142.5f, 121, 8, 236,
            142, 122, 95, 11,
            815, 131, 141, 19,
            551, 136, 227, 15,
            266, 138, 16.5f, 122,
            271, 139, 76, 9.5f,
            271, 140, 7, 174,
            406, 142, 64, 52,
            999.5f, 151, 30.5f, 260,
            177, 160, 22, 207,
            314, 179, 9, 81,
            769, 181, 10, 233,
            261, 182, 519, 10.5f,
            949, 182, 11, 12.5f,
            858, 183, 100, 5.5f,
            854, 207, 8, 42,
            854, 207, 73, 6,
            922.5f, 207, 8, 143,
            707, 210, 29, 164,
            526, 211, 155, 10,
            949, 216.5f, 11, 195.5f,
            679, 220, 5, 199,
            820, 237, 42, 12,
            357, 243, 14, 208,
            357, 245.5f, 127, 11,
            524, 246, 110, 49,
            566, 246, 67, 193,
            472, 247, 11, 53,
            418, 287, 11, 156,
            418, 289, 65, 11,
            305, 297, 14, 89,
            275, 298, 9, 15,
            306, 298, 12, 15,
            820, 308, 34, 106,
            117, 314, 35, 43,
            228, 368, 92, 16,
            117, 388, 35, 35,
            143, 388, 8, 144,
            820, 393, 144, 20,
            681, 406, 97, 7,
            418, 412.5f, 215, 29,
            221, 429, 9, 24,
            218, 431, 151, 20,
            64, 438, 52, 10,
            109, 438, 8, 96,
            629, 468, 149, 21,
            814, 468, 213, 71,
            629, 469, 39, 69.5f,
            221, 475, 9, 12,
            577, 479, 9.5f, 68,
            220, 480.5f, 320, 7,
            353, 512, 14, 60,
            142, 513, 450, 31.5f,
            718, 527, 65, 15,
            629, 560.5f, 39, 56.5f,
            405, 561.5f, 163, 12,
            119, 571, 197, 10,
            627, 575, 474, 42,
            63, 610, 1038, 7,
            819.5f, 184, 4, 64
    };
    // tramos de pared que cruzan los trazos naranjas, con hueco abajo
    static readonly float[] ParedesElevadas = {
            362.5f, 101.5f, 22, 8,
            949, 194.5f, 11, 22,
            284, 298, 22, 15,
            221, 453, 9, 22,
            629, 538.5f, 39, 22
    };
    // x, y, ancho, alto en pixeles
    static readonly float[] BloquesAzules = {
            560, 79, 21, 20,
            146, 130, 22, 25,
            451, 255, 23, 20,
            750, 381, 24, 25,
            1049, 550, 17, 30,
            80, 588, 33, 23
    };

    static LaberintoBuilder()
    {
        RevisarFlag();
        EditorApplication.focusChanged += enfocado => { if (enfocado) RevisarFlag(); };
    }

    static void RevisarFlag()
    {
        if (System.IO.File.Exists(FlagPath))
        {
            System.IO.File.Delete(FlagPath);
            EditorApplication.delayCall += Construir;
        }
    }

    [MenuItem("Tools/Laberinto/Construir")]
    public static void Construir()
    {
        var scene = EditorSceneManager.GetActiveScene();

        var viejo = GameObject.Find("Laberinto");
        Vector3? posVieja = viejo != null ? viejo.transform.position : (Vector3?)null;
        if (viejo != null) Undo.DestroyObjectImmediate(viejo);

        var root = new GameObject("Laberinto");
        Undo.RegisterCreatedObjectUndo(root, "Construir laberinto");
        var level1 = GameObject.Find("level 1");
        if (posVieja.HasValue) root.transform.position = posVieja.Value;
        else if (level1 != null)
        {
            root.transform.position = level1.transform.position;
            Undo.RecordObject(level1, "Ocultar level 1");
            level1.SetActive(false);
        }

        var matPared = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/LadrilloMusgo.mat");
        var matPiso = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Gris.mat");
        var matAzul = MaterialAzul();

        var piso = Bloque("Piso", root.transform, matPiso);
        piso.transform.localPosition = new Vector3(0, -0.1f, 0);
        piso.transform.localScale = new Vector3(AnchoPx * MetrosPorPixel, 0.2f, AltoPx * MetrosPorPixel);

        var paredes = new GameObject("Paredes").transform;
        paredes.SetParent(root.transform, false);
        for (int k = 0; k < Paredes.Length; k += 4)
            Caja("Pared " + (k / 4), paredes, matPared, Paredes[k], Paredes[k + 1],
                Paredes[k + 2], Paredes[k + 3], 0, AltoPared);

        var elevadas = new GameObject("Paredes Elevadas").transform;
        elevadas.SetParent(root.transform, false);
        for (int k = 0; k < ParedesElevadas.Length; k += 4)
            Caja("Pared Elevada " + (k / 4), elevadas, matPared, ParedesElevadas[k], ParedesElevadas[k + 1],
                ParedesElevadas[k + 2], ParedesElevadas[k + 3], HuecoParedElevada, AltoPared);

        var azules = new GameObject("Bloques Azules").transform;
        azules.SetParent(root.transform, false);
        for (int k = 0; k < BloquesAzules.Length; k += 4)
            Caja("Bloque Azul " + (k / 4), azules, matAzul, BloquesAzules[k], BloquesAzules[k + 1],
                BloquesAzules[k + 2], BloquesAzules[k + 3], 0, AltoBloqueAzul);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("Laberinto construido en la escena " + scene.name);
    }

    // Caja a partir de un rectangulo en pixeles de la imagen; la imagen queda centrada en el root.
    static void Caja(string nombre, Transform padre, Material mat, float px, float py, float pw, float ph, float y0, float y1)
    {
        var go = Bloque(nombre, padre, mat);
        float cx = (px + pw * 0.5f - AnchoPx * 0.5f) * MetrosPorPixel;
        float cz = (AltoPx * 0.5f - (py + ph * 0.5f)) * MetrosPorPixel;
        go.transform.localPosition = new Vector3(cx, (y0 + y1) * 0.5f, cz);
        go.transform.localScale = new Vector3(pw * MetrosPorPixel, y1 - y0, ph * MetrosPorPixel);
    }

    static GameObject Bloque(string nombre, Transform padre, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
        return go;
    }

    static Material MaterialAzul()
    {
        const string path = "Assets/Materials/BloqueAzul.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        var azul = new Color(0.25f, 0.3f, 0.85f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", azul);
        mat.color = azul;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
