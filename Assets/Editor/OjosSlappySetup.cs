using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Ojos brillantes (tenues) para Slappy, el enemigo de la Zona Roja ("Enemy 4"): dos esferas que brillan, hijas del
// hueso de la cabeza de CADA pose del modelo (Caminar, Correr, Jumpscare: cada una es su propio FBX con su esqueleto),
// asi siguen a la cabeza en todas las animaciones. OjosBrillantes (en la raiz) les da el pulso y el parpadeo.
// La cabeza y la cara se buscan en la malla (no por nombre: la caminata usa un esqueleto de Blender y las demas el de
// Mixamo): el hueso que mueve la parte mas alta del cuerpo es la cabeza; lo mas adelantado de esa parte es la cara.
// Tambien sus materiales: el FBX traia el cuerpo NEGRO PURO (0,0,0, no refleja nada de luz: ni el faro ni la linterna
// lo alumbraban y en la Zona Roja era invisible) y el del Jumpscare apuntaba a un material borrado. Ahora: traje oscuro
// (pero no negro) y la cara de madera pintada, sin brillo (Smoothness 0) y apagados (no compiten con el faro).
// Se aplica solo si faltan (tambien despues de que ModelosAnimadosSetup rehaga el modelo); rehacer:
// Tools > Modelos > Ojos de Slappy.
[InitializeOnLoad]
public static class OjosSlappySetup
{
    const string Prefab = "Assets/Prefabs/Enemy 4.prefab";
    const string RutaMaterial = "Assets/Materials/OjosSlappy.mat";
    // v2: con materiales propios (era negro puro: invisible). v3: ojos mas brillantes, el sonido del susto
    // ("sonido kill enemigo 2 y 4") y el collider / la vista a la altura del modelo nuevo (3 m)
    // v4: correr y el susto (una sola submalla: salian todos grises) con la cara aparte, como la caminata; y la IA un
    // poco mas lista (la linterna lo delata, le corta el paso, tarda mas en rendirse, acecha mas)
    const string Marca = "_ojos v4";
    static readonly string[] MarcasViejas = { "_ojos v1", "_ojos v2", "_ojos v3" };
    const string CarpetaMallas = "Assets/Modelos De enemigos/Slappy";
    const string SonidoSusto = "Assets/Sounds/SFX/sonido kill enemigo 2 y 4.mp3";
    const float BrilloOjos = 2.4f, BrilloOjosPersiguiendo = 3.6f;
    const string RutaCuerpo = "Assets/Materials/SlappyTraje.mat";
    const string RutaCara = "Assets/Materials/SlappyCara.mat";
    static readonly Color ColorTraje = new Color(0.2f, 0.19f, 0.22f);
    static readonly Color ColorCara = new Color(0.45f, 0.39f, 0.32f);
    const string NombreGrupo = "Ojos brillantes";

    static OjosSlappySetup()
    {
        // Despues de ModelosAnimadosSetup (que puede rehacer el modelo y borrar los ojos)
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (!p) return;
        Transform modelo = p.transform.Find("Modelo");
        if (modelo && !modelo.Find(Marca)) Aplicar();
        SonidoEnEscena(p);
    }

    // El Slappy de la escena tenia el sonido del susto vaciado (un cambio sobre el prefab): se quita ese cambio, asi usa
    // el del prefab ("sonido kill enemigo 2 y 4")
    static void SonidoEnEscena(GameObject prefab)
    {
        var deLaEscena = prefab.GetComponent<JefeIndigoAI>();
        if (!deLaEscena) return;
        foreach (JefeIndigoAI e in Object.FindObjectsByType<JefeIndigoAI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (PrefabUtility.GetCorrespondingObjectFromSource(e) != deLaEscena) continue;
            var so = new SerializedObject(e);
            SerializedProperty prop = so.FindProperty("sonidoScreamer");
            if (prop == null || !prop.prefabOverride) continue;
            PrefabUtility.RevertPropertyOverride(prop, InteractionMode.UserAction);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(e.gameObject.scene);
            Debug.Log("[Ojos] " + e.name + " (escena): vuelve a usar el sonido del susto del prefab. Guarda la escena (Ctrl+S).", e);
        }
    }

    [MenuItem("Tools/Modelos/Ojos de Slappy")]
    static void Aplicar()
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)) { Debug.LogWarning("[Ojos] No encuentro " + Prefab); return; }
        Material mat = Material();
        Material traje = MatLiso(RutaCuerpo, "SlappyTraje", ColorTraje);
        Material cara = MatLiso(RutaCara, "SlappyCara", ColorCara);
        GameObject raiz = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            Transform modelo = raiz.transform.Find("Modelo");
            if (!modelo) { Debug.LogWarning("[Ojos] Enemy 4 no tiene \"Modelo\" (falta ModelosAnimadosSetup)."); return; }
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true).Where(t => t.name == NombreGrupo || MarcasViejas.Contains(t.name)).ToArray())
                Object.DestroyImmediate(t.gameObject);
            // Materiales: la submalla mas alta (la cabeza) con la cara, el resto con el traje
            foreach (SkinnedMeshRenderer s in modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (s.sharedMesh != null) Materiales(s, raiz.transform.up, traje, cara);

            var log = new List<string>();
            foreach (Transform pose in modelo)
            {
                if (pose.name.StartsWith("_")) continue;
                SkinnedMeshRenderer cuerpo = pose.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(s => s.sharedMesh != null).OrderByDescending(s => s.sharedMesh.vertexCount).FirstOrDefault();
                if (cuerpo == null) continue;
                if (Ojos(raiz.transform, cuerpo, mat, out string info)) log.Add(pose.name + ": " + info);
                else log.Add(pose.name + ": sin ojos (" + info + ")");
            }
            var ob = raiz.GetComponent<OjosBrillantes>();
            if (!ob) ob = raiz.AddComponent<OjosBrillantes>();
            ob.brillo = BrilloOjos;
            ob.brilloPersiguiendo = BrilloOjosPersiguiendo;

            // El susto: con su sonido. Y el collider y la vista a la altura del modelo (que ahora es mas alto)
            var ia = raiz.GetComponent<JefeIndigoAI>();
            AudioClip susto = AssetDatabase.LoadAssetAtPath<AudioClip>(SonidoSusto);
            if (ia && susto) ia.sonidoScreamer = susto;
            else if (!susto) Debug.LogWarning("[Ojos] No encuentro " + SonidoSusto);
            // Un poco mas listo: la linterna lo delata, le corta el paso, tarda mas en rendirse y acecha mas seguido
            if (ia)
            {
                ia.inteligente = true;
                ia.verLinterna = true;
                ia.interceptar = true;
                ia.loseSightGrace = Mathf.Max(ia.loseSightGrace, 3f);
                ia.searchDuration = Mathf.Max(ia.searchDuration, 10f);
                ia.memoria = Mathf.Max(ia.memoria, 60f);
                ia.acechar = true;
                ia.acechoCada = new Vector2(10f, 18f);
                ia.errorAcecho = Mathf.Min(ia.errorAcecho, 5f);
            }
            if (Alto(raiz.transform, modelo, out float pies, out float alto))
            {
                if (raiz.TryGetComponent(out CapsuleCollider col))
                {
                    col.height = alto;
                    col.center = new Vector3(0f, pies + alto * 0.5f, 0f);
                }
                if (ia) ia.eyeHeight = Mathf.Max(ia.eyeHeight, alto * 0.88f);
                log.Add($"mide {alto:0.0} m");
            }
            new GameObject(Marca).transform.SetParent(modelo, false);
            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            Debug.Log("[Ojos] Slappy con ojos brillantes (tenues). " + string.Join("; ", log));
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    static bool Ojos(Transform raiz, SkinnedMeshRenderer smr, Material mat, out string info)
    {
        Mesh m = smr.sharedMesh;
        BoneWeight[] pesos = m.boneWeights;
        Transform[] huesos = smr.bones;
        if (pesos == null || pesos.Length != m.vertexCount || huesos == null || huesos.Length == 0) { info = "la malla no tiene pesos"; return false; }

        // La pose tal como quedo (el primer cuadro de su animacion), en el mundo
        var horneada = new Mesh();
        smr.BakeMesh(horneada, true);
        Vector3[] v = horneada.vertices;
        Object.DestroyImmediate(horneada);
        Transform st = smr.transform;
        for (int i = 0; i < v.Length; i++) v[i] = st.position + st.rotation * v[i];

        Vector3 arriba = raiz.up, frente = raiz.forward, derecha = raiz.right;
        float abajo = float.MaxValue, top = float.MinValue;
        foreach (Vector3 p in v) { float h = Vector3.Dot(p, arriba); abajo = Mathf.Min(abajo, h); top = Mathf.Max(top, h); }
        float alto = top - abajo;
        if (alto < 0.01f) { info = "malla sin alto"; return false; }

        // El hueso de la cabeza: por nombre ("Head" de Mixamo, "spine.006" de Rigify) o, si no, el que mas mueve lo
        // mas alto del cuerpo (el 13% de arriba)
        int iHueso = System.Array.FindIndex(huesos, b => b != null && b.name.ToLowerInvariant().Contains("head") &&
                                                        !b.name.ToLowerInvariant().Contains("top") && !b.name.ToLowerInvariant().Contains("end"));
        if (iHueso < 0) iHueso = System.Array.FindIndex(huesos, b => b != null && b.name == "spine.006");
        if (iHueso < 0)
        {
            var peso = new Dictionary<int, float>();
            void Sumar(int b, float w) { if (w <= 0f || b < 0 || b >= huesos.Length) return; peso.TryGetValue(b, out float x); peso[b] = x + w; }
            for (int i = 0; i < v.Length; i++)
                if (Vector3.Dot(v[i], arriba) > top - alto * 0.13f)
                { BoneWeight w = pesos[i]; Sumar(w.boneIndex0, w.weight0); Sumar(w.boneIndex1, w.weight1); Sumar(w.boneIndex2, w.weight2); Sumar(w.boneIndex3, w.weight3); }
            if (peso.Count > 0) iHueso = peso.OrderByDescending(kv => kv.Value).First().Key;
        }
        if (iHueso < 0 || huesos[iHueso] == null) { info = "no encontre el hueso de la cabeza"; return false; }
        Transform hueso = huesos[iHueso];

        // La cabeza: los vertices que mueve sobre todo ese hueso (o los que cuelgan de el)
        var deLaCabeza = new HashSet<int>();
        for (int b = 0; b < huesos.Length; b++) if (huesos[b] != null && huesos[b].IsChildOf(hueso)) deLaCabeza.Add(b);
        var cabeza = new List<int>();
        for (int i = 0; i < v.Length; i++)
        {
            BoneWeight w = pesos[i];
            float enCabeza = (deLaCabeza.Contains(w.boneIndex0) ? w.weight0 : 0f) + (deLaCabeza.Contains(w.boneIndex1) ? w.weight1 : 0f)
                           + (deLaCabeza.Contains(w.boneIndex2) ? w.weight2 : 0f) + (deLaCabeza.Contains(w.boneIndex3) ? w.weight3 : 0f);
            if (enCabeza >= 0.5f) cabeza.Add(i);
        }
        if (cabeza.Count < 8) { info = "casi no hay malla en la cabeza (" + hueso.name + ")"; return false; }

        // Tamaño y centro de la cabeza; los ojos un poco arriba de la mitad, separados un 22% del ancho
        Vector3 centro = Vector3.zero;
        float izq = float.MaxValue, der = float.MinValue, cBajo = float.MaxValue, cAlto = float.MinValue;
        foreach (int i in cabeza)
        {
            centro += v[i];
            float x = Vector3.Dot(v[i], derecha), h = Vector3.Dot(v[i], arriba);
            izq = Mathf.Min(izq, x); der = Mathf.Max(der, x); cBajo = Mathf.Min(cBajo, h); cAlto = Mathf.Max(cAlto, h);
        }
        centro /= cabeza.Count;
        float ancho = der - izq, altoCabeza = Mathf.Max(0.01f, cAlto - cBajo);
        float altoOjos = cBajo + altoCabeza * 0.58f;
        float radio = Mathf.Clamp(altoCabeza * 0.075f, alto * 0.006f, alto * 0.02f);
        var grupo = new GameObject(NombreGrupo).transform;
        grupo.SetParent(hueso, false);
        foreach (float lado in new[] { -1f, 1f })
        {
            float x = Vector3.Dot(centro, derecha) + lado * ancho * 0.22f;
            // La cara en ese punto: lo mas adelantado de los vertices cercanos (si no hay, lo mas adelantado de la cabeza)
            float delante = float.MinValue;
            foreach (int i in cabeza)
            {
                if (Mathf.Abs(Vector3.Dot(v[i], derecha) - x) > ancho * 0.12f || Mathf.Abs(Vector3.Dot(v[i], arriba) - altoOjos) > altoCabeza * 0.12f) continue;
                delante = Mathf.Max(delante, Vector3.Dot(v[i], frente));
            }
            if (delante == float.MinValue) foreach (int i in cabeza) delante = Mathf.Max(delante, Vector3.Dot(v[i], frente));
            Vector3 p = derecha * x + arriba * altoOjos + frente * (delante - radio * 0.35f);

            var ojo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ojo.name = OjosBrillantes.NombreOjo;
            Object.DestroyImmediate(ojo.GetComponent<Collider>());
            ojo.transform.SetParent(grupo, true);
            ojo.transform.position = p;
            ojo.transform.rotation = Quaternion.LookRotation(frente, arriba);
            // Mismo tamaño en el mundo aunque el hueso este escalado
            Vector3 s = hueso.lossyScale;
            ojo.transform.localScale = new Vector3(radio * 2.2f / Mathf.Max(1e-4f, Mathf.Abs(s.x)), radio * 2f / Mathf.Max(1e-4f, Mathf.Abs(s.y)), radio * 1.2f / Mathf.Max(1e-4f, Mathf.Abs(s.z)));
            var r = ojo.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        info = $"en {hueso.name} ({radio * 200f:0.#} cm)";
        return true;
    }

    // Alto del modelo visible (la pose que se ve, Caminar) en el espacio de la raiz
    static bool Alto(Transform raiz, Transform modelo, out float pies, out float alto)
    {
        pies = 0f; alto = 0f;
        Transform pose = modelo.Find(AnimacionesModelo.Caminar);
        if (!pose) pose = modelo;
        bool hay = false;
        float bajo = float.MaxValue, arriba = float.MinValue;
        foreach (Renderer r in pose.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name == OjosBrillantes.NombreOjo) continue;
            Bounds b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = raiz.InverseTransformPoint(new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i < 4 ? b.min.z : b.max.z));
                bajo = Mathf.Min(bajo, p.y); arriba = Mathf.Max(arriba, p.y);
            }
            hay = true;
        }
        if (!hay) return false;
        pies = bajo;
        alto = arriba - bajo;
        return alto > 0.3f;
    }

    // Con una sola submalla (correr, el susto): se separa la cabeza en su propia submalla (lo que mueve el hueso de la
    // cabeza) y se guarda como malla nueva. Con varias: la que queda mas arriba en promedio (la cabeza) es la cara
    static void Materiales(SkinnedMeshRenderer s, Vector3 arriba, Material traje, Material cara)
    {
        Mesh m = s.sharedMesh;
        if (m.subMeshCount <= 1 && SepararCabeza(s, out Mesh dividida))
        {
            s.sharedMesh = dividida;
            s.sharedMaterials = new[] { traje, cara };
            return;
        }
        int n = Mathf.Max(1, m.subMeshCount);
        var mats = new Material[n];
        for (int i = 0; i < n; i++) mats[i] = traje;
        if (n > 1)
        {
            // La pose real en el mundo (la malla viene girada dentro del FBX: su "y" no es arriba)
            var horneada = new Mesh();
            s.BakeMesh(horneada, true);
            Vector3[] v = horneada.vertices;
            Object.DestroyImmediate(horneada);
            Transform st = s.transform;
            for (int i = 0; i < v.Length; i++) v[i] = new Vector3(0f, Vector3.Dot(st.position + st.rotation * v[i], arriba), 0f);
            int cabeza = -1;
            float mejor = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                int[] t = m.GetTriangles(i);
                if (t.Length == 0) continue;
                float suma = 0f;
                foreach (int k in t) suma += v[k].y;
                float media = suma / t.Length;
                // (en el espacio de la malla; la cabeza es la de mas arriba)
                if (media > mejor) { mejor = media; cabeza = i; }
            }
            if (cabeza >= 0 && m.GetTriangles(cabeza).Length < m.triangles.Length * 0.5f) mats[cabeza] = cara;
        }
        s.sharedMaterials = mats;
    }

    // Copia de la malla con dos submallas: 0 = el cuerpo, 1 = la cabeza (los triangulos que mueve sobre todo el hueso de
    // la cabeza o los que cuelgan de el)
    static bool SepararCabeza(SkinnedMeshRenderer s, out Mesh dividida)
    {
        dividida = null;
        Mesh m = s.sharedMesh;
        Transform[] huesos = s.bones;
        BoneWeight[] pesos = m.boneWeights;
        if (huesos == null || huesos.Length == 0 || pesos == null || pesos.Length != m.vertexCount) return false;
        int iCabeza = System.Array.FindIndex(huesos, b => b != null && b.name.ToLowerInvariant().Contains("head") &&
                                                         !b.name.ToLowerInvariant().Contains("top") && !b.name.ToLowerInvariant().Contains("end"));
        if (iCabeza < 0) iCabeza = System.Array.FindIndex(huesos, b => b != null && b.name == "spine.006");
        if (iCabeza < 0) return false;
        var deLaCabeza = new HashSet<int>();
        for (int b = 0; b < huesos.Length; b++) if (huesos[b] != null && huesos[b].IsChildOf(huesos[iCabeza])) deLaCabeza.Add(b);
        bool EnCabeza(int v)
        {
            BoneWeight w = pesos[v];
            float k = (deLaCabeza.Contains(w.boneIndex0) ? w.weight0 : 0f) + (deLaCabeza.Contains(w.boneIndex1) ? w.weight1 : 0f)
                    + (deLaCabeza.Contains(w.boneIndex2) ? w.weight2 : 0f) + (deLaCabeza.Contains(w.boneIndex3) ? w.weight3 : 0f);
            return k >= 0.5f;
        }
        int[] t = m.GetTriangles(0);
        var cuerpo = new List<int>(t.Length);
        var cabeza = new List<int>();
        for (int i = 0; i + 2 < t.Length; i += 3)
        {
            int c = (EnCabeza(t[i]) ? 1 : 0) + (EnCabeza(t[i + 1]) ? 1 : 0) + (EnCabeza(t[i + 2]) ? 1 : 0);
            var lista = c >= 2 ? cabeza : cuerpo;
            lista.Add(t[i]); lista.Add(t[i + 1]); lista.Add(t[i + 2]);
        }
        if (cabeza.Count == 0 || cuerpo.Count == 0) return false;

        // Ya separada antes (rehaciendo): se reemplaza la misma
        string limpio = m.name.Replace(" (cara aparte)", "");
        if (!AssetDatabase.IsValidFolder(CarpetaMallas)) AssetDatabase.CreateFolder("Assets/Modelos De enemigos", "Slappy");
        string ruta = CarpetaMallas + "/" + string.Concat(limpio.Split(System.IO.Path.GetInvalidFileNameChars())) + " (cara aparte).asset";
        Mesh nueva = Object.Instantiate(m);
        nueva.name = limpio + " (cara aparte)";
        nueva.subMeshCount = 2;
        nueva.SetTriangles(cuerpo, 0);
        nueva.SetTriangles(cabeza, 1);
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (existente)
        {
            existente.Clear();
            EditorUtility.CopySerialized(nueva, existente);
            Object.DestroyImmediate(nueva);
            nueva = existente;
            EditorUtility.SetDirty(nueva);
        }
        else AssetDatabase.CreateAsset(nueva, ruta);
        dividida = nueva;
        return true;
    }

    static Material MatLiso(string ruta, string nombre, Color color)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = nombre };
            AssetDatabase.CreateAsset(m, ruta);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_SpecularHighlights")) { m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
        if (m.HasProperty("_EnvironmentReflections")) { m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
        EditorUtility.SetDirty(m);
        return m;
    }

    // Casi negro con brillo propio (la emision la anima OjosBrillantes), sin reflejos
    static Material Material()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "OjosSlappy" };
            AssetDatabase.CreateAsset(m, RutaMaterial);
        }
        m.SetColor("_BaseColor", new Color(0.05f, 0.06f, 0.07f));
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_Metallic", 0f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.82f, 0.92f, 1f) * 0.9f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        if (m.HasProperty("_SpecularHighlights")) { m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
        if (m.HasProperty("_EnvironmentReflections")) { m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }
}
