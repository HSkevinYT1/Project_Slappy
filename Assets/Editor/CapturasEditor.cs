using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Herramienta de revision: saca capturas de la escena desde puntos dados y vuelca datos de geometria
// a la carpeta Temp/Capturas del proyecto (no se guarda nada en la escena).
// Uso: Tools > Capturas > Tienda, o creando el archivo Temp/Capturas/pedido.txt
// (una linea por captura: nombre;px;py;pz;mirarX;mirarY;mirarZ;fov).
[InitializeOnLoad]
public static class CapturasEditor
{
    const string Carpeta = "Temp/Capturas";
    const string Pedido = Carpeta + "/pedido.txt";

    static CapturasEditor()
    {
        // Espera unos ciclos para correr despues de las herramientas que modifican la escena.
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
            EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (File.Exists(Pedido)) ProcesarPedido();
        };
    }

    [MenuItem("Tools/Capturas/Procesar pedido")]
    public static void ProcesarPedido()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Pedido)) return;
        string[] lineas = File.ReadAllLines(Pedido);
        File.Delete(Pedido);
        int n = 0;
        foreach (string l in lineas)
        {
            if (l.Trim() == "volcar") { VolcarTienda(); continue; }
            if (l.Trim().StartsWith("volcar:")) { Volcar(l.Trim().Substring(7)); continue; }
            // medir:<ruta1>|<ruta2>...  -> Temp/Capturas/medidas.txt con jugadores (CharacterController) y limites de cada hijo
            // asset:<ruta> -> Temp/Capturas/asset.txt con las piezas (renderers, mallas, materiales) de un modelo/prefab
            if (l.Trim().StartsWith("asset:"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(l.Trim().Substring(6));
                var sba = new StringBuilder();
                if (go)
                {
                    var inst = (GameObject)Object.Instantiate(go);
                    foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
                    {
                        string linea = Ruta(t, inst.transform) + " escala=" + V(t.localScale);
                        if (t.TryGetComponent(out Renderer r))
                        {
                            Mesh m = r is SkinnedMeshRenderer sk ? sk.sharedMesh : (t.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null);
                            linea += " R=" + r.GetType().Name + " bounds=" + V(r.bounds.size) + " centro=" + V(r.bounds.center) + (m ? " malla=" + m.name + " sub=" + m.subMeshCount + " verts=" + m.vertexCount : "");
                            foreach (Material mt in r.sharedMaterials) linea += " [" + (mt ? mt.name : "null") + "]";
                        }
                        foreach (var c in t.GetComponents<Component>()) if (!(c is Transform) && !(c is Renderer) && !(c is MeshFilter)) linea += " <" + c.GetType().Name + ">";
                        sba.AppendLine(linea);
                    }
                    Object.DestroyImmediate(inst);
                }
                Directory.CreateDirectory(Carpeta);
                File.WriteAllText(Path.Combine(Carpeta, "asset.txt"), sba.ToString());
                continue;
            }
            // dentro:minx,minz,maxx,maxz -> objetos (con renderer, collider o componentes) en esa area que NO cuelgan de "Laberinto"
            if (l.Trim().StartsWith("dentro:"))
            {
                string[] q = l.Trim().Substring(7).Split(',');
                float F2(int i) => float.Parse(q[i], System.Globalization.CultureInfo.InvariantCulture);
                Rect area = Rect.MinMaxRect(F2(0), F2(1), F2(2), F2(3));
                GameObject labG = GameObject.Find("Laberinto");
                var sbd = new StringBuilder();
                foreach (GameObject raiz in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                    {
                        if (labG && t.IsChildOf(labG.transform)) continue;
                        if (!area.Contains(new Vector2(t.position.x, t.position.z))) continue;
                        // solo el objeto mas alto que este dentro (no cada hijo)
                        if (t.parent && area.Contains(new Vector2(t.parent.position.x, t.parent.position.z)) && t.parent.parent != null) continue;
                        string comps = "";
                        foreach (var c in t.GetComponents<Component>()) if (!(c is Transform)) comps += c.GetType().Name + " ";
                        string ruta = t.name; for (Transform pp = t.parent; pp != null; pp = pp.parent) ruta = pp.name + "/" + ruta;
                        sbd.AppendLine(ruta + " pos=" + V(t.position) + " activo=" + t.gameObject.activeInHierarchy + " hijos=" + t.childCount + " [" + comps.Trim() + "]");
                    }
                Directory.CreateDirectory(Carpeta);
                File.WriteAllText(Path.Combine(Carpeta, "dentro.txt"), sbd.ToString());
                continue;
            }
            if (l.Trim().StartsWith("medir:")) { Medir(l.Trim().Substring(6).Split('|')); continue; }
            // raices -> Temp/Capturas/raices.txt con los limites (renderers) de cada objeto raiz de la escena
            if (l.Trim() == "raices")
            {
                var sbr = new StringBuilder();
                foreach (GameObject g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    Bounds b = default; bool hay = false;
                    foreach (Renderer r in g.GetComponentsInChildren<Renderer>())
                    { if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds); }
                    sbr.AppendLine(hay ? $"{g.name} min={V(b.min)} max={V(b.max)}" : $"{g.name} (sin renderers) pos={V(g.transform.position)}");
                }
                Directory.CreateDirectory(Carpeta);
                File.WriteAllText(Path.Combine(Carpeta, "raices.txt"), sbr.ToString());
                continue;
            }
            // prefab:<ruta>;<nombre>;<giro>;<inclinacion>;<distancia en tamanos>  -> foto del prefab aislado
            if (l.Trim().StartsWith("prefab:"))
            {
                string[] q = l.Trim().Substring(7).Split(';');
                float G(int i) => float.Parse(q[i], System.Globalization.CultureInfo.InvariantCulture);
                CapturarPrefab(q[0], q[1], G(2), G(3), G(4));
                n++;
                continue;
            }
            // colorMaterial:<prefijo de nombre>:r,g,b  -> cambia _BaseColor de los materiales de la escena que empiecen asi
            if (l.Trim().StartsWith("colorMaterial:"))
            {
                string[] c = l.Trim().Split(':');
                string[] rgb = c[2].Split(',');
                var col = new Color(float.Parse(rgb[0], System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(rgb[1], System.Globalization.CultureInfo.InvariantCulture),
                                    float.Parse(rgb[2], System.Globalization.CultureInfo.InvariantCulture));
                var hechos = new System.Collections.Generic.HashSet<Material>();
                foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    foreach (Material m in r.sharedMaterials)
                        if (m && m.name.StartsWith(c[1]) && hechos.Add(m)) { m.SetColor("_BaseColor", col); EditorUtility.SetDirty(m); }
                continue;
            }
            string[] p = l.Split(';');
            if (p.Length < 8) continue;
            float F(int i) => float.Parse(p[i], System.Globalization.CultureInfo.InvariantCulture);
            Capturar(p[0], new Vector3(F(1), F(2), F(3)), new Vector3(F(4), F(5), F(6)), F(7));
            n++;
        }
        File.WriteAllText(Carpeta + "/listo.txt", n + " capturas " + System.DateTime.Now.ToString("HH:mm:ss"));
        Debug.Log($"[Capturas] {n} capturas en {Carpeta}");
    }

    public static void Capturar(string nombre, Vector3 pos, Vector3 mirar, float fov, int ancho = 1280, int alto = 720)
    {
        Directory.CreateDirectory(Carpeta);
        var go = new GameObject("_CamaraCaptura") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.transform.position = pos;
        cam.transform.LookAt(mirar);
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 300f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);

        // Luz de apoyo para que se vea el interior (solo durante la captura).
        // Si el nombre termina en "_real" no se agrega (se ve la iluminacion real de la escena).
        var luzGo = new GameObject("_LuzCaptura") { hideFlags = HideFlags.HideAndDontSave };
        luzGo.transform.position = pos;
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.range = 25f;
        luz.intensity = 6f;
        luz.shadows = LightShadows.None;
        luz.enabled = !nombre.EndsWith("_real");

        var rt = new RenderTexture(ancho, alto, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(Carpeta, nombre + ".png"), tex.EncodeToPNG());

        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(luzGo);
    }

    // Foto de un prefab solo, en una escena de vista previa con dos luces.
    public static void CapturarPrefab(string ruta, string nombre, float giro, float inclinacion, float distancia, int ancho = 1280, int alto = 720)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        // Si el prefab esta abierto en modo prefab, se fotografia lo abierto (incluye cambios sin guardar).
        var etapa = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (etapa != null && etapa.assetPath == ruta) asset = etapa.prefabContentsRoot;
        if (!asset) return;
        Directory.CreateDirectory(Carpeta);
        var escena = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var inst = (GameObject)Object.Instantiate(asset);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(inst, escena);
        Bounds b = default; bool hay = false;
        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>()) { if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds); }

        var camGo = new GameObject("_Cam");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, escena);
        var cam = camGo.AddComponent<Camera>();
        cam.scene = escena;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
        cam.fieldOfView = 40f;
        Quaternion rot = Quaternion.Euler(inclinacion, giro, 0f);
        cam.transform.position = b.center - rot * Vector3.forward * (b.extents.magnitude * distancia);
        cam.transform.LookAt(b.center);

        foreach (var (dir, inten, col) in new[] { (new Vector3(35f, giro - 40f, 0f), 1.3f, new Color(1f, 0.95f, 0.88f)), (new Vector3(15f, giro + 130f, 0f), 0.5f, new Color(0.7f, 0.8f, 1f)) })
        {
            var lg = new GameObject("_Luz");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lg, escena);
            lg.transform.rotation = Quaternion.Euler(dir);
            var luz = lg.AddComponent<Light>();
            luz.type = LightType.Directional; luz.intensity = inten; luz.color = col; luz.shadows = LightShadows.Soft;
        }

        var rt = new RenderTexture(ancho, alto, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(Path.Combine(Carpeta, nombre + ".png"), tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(escena);
    }

    // Vuelca la geometria de la tienda (estantes, mesas, paredes) para planificar la decoracion.
    [MenuItem("Tools/Capturas/Volcar geometria de la Tienda")]
    public static void VolcarTienda() => Volcar("Tienda");

    // Vuelca la geometria de cualquier objeto raiz (por nombre) a Temp/Capturas/<nombre>_geometria.txt
    public static void Volcar(string nombre)
    {
        GameObject tienda = GameObject.Find(nombre);
        if (!tienda) return;
        Directory.CreateDirectory(Carpeta);
        var sb = new StringBuilder();
        foreach (Transform t in tienda.GetComponentsInChildren<Transform>(true))
        {
            var r = t.GetComponent<Renderer>();
            string ruta = Ruta(t, tienda.transform);
            if (r == null)
            {
                if (t.childCount > 0) sb.AppendLine($"G {ruta} pos={V(t.position)} rot={V(t.eulerAngles)}");
                continue;
            }
            Bounds b = r.bounds;
            var mats = new System.Collections.Generic.List<string>();
            foreach (Material m in r.sharedMaterials) mats.Add(m ? m.name + "[" + (m.shader ? m.shader.name : "?") + (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") ? ",tex" : "") + (m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") ? ",nrm" : "") + "]" : "null");
            sb.AppendLine($"R {ruta} min={V(b.min)} max={V(b.max)} col={(t.GetComponent<Collider>() ? 1 : 0)} activo={(t.gameObject.activeInHierarchy ? 1 : 0)} mat={string.Join("|", mats)}");
        }
        File.WriteAllText(Path.Combine(Carpeta, nombre.ToLowerInvariant() + "_geometria.txt"), sb.ToString());
    }

    public static void Medir(string[] rutas)
    {
        Directory.CreateDirectory(Carpeta);
        var sb = new StringBuilder();
        foreach (var cc in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Vector3 s = cc.transform.lossyScale;
            sb.AppendLine($"JUGADOR {cc.name} pos={V(cc.transform.position)} escala={V(s)} alto={cc.height * s.y:0.00} radio={cc.radius * Mathf.Max(s.x, s.z):0.00} bounds={V(cc.bounds.min)}-{V(cc.bounds.max)}");
            var cam = cc.GetComponentInChildren<Camera>(true);
            if (cam) sb.AppendLine($"  camara {cam.name} y={cam.transform.position.y:0.00} (ojos a {cam.transform.position.y - cc.bounds.min.y:0.00} del piso)");
        }
        foreach (var pm in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            sb.AppendLine($"PLAYERMOVEMENT {pm.name} pos={V(pm.transform.position)} escala={V(pm.transform.lossyScale)} activo={pm.gameObject.activeInHierarchy}");
            foreach (var c in pm.GetComponentsInChildren<Collider>(true))
            {
                string extra = c is CapsuleCollider cap ? $" capsula alto={cap.height * c.transform.lossyScale.y:0.00} radio={cap.radius * c.transform.lossyScale.x:0.00}" : "";
                sb.AppendLine($"  collider {c.name} {c.GetType().Name} bounds={V(c.bounds.min)}-{V(c.bounds.max)}{extra}");
            }
            foreach (var cam in pm.transform.root.GetComponentsInChildren<Camera>(true))
                sb.AppendLine($"  camara {cam.name} y={cam.transform.position.y:0.00} (sobre pies: {cam.transform.position.y - pm.transform.position.y:0.00})");
        }
        foreach (string r in rutas)
        {
            GameObject g = GameObject.Find(r.Trim());
            if (!g) { sb.AppendLine("NO ENCONTRADO " + r); continue; }
            sb.AppendLine($"OBJETO {r} pos={V(g.transform.position)} rot={V(g.transform.eulerAngles)} escalaLocal={V(g.transform.localScale)} escala={V(g.transform.lossyScale)}");
            foreach (Transform t in g.GetComponentsInChildren<Transform>(true))
            {
                string linea = $"  {Ruta(t, g.transform)} pos={V(t.position)} rotY={t.eulerAngles.y:0} escalaLocal={V(t.localScale)}";
                if (t.TryGetComponent(out Renderer rr)) linea += $" R={V(rr.bounds.min)}-{V(rr.bounds.max)} tam={V(rr.bounds.size)}";
                foreach (var c in t.GetComponents<Collider>()) linea += $" C[{c.GetType().Name}{(c.isTrigger ? ",trigger" : "")}]={V(c.bounds.size)}";
                foreach (var c in t.GetComponents<MonoBehaviour>()) if (c) linea += $" <{c.GetType().Name}>";
                sb.AppendLine(linea);
            }
        }
        File.WriteAllText(Path.Combine(Carpeta, "medidas.txt"), sb.ToString());
    }

    static string Ruta(Transform t, Transform raiz) => t == raiz ? t.name : Ruta(t.parent, raiz) + "/" + t.name;
    static string V(Vector3 v) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:0.00},{1:0.00},{2:0.00})", v.x, v.y, v.z);
}



























































