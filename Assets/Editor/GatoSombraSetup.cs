using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// Arregla el modelo del gato de los Backrooms (Enemy 2), que se veia diminuto (unos 3 cm: solo se veian los ojos):
// - ModelosAnimadosSetup lo midio con la escala aplicada dos veces (el FBX trae escala 100 dentro) y lo achico 100
//   veces de mas; y NovedadesGuionSetup.EscalarGato solo mide mallas fijas, asi que no lo corregia.
// - Ademas el FBX mira hacia -X (caminaba de costado) y las poses Idle y Caminar tenian escalas distintas (cambiaba
//   de tamano al moverse).
// Aqui: las dos poses con la misma escala, mirando hacia adelante (+Z), de ~0.85 m de la nariz a la cola, con las
// patas en el piso; material de sombra (gris muy oscuro con algo de brillo, para que se lea su forma) y los ojos que
// brillan pegados a la cabeza de cada pose (siguen la animacion). Tambien le pone VisionPiedra (se ve a traves de las
// paredes mirando por la piedra bruja).
// Se aplica sola una vez (marca "_gato v6" dentro de "Modelo"); rehacer: Tools > Backrooms > Arreglar modelo del gato.
[InitializeOnLoad]
public static class GatoSombraSetup
{
    const string Prefab = "Assets/Prefabs/Enemy 2 (gato).prefab";
    const string Marca = "_gato v6";
    const string Carpeta = "Assets/Backrooms/Palancas";
    const float Largo = 0.85f;     // de la nariz a la punta de la cola, caminando
    const float TamOjo = 0.024f;   // diametro de cada ojo (m)

    static GatoSombraSetup()
    {
        EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Aplicar(false);
    }

    [MenuItem("Tools/Backrooms/Arreglar modelo del gato")]
    static void Menu() => Aplicar(true);

    static void Aplicar(bool forzar)
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)) return;
        GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            Transform modelo = root.transform.Find("Modelo");
            if (!modelo || (!forzar && modelo.Find(Marca))) return;
            var poses = new List<Transform>();
            foreach (Transform t in modelo) if (t.GetComponentInChildren<SkinnedMeshRenderer>(true)) poses.Add(t);
            if (poses.Count == 0) { Debug.LogWarning("[Gato] \"Modelo\" no tiene poses animadas: no se toca."); return; }
            Transform refPose = poses.Find(p => p.name == AnimacionesModelo.Caminar) ?? poses[0];
            var agent = root.GetComponent<NavMeshAgent>();
            float pies = agent ? -agent.baseOffset : 0f;

            // 1) Hacia adelante: de la punta de la cola a la cabeza
            Transform cabeza = Cabeza(refPose), cola = PuntaCola(refPose);
            if (!cabeza || !cola) { Debug.LogWarning("[Gato] No encuentro la cabeza o la cola del esqueleto: no se toca."); return; }
            Vector3 dir = root.transform.InverseTransformPoint(cabeza.position) - root.transform.InverseTransformPoint(cola.position);
            dir.y = 0f;
            float giro = dir.sqrMagnitude > 1e-10f ? Vector3.SignedAngle(dir.normalized, Vector3.forward, Vector3.up) : 0f;
            foreach (Transform p in poses)
            {
                p.localScale = refPose.localScale;   // mismas unidades en los dos FBX: misma escala
                p.localRotation = Quaternion.AngleAxis(giro, Vector3.up) * p.localRotation;
            }

            // 2) Tamano: el largo caminando (la malla; si da algo raro, el esqueleto con un margen)
            Bounds bh = Limites(Puntos(root.transform, refPose, false));
            Bounds bm = Limites(Puntos(root.transform, refPose, true));
            float largo = bm.size.z > bh.size.z * 0.9f && bm.size.z < bh.size.z * 2f ? bm.size.z : bh.size.z * 1.12f;
            if (largo < 1e-6f) { Debug.LogWarning("[Gato] No pude medir el modelo."); return; }
            float k = Largo / largo;
            foreach (Transform p in poses) p.localScale *= k;

            // 3) Patas en el piso y centrado en el agente
            foreach (Transform p in poses) Asentar(root.transform, modelo, p, pies);

            // 4) Material de sombra
            Material sombra = Mat("GatoSombra", new Color(0.055f, 0.05f, 0.06f), 0.4f, false, default);
            Material ojo = Mat("OjoGato", new Color(1f, 0.9f, 0.4f), 0.5f, true, new Color(5f, 4f, 1.2f));
            foreach (Transform p in poses)
                foreach (SkinnedMeshRenderer r in p.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = sombra;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.updateWhenOffscreen = true;
                }

            // 5) Ojos pegados a la cabeza de cada pose (los viejos eran esferas sueltas del modelo anterior)
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t && t.name == "Ojo") Object.DestroyImmediate(t.gameObject);
            Ojos(root.transform, refPose, poses, ojo);

            // 6) Colision del tamano del cuerpo (sin la cola)
            Bounds b = Limites(Puntos(root.transform, refPose, true));
            if (b.size.y < 1e-4f) b = Limites(Puntos(root.transform, refPose, false));
            var col = root.GetComponent<CapsuleCollider>();
            if (col)
            {
                col.direction = 2;
                col.radius = Mathf.Clamp(b.size.y * 0.28f, 0.08f, 0.2f);
                col.height = Largo * 0.62f;
                col.center = new Vector3(0f, pies + b.size.y * 0.42f, Largo * 0.08f);
            }

            // 7) Se ve a traves de las paredes con la piedra bruja
            var vision = root.GetComponent<VisionPiedra>();
            if (!vision) vision = root.AddComponent<VisionPiedra>();
            vision.contorno = MaterialVision();

            new GameObject(Marca).transform.SetParent(modelo, false);
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            Debug.Log($"[Gato] Modelo arreglado: {Largo} m de largo (antes {largo * 100f:0.0} cm segun la {(largo == bm.size.z ? "malla" : "esqueleto")}), " +
                      $"girado {giro:0} grados para que mire hacia adelante, misma escala en sus {poses.Count} poses, material de sombra y ojos en la cabeza.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ------------------------------------------------------------------ esqueleto

    static Transform Hueso(Transform pose, string nombre)
    {
        foreach (Transform t in pose.GetComponentsInChildren<Transform>(true)) if (t.name == nombre) return t;
        return null;
    }

    // La cabeza: el padre de las orejas; si no hay, el "spine.N" de numero mas alto (o uno que se llame head)
    static Transform Cabeza(Transform pose)
    {
        Transform oreja = Hueso(pose, "ear.L") ?? Hueso(pose, "ear.R");
        if (oreja && oreja.parent) return oreja.parent;
        Transform mejor = null; int n = -1;
        foreach (Transform t in pose.GetComponentsInChildren<Transform>(true))
        {
            string s = t.name.ToLowerInvariant();
            if (s.Contains("head") && !s.Contains("end")) return t;
            if (s.StartsWith("spine.") && !s.Contains("end") && int.TryParse(s.Substring(6), out int k) && k > n) { n = k; mejor = t; }
        }
        return mejor;
    }

    // La punta de la cola: el hueso "tail..." mas profundo
    static Transform PuntaCola(Transform pose)
    {
        Transform mejor = null; int prof = -1;
        foreach (Transform t in pose.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.ToLowerInvariant().StartsWith("tail")) continue;
            int d = 0;
            for (Transform x = t; x != null && x != pose; x = x.parent) d++;
            if (d > prof) { prof = d; mejor = t; }
        }
        return mejor;
    }

    // Puntos del modelo en el espacio de la raiz: vertices de las mallas (con su pose) o posiciones de los huesos
    static List<Vector3> Puntos(Transform raiz, Transform pose, bool malla)
    {
        var res = new List<Vector3>();
        if (malla)
        {
            var m = new Mesh();
            foreach (SkinnedMeshRenderer r in pose.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!r.sharedMesh) continue;
                r.BakeMesh(m, true);
                Matrix4x4 a = raiz.worldToLocalMatrix * r.transform.localToWorldMatrix;
                Vector3[] vs = m.vertices;
                int paso = Mathf.Max(1, vs.Length / 4000);
                for (int i = 0; i < vs.Length; i += paso) res.Add(a.MultiplyPoint3x4(vs[i]));
            }
            Object.DestroyImmediate(m);
        }
        else
            foreach (Transform t in pose.GetComponentsInChildren<Transform>(true))
                if (t != pose && !t.GetComponent<Renderer>() && !t.GetComponent<Animator>()) res.Add(raiz.InverseTransformPoint(t.position));
        return res;
    }

    static Bounds Limites(List<Vector3> pts)
    {
        if (pts.Count == 0) return new Bounds();
        var b = new Bounds(pts[0], Vector3.zero);
        foreach (Vector3 p in pts) b.Encapsulate(p);
        return b;
    }

    static void Asentar(Transform raiz, Transform modelo, Transform pose, float pies)
    {
        List<Vector3> pts = Puntos(raiz, pose, true);
        if (pts.Count == 0) pts = Puntos(raiz, pose, false);
        Bounds b = Limites(pts);
        Vector3 delta = new Vector3(-b.center.x, pies - b.min.y, -b.center.z);
        pose.localPosition += modelo.InverseTransformVector(raiz.TransformVector(delta));
    }

    // Ojos delante de la base de las orejas, apoyados en la superficie de la cara (de la pose de caminar) y copiados,
    // con la misma posicion relativa a la cabeza, en las demas poses (es el mismo esqueleto)
    static void Ojos(Transform raiz, Transform refPose, List<Transform> poses, Material mat)
    {
        Transform cabeza = Cabeza(refPose);
        Transform oL = Hueso(refPose, "ear.L"), oR = Hueso(refPose, "ear.R");
        Vector3 f = raiz.forward, u = raiz.up, r = raiz.right;
        Vector3 medio; float d;
        if (oL && oR)
        {
            medio = (oL.position + oR.position) * 0.5f;
            d = Mathf.Clamp(Vector3.Distance(oL.position, oR.position), Largo * 0.03f, Largo * 0.09f);
        }
        else { d = Largo * 0.06f; medio = cabeza.position + u * d * 0.5f; }
        Vector3 baseOjos = medio + f * (0.7f * d) - u * (0.5f * d);

        // Superficie de la cara: el vertice mas adelantado cerca de la linea que sale de cada ojo hacia adelante
        var vs = new List<Vector3>();
        foreach (Vector3 p in Puntos(raiz, refPose, true)) vs.Add(raiz.TransformPoint(p));
        var locales = new List<Vector3>();
        foreach (float lado in new[] { -1f, 1f })
        {
            Vector3 e = baseOjos + r * (lado * 0.38f * d);
            float mejor = float.MinValue; Vector3 sup = e;
            foreach (Vector3 v in vs)
            {
                Vector3 rel = v - e;
                float adelante = Vector3.Dot(rel, f);
                if (adelante < -0.6f * d) continue;
                if ((rel - f * adelante).magnitude > 0.3f * d) continue;
                if (adelante > mejor) { mejor = adelante; sup = v; }
            }
            if (mejor > float.MinValue) e = sup - f * (TamOjo * 0.25f);
            locales.Add(cabeza.InverseTransformPoint(e));
        }

        foreach (Transform pose in poses)
        {
            Transform h = pose == refPose ? cabeza : Cabeza(pose);
            if (!h) continue;
            bool visible = false;
            foreach (SkinnedMeshRenderer s in pose.GetComponentsInChildren<SkinnedMeshRenderer>(true)) visible |= s.enabled;
            foreach (Vector3 lp in locales)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Ojo";
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(h, false);
                go.transform.localPosition = lp;
                float esc = Mathf.Abs(h.lossyScale.x) > 1e-6f ? TamOjo / Mathf.Abs(h.lossyScale.x) : 1f;
                go.transform.localScale = Vector3.one * esc;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.enabled = visible;   // la pose que no se ve empieza con todo apagado (AnimacionesModelo las alterna)
            }
        }
    }

    // ------------------------------------------------------------------ materiales

    static Material Mat(string nombre, Color color, float liso, bool emision, Color brillo)
    {
        string ruta = Carpeta + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        bool nuevo = !m;
        if (nuevo) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", liso);
        m.SetFloat("_Metallic", 0f);
        if (emision)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", brillo);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        Directory.CreateDirectory(Carpeta);
        if (nuevo) AssetDatabase.CreateAsset(m, ruta); else EditorUtility.SetDirty(m);
        return m;
    }

    // Contorno rojo que se ve a traves de todo (el shader del contorno del tutorial)
    internal static Material MaterialVision()
    {
        string ruta = Carpeta + "/VisionGato.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m) return m;
        Shader s = Shader.Find("Custom/SeeThroughOutline");
        if (!s) { Debug.LogWarning("[Gato] No encuentro el shader Custom/SeeThroughOutline: sin vision a traves de paredes."); return null; }
        m = new Material(s);
        m.SetColor("_GlowColor", new Color(3.2f, 0.35f, 0.3f, 1f));
        m.SetFloat("_OutlineWidth", 3f);
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }
}
