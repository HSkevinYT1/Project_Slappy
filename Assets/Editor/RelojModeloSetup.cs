using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Pone el modelo "reloj de pulsera" (Assets/Models/Player/reloj) en la muneca izquierda de los brazos del jugador,
// como hijo del hueso Forearm.L, con las agujas y la esfera separadas para que RelojJugador las mueva.
// Se aplica solo si falta (prefab Player y jugador de la escena). Rehacer: Tools > Jugador > Reloj 3D en la muneca.
[InitializeOnLoad]
public static class RelojModeloSetup
{
    const string RutaPlayer = "Assets/Prefabs/Player.prefab";
    const string RutaModelo = "Assets/Models/Player/reloj/reloj de pulsera.obj";
    const string CarpetaMat = "Assets/Models/Player/reloj/Materiales";
    const string Antebrazo = "Forearm.L", Muneca = "wrist.L";

    static RelojModeloSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var pm = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            if (pm && !Buscar(pm.transform.root, RelojJugador.NombreReloj + " v4")) Aplicar(true);
            if (File.Exists("Temp/Capturas/reloj3d_fotos.txt")) { var lin = File.ReadAllLines("Temp/Capturas/reloj3d_fotos.txt"); File.Delete("Temp/Capturas/reloj3d_fotos.txt"); Fotos(); foreach (var l in lin) Variante(l); }
        };
    }

    [MenuItem("Tools/Jugador/Reloj 3D en la muneca")]
    static void Rehacer() => Aplicar(true);

    static void Aplicar(bool rehacer)
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaModelo);
        if (!modelo) { Debug.LogWarning("[Reloj 3D] No esta el modelo " + RutaModelo); return; }
        var mats = Materiales();
        string informe = "";

        // Prefab del jugador
        GameObject raiz = PrefabUtility.LoadPrefabContents(RutaPlayer);
        try
        {
            if (Montar(raiz.transform, modelo, mats, rehacer, false, out string inf)) { PrefabUtility.SaveAsPrefabAsset(raiz, RutaPlayer); informe += "prefab: " + inf; }
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }

        // Jugador de la escena (si no lo recibio del prefab)
        var pm = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        if (pm)
        {
            Transform jug = pm.transform.root;
            if (Montar(jug, modelo, mats, rehacer, true, out string inf))
            {
                EditorSceneManager.MarkSceneDirty(jug.gameObject.scene);
                informe += " | escena: " + inf;
            }
        }
        Debug.Log("[Reloj 3D] " + (informe == "" ? "ya estaba" : informe) + ". Guarda la escena (Ctrl+S).");
    }

    // Monta el reloj bajo Forearm.L. Devuelve true si cambio algo
    static bool Montar(Transform raiz, GameObject modelo, Dictionary<string, Material> mats, bool rehacer, bool conUndo, out string informe)
    {
        informe = "";
        Transform antebrazo = Buscar(raiz, Antebrazo);
        Transform muneca = antebrazo ? antebrazo.Find(Muneca) : null;
        Camera cam = antebrazo ? antebrazo.GetComponentInParent<Camera>(true) : null;
        if (!antebrazo || !muneca || !cam) { informe = "sin brazos"; return false; }

        Transform ya = Buscar(raiz, RelojJugador.NombreReloj);
        if (ya)
        {
            if (!rehacer || PrefabUtility.IsPartOfPrefabInstance(ya)) return false;
            if (conUndo) Undo.DestroyObjectImmediate(ya.gameObject); else Object.DestroyImmediate(ya.gameObject);
        }

        // Seccion del antebrazo cerca de la muneca: se mide la superficie real del brazo (malla deformada en
        // pose de reposo, como collider temporal) con rayos desde arriba, abajo y los lados
        Vector3 codo = antebrazo.position, mano = muneca.position;
        Vector3 eje = (mano - codo).normalized;
        float largo = Vector3.Distance(codo, mano);
        Vector3 arriba = Vector3.ProjectOnPlane(cam.transform.up, eje).normalized;
        Vector3 lado = Vector3.Cross(eje, arriba).normalized;
        var verts = new List<Vector3>(); var tris = new List<int>();
        foreach (var sk in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!sk.sharedMesh || System.Array.IndexOf(sk.bones, antebrazo) < 0) continue;
            Mesh m = sk.sharedMesh;
            Vector3[] v = m.vertices; BoneWeight[] w = m.boneWeights; Matrix4x4[] bp = m.bindposes;
            var mt = sk.bones.Select((bo, i) => bo ? bo.localToWorldMatrix * bp[i] : Matrix4x4.identity).ToArray();
            int baseV = verts.Count;
            for (int i = 0; i < v.Length; i++)
            {
                BoneWeight bw = w[i];
                verts.Add(mt[bw.boneIndex0].MultiplyPoint3x4(v[i]) * bw.weight0 + mt[bw.boneIndex1].MultiplyPoint3x4(v[i]) * bw.weight1
                        + mt[bw.boneIndex2].MultiplyPoint3x4(v[i]) * bw.weight2 + mt[bw.boneIndex3].MultiplyPoint3x4(v[i]) * bw.weight3);
            }
            foreach (int ti in m.triangles) tris.Add(baseV + ti);
        }
        var malla = new Mesh { indexFormat = IndexFormat.UInt32 };
        malla.SetVertices(verts); malla.SetTriangles(tris, 0);
        var tmp = new GameObject("tmp brazo") { hideFlags = HideFlags.HideAndDontSave };
        var col = tmp.AddComponent<MeshCollider>(); col.sharedMesh = malla;
        float Borde(Vector3 punto, Vector3 dir)
        {
            // Distancia (sobre dir) desde punto hasta la superficie del brazo, entrando desde afuera
            var ray = new Ray(punto + dir * 0.25f, -dir);
            return col.Raycast(ray, out RaycastHit h, 0.5f) ? 0.25f - h.distance : float.NaN;
        }
        float t0 = 0.8f;
        Vector3 c0 = codo + eje * (largo * t0);
        float top = Borde(c0, arriba), bot = Borde(c0, -arriba), der = Borde(c0, lado), izq = Borde(c0, -lado);
        float ancho = 0.055f, grosor = 0.045f;
        Vector3 centro = c0;
        if (!float.IsNaN(top) && !float.IsNaN(bot) && !float.IsNaN(der) && !float.IsNaN(izq))
        {
            ancho = der + izq; grosor = top + bot;
            centro = c0 + lado * ((der - izq) * 0.5f) + arriba * ((top - bot) * 0.5f);
        }

        // La correa del modelo (pCylinder4) es un aro de 2.45 (Y) x 3.22 (Z) alrededor del eje X, centrado en el origen;
        // la esfera queda en el plano XZ, asi que Y se puede aplastar sin deformarla
        float escala = Mathf.Clamp(ancho * 1.08f / 3.0f, 0.012f, 0.03f);
        float escalaY = Mathf.Clamp(grosor * 1.1f / 2.25f, escala * 0.6f, escala);
        // Que la caja no quede metida en el brazo: la parte mas alta del brazo bajo la caja (radio 1.0)
        float alto = top;
        foreach (float dx in new[] { -1f, -0.5f, 0.5f, 1f })
            foreach (float dz in new[] { -0.7f, 0f, 0.7f })
            {
                float h = Borde(centro + eje * (dx * escala) + lado * (dz * escala), arriba);
                if (!float.IsNaN(h)) alto = Mathf.Max(alto, h + Vector3.Dot(centro - c0, arriba));
            }
        float subir = Mathf.Max(0f, alto - Vector3.Dot(centro - c0, arriba) - 1.0f * escalaY + 0.002f);
        centro += arriba * subir;
        Object.DestroyImmediate(tmp); Object.DestroyImmediate(malla);

        GameObject go = (GameObject)Object.Instantiate(modelo);
        go.name = RelojJugador.NombreReloj;
        go.transform.SetPositionAndRotation(centro, Quaternion.LookRotation(Vector3.Cross(eje, arriba), arriba));
        go.transform.localScale = new Vector3(escala, escalaY, escala);
        go.transform.SetParent(antebrazo, true);
        if (conUndo) Undo.RegisterCreatedObjectUndo(go, "Reloj 3D");

        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            var r = t.GetComponent<MeshRenderer>();
            if (!r) continue;
            r.shadowCastingMode = ShadowCastingMode.Off;
            string n = t.name;
            if (n.Contains("pCube19")) { t.name = RelojJugador.NombreAgujaMinuto; r.sharedMaterial = mats["Agujas"]; }
            else if (n.Contains("pCube20")) { t.name = RelojJugador.NombreAgujaHora; r.sharedMaterial = mats["Agujas"]; }
            else if (n.Contains("pPlane1")) { t.name = RelojJugador.NombreEsfera; r.sharedMaterial = mats["Esfera"]; }
            else if (n.Contains("pCylinder4")) { t.name = "Correa"; r.sharedMaterial = mats["Correa"]; }
            else if (n.Contains("pCube18")) { t.name = "Marcas"; r.sharedMaterial = mats["Marcas"]; }
            else if (n.Contains("pCylinder5")) { t.name = "Eje"; r.sharedMaterial = mats["Eje"]; }
            else if (n.Contains("pCylinder3")) { t.name = "Caja"; r.sharedMaterial = mats["Metal"]; }
            else { t.name = "Asa " + n.Replace("group1_", ""); r.sharedMaterial = mats["Metal"]; }
        }

        new GameObject(RelojJugador.NombreReloj + " v4").transform.SetParent(go.transform, false);

        // Brillo rojo (lo enciende RelojJugador con una entidad cerca)
        var luzGo = new GameObject(RelojJugador.NombreBrillo);
        luzGo.transform.SetParent(go.transform, false);
        luzGo.transform.localPosition = new Vector3(0f, 3f, 0f);
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.15f, 0.08f);
        luz.range = 0.35f;
        luz.intensity = 0f;
        luz.shadows = LightShadows.None;
        luz.enabled = false;

        // La malla del brazo izquierdo (el SkinnedMeshRenderer se llama "reloj de pulsera") siempre visible
        foreach (var sk in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (!sk.enabled)
            {
                if (conUndo) Undo.RecordObject(sk, "Reloj 3D");
                sk.enabled = true;
            }

        informe = $"reloj en {Antebrazo} (escala {escala:0.0000}/{escalaY:0.0000}, muneca {ancho * 100f:0.0}x{grosor * 100f:0.0} cm, subido {subir * 1000f:0} mm)";
        return true;
    }

    static Dictionary<string, Material> Materiales()
    {
        if (!AssetDatabase.IsValidFolder(CarpetaMat)) AssetDatabase.CreateFolder("Assets/Models/Player/reloj", "Materiales");
        var d = new Dictionary<string, Material>
        {
            ["Metal"] = Mat("Reloj_Metal", new Color(0.62f, 0.6f, 0.56f), 0.85f, 0.6f),
            ["Correa"] = Mat("Reloj_Correa", new Color(0.17f, 0.11f, 0.08f), 0f, 0.25f),
            ["Esfera"] = Mat("Reloj_Esfera", new Color(0.9f, 0.86f, 0.74f), 0f, 0.35f, true),
            ["Marcas"] = Mat("Reloj_Marcas", new Color(0.1f, 0.09f, 0.08f), 0.3f, 0.4f),
            ["Agujas"] = Mat("Reloj_Agujas", new Color(0.06f, 0.05f, 0.05f), 0.4f, 0.5f),
            ["Eje"] = Mat("Reloj_Eje", new Color(0.6f, 0.15f, 0.1f), 0.5f, 0.5f),
        };
        AssetDatabase.SaveAssets();
        return d;
    }

    static Material Mat(string nombre, Color c, float metal, float liso, bool emision = false)
    {
        string ruta = CarpetaMat + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, ruta);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", metal);
        m.SetFloat("_Smoothness", liso);
        if (emision)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == nombre) return t;
        return null;
    }

    // Revision: "nombre x y z inclinacionBrazo inclinacionEsfera" -> foto de la pose de mirar con esos valores
    static void Variante(string l)
    {
        var p = l.Split(' ');
        if (p.Length < 6) return;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        float F(int i) => float.Parse(p[i], ci);
        var pm = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        Transform reloj = Buscar(pm.transform.root, RelojJugador.NombreReloj);
        Transform brazo = reloj.parent; while (brazo.name != RelojJugador.HuesoBrazo) brazo = brazo.parent;
        Camera cam = reloj.GetComponentInParent<Camera>(true);
        Transform arms = cam.transform.Find("Player_Arms");
        bool act = arms.gameObject.activeSelf; arms.gameObject.SetActive(true);
        foreach (var sk in arms.GetComponentsInChildren<SkinnedMeshRenderer>()) sk.forceMatrixRecalculationPerRender = true;
        Vector3 lp = brazo.localPosition; Quaternion lr = brazo.localRotation;
        RelojJugador.PoseMirar(brazo, reloj, cam.transform, new Vector3(F(1), F(2), F(3)), F(4), F(5), 1f);
        var go = new GameObject("camFotos"); var c = go.AddComponent<Camera>();
        c.nearClipPlane = 0.01f; c.farClipPlane = 200f; c.fieldOfView = cam.fieldOfView;
        go.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
        var rt = new RenderTexture(960, 540, 24); c.targetTexture = rt; c.Render();
        RenderTexture.active = rt; var t = new Texture2D(960, 540); t.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); t.Apply();
        File.WriteAllBytes("Temp/Capturas/reloj3d_v_" + p[0] + ".png", t.EncodeToPNG());
        Transform ct = cam.transform;
        go.transform.position = reloj.position + ct.right * 0.12f + ct.up * 0.12f - ct.forward * 0.02f;
        go.transform.LookAt(reloj.position, ct.up);
        c.Render(); t.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); t.Apply();
        File.WriteAllBytes("Temp/Capturas/reloj3d_v_" + p[0] + "_lado.png", t.EncodeToPNG());
        RenderTexture.active = null; c.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(t); Object.DestroyImmediate(go);
        brazo.localPosition = lp; brazo.localRotation = lr;
        arms.gameObject.SetActive(act);
    }

    // Revision: fotos de la vista del jugador con el reloj en reposo y mirandolo (no guarda nada)
    [MenuItem("Tools/Jugador/Reloj 3D (fotos de revision)")]
    static void Fotos()
    {
        var pm = Object.FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        Transform reloj = pm ? Buscar(pm.transform.root, RelojJugador.NombreReloj) : null;
        if (!reloj) return;
        Transform brazo = reloj.parent; while (brazo && brazo.name != RelojJugador.HuesoBrazo) brazo = brazo.parent;
        Camera cam = reloj.GetComponentInParent<Camera>(true);
        Transform arms = cam.transform.Find("Player_Arms");
        bool act = arms.gameObject.activeSelf; arms.gameObject.SetActive(true);
        foreach (var sk in arms.GetComponentsInChildren<SkinnedMeshRenderer>()) sk.forceMatrixRecalculationPerRender = true;
        Vector3 lp = brazo.localPosition; Quaternion lr = brazo.localRotation;
        var rj = Object.FindFirstObjectByType<RelojJugador>(FindObjectsInactive.Include);
        Vector3 pos = rj ? rj.posicionMirar : new Vector3(-0.03f, -0.05f, 0.25f);
        float ib = rj ? rj.inclinacionBrazo : 12f, ie = rj ? rj.inclinacionEsfera : 10f;

        var go = new GameObject("camFotos"); var c = go.AddComponent<Camera>();
        c.nearClipPlane = 0.01f; c.farClipPlane = 200f;
        Directory.CreateDirectory("Temp/Capturas");
        void Foto(string n, Vector3 p, Quaternion q, float fov, int w = 960, int h = 540)
        {
            go.transform.SetPositionAndRotation(p, q); c.fieldOfView = fov;
            var rt = new RenderTexture(w, h, 24); c.targetTexture = rt; c.Render();
            RenderTexture.active = rt; var t = new Texture2D(w, h); t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply();
            File.WriteAllBytes("Temp/Capturas/reloj3d_" + n + ".png", t.EncodeToPNG());
            RenderTexture.active = null; c.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(t);
        }
        Transform ct = cam.transform;
        Foto("reposo", ct.position, ct.rotation, cam.fieldOfView);
        // Primer plano del reloj en reposo
        Foto("muneca", reloj.position + reloj.up * 0.12f + reloj.forward * 0.05f, Quaternion.LookRotation(-reloj.up, reloj.forward), 40f);
        foreach (float e in new[] { 0.5f, 1f })
        {
            brazo.localPosition = lp; brazo.localRotation = lr;
            RelojJugador.PoseMirar(brazo, reloj, ct, pos, ib, ie, e);
            Foto("mirar_" + e.ToString("0.0"), ct.position, ct.rotation, cam.fieldOfView);
            if (e == 1f)
            {
                Foto("mirar_arriba", reloj.position + ct.up * 0.3f + ct.forward * 0.02f, Quaternion.LookRotation(-ct.up, ct.forward), 45f);
                Foto("mirar_lado", reloj.position - ct.right * 0.05f + ct.forward * 0.3f, Quaternion.LookRotation(-ct.forward, ct.up), 45f);
            }
        }
        brazo.localPosition = lp; brazo.localRotation = lr;
        Object.DestroyImmediate(go);
        arms.gameObject.SetActive(act);
        File.WriteAllText("Temp/Capturas/reloj3d_estado.txt", $"reloj escala={reloj.lossyScale.x:0.0000} pos enCam={ct.InverseTransformPoint(reloj.position):F3}");
    }
}


