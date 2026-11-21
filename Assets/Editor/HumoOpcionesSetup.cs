using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Opciones de humo negro (shader Slappy/HumoNegro) para las "sombra ..." de la entrada de cada pasillo.
// Crea Humo_Opcion1..5 en Assets/Pasillos/Humo. Revision: Temp/Capturas/humo_fotos.txt -> fotos de cada opcion
// puesta un momento en las 4 sombras (se restaura el material original; no guarda nada en la escena).
[InitializeOnLoad]
public static class HumoOpcionesSetup
{
    const string Carpeta = "Assets/Pasillos/Humo";
    static readonly string[] Sombras = { "sombra cloacas", "sombra backrooms", "sombra laberinto", "sombra parte roja" };

    static HumoOpcionesSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists("Temp/Capturas/humo_aplicar.txt")) { File.Delete("Temp/Capturas/humo_aplicar.txt"); AplicarElegido(); }
            if (File.Exists("Temp/Capturas/humo_fotos.txt")) { File.Delete("Temp/Capturas/humo_fotos.txt"); Crear(); soloActual = false; Fotos(); }
            if (File.Exists("Temp/Capturas/humo_actual.txt")) { File.Delete("Temp/Capturas/humo_actual.txt"); soloActual = true; Fotos(); }
        };
    }

    struct Op { public string nombre; public Color color, claro, brillo; public float densidad, nucleo, escala, remolino, velRem, estirar, capas, contraste, bordes, cantBrillo; public Vector2 vel; }

    static readonly Op[] Opciones =
    {
        // 1. Cortina densa: muro negro que se arremolina despacio, volutas gris oscuro
        new Op { nombre = "1 Cortina densa", color = new Color(0.004f, 0.004f, 0.005f), claro = new Color(0.09f, 0.09f, 0.095f), densidad = 2f, nucleo = 1f, escala = 0.55f, remolino = 0.8f, velRem = 0.15f, estirar = 1f, capas = 0.4f, contraste = 1.6f, bordes = 0.06f, vel = new Vector2(0.05f, 0.12f) },
        // 2. Humo que sube: volutas estiradas que suben rapido y se deshacen arriba
        new Op { nombre = "2 Humo que sube", color = new Color(0.003f, 0.003f, 0.004f), claro = new Color(0.16f, 0.16f, 0.16f), densidad = 2f, nucleo = 1f, escala = 0.8f, remolino = 0.6f, velRem = 0.3f, estirar = 2.2f, capas = 0.6f, contraste = 2.4f, bordes = 0.16f, vel = new Vector2(0f, 0.6f) },
        // 3. Remolino: el humo gira y se retuerce
        new Op { nombre = "3 Remolino", color = new Color(0.004f, 0.003f, 0.006f), claro = new Color(0.13f, 0.12f, 0.15f), densidad = 2f, nucleo = 1f, escala = 0.5f, remolino = 2.4f, velRem = 0.45f, estirar = 1f, capas = 0.5f, contraste = 2f, bordes = 0.1f, vel = new Vector2(0.15f, 0.05f) },
        // 4. Niebla profunda: dos capas que se cruzan, suave, bordes muy deshechos
        new Op { nombre = "4 Niebla profunda", color = new Color(0.006f, 0.008f, 0.012f), claro = new Color(0.07f, 0.08f, 0.1f), densidad = 2f, nucleo = 1f, escala = 0.35f, remolino = 1f, velRem = 0.1f, estirar = 1.3f, capas = 1f, contraste = 1f, bordes = 0.28f, vel = new Vector2(-0.1f, 0.05f) },
        // 5. Humo con brasas: negro con venas que brillan rojo apagado
        new Op { nombre = "5 Humo con brasas", color = new Color(0.004f, 0.003f, 0.003f), claro = new Color(0.1f, 0.07f, 0.065f), brillo = new Color(1f, 0.2f, 0.05f), cantBrillo = 1.4f, densidad = 2f, nucleo = 1f, escala = 0.6f, remolino = 1.4f, velRem = 0.25f, estirar = 1.2f, capas = 0.5f, contraste = 1.8f, bordes = 0.1f, vel = new Vector2(0.05f, 0.3f) },
    };

    [MenuItem("Tools/Pasillos/Opciones de humo (crear)")]
    static void Crear()
    {
        Directory.CreateDirectory(Carpeta);
        Shader sh = Shader.Find("Slappy/HumoNegro");
        for (int i = 0; i < Opciones.Length; i++)
        {
            Op o = Opciones[i];
            string ruta = $"{Carpeta}/Humo_Opcion{i + 1}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, ruta); }
            m.shader = sh;
            m.SetColor("_Color", o.color); m.SetColor("_ColorClaro", o.claro); m.SetColor("_BrilloColor", o.brillo);
            m.SetFloat("_Brillo", o.cantBrillo); m.SetFloat("_Densidad", o.densidad); m.SetFloat("_Nucleo", o.nucleo);
            m.SetFloat("_Escala", o.escala); m.SetFloat("_Remolino", o.remolino); m.SetFloat("_VelRemolino", o.velRem);
            m.SetFloat("_Estirar", o.estirar); m.SetFloat("_Capas", o.capas); m.SetFloat("_Contraste", o.contraste);
            m.SetFloat("_Bordes", o.bordes); m.SetVector("_Velocidad", o.vel);
            m.renderQueue = 3000;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
    }

    // Opcion elegida: la 2 (humo que sube) en las 4 sombras de los pasillos
    const int Elegida = 2;

    // Volumen: capas de humo semitransparentes a lo largo del pasillo (se ve con profundidad de lado),
    // mas anchas y altas que la abertura para que los bordes queden dentro de paredes, piso y techo
    const int CapasN = 10;
    const float Fondo = 3f, AnchoExtra = 2.4f, AltoExtra = 1f;
    const string NombreVolumen = "Humo Volumen";

    [MenuItem("Tools/Pasillos/Aplicar humo a las sombras")]
    static void AplicarElegido()
    {
        var baseMat = AssetDatabase.LoadAssetAtPath<Material>($"{Carpeta}/Humo_Opcion{Elegida}.mat");
        if (!baseMat) { Crear(); baseMat = AssetDatabase.LoadAssetAtPath<Material>($"{Carpeta}/Humo_Opcion{Elegida}.mat"); }
        // Material de las capas: el elegido, pero cada capa deja pasar algo (juntas tapan del todo)
        string rutaCapas = $"{Carpeta}/Humo_Capas.mat";
        var capas = AssetDatabase.LoadAssetAtPath<Material>(rutaCapas);
        if (!capas) { capas = new Material(baseMat); AssetDatabase.CreateAsset(capas, rutaCapas); }
        capas.CopyPropertiesFromMaterial(baseMat);
        capas.SetFloat("_Nucleo", 0f); // capas apagadas: con humo semitransparente encima el fondo se veia gris (pendiente)
        capas.SetFloat("_Densidad", 0f);
        capas.SetFloat("_Bordes", 0.14f);
        capas.SetFloat("_Profundidad", 0f);
        capas.SetFloat("_Rasante", 0f);
        // Juntas, las volutas claras de cada capa se suman: aqui van mucho mas oscuras
        capas.SetColor("_Color", new Color(0.002f, 0.002f, 0.0025f));
        capas.SetColor("_ColorClaro", new Color(0.012f, 0.012f, 0.013f));
        capas.SetFloat("_Contraste", 1.8f);
        capas.SetFloat("_ZWrite", 0f);
        capas.renderQueue = 3101; // despues de la niebla de las zonas (FogVolume, cola 3000), que si no lo pinta de gris
        EditorUtility.SetDirty(capas);
        // Fondo del volumen: la ultima capa es totalmente opaca (nunca se ve el pasillo a traves)
        string rutaFondo = $"{Carpeta}/Humo_Fondo.mat";
        var fondo = AssetDatabase.LoadAssetAtPath<Material>(rutaFondo);
        if (!fondo) { fondo = new Material(capas); AssetDatabase.CreateAsset(fondo, rutaFondo); }
        fondo.CopyPropertiesFromMaterial(capas);
        fondo.SetFloat("_Nucleo", 1f); fondo.SetFloat("_Densidad", 2f);
        fondo.SetFloat("_Rasante", 0f);
        fondo.SetFloat("_ZWrite", 1f);
        fondo.renderQueue = 2450; // se dibuja con lo opaco y escribe profundidad: los efectos de pantalla lo ven como pared
        EditorUtility.SetDirty(fondo);
        AssetDatabase.SaveAssets();

        int n = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!r || System.Array.IndexOf(Sombras, r.name) < 0) continue;
            Transform s = r.transform;
            Undo.RecordObject(r, "Humo en las sombras");
            r.sharedMaterials = new[] { baseMat };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.enabled = false; // el volumen de capas reemplaza al bloque (sigue el collider y la puerta)

            Transform viejo = s.Find(NombreVolumen);
            if (viejo) Undo.DestroyObjectImmediate(viejo.gameObject);
            var vol = new GameObject(NombreVolumen).transform;
            Undo.RegisterCreatedObjectUndo(vol.gameObject, "Humo en las sombras");
            vol.SetParent(s, false);
            Vector3 ls = s.localScale;
            vol.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z); // dentro del volumen todo en metros
            float ancho = ls.x + AnchoExtra, alto = ls.y + AltoExtra;
            // Todo el volumen dentro del pasillo (hacia donde esta su piso), desde un poco detras de la abertura
            Transform piso = s.parent ? s.parent.Find("Piso") : null;
            float haciaDentro = piso && Vector3.Dot(piso.position - s.position, s.forward) < 0f ? -1f : 1f;
            float centroZ = haciaDentro * (Fondo * 0.5f + 0.7f);
            for (int i = 0; i < CapasN; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = "Capa " + (i + 1);
                q.transform.SetParent(vol, false);
                // La capa 1 es la de la abertura; la ultima, la del fondo
                float z = centroZ + haciaDentro * Mathf.Lerp(-Fondo * 0.5f, Fondo * 0.5f, (i + 0.5f) / CapasN);
                q.transform.localPosition = new Vector3(0f, 0f, z);
                q.transform.localScale = new Vector3(ancho, alto, 1f);
                var qr = q.GetComponent<MeshRenderer>();
                bool ultima = i == CapasN - 1;
                qr.sharedMaterial = ultima ? fondo : capas;
                qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                qr.receiveShadows = false;
            }
            EditorUtility.SetDirty(r);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(r.gameObject.scene);
            n++;
        }
        Debug.Log($"[Humo] Opcion {Elegida} (humo que sube) en {n} sombras: {CapasN} capas en {Fondo} m de fondo, bordes escondidos en paredes, piso y techo. Guarda la escena (Ctrl+S).");
    }

    static bool soloActual;

    static void Fotos()
    {
        var rs = new List<Renderer>();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (System.Array.IndexOf(Sombras, r.name) >= 0) rs.Add(r);
        if (rs.Count == 0) return;
        var orig = new Dictionary<Renderer, Material[]>();
        foreach (var r in rs) orig[r] = r.sharedMaterials;

        var go = new GameObject("camHumo") { hideFlags = HideFlags.HideAndDontSave };
        var c = go.AddComponent<Camera>();
        c.fieldOfView = 60f; c.nearClipPlane = 0.05f; c.farClipPlane = 300f;
        // Vista de cerca a la entrada de las Cloacas y vista general desde el centro de la estacion
        Renderer cl = rs.Find(r => r.name == "sombra cloacas");
        Vector3 centro = new Vector3(100f, 1.6f, -73f);
        try
        {
            for (int i = 0; i <= (soloActual ? 0 : Opciones.Length); i++)
            {
                if (i > 0)
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>($"{Carpeta}/Humo_Opcion{i}.mat");
                    foreach (var r in rs) r.sharedMaterials = new[] { m };
                }
                if (cl)
                {
                    Vector3 p = cl.transform.position; p.y = 1.6f;
                    go.transform.position = p + new Vector3(0f, 0f, -7f);
                    go.transform.LookAt(new Vector3(p.x, 1.9f, p.z));
                    Foto(c, $"humo_{i}_cerca");
                }
                if (cl)
                {
                    Vector3 p = cl.transform.position; p.y = 1.6f;
                    go.transform.position = p + new Vector3(-6f, 0f, -4f);
                    go.transform.LookAt(new Vector3(p.x, 1.6f, p.z));
                    Foto(c, $"humo_{i}_lado");
                    go.transform.position = p + new Vector3(3.5f, 0f, -2.5f);
                    go.transform.LookAt(new Vector3(p.x - 1f, 1.6f, p.z + 1f));
                    Foto(c, $"humo_{i}_rasante");
                }
                go.transform.position = centro + new Vector3(-6f, 0f, 6f);
                go.transform.rotation = Quaternion.Euler(5f, 120f, 0f);
                Foto(c, $"humo_{i}_estacion");
            }
        }
        finally
        {
            foreach (var kv in orig) kv.Key.sharedMaterials = kv.Value;
            Object.DestroyImmediate(go);
        }
    }

    static void Foto(Camera c, string n)
    {
        var rt = new RenderTexture(960, 540, 24); c.targetTexture = rt; c.Render(); RenderTexture.active = rt;
        var t = new Texture2D(960, 540); t.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); t.Apply();
        if (n.EndsWith("_cerca"))
        {
            var nieblas = new List<Renderer>();
            foreach (var f in Object.FindObjectsByType<FogVolume>(FindObjectsSortMode.None)) { var fr = f.GetComponent<Renderer>(); if (fr.enabled) { fr.enabled = false; nieblas.Add(fr); } }
            c.Render(); RenderTexture.active = rt;
            Texture2D t2 = new Texture2D(960, 540); t2.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); t2.Apply();
            File.WriteAllBytes($"Temp/Capturas/{n}_sinniebla.png", t2.EncodeToPNG());
            // Solo las capas, sobre blanco
            var capasR = new List<Renderer>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.name.StartsWith("Capa ")) capasR.Add(r);
            var capaOrig = new Dictionary<Renderer, int>();
            foreach (var r in capasR) { capaOrig[r] = r.gameObject.layer; r.gameObject.layer = 31; }
            int mask = c.cullingMask; var clear = c.clearFlags; var bg = c.backgroundColor;
            c.cullingMask = 1 << 31; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = Color.white;
            c.Render(); RenderTexture.active = rt;
            t2 = new Texture2D(960, 540); t2.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); t2.Apply();
            File.WriteAllBytes($"Temp/Capturas/{n}_solocapas.png", t2.EncodeToPNG());
            Object.DestroyImmediate(t2);
            c.cullingMask = mask; c.clearFlags = clear; c.backgroundColor = bg;
            foreach (var kv in capaOrig) kv.Key.gameObject.layer = kv.Value;
            Object.DestroyImmediate(t2);
            foreach (var fr in nieblas) fr.enabled = true;
        }
        File.WriteAllBytes($"Temp/Capturas/{n}.png", t.EncodeToPNG());
        if (n.EndsWith("_cerca"))
        {
            var sb = new System.Text.StringBuilder();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.name.StartsWith("Capa ") && r.transform.parent && r.transform.parent.parent && r.transform.parent.parent.name == "sombra cloacas")
                    sb.AppendLine(r.name + " en=" + r.enabled + " mat=" + (r.sharedMaterial ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name + " q=" + r.sharedMaterial.renderQueue : "null") + " pos=" + r.transform.position.ToString("F2") + " capa=" + r.gameObject.layer);
            foreach (var f in Object.FindObjectsByType<FogVolume>(FindObjectsSortMode.None))
            {
                var fr = f.GetComponent<Renderer>();
                sb.AppendLine("niebla " + f.name + " bounds=" + fr.bounds.center.ToString("F0") + " " + fr.bounds.size.ToString("F0") + " cola=" + (fr.sharedMaterial ? fr.sharedMaterial.renderQueue : -1) + " en=" + fr.enabled);
            }
            var zona = new Bounds(new Vector3(100f, 2.5f, -61f), new Vector3(14f, 6f, 14f));
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.bounds.Intersects(zona) || r.name.StartsWith("Capa ")) continue;
                foreach (var m in r.sharedMaterials)
                    if (m && m.renderQueue > 2500)
                    {
                        string ruta = r.name; for (var t2 = r.transform.parent; t2; t2 = t2.parent) ruta = t2.name + "/" + ruta;
                        sb.AppendLine("transparente " + ruta + " mat=" + m.name + " shader=" + m.shader.name + " cola=" + m.renderQueue + " bounds=" + r.bounds.center.ToString("F1") + " " + r.bounds.size.ToString("F1"));
                    }
            }
            File.WriteAllText("Temp/Capturas/humo_capas.txt", sb.ToString());
        }
        RenderTexture.active = null; c.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(t);
    }
}
// 
// 
