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
    const int Celda = 4;               // pixeles por celda en los datos
    const int AnchoPx = 1100, AltoPx = 616;
    const string FlagPath = "Library/ConstruirLaberinto.flag";

    // x, y, ancho, alto en celdas (origen arriba-izquierda de la imagen)
    static readonly int[] Paredes = {
            114, 5, 2, 23,
            137, 5, 4, 15,
            113, 6, 1, 22,
            141, 14, 1, 6,
            172, 16, 103, 4,
            142, 17, 30, 3,
            17, 18, 96, 2,
            16, 20, 6, 74,
            40, 20, 73, 1,
            137, 20, 3, 2,
            265, 20, 10, 21,
            107, 21, 6, 1,
            112, 22, 1, 6,
            138, 22, 2, 16,
            140, 23, 1, 15,
            29, 25, 61, 2,
            204, 25, 55, 3,
            96, 26, 16, 2,
            152, 26, 52, 2,
            29, 27, 3, 41,
            63, 27, 27, 1,
            151, 27, 1, 1,
            137, 28, 1, 4,
            35, 30, 24, 3,
            231, 32, 8, 6,
            35, 33, 2, 33,
            55, 33, 4, 1,
            180, 33, 14, 5,
            204, 33, 27, 5,
            56, 34, 3, 15,
            67, 34, 1, 27,
            79, 34, 7, 3,
            141, 34, 39, 4,
            194, 34, 1, 3,
            68, 35, 11, 2,
            86, 35, 1, 2,
            102, 35, 12, 14,
            114, 36, 3, 13,
            68, 37, 4, 1,
            59, 38, 1, 58,
            68, 38, 3, 11,
            250, 38, 5, 63,
            37, 39, 1, 50,
            249, 39, 1, 21,
            44, 40, 6, 51,
            66, 40, 1, 10,
            255, 40, 1, 63,
            117, 41, 1, 8,
            266, 41, 9, 23,
            71, 45, 31, 3,
            177, 45, 18, 3,
            118, 46, 59, 2,
            205, 46, 1, 16,
            214, 46, 26, 1,
            215, 47, 3, 1,
            236, 47, 4, 1,
            71, 48, 13, 1,
            85, 48, 17, 1,
            118, 48, 2, 1,
            192, 48, 3, 43,
            57, 49, 2, 47,
            68, 49, 1, 23,
            78, 49, 3, 10,
            177, 52, 1, 42,
            213, 52, 19, 1,
            132, 53, 38, 2,
            176, 53, 1, 9,
            178, 53, 5, 40,
            213, 53, 3, 9,
            224, 53, 8, 1,
            131, 54, 1, 1,
            230, 54, 2, 15,
            236, 54, 4, 2,
            256, 54, 1, 49,
            169, 55, 1, 22,
            237, 56, 3, 21,
            170, 58, 1, 47,
            79, 59, 2, 6,
            183, 59, 1, 34,
            206, 59, 7, 3,
            89, 61, 26, 3,
            131, 61, 27, 13,
            115, 62, 6, 2,
            158, 62, 1, 25,
            69, 63, 1, 15,
            89, 64, 4, 41,
            118, 64, 3, 11,
            267, 64, 8, 21,
            60, 65, 1, 25,
            36, 66, 1, 23,
            232, 66, 1, 22,
            117, 69, 1, 6,
            231, 69, 1, 19,
            104, 72, 13, 3,
            257, 73, 1, 29,
            70, 74, 1, 4,
            79, 74, 1, 23,
            141, 74, 17, 36,
            76, 75, 3, 22,
            104, 75, 4, 1,
            104, 76, 3, 35,
            68, 77, 1, 1,
            205, 77, 9, 26,
            238, 77, 2, 26,
            29, 78, 1, 12,
            35, 78, 1, 11,
            30, 79, 5, 10,
            184, 80, 1, 14,
            240, 82, 1, 21,
            233, 84, 1, 3,
            268, 85, 7, 69,
            45, 91, 5, 1,
            193, 91, 2, 12,
            60, 92, 16, 4,
            258, 92, 1, 10,
            16, 94, 5, 56,
            75, 96, 1, 1,
            29, 97, 9, 8,
            107, 98, 1, 1,
            214, 98, 24, 5,
            107, 100, 1, 11,
            181, 101, 12, 2,
            251, 101, 4, 2,
            171, 102, 1, 3,
            177, 102, 4, 1,
            108, 103, 33, 7,
            172, 103, 8, 1,
            193, 103, 1, 1,
            205, 103, 8, 1,
            214, 103, 1, 1,
            172, 104, 6, 1,
            29, 105, 1, 1,
            31, 105, 7, 1,
            89, 105, 3, 8,
            36, 106, 2, 27,
            55, 107, 14, 6,
            69, 108, 20, 5,
            158, 108, 1, 1,
            21, 109, 8, 3,
            108, 110, 17, 1,
            27, 112, 2, 21,
            265, 116, 3, 21,
            157, 117, 38, 5,
            203, 117, 54, 1,
            204, 118, 53, 16,
            55, 119, 3, 3,
            58, 120, 77, 2,
            144, 120, 3, 17,
            35, 121, 1, 8,
            157, 122, 10, 12,
            177, 122, 11, 1,
            38, 127, 11, 6,
            49, 128, 94, 5,
            143, 129, 1, 8,
            189, 131, 7, 4,
            179, 132, 10, 3,
            68, 133, 75, 1,
            147, 133, 1, 1,
            88, 134, 55, 2,
            205, 134, 52, 1,
            180, 135, 10, 1,
            88, 136, 4, 7,
            120, 136, 23, 1,
            266, 137, 2, 17,
            92, 140, 1, 3,
            101, 140, 33, 3,
            157, 140, 11, 12,
            134, 141, 8, 2,
            30, 142, 6, 3,
            36, 143, 43, 2,
            127, 143, 14, 1,
            201, 143, 23, 11,
            168, 144, 1, 10,
            189, 144, 12, 10,
            224, 144, 38, 10,
            265, 144, 1, 10,
            64, 145, 15, 1,
            169, 145, 20, 9,
            262, 145, 3, 9,
            16, 150, 4, 4,
            20, 152, 2, 2,
            27, 152, 58, 2,
            158, 152, 10, 2,
            22, 153, 5, 1,
            85, 153, 73, 1
    };
    static readonly int[] ParedesElevadas = {
            90, 25, 6, 3,
            236, 48, 4, 6,
            71, 74, 5, 5,
            55, 113, 3, 6,
            157, 134, 10, 6
    };
    // x, y, ancho, alto en pixeles
    static readonly int[] BloquesAzules = {
            560, 79, 21, 20,
            146, 130, 22, 25,
            451, 255, 23, 20,
            750, 381, 24, 25,
            1049, 550, 17, 30,
            80, 588, 33, 23
    };

    static LaberintoBuilder()
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
        if (viejo != null) Undo.DestroyObjectImmediate(viejo);

        var root = new GameObject("Laberinto");
        Undo.RegisterCreatedObjectUndo(root, "Construir laberinto");
        var level1 = GameObject.Find("level 1");
        if (level1 != null)
        {
            root.transform.position = level1.transform.position;
            Undo.RecordObject(level1, "Ocultar level 1");
            level1.SetActive(false);
        }

        var matPared = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/negro.mat");
        var matPiso = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Gris.mat");
        var matAzul = MaterialAzul();

        var piso = Bloque("Piso", root.transform, matPiso);
        piso.transform.localPosition = new Vector3(0, -0.1f, 0);
        piso.transform.localScale = new Vector3(AnchoPx * MetrosPorPixel, 0.2f, AltoPx * MetrosPorPixel);

        var paredes = new GameObject("Paredes").transform;
        paredes.SetParent(root.transform, false);
        for (int k = 0; k < Paredes.Length; k += 4)
            Caja("Pared " + (k / 4), paredes, matPared, Paredes[k] * Celda, Paredes[k + 1] * Celda,
                Paredes[k + 2] * Celda, Paredes[k + 3] * Celda, 0, AltoPared);

        var elevadas = new GameObject("Paredes Elevadas").transform;
        elevadas.SetParent(root.transform, false);
        for (int k = 0; k < ParedesElevadas.Length; k += 4)
            Caja("Pared Elevada " + (k / 4), elevadas, matPared, ParedesElevadas[k] * Celda, ParedesElevadas[k + 1] * Celda,
                ParedesElevadas[k + 2] * Celda, ParedesElevadas[k + 3] * Celda, HuecoParedElevada, AltoPared);

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
    static void Caja(string nombre, Transform padre, Material mat, int px, int py, int pw, int ph, float y0, float y1)
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
