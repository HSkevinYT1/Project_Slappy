using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Herramientas para preparar el Laberinto y la Zona Principal.
// Se aplican SOLAS al abrir/compilar la escena (una sola vez) y tambien desde el menu Tools.
// Todo se puede deshacer con Ctrl+Z. Despues hay que guardar la escena.
[InitializeOnLoad]
public static class ZonasSetup
{
    const float FactorEscala = 1.25f;

    static ZonasSetup()
    {
        EditorApplication.delayCall += AutoAplicar;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += AutoAplicar;
    }

    // Aplica lo que falte en la escena abierta. Es seguro llamarlo varias veces:
    // - Laberinto sin LianasLaberinto -> se agrega el componente y se escala una vez.
    // - Zona_Principal sin "Marcas Caminos" -> se crean las marcas.
    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        GameObject lab = GameObject.Find("Laberinto");
        if (lab && !lab.GetComponent<LianasLaberinto>())
        {
            var lianas = Undo.AddComponent<LianasLaberinto>(lab);
            lianas.AutoConfigurar();
            if (!lianas.materialLianas)
                lianas.materialLianas = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/LianasIndigo.mat");
            EditorUtility.SetDirty(lianas);
            EscalarLaberinto(lab);
            Debug.Log("[ZonasSetup] Laberinto: lianas agregadas y escala aplicada. Guarda la escena (Ctrl+S).", lab);
        }
        // Vista previa de las lianas en el editor (se pierde al recompilar, asi que se rehace aqui).
        if (lab && lab.TryGetComponent(out LianasLaberinto vista)) vista.RegenerarVistaPrevia();

        GameObject zp = GameObject.Find("Zona_Principal");
        if (zp && !zp.transform.Find("Marcas Caminos"))
        {
            CrearMarcas(zp, false);
            Debug.Log("[ZonasSetup] Zona_Principal: marcas de caminos creadas. Guarda la escena (Ctrl+S).", zp);
        }
        // Pasillos rectos hacia cada zona (ver PasillosSetup.cs).
        if (zp && zp.transform.Find("Marcas Caminos")) PasillosSetup.AutoAplicar(zp);

        // Zona roja: isla con el faro (ver ZonaRojaSetup.cs).
        if (zp && zp.transform.Find("Pasillos")) ZonaRojaSetup.AutoAplicar();

        // NavMesh solo en pisos, enemigos de cada area, llave de las Cloacas y triggers de Volume (ver EnemigosZonasSetup.cs).
        if (zp && GameObject.Find("Zona Roja")) EnemigosZonasSetup.AutoAplicar();

        // Caminos y puertas segun el progreso, pilas, linterna y menu principal (ver MenuProgresoSetup.cs).
        if (zp && GameObject.Find("Enemigos y Zonas")) MenuProgresoSetup.AutoAplicar();
    }

    // ---------------------------------------------------------------- Laberinto

    [MenuItem("Tools/Laberinto/Escalar Laberinto +25% (solo a lo ancho)")]
    static void EscalarLaberintoMenu()
    {
        GameObject lab = GameObject.Find("Laberinto");
        if (!lab)
        {
            EditorUtility.DisplayDialog("Laberinto", "No se encontro un objeto llamado \"Laberinto\".", "OK");
            return;
        }
        EscalarLaberinto(lab);
    }

    static void EscalarLaberinto(GameObject lab)
    {
        Undo.RecordObject(lab.transform, "Escalar Laberinto");
        Vector3 s = lab.transform.localScale;
        // Solo X y Z: los pasillos quedan mas anchos y largos, pero la altura de las
        // paredes (y de las paredes rosadas que se pasan por debajo) no cambia.
        lab.transform.localScale = new Vector3(s.x * FactorEscala, s.y, s.z * FactorEscala);
        EditorSceneManager.MarkSceneDirty(lab.scene);

        Debug.Log($"[Laberinto] Escala {s} -> {lab.transform.localScale}. Ctrl+Z para deshacer.", lab);
    }

    // ---------------------------------------------------------- Zona Principal

    struct DefMarca
    {
        public string nombre, zona, destino;
        public Color color;
        public float intensidad;
    }

    [MenuItem("Tools/Zona Principal/Crear marcas de caminos")]
    static void CrearMarcasMenu()
    {
        GameObject zp = GameObject.Find("Zona_Principal");
        if (!zp)
        {
            EditorUtility.DisplayDialog("Zona Principal", "No se encontro un objeto llamado \"Zona_Principal\".", "OK");
            return;
        }
        CrearMarcas(zp, true);
    }

    internal static void CrearMarcas(GameObject zp, bool seleccionar)
    {
        Transform raiz = zp.transform;

        // Centro: la cabina (si existe) y altura: el piso de la zona.
        Vector3 centro = raiz.position;
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name.ToLowerInvariant().Contains("cabina")) { centro = t.position; break; }

        float alturaPiso = raiz.position.y;
        float mitadPiso = 12f;
        Transform piso = raiz.Find("piso");
        if (!piso) piso = raiz.Find("Piso");
        if (piso && piso.TryGetComponent(out Renderer rp))
        {
            alturaPiso = rp.bounds.max.y;
            mitadPiso = Mathf.Min(rp.bounds.extents.x, rp.bounds.extents.z);
        }
        centro.y = alturaPiso + 0.02f;

        var defs = new[]
        {
            new DefMarca { nombre = "Camino Indigo - Laberinto",   zona = "Laberinto",  destino = "Laberinto",   color = new Color(0.294f, 0f, 0.51f), intensidad = 6f },
            new DefMarca { nombre = "Camino Amarillo - Backrooms", zona = "Backrooms",  destino = "Backrooms",   color = new Color(1f, 0.85f, 0f),     intensidad = 2.5f },
            new DefMarca { nombre = "Camino Negro - Cloacas",      zona = "Cloacas",    destino = "Cloacas",     color = new Color(0.02f, 0.02f, 0.025f), intensidad = 1f },
            new DefMarca { nombre = "Camino Rojo - Proximamente",  zona = null,         destino = "Proximamente", color = new Color(0.6f, 0f, 0.06f), intensidad = 4f },
        };

        // Direccion de cada camino: hacia su zona, redondeada a 90 grados (como en el dibujo).
        var angulos = new float[defs.Length];
        var usados = new bool[4];
        var zonas = new Transform[defs.Length];
        for (int i = 0; i < defs.Length; i++)
        {
            angulos[i] = float.NaN;
            if (defs[i].zona == null) continue;
            GameObject z = GameObject.Find(defs[i].zona);
            if (!z) continue;
            zonas[i] = z.transform;
            Vector3 d = raiz.InverseTransformDirection(z.transform.position - centro);
            int cuadrante = Mod(Mathf.RoundToInt(Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg / 90f), 4);
            if (usados[cuadrante]) continue;
            usados[cuadrante] = true;
            angulos[i] = cuadrante * 90f;
        }
        // Los que no tienen zona (rojo) toman una direccion libre.
        for (int i = 0; i < defs.Length; i++)
        {
            if (!float.IsNaN(angulos[i])) continue;
            for (int c = 0; c < 4; c++)
                if (!usados[c]) { usados[c] = true; angulos[i] = c * 90f; break; }
        }

        // Reemplazar marcas anteriores.
        Transform viejo = raiz.Find("Marcas Caminos");
        if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);

        var contenedor = new GameObject("Marcas Caminos");
        Undo.RegisterCreatedObjectUndo(contenedor, "Crear marcas de caminos");
        contenedor.transform.SetParent(raiz, false);
        contenedor.transform.position = centro;
        contenedor.transform.localRotation = Quaternion.identity;

        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/MarcaPiso.mat");

        for (int i = 0; i < defs.Length; i++)
        {
            var go = new GameObject(defs[i].nombre);
            Undo.RegisterCreatedObjectUndo(go, "Crear marca");
            go.transform.SetParent(contenedor.transform, false);

            var m = go.AddComponent<MarcaCamino>();
            m.zonaDestino = zonas[i];
            m.nombreDestino = defs[i].destino;
            m.color = defs[i].color;
            m.intensidad = defs[i].intensidad;
            m.anguloSalida = angulos[i];
            m.radioAnillo = Mathf.Clamp(mitadPiso * 0.4f, 2.5f, 8f);
            m.largo = Mathf.Max(m.radioAnillo + 1f, mitadPiso - 0.6f);
            m.material = mat;
        }

        if (seleccionar) Selection.activeGameObject = contenedor;
        EditorSceneManager.MarkSceneDirty(zp.scene);
        Debug.Log("[Zona Principal] Marcas creadas en \"Marcas Caminos\". Cada una se enciende/apaga con MarcaCamino.Encender()/Apagar().", contenedor);
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;
}
