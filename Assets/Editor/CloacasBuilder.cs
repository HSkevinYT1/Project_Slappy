using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static BlockingUtils;

// Genera un blocking de cloacas: una cisterna abierta con una cuadricula de columnas y vigas,
// dos canales de agua hundidos con puentes y escalones, salidas de canal con rejas,
// tubos y lamparas en las paredes, y una puerta de entrada.
// Menu: Tools > Cloacas > Construir. Cambia Semilla para otra distribucion de puentes y columnas.
[InitializeOnLoad]
public static class CloacasBuilder
{
    const int Semilla = 2024;
    const int ColumnasX = 9, ColumnasZ = 7;   // cuadricula de columnas
    const float Separacion = 6f;               // metros entre columnas -> sala de 54 x 42 m
    const float Alto = 5.5f;                   // altura del techo sobre las pasarelas
    const float Profundidad = 1f;              // fondo de los canales bajo las pasarelas
    const float NivelAgua = 0.4f;              // agua sobre el fondo del canal
    const float AnchoCanal = 3f;
    const float GrosorMuro = 0.4f, GrosorColumna = 1f;
    const float ProbFaltaColumna = 0.1f;
    const int PuentesPorCanal = 3;
    const float LargoSalida = 4f;              // tunel que sale por cada extremo de los canales
    const float AltoSalida = 1.2f;             // sobre la pasarela
    const float AltoPuerta = 2.2f, AnchoPuerta = 1.2f;
    static readonly Vector3 SeparacionDelLaberinto = new Vector3(110, 0, 0);
    const string FlagPath = "Library/ConstruirCloacas.flag";

    static CloacasBuilder()
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

    [MenuItem("Tools/Cloacas/Construir")]
    public static void Construir()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var viejo = GameObject.Find("Cloacas");
        if (viejo != null) Undo.DestroyObjectImmediate(viejo);

        var root = new GameObject("Cloacas");
        Undo.RegisterCreatedObjectUndo(root, "Construir Cloacas");
        var laberinto = GameObject.Find("Laberinto");
        root.transform.position = (laberinto != null ? laberinto.transform.position : Vector3.zero) + SeparacionDelLaberinto;

        var rnd = new System.Random(Semilla);
        float mx = ColumnasX * Separacion / 2, mz = ColumnasZ * Separacion / 2;   // medio ancho y medio largo
        float fondo = -Profundidad, g = GrosorMuro;

        // Columnas y canales sobre la misma cuadricula; los canales corren a lo largo de X entre filas de columnas.
        var colX = new float[ColumnasX];
        var colZ = new float[ColumnasZ];
        for (int i = 0; i < ColumnasX; i++) colX[i] = -mx + Separacion / 2 + i * Separacion;
        for (int j = 0; j < ColumnasZ; j++) colZ[j] = -mz + Separacion / 2 + j * Separacion;
        var canales = new[] { (colZ[1] + colZ[2]) / 2, (colZ[ColumnasZ - 3] + colZ[ColumnasZ - 2]) / 2 };
        var centrosX = new List<float>();
        for (int i = 0; i < ColumnasX - 1; i++) centrosX.Add((colX[i] + colX[i + 1]) / 2);

        var concreto = new List<Bounds>();
        var piso = new List<Bounds>();
        var agua = new List<Bounds>();
        var metal = new List<Bounds>();
        var lamparas = new List<Bounds>();

        // Losa del fondo (tambien bajo los tuneles de salida) y pasarelas entre canales.
        piso.Add(Caja(-mx - LargoSalida, fondo - 0.2f, -mz, mx + LargoSalida, fondo, mz));
        float z = -mz;
        foreach (var c in canales)
        {
            piso.Add(Caja(-mx, fondo, z, mx, 0, c - AnchoCanal / 2));
            z = c + AnchoCanal / 2;
        }
        piso.Add(Caja(-mx, fondo, z, mx, 0, mz));
        foreach (var c in canales)
            agua.Add(Caja(-mx - LargoSalida, fondo, c - AnchoCanal / 2, mx + LargoSalida, fondo + NivelAgua, c + AnchoCanal / 2));

        // Muros largos (norte y sur, con la puerta de entrada en el sur).
        float puertaX = centrosX[ColumnasX / 2];
        float p0 = puertaX - AnchoPuerta / 2, p1 = puertaX + AnchoPuerta / 2;
        concreto.Add(Caja(-mx - g, fondo - 0.2f, mz, mx + g, Alto, mz + g));
        concreto.Add(Caja(-mx - g, fondo - 0.2f, -mz - g, p0, Alto, -mz));
        concreto.Add(Caja(p1, fondo - 0.2f, -mz - g, mx + g, Alto, -mz));
        concreto.Add(Caja(p0, AltoPuerta, -mz - g, p1, Alto, -mz));
        concreto.Add(Caja(p0, fondo - 0.2f, -mz - g, p1, 0, -mz));

        // Muros de los extremos: huecos sobre los canales que dan a tuneles cerrados con reja.
        foreach (float lado in new[] { -1f, 1f })
        {
            float xm = lado * mx, xf = xm + lado * g, xt = xm + lado * LargoSalida;
            z = -mz;
            foreach (var c in canales)
            {
                float c0 = c - AnchoCanal / 2, c1 = c + AnchoCanal / 2;
                concreto.Add(Caja(xm, fondo - 0.2f, z, xf, Alto, c0));
                concreto.Add(Caja(xm, AltoSalida, c0, xf, Alto, c1));
                // Tunel: paredes laterales, techo y fondo.
                concreto.Add(Caja(xm, fondo - 0.2f, c0 - 0.3f, xt, AltoSalida + 0.3f, c0));
                concreto.Add(Caja(xm, fondo - 0.2f, c1, xt, AltoSalida + 0.3f, c1 + 0.3f));
                concreto.Add(Caja(xm, AltoSalida, c0 - 0.3f, xt, AltoSalida + 0.3f, c1 + 0.3f));
                concreto.Add(Caja(xt, fondo - 0.2f, c0 - 0.3f, xt + lado * 0.3f, AltoSalida + 0.3f, c1 + 0.3f));
                // Reja en la boca del tunel.
                for (float b = c0 + 0.15f; b < c1; b += 0.3f)
                    metal.Add(Caja(xm, fondo, b - 0.04f, xm + lado * 0.08f, AltoSalida, b + 0.04f));
                metal.Add(Caja(xm, AltoSalida - 0.3f, c0, xm + lado * 0.1f, AltoSalida - 0.2f, c1));
                z = c1;
            }
            concreto.Add(Caja(xm, fondo - 0.2f, z, xf, Alto, mz));
        }

        // Techo y vigas que unen las columnas.
        concreto.Add(Caja(-mx - g, Alto, -mz - g, mx + g, Alto + 0.3f, mz + g));
        foreach (var cz in colZ) concreto.Add(Caja(-mx, Alto - 0.6f, cz - 0.3f, mx, Alto, cz + 0.3f));
        foreach (var cx in colX) concreto.Add(Caja(cx - 0.3f, Alto - 0.6f, -mz, cx + 0.3f, Alto, mz));

        // Columnas con base y capitel; algunas faltan para abrir el espacio.
        int nCol = 0;
        float r = GrosorColumna / 2;
        foreach (var cx in colX)
            foreach (var cz in colZ)
            {
                if (rnd.NextDouble() < ProbFaltaColumna) continue;
                concreto.Add(Caja(cx - r, 0, cz - r, cx + r, Alto, cz + r));
                concreto.Add(Caja(cx - r - 0.15f, 0, cz - r - 0.15f, cx + r + 0.15f, 0.3f, cz + r + 0.15f));
                concreto.Add(Caja(cx - r - 0.15f, Alto - 0.9f, cz - r - 0.15f, cx + r + 0.15f, Alto - 0.6f, cz + r + 0.15f));
                nCol++;
            }

        // Puentes y escalones por canal, entre columnas; los bordillos se cortan donde hay paso.
        int nPuentes = 0;
        for (int k = 0; k < canales.Length; k++)
        {
            float c = canales[k], c0 = c - AnchoCanal / 2, c1 = c + AnchoCanal / 2;
            var libres = new List<float>(centrosX);
            var pasos0 = new List<(float, float)>();   // cortes en el bordillo del lado c0
            var pasos1 = new List<(float, float)>();   // cortes en el bordillo del lado c1
            for (int b = 0; b < PuentesPorCanal && libres.Count > 0; b++)
            {
                int idx = rnd.Next(libres.Count);
                float px = libres[idx];
                libres.RemoveAt(idx);
                piso.Add(Caja(px - 1.2f, -0.25f, c0, px + 1.2f, 0, c1));
                concreto.Add(Caja(px - 1.2f, 0, c0, px - 1.05f, 0.9f, c1));
                concreto.Add(Caja(px + 1.05f, 0, c0, px + 1.2f, 0.9f, c1));
                pasos0.Add((px - 1.2f, px + 1.2f));
                pasos1.Add((px - 1.2f, px + 1.2f));
                nPuentes++;
            }
            // Escalones desde la pasarela central hacia el agua.
            float ex = libres[rnd.Next(libres.Count)];
            bool desdeC1 = k == 0;   // la pasarela central queda del lado c1 del primer canal y c0 del segundo
            for (int s = 0; s < 3; s++)
            {
                float alto = -0.25f * (s + 1);
                float za = desdeC1 ? c1 - 0.4f * s : c0 + 0.4f * s;
                float zb = desdeC1 ? za - 0.4f : za + 0.4f;
                piso.Add(Caja(ex - 0.7f, fondo, za, ex + 0.7f, alto, zb));
            }
            (desdeC1 ? pasos1 : pasos0).Add((ex - 0.8f, ex + 0.8f));
            Bordillo(concreto, c0 - 0.2f, c0, -mx, mx, pasos0);
            Bordillo(concreto, c1, c1 + 0.2f, -mx, mx, pasos1);
        }

        // Tubos a lo largo de los muros largos.
        foreach (float lado in new[] { -1f, 1f })
        {
            float zm = lado * mz;
            metal.Add(Caja(-mx, 3.8f, zm - lado * 0.6f, mx, 4.15f, zm - lado * 0.25f));
            metal.Add(Caja(-mx, 4.3f, zm - lado * 0.6f, mx, 4.6f, zm - lado * 0.3f));
        }

        // Lamparas: de pared en los muros largos, colgantes sobre las pasarelas y sobre los canales.
        var gLuces = new GameObject("Luces").transform;
        gLuces.SetParent(root.transform, false);
        var colorLuz = new Color(1f, 0.9f, 0.65f);
        int nl = 0;
        foreach (float lado in new[] { -1f, 1f })
            foreach (var x in centrosX)
            {
                float zm = lado * mz;
                lamparas.Add(Caja(x - 0.25f, 2.9f, zm - lado * 0.2f, x + 0.25f, 3.15f, zm));
                Luz(gLuces, "Luz " + nl++, new Vector3(x, 2.85f, zm - lado * 0.6f), colorLuz, 9f, 1.2f);
            }
        var filasPasarela = new List<float>();
        for (int j = 0; j < ColumnasZ - 1; j++)
        {
            float cz = (colZ[j] + colZ[j + 1]) / 2;
            bool esCanal = false;
            foreach (var c in canales) esCanal |= Mathf.Abs(cz - c) < 0.01f;
            if (!esCanal) filasPasarela.Add(cz);
        }
        for (int j = 0; j < filasPasarela.Count; j++)
            for (int i = j % 2; i < centrosX.Count; i += 2)
            {
                float x = centrosX[i], cz = filasPasarela[j];
                lamparas.Add(Caja(x - 0.3f, 4.25f, cz - 0.3f, x + 0.3f, 4.4f, cz + 0.3f));
                metal.Add(Caja(x - 0.03f, 4.4f, cz - 0.03f, x + 0.03f, Alto, cz + 0.03f));
                Luz(gLuces, "Luz " + nl++, new Vector3(x, 4.1f, cz), colorLuz, 10f, 1.5f);
            }
        foreach (var c in canales)
            for (int i = 1; i < centrosX.Count; i += 3)
            {
                float x = centrosX[i];
                lamparas.Add(Caja(x - 0.3f, 4.25f, c - 0.3f, x + 0.3f, 4.4f, c + 0.3f));
                metal.Add(Caja(x - 0.03f, 4.4f, c - 0.03f, x + 0.03f, Alto, c + 0.03f));
                Luz(gLuces, "Luz " + nl++, new Vector3(x, 4.1f, c), new Color(0.8f, 1f, 0.75f), 8f, 1f);
            }

        // Descanso afuera de la entrada.
        piso.Add(Caja(puertaX - 1.5f, -0.2f, -mz - g - 3f, puertaX + 1.5f, 0, -mz - g));

        var matConcreto = CrearMaterial("CloacaConcreto", new Color(0.42f, 0.44f, 0.4f), false, 0.2f);
        var matPiso = CrearMaterial("CloacaPiso", new Color(0.32f, 0.33f, 0.3f), false, 0.35f);
        var matAgua = CrearMaterial("CloacaAgua", new Color(0.14f, 0.2f, 0.14f), false, 0.92f);
        var matMetal = CrearMaterial("CloacaMetal", new Color(0.27f, 0.22f, 0.18f), false, 0.4f);
        var matLuz = CrearMaterial("CloacaLuz", new Color(1f, 0.9f, 0.65f), true);
        var matPuerta = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DoorMaterial.mat");

        Grupo(root.transform, "Concreto", matConcreto, concreto, true, true);
        Grupo(root.transform, "Piso", matPiso, piso, true, true);
        Grupo(root.transform, "Agua", matAgua, agua, false, false);
        Grupo(root.transform, "Metal", matMetal, metal, true, true);
        Grupo(root.transform, "Lamparas", matLuz, lamparas, false, false);

        // Puerta de entrada abierta hacia afuera e Inicio adentro.
        var puerta = GameObject.CreatePrimitive(PrimitiveType.Cube);
        puerta.name = "Puerta Entrada";
        puerta.transform.SetParent(root.transform, false);
        puerta.transform.localPosition = new Vector3(p0 + 0.03f, AltoPuerta / 2, -mz - g - AnchoPuerta / 2);
        puerta.transform.localScale = new Vector3(0.05f, AltoPuerta, AnchoPuerta);
        if (matPuerta != null) puerta.GetComponent<MeshRenderer>().sharedMaterial = matPuerta;
        var inicio = new GameObject("Inicio");
        inicio.transform.SetParent(root.transform, false);
        inicio.transform.localPosition = new Vector3(puertaX, 0, -mz + 1.5f);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log($"Cloacas construidas en {scene.name}: {nCol} columnas, {nPuentes} puentes, {nl} luces, " +
                  $"{concreto.Count + piso.Count + metal.Count} bloques.");
    }

    // Bordillo bajo a lo largo de X, cortado en los tramos de paso.
    static void Bordillo(List<Bounds> salida, float z0, float z1, float x0, float x1, List<(float a, float b)> cortes)
    {
        cortes.Sort((p, q) => p.a.CompareTo(q.a));
        float x = x0;
        foreach (var c in cortes)
        {
            if (c.a > x) salida.Add(Caja(x, 0, z0, c.a, 0.35f, z1));
            x = Mathf.Max(x, c.b);
        }
        if (x < x1) salida.Add(Caja(x, 0, z0, x1, 0.35f, z1));
    }
}
