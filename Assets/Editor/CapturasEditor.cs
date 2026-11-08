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

    static string Ruta(Transform t, Transform raiz) => t == raiz ? t.name : Ruta(t.parent, raiz) + "/" + t.name;
    static string V(Vector3 v) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:0.00},{1:0.00},{2:0.00})", v.x, v.y, v.z);
}



