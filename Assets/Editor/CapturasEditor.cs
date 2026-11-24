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
            // pose:<ruta>;<pose>;<t 0-1>;<nombre>;<giro>;<inclinacion>;<distancia m>;<altura a mirar m>;<oscuro 0/1>
            // -> foto del prefab con esa pose animada (AnimacionesModelo.Previsualizar, pies en el suelo incluidos), sobre un
            //    piso donde pisa y con bloom. giro 180 = de frente. oscuro = solo una linterna (para ver lo que brilla)
            if (l.Trim().StartsWith("pose:"))
            {
                string[] q = l.Trim().Substring(5).Split(';');
                float G(int i) => float.Parse(q[i], System.Globalization.CultureInfo.InvariantCulture);
                CapturarPose(q[0], q[1], G(2), q[3], G(4), G(5), G(6), G(7), q.Length > 8 && q[8].Trim() == "1");
                n++;
                continue;
            }
            // pasos:<ruta>;<velocidad m/s>;<nombre>  -> simula 6 s caminando hacia adelante (sin Play) con AnimacionesModelo,
            // apunta en Temp/Capturas/<nombre>.txt cuanto resbala el pie apoyado y saca 4 fotos de perfil de un paso
            if (l.Trim().StartsWith("pasos:"))
            {
                string[] q = l.Trim().Substring(6).Split(';');
                SimularPasos(q[0], float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture), q[2]);
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

    // Foto de un prefab animado (AnimacionesModelo) en una pose y momento dados, en una escena de vista previa.
    public static void CapturarPose(string ruta, string pose, float t, string nombre, float giro, float inclinacion,
        float distancia, float alturaMirar, bool oscuro, int ancho = 1280, int alto = 720)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (!asset) { Debug.LogWarning("[Capturas] No encuentro " + ruta); return; }
        Directory.CreateDirectory(Carpeta);
        var escena = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var tirar = new System.Collections.Generic.List<Object>();
        GameObject Nuevo(string n)
        {
            var g = new GameObject(n);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, escena);
            return g;
        }
        var inst = (GameObject)Object.Instantiate(asset);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(inst, escena);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var anim = inst.GetComponent<AnimacionesModelo>();
        try
        {
            if (anim) anim.Previsualizar(pose, t);
            var ag = inst.GetComponent<UnityEngine.AI.NavMeshAgent>();
            float pisoY = ag ? -ag.baseOffset : 0f;

            // Piso (para ver que los pies lo tocan y no lo atraviesan)
            var piso = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(piso, escena);
            Object.DestroyImmediate(piso.GetComponent<Collider>());
            piso.transform.SetPositionAndRotation(new Vector3(0f, pisoY, 0f), Quaternion.Euler(90f, 0f, 0f));
            piso.transform.localScale = Vector3.one * 8f;
            var matPiso = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matPiso.SetColor("_BaseColor", new Color(0.23f, 0.22f, 0.21f));
            matPiso.SetFloat("_Smoothness", 0.15f);
            piso.GetComponent<Renderer>().sharedMaterial = matPiso;
            tirar.Add(matPiso);

            var cam = Nuevo("_Cam").AddComponent<Camera>();
            cam.scene = escena;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = oscuro ? new Color(0.01f, 0.01f, 0.015f) : new Color(0.12f, 0.12f, 0.14f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            Vector3 mirar = new Vector3(0f, pisoY + alturaMirar, 0f);
            Quaternion rot = Quaternion.Euler(inclinacion, giro, 0f);
            cam.transform.position = mirar - rot * Vector3.forward * distancia;
            cam.transform.LookAt(mirar);
            var datos = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            datos.renderPostProcessing = true;
            datos.volumeLayerMask = ~0;
            datos.volumeTrigger = cam.transform;

            // Bloom (como en el laberinto) para que se vea el brillo
            var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            tirar.Add(perfil);
            var bloom = perfil.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.threshold.Override(0.8f);
            bloom.intensity.Override(1.2f);
            bloom.scatter.Override(0.7f);
            var vol = Nuevo("_Vol").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1000f;
            vol.sharedProfile = perfil;

            if (oscuro)
            {
                // Solo una linterna desde la camara, tenue
                var lg = Nuevo("_Linterna");
                lg.transform.SetPositionAndRotation(cam.transform.position + cam.transform.right * 0.25f, cam.transform.rotation);
                var luz = lg.AddComponent<Light>();
                luz.type = LightType.Spot; luz.spotAngle = 45f; luz.range = 15f; luz.intensity = 2.2f;
                luz.color = new Color(1f, 0.95f, 0.85f); luz.shadows = LightShadows.Soft;
            }
            else
            {
                foreach (var (dir, inten, col) in new[] { (new Vector3(35f, giro - 40f, 0f), 1.3f, new Color(1f, 0.95f, 0.88f)), (new Vector3(15f, giro + 130f, 0f), 0.5f, new Color(0.7f, 0.8f, 1f)) })
                {
                    var lg = Nuevo("_Luz");
                    lg.transform.rotation = Quaternion.Euler(dir);
                    var luz = lg.AddComponent<Light>();
                    luz.type = LightType.Directional; luz.intensity = inten; luz.color = col; luz.shadows = LightShadows.Soft;
                }
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
        }
        finally
        {
            if (anim) anim.Liberar();
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(escena);
            foreach (Object o in tirar) if (o) Object.DestroyImmediate(o);
        }
    }

    public static void SimularPasos(string ruta, float velocidad, string nombre)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (!asset) return;
        Directory.CreateDirectory(Carpeta);
        var escena = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var inst = (GameObject)Object.Instantiate(asset);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(inst, escena);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var anim = inst.GetComponent<AnimacionesModelo>();
        var sb = new StringBuilder();
        var tirar = new System.Collections.Generic.List<Object>();
        try
        {
            if (!anim) return;
            anim.Preparar();
            // Varias fotos en el mismo cuadro del editor: sin esto la malla con huesos se queda con la deformacion de la primera
            foreach (SkinnedMeshRenderer s in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s.forceMatrixRecalculationPerRender = true;
            var pose = anim.Buscar(AnimacionesModelo.Caminar);
            var piernas = pose != null ? pose.piernas : null;
            sb.AppendLine($"velocidad del cuerpo: {velocidad:0.00} m/s; pasos con IK: {(piernas != null ? "si" : "NO")}; paso max {(pose != null ? pose.pasoMax : 0f):0.00} m");

            // Escena para las fotos: piso, luces y camara de perfil que sigue al personaje
            var ag = inst.GetComponent<UnityEngine.AI.NavMeshAgent>();
            float pisoY = ag ? -ag.baseOffset : 0f;
            var piso = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(piso, escena);
            Object.DestroyImmediate(piso.GetComponent<Collider>());
            piso.transform.SetPositionAndRotation(new Vector3(0f, pisoY, 10f), Quaternion.Euler(90f, 0f, 0f));
            piso.transform.localScale = new Vector3(6f, 30f, 1f);
            var matPiso = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            tirar.Add(matPiso);
            piso.GetComponent<Renderer>().sharedMaterial = matPiso;
            // Marcas en el piso cada 0.5 m para ver si el pie resbala
            for (int m = 0; m < 30; m++)
            {
                var marca = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(marca, escena);
                Object.DestroyImmediate(marca.GetComponent<Collider>());
                marca.transform.position = new Vector3(0.9f, pisoY + 0.005f, m * 0.5f);
                marca.transform.localScale = new Vector3(0.6f, 0.01f, 0.03f);
            }
            var camGo = new GameObject("_Cam");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, escena);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = escena;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            cam.fieldOfView = 40f;
            foreach (var (dir, inten) in new[] { (new Vector3(40f, 60f, 0f), 1.3f), (new Vector3(20f, -120f, 0f), 0.5f) })
            {
                var lg = new GameObject("_Luz");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lg, escena);
                lg.transform.rotation = Quaternion.Euler(dir);
                var luz = lg.AddComponent<Light>();
                luz.type = LightType.Directional; luz.intensity = inten; luz.shadows = LightShadows.Soft;
            }

            const float dt = 1f / 60f;
            int cuadros = 360;
            float largo = 0f;
            if (pose != null) foreach (AnimationClip c in pose.clips) if (c) largo = Mathf.Max(largo, c.length);
            float resbala = 0f; int nApoyo = 0;
            Vector3[] antes = new Vector3[2];
            int foto = 0, proxFoto = 240;
            for (int f = 0; f < cuadros; f++)
            {
                inst.transform.position += Vector3.forward * velocidad * dt;
                anim.Paso(dt);
                if (piernas == null) continue;
                int apoyada = piernas[0].punta.position.y < piernas[1].punta.position.y ? 0 : 1;
                if (f > 90)
                {
                    Vector3 d = piernas[apoyada].punta.position - antes[apoyada];
                    d.y = 0f;
                    resbala += d.magnitude / dt;
                    nApoyo++;
                }
                antes[0] = piernas[0].punta.position;
                antes[1] = piernas[1].punta.position;
                if (f == proxFoto && foto < 4)
                {
                    Vector3 mirar = new Vector3(0f, pisoY + 1.2f, inst.transform.position.z);
                    cam.transform.position = mirar + new Vector3(-5.5f, 0.4f, 0f);
                    cam.transform.LookAt(mirar);
                    var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    cam.targetTexture = rt;
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(Carpeta, nombre + "_" + foto + ".png"), tex.EncodeToPNG());
                    cam.targetTexture = null;
                    Object.DestroyImmediate(tex);
                    Object.DestroyImmediate(rt);
                    foto++;
                    proxFoto += 15;
                }
            }
            sb.AppendLine($"el pie apoyado se mueve en promedio {(nApoyo > 0 ? resbala / nApoyo : -1f):0.000} m/s (0 = no resbala; sin IK resbalaria {velocidad:0.00})");
        }
        finally
        {
            File.WriteAllText(Path.Combine(Carpeta, nombre + ".txt"), sb.ToString());
            if (anim) anim.Liberar();
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(escena);
            foreach (Object o in tirar) if (o) Object.DestroyImmediate(o);
        }
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
































































