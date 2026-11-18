using System.IO;
using UnityEditor;
using UnityEngine;

// Herramienta de revision: si existe Temp/Capturas/probar_dialogo.txt, entra a Play, lanza un dialogo,
// saca capturas de la Game view (con la interfaz) y sale de Play. No guarda nada en la escena.
// Contenido del archivo: ruta del DialogueData a probar (opcional).
[InitializeOnLoad]
public static class PruebaDialogoEditor
{
    const string Pedido = "Temp/Capturas/probar_dialogo.txt";
    const string Clave = "PruebaDialogo.Activa";
    static double inicio;
    static int paso;

    static PruebaDialogoEditor()
    {
        EditorApplication.playModeStateChanged += Cambio;
        EditorApplication.delayCall += () => EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Pedido) && !SessionState.GetBool(Clave, false))
            {
                SessionState.SetString("PruebaDialogo.Asset", File.ReadAllText(Pedido).Trim());
                File.Delete(Pedido);
                SessionState.SetBool(Clave, true);
                EditorApplication.isPlaying = true;
            }
        };
    }

    static void Cambio(PlayModeStateChange e)
    {
        if (!SessionState.GetBool(Clave, false)) return;
        if (e == PlayModeStateChange.EnteredPlayMode)
        {
            inicio = EditorApplication.timeSinceStartup;
            paso = 0;
            for (int m = 0; m < 5; m++) SessionState.EraseBool("PruebaMenu" + m);
            EditorApplication.update += Tick;
        }
        else if (e == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Clave, false);
        }
    }

    static double ultimoLog;
    static bool capturada;
    static bool sonidoVisto;

    static void Tick()
    {
        double t = EditorApplication.timeSinceStartup - inicio;
        string carpeta = "Temp/Capturas";
        // Modo "intro": darle a Jugar y sacar fotos de la cinematica de inicio
        if (SessionState.GetString("PruebaDialogo.Asset", "") == "intro")
        {
            void Log(string x) => File.AppendAllText(carpeta + "/intro_estado.txt", x + System.Environment.NewLine);
            double[] tiempos = { 1.5, 3.2, 4.6, 6.2, 7.6, 8.8, 10.0, 11.2, 13.0, 16.5 };
            // Fotos del menu antes y despues de darle a Jugar (titulo, carro y el cliente que se baja)
            double[] menuT = { 0.5, 2.6, 3.3, 3.9, 4.4 };
            for (int m = 0; m < menuT.Length; m++)
                if (t > menuT[m] && !SessionState.GetBool("PruebaMenu" + m, false))
                {
                    SessionState.SetBool("PruebaMenu" + m, true);
                    ScreenCapture.CaptureScreenshot(carpeta + "/menu_" + m + ".png");
                    var tit = GameObject.Find("Titulo");
                    if (tit) Log("menu " + m + " t=" + t.ToString("0.00") + " titulo escala=" + tit.transform.lossyScale + " rect=" + ((RectTransform)tit.transform).rect + " font=" + tit.GetComponent<TMPro.TMP_Text>().fontSize);
                }
            // Seguimiento fino del tendero (cada 0.25 s, entre 7 y 10.5 s)
            int tick = (int)(t * 4);
            if (t > 7 && t < 10.5 && tick != SessionState.GetInt("PruebaTick", -1))
            {
                SessionState.SetInt("PruebaTick", tick);
                var ten2 = GameObject.Find("Tendero (intro)");
                if (ten2)
                {
                    var an = ten2.GetComponentInChildren<Animator>();
                    Log("  ten t=" + t.ToString("0.00") + " pos=" + ten2.transform.position.ToString("F2") + " act=" + ten2.activeSelf + (an ? " rootMotion=" + an.applyRootMotion : ""));
                }
            }
            if (paso < tiempos.Length && t > tiempos[paso])
            {
                if (paso == 0) { File.WriteAllText(carpeta + "/intro_estado.txt", ""); var menu = Object.FindFirstObjectByType<MenuPrincipal>(); if (menu) menu.Jugar(); Log("Jugar"); }
                else if (paso < tiempos.Length - 1)
                {
                    ScreenCapture.CaptureScreenshot(carpeta + "/intro_" + paso + ".png");
                    var carro = GameObject.Find("Intro Tienda/Carro");
                    var cli = GameObject.Find("Cliente (intro)");
                    var ten = GameObject.Find("Tendero (intro)");
                    var pm = Object.FindFirstObjectByType<PlayerMovement>();
                    Log("t=" + (t - 1.5).ToString("0.0") + " carro=" + (carro ? carro.transform.position.ToString() : "-") + " cliente=" + (cli ? cli.transform.position.ToString() : "-") + " tendero=" + (ten && ten.activeSelf ? ten.transform.position.ToString() : "-") + " camMain=" + (Camera.main ? Camera.main.name : "-") + " mov=" + (pm && pm.enabled) + " brazos=" + (GameObject.Find("Player_Arms") ? "activos" : "inactivos"));
                }
                else EditorApplication.isPlaying = false;
                paso++;
            }
            return;
        }

        // Modo "brillo": dentro y fuera de la sala del panel central
        if (SessionState.GetString("PruebaDialogo.Asset", "") == "brillo")
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            var pz = Object.FindFirstObjectByType<PuzzleContrasena>();
            void Log(string x) => File.AppendAllText(carpeta + "/brillo_estado.txt", x + System.Environment.NewLine);
            void Poner(Vector3 p, float yaw) { var rb = pm.GetComponent<Rigidbody>(); p.y = 1f; rb.position = p; rb.rotation = Quaternion.Euler(0f, yaw, 0f); pm.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f)); rb.linearVelocity = Vector3.zero; }
            int Brillan() { int n = 0; foreach (var h in pz.brilloPaneles) if (h && h.IsHighlighted) n++; return n; }
            if (paso == 0 && t > 1.5) { paso++; var menu = Object.FindFirstObjectByType<MenuPrincipal>(); if (menu) menu.Empezar(); File.WriteAllText(carpeta + "/brillo_estado.txt", ""); var pr = Object.FindFirstObjectByType<PresentacionEnemigo>(); if (pr) pr.enabled = false; var rj = Object.FindFirstObjectByType<RejasEntrada>(); if (rj) rj.enabled = false; }
            else if (paso == 1 && t > 2.5) { paso++; Poner(pz.salaCentral.bounds.center + new Vector3(-3f, 0f, 3f), -40f); }
            else if (paso == 2 && t > 3.5) { paso++; Log("dentro de la sala: brillan=" + Brillan()); ScreenCapture.CaptureScreenshot(carpeta + "/brillo_dentro.png"); }
            else if (paso == 3 && t > 4.5) { paso++; Poner(pz.salaCentral.bounds.center + new Vector3(-14f, 0f, 0f), 0f); }
            else if (paso == 4 && t > 5.5) { paso++; Log("fuera de la sala: brillan=" + Brillan()); EditorApplication.isPlaying = false; }
            return;
        }

        // Modo "puzzle": la reja cae, codigo erroneo, codigo correcto y cinematica de la puerta
        if (SessionState.GetString("PruebaDialogo.Asset", "") == "puzzle")
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            var pz = Object.FindFirstObjectByType<PuzzleContrasena>();
            var rj = Object.FindFirstObjectByType<RejasEntrada>();
            void Log(string x) => File.AppendAllText(carpeta + "/puzzle_estado.txt", x + System.Environment.NewLine);
            void Poner(Vector3 p) { var rb = pm.GetComponent<Rigidbody>(); p.y = 1f; rb.position = p; pm.transform.position = p; rb.linearVelocity = Vector3.zero; }
            if (paso == 0 && t > 1.5) { paso++; var menu = Object.FindFirstObjectByType<MenuPrincipal>(); if (menu) menu.Empezar(); File.WriteAllText(carpeta + "/puzzle_estado.txt", ""); var pr = Object.FindFirstObjectByType<PresentacionEnemigo>(); if (pr) pr.enabled = false; }
            else if (paso == 1 && t > 2.5) { paso++; Poner(rj.zona.bounds.center); }
            else if (paso == 2 && t > 6.0) { paso++; Log("reja cayo=" + rj.Usada + " y=" + rj.reja.position.y.ToString("0.00") + " codigo=" + string.Join("", pz.Codigo)); Poner(pz.transform.parent.Find("Panel Central").position + Vector3.left * 1.2f); }
            else if (paso == 3 && t > 7.0)
            {
                paso++; pz.Enviar(); Log("enviado codigo erroneo; resuelto=" + pz.Resuelto + " sonidoBoton=" + (pz.sonidoBoton ? pz.sonidoBoton.name : "ninguno"));
                var lab = GameObject.Find("Laberinto/casilleros");
                int con = 0, total = 0;
                foreach (var e in lab.GetComponentsInChildren<Escondite>(true)) { total++; if (e.sonidoAbrir && e.sonidoCerrar) con++; }
                Log("armarios del laberinto con sonido: " + con + "/" + total);
            }
            else if (paso == 4 && t > 9.0)
            {
                paso++;
                for (int c = 0; c < 4; c++) for (int k = 0; k < 10 && pz.columnas[c].text != pz.Codigo[c].ToString(); k++) pz.Subir(c);
                pz.Enviar(); Log("enviado codigo correcto; resuelto=" + pz.Resuelto);
            }
            else if (paso == 5 && t > 12.5) { paso++; ScreenCapture.CaptureScreenshot(carpeta + "/puzzle_cinematica.png"); Log("en la cinematica: camara puerta=" + (GameObject.Find("Camara Puerta Laberinto") != null) + " reja y=" + rj.reja.position.y.ToString("0.00")); }
            else if (paso == 6 && t > 19.0) { paso++; Log("fin: reja y=" + rj.reja.position.y.ToString("0.00") + " movimiento=" + pm.enabled + " camara principal=" + (Camera.main != null && Camera.main.enabled)); EditorApplication.isPlaying = false; }
            return;
        }

        // Modo "reloj": entra al cruce del laberinto; Fede no debe verse hasta mirar el reloj
        if (SessionState.GetString("PruebaDialogo.Asset", "") == "reloj")
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            var pr = Object.FindFirstObjectByType<PresentacionEnemigo>();
            void Log(string x) => File.AppendAllText(carpeta + "/reloj_estado.txt", x + System.Environment.NewLine);
            bool Visible() { foreach (var r in pr.enemigo.GetComponentsInChildren<Renderer>()) if (r.enabled) return true; return false; }
            if (paso >= 4 && paso < 7 && !sonidoVisto && GameObject.Find("Sonido Presentacion") != null)
            {
                sonidoVisto = true;
                Log("SONIDO a t=" + t.ToString("0.00") + " (reloj a t=8.0), fede en " + pr.enemigo.transform.position);
                ScreenCapture.CaptureScreenshot(carpeta + "/reloj_sonido.png");
            }
            if (paso == 0 && t > 1.5) { paso++; sonidoVisto = false; var menu = Object.FindFirstObjectByType<MenuPrincipal>(); if (menu) menu.Empezar(); File.WriteAllText(carpeta + "/reloj_estado.txt", ""); }
            else if (paso == 1 && t > 2.5) { paso++; var rj = Object.FindFirstObjectByType<RejasEntrada>(); Vector3 p = pr.zona.bounds.center; p.y = pm.transform.position.y > 2f ? 1f : pm.transform.position.y; var rb = pm.GetComponent<Rigidbody>(); rb.position = p; pm.transform.position = p; Log("en la zona; espera max=" + pr.esperaMaximaReloj); }
            else if (paso == 2 && t > 6.0) { paso++; Log("3.5 s sin tecla: fede visible=" + Visible() + " sonando=" + (GameObject.Find("Sonido Presentacion") != null) + " clip=" + (pr.sonido ? pr.sonido.name : "-")); ScreenCapture.CaptureScreenshot(carpeta + "/reloj_antes.png"); }
            else if (paso == 3 && t > 8.0) { paso++; RelojJugador.Instance.Mostrar(true); Log("se mira el reloj (como si pulsara C)"); }
            else if (paso == 4 && t > 9.0) { paso++; Log("1 s despues: fede visible=" + Visible() + " sonando=" + (GameObject.Find("Sonido Presentacion") != null));
                { Transform r3 = null; foreach (var tt in pm.transform.root.GetComponentsInChildren<Transform>(true)) if (tt.name == RelojJugador.NombreReloj) r3 = tt;
                  Log("reloj3d=" + (r3 ? r3.gameObject.activeInHierarchy + " capa=" + r3.GetComponentInChildren<Renderer>(true).gameObject.layer + " pos=" + r3.position : "NO") + " cercania=" + RelojJugador.Instance.Cercania.ToString("0.00"));
                  foreach (var cc in Camera.allCameras) Log("  cam " + cc.name + " depth=" + cc.depth + " mask=" + cc.cullingMask + " pos=" + cc.transform.position); } ScreenCapture.CaptureScreenshot(carpeta + "/reloj_despues.png"); }
            else if (paso == 5 && t > 11.0)
            {
                paso++; Log("3 s despues: fede en " + pr.enemigo.transform.position);
                var cg = GameObject.Find("Camara Presentacion");
                if (cg)
                {
                    var c = cg.GetComponent<Camera>();
                    foreach (float alto in new[] { 0.3f, 0.9f })
                    {
                        Vector3 pp = pr.enemigo.transform.position + Vector3.up * alto;
                        Vector3 vv = c.WorldToViewportPoint(pp);
                        string hits = "";
                        Vector3 o = cg.transform.position, dd = pp - o;
                        foreach (RaycastHit h in Physics.RaycastAll(o, dd.normalized, dd.magnitude - 0.3f, ~0, QueryTriggerInteraction.Ignore))
                            hits += h.collider.name + "(" + h.distance.ToString("0.0") + ") ";
                        Log("  alto " + alto + ": viewport=" + vv + " golpes=" + hits);
                    }
                }
                else Log("  no hay Camara Presentacion"); ScreenCapture.CaptureScreenshot(carpeta + "/reloj_cruce.png"); }
            else if (paso == 6 && t > 17.0) { paso++; Log("fin: cinematica terminada, movimiento=" + pm.enabled); EditorApplication.isPlaying = false; }
            return;
        }

        // Modo "brazos": el jugador pegado a una pared; los brazos no deben atravesarla
        if (SessionState.GetString("PruebaDialogo.Asset", "") == "brazos")
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (paso == 0 && t > 1.5) { paso++; var menu = Object.FindFirstObjectByType<MenuPrincipal>(); if (menu) menu.Empezar(); }
            else if (paso == 1 && t > 2.5)
            {
                paso++;
                var rb = pm.GetComponent<Rigidbody>();
                // Frente a la pared del fondo de la tienda (z ~36.1), a 0.35 m, mirando hacia ella
                Vector3 p = new Vector3(-12.5f, pm.transform.position.y, 36.45f);
                rb.position = p; rb.rotation = Quaternion.Euler(0f, 180f, 0f); pm.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 180f, 0f));
            }
            else if (paso == 2 && t > 3.5)
            {
                paso++;
                pm.GetComponent<Rigidbody>().linearVelocity = new Vector3(0f, 0f, -2f); // empuja contra la pared
                var b = Object.FindFirstObjectByType<BrazosPrimeraPersona>();
                var cam = Camera.main;
                var stack = cam ? UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam).cameraStack : null;
                File.WriteAllText(carpeta + "/brazos_estado.txt", $"componente={(b && b.enabled)} capa={LayerMask.NameToLayer("Primera Persona")} stack={(stack != null ? stack.Count : -1)} " +
                    $"brazosEnCapa={(GameObject.Find("Player_Arms") ? GameObject.Find("Player_Arms").GetComponentInChildren<Renderer>(true).gameObject.layer : -1)} mascaraMain={(cam ? cam.cullingMask : 0)}" + System.Environment.NewLine);
            }
            else if (paso == 3 && t > 4.3) { paso++; ScreenCapture.CaptureScreenshot(carpeta + "/brazos_pared.png"); }
            else if (paso == 4 && t > 5.5) { paso++; EditorApplication.isPlaying = false; }
            return;
        }

        // Modo "cabina": costados solidos, requisito de las cajas y FogVolume al llegar
        if (SessionState.GetString("PruebaDialogo.Asset", "") == "cabina")
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            var rb = pm ? pm.GetComponent<Rigidbody>() : null;
            var ct = Object.FindFirstObjectByType<CabinaTeletransporte>();
            Bounds cuerpo = ct ? CabinaSotanoSetup.Limites(ct.transform.Find("Cabina")) : new Bounds();
            void Log(string x) => File.AppendAllText(carpeta + "/cabina_estado.txt", x + System.Environment.NewLine);
            GameObject fog = null;
            foreach (GameObject r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) if (r.name == "FogVolume") fog = r;
            void Empujar(Vector3 desde, Vector3 dir)
            {
                pm.enabled = false;
                rb.position = desde; pm.transform.position = desde;
                rb.linearVelocity = dir * 3f;
            }
            if (paso == 0 && t > 1.5) { paso++; var menu = Object.FindFirstObjectByType<MenuPrincipal>(); if (menu) menu.Empezar(); File.WriteAllText(carpeta + "/cabina_estado.txt", ""); Log("cuerpo cabina " + cuerpo); Log("fog activo al inicio=" + (fog && fog.activeSelf)); }
            else if (paso == 1 && t > 2.5) { paso++; Empujar(new Vector3(cuerpo.max.x + 1.2f, cuerpo.min.y + 0.95f, cuerpo.center.z), Vector3.left); }
            else if (paso == 2 && t < 4.0) { rb.linearVelocity = new Vector3(-3f, rb.linearVelocity.y, 0f); }
            else if (paso == 2) { paso++; Log("empujando desde +x: quedo en " + rb.position + " dentro=" + cuerpo.Contains(new Vector3(rb.position.x, cuerpo.center.y, rb.position.z))); Empujar(new Vector3(cuerpo.center.x, cuerpo.min.y + 0.95f, cuerpo.min.z - 1.2f), Vector3.forward); }
            else if (paso == 3 && t < 5.6) { rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 3f); }
            else if (paso == 3) { paso++; Log("empujando desde -z: quedo en " + rb.position + " dentro=" + cuerpo.Contains(new Vector3(rb.position.x, cuerpo.center.y, rb.position.z))); }
            else if (paso == 4 && t > 6.0)
            {
                // Adentro de la cabina sin haber revisado las cajas
                paso++;
                Vector3 p = ct.zona.bounds.center; p.y = cuerpo.min.y + 0.95f;
                rb.linearVelocity = Vector3.zero; rb.position = p; pm.transform.position = p;
                Log("cajas completas=" + (ct.cajasRequeridas && ct.cajasRequeridas.Completa));
            }
            else if (paso == 5 && t > 8.0) { paso++; Log("2 s adentro sin cajas: teletransporte usado=" + ct.Usada); foreach (var c in ct.cajasRequeridas.cajas) c.Revisada = true; ct.cajasRequeridas.alEncontrarLinterna?.Invoke(); var mu = GameObject.Find("zotano/muñeco ventriloco"); Log("cajas revisadas; muneco brilla=" + (mu && mu.GetComponent<Highlightable>() && mu.GetComponent<Highlightable>().IsHighlighted)); }
            else if (paso == 6 && t > 9.0) { paso++; Log("1 s despues: teletransporte usado=" + ct.Usada); }
            else if (paso == 7 && t > 12.5 && !File.Exists(carpeta + "/desmayo.png"))
            {
                ScreenCapture.CaptureScreenshot(carpeta + "/desmayo.png");
                var cb = GameObject.Find("Camara Brazos");
                Log("durante el desmayo: camara de brazos activa=" + (cb && cb.GetComponent<Camera>().enabled));
            }
            else if (paso == 7 && t > 22.0)
            {
                paso++;
                var mu = GameObject.Find("zotano/muñeco ventriloco");
                var pa = GameObject.Find("Zona_Principal/Cabina_Telefonica (1)/Telefono/panel del telefono");
                Log("despues del viaje: jugador en " + pm.transform.position + " muneco brilla=" + (mu && mu.GetComponent<Highlightable>().IsHighlighted) + " panel brilla=" + (pa && pa.GetComponent<Highlightable>().IsHighlighted));
                ScreenCapture.CaptureScreenshot(carpeta + "/panel_brillo.png");
                var cs = Object.FindFirstObjectByType<CabinaSalida>();
                if (cs) cs.Abrir();
            }
            else if (paso == 8 && t > 24.0)
            {
                paso++;
                var rb2 = pm.GetComponent<Rigidbody>();
                Vector3 fuera = pm.transform.position + Vector3.left * 3f;
                rb2.position = fuera; pm.transform.position = fuera;
            }
            else if (paso == 9 && t > 25.5)
            {
                paso++;
                var dm = DialogueManager.Instance;
                Log("al salir: dialogo activo=" + (dm && dm.IsActive) + " texto=" + (dm && dm.CurrentData ? dm.CurrentData.lineas[0] : "-"));
                ScreenCapture.CaptureScreenshot(carpeta + "/aviso_correr.png");
            }
            else if (paso == 10 && t > 26.5) { paso++; EditorApplication.isPlaying = false; }
            return;
        }

        // Modo "volumen": entra al laberinto caminando desde el pasillo y revisa el Volume de zonas
        if (SessionState.GetString("PruebaDialogo.Asset", "") == "volumen")
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            var vz = Object.FindFirstObjectByType<VolumenZonas>();
            var trig = (GameObject.Find("Laberinto/Trigger Laberinto") ?? GameObject.Find("Enemigos y Zonas/Triggers Volumen/Trigger Laberinto"));
            Vector3 ini = GameObject.Find("Laberinto/Inicio Laberinto").transform.position;
            void Estado(string cuando)
            {
                var v = vz ? vz.GetComponent<UnityEngine.Rendering.Volume>() : null;
                File.AppendAllText(carpeta + "/volumen_estado.txt", cuando + ": jugador=" + (pm ? pm.transform.position.ToString() : "?") +
                    " tag=" + (pm ? pm.tag : "?") + " perfil=" + (v && v.sharedProfile ? v.sharedProfile.name : "ninguno") + " peso=" + (v ? v.weight.ToString("0.00") : "?") +
                    " trigger=" + (trig ? trig.GetComponent<Collider>().bounds.ToString() + " activo=" + trig.activeInHierarchy + " capa=" + trig.layer : "NO") +
                    " deseado=" + (vz && vz.PerfilDeseado ? vz.PerfilDeseado.name : "ninguno") + System.Environment.NewLine);
            }
            if (paso == 0 && t > 1.5) { paso++; var menu = Object.FindFirstObjectByType<MenuPrincipal>(); if (menu) menu.Empezar(); File.WriteAllText(carpeta + "/volumen_estado.txt", ""); }
            else if (paso == 1 && t > 2.5)
            {
                paso++;
                var rb = pm.GetComponent<Rigidbody>();
                Vector3 p0 = ini + new Vector3(-10f, 1f, 0f); // en el pasillo, antes de entrar
                rb.position = p0; pm.transform.position = p0; rb.linearVelocity = Vector3.zero;
                Estado("en el pasillo");
            }
            else if (paso >= 2 && paso < 40 && t > 3.0 + (paso - 2) * 0.1)
            {
                // camina hacia adentro moviendo el Rigidbody (como si caminara)
                paso++;
                var rb = pm.GetComponent<Rigidbody>();
                rb.MovePosition(rb.position + new Vector3(0.6f, 0f, 0f));
            }
            else if (paso >= 40 && paso < 52 && t > 7.0 + (paso - 40) * 1.0)
            {
                paso++;
                var fogv = Object.FindFirstObjectByType<FogVolume>();
                Estado("laberinto +" + (paso - 41) + "s niebla=" + (fogv ? fogv.Weight.ToString("0.00") : "no hay"));
            }
            else if (paso == 52)
            {
                paso++;
                var rb = pm.GetComponent<Rigidbody>();
                Vector3 b = GameObject.Find("Enemigos y Zonas/Triggers Volumen/Trigger Backrooms").transform.position; b.y = 1f;
                rb.position = b; pm.transform.position = b;
            }
            else if (paso >= 53 && paso < 63 && t > 20.0 + (paso - 53) * 1.0)
            {
                paso++;
                var fogv = Object.FindFirstObjectByType<FogVolume>();
                Estado("backrooms +" + (paso - 54) + "s niebla=" + (fogv ? fogv.Weight.ToString("0.00") : "no hay"));
            }
            else if (paso == 63 && t > 31) { paso++; EditorApplication.isPlaying = false; }
            return;
        }

        // Modo "laberinto": prueba la reja de la entrada y la presentacion de Fede en el cruce
        if (SessionState.GetString("PruebaDialogo.Asset", "") == "laberinto")
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            var lab = GameObject.Find("Laberinto");
            Vector3 x0 = lab ? lab.transform.Find("Inicio Laberinto").position : Vector3.zero; // centro de la celda (0,10)
            void Poner(Vector3 pos, float yaw)
            {
                if (!pm) return;
                var rb = pm.GetComponent<Rigidbody>();
                pos.y = pm.transform.position.y > 2f ? 1.0f : pm.transform.position.y;
                if (rb) { rb.position = pos; rb.rotation = Quaternion.Euler(0f, yaw, 0f); rb.linearVelocity = Vector3.zero; }
                pm.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            }
            void Foto(string n) => ScreenCapture.CaptureScreenshot(carpeta + "/" + n + ".png");
            double[] tiempos = { 1.5, 2.6, 3.2, 3.5, 4.0, 4.6, 5.6, 7.0, 7.6, 9.4, 11.0, 12.0, 13.0, 14.5, 18.5 };
            if (paso < tiempos.Length && t > tiempos[paso])
            {
                switch (paso)
                {
                    case 0: { var menu = Object.FindFirstObjectByType<MenuPrincipal>(); if (menu) menu.Empezar(); break; }
                    case 1: Poner(x0 + new Vector3(0.2f, 0f, 0f), 90f); break;
                    case 2:
                        Poner(x0 + new Vector3(8f, 0f, 0f), 90f);
                        var rj = Object.FindFirstObjectByType<RejasEntrada>();
                        File.WriteAllText(carpeta + "/lab_estado.txt", $"rejas={(rj != null)} sonido={(rj && rj.estruendo)} friccion={(pm && pm.GetComponent<Collider>().sharedMaterial ? pm.GetComponent<Collider>().sharedMaterial.name : "ninguna")}\n");
                        break;
                    case 3: Foto("rejas_1"); break;
                    case 4: Foto("rejas_2"); break;
                    case 5: Foto("rejas_3"); break;
                    case 6: Foto("rejas_4"); File.AppendAllText(carpeta + "/lab_estado.txt", $"rejas usada={Object.FindFirstObjectByType<RejasEntrada>()?.Usada} yaw jugador={pm.transform.eulerAngles.y:0}\n"); break;
                    case 7: Poner(x0 + new Vector3(16f, 0f, 0.4f), 0f); break;
                    case 8: Poner(x0 + new Vector3(16f, 0f, 4.6f), 0f); break;
                    case 9: Foto("fede_1"); break;
                    case 10: Foto("fede_2"); break;
                    case 11: Foto("fede_3"); break;
                    case 12: Foto("fede_4"); break;
                    case 13: { var pr = Object.FindFirstObjectByType<PresentacionEnemigo>(); File.AppendAllText(carpeta + "/lab_estado.txt", $"presentacion usada={(pr && pr.Usada)} fede en {(pr && pr.enemigo ? pr.enemigo.transform.position.ToString() : "?")}\n"); break; }
                    case 14: EditorApplication.isPlaying = false; break;
                }
                paso++;
            }
            return;
        }

        // Modo "puerta": deja correr el tutorial y registra el angulo de la puerta mientras entra el cliente
        if (SessionState.GetString("PruebaDialogo.Asset", "") == "puerta")
        {
            if (paso == 0 && t > 1.5) { paso++; var menu = Object.FindFirstObjectByType<MenuPrincipal>(); if (menu) menu.Empezar(); File.WriteAllText(carpeta + "/puerta_estado.txt", ""); ultimoLog = 0; capturada = false; }
            var puerta = Object.FindFirstObjectByType<PuertaNPC>();
            var tut = Object.FindFirstObjectByType<TutorialTienda>();
            if (paso == 1 && puerta && t - ultimoLog > 0.5)
            {
                ultimoLog = t;
                float ang = Quaternion.Angle(Quaternion.identity, puerta.transform.localRotation);
                string npc = tut && tut.cliente && tut.cliente.gameObject.activeInHierarchy ? $"cliente en {tut.cliente.position}" : "cliente inactivo";
                File.AppendAllText(carpeta + "/puerta_estado.txt", $"t={t:0.0} angulo={ang:0} {npc} paso={(tut ? tut.Paso : "?")}\n");
                if (!capturada && ang > 80f) { capturada = true; CapturasEditor.Capturar("puerta_abierta_real", new Vector3(-13.2f, 5.7f, 50.5f), new Vector3(-15.4f, 5.2f, 46f), 65f); }
            }
            if (t > 16) { paso = 99; EditorApplication.isPlaying = false; }
            return;
        }
        if (paso == 0 && t > 1.5)
        {
            paso++;
            var menu = Object.FindFirstObjectByType<MenuPrincipal>();
            if (menu) menu.Empezar(); // sin cinematica
        }
        else if (paso == 1 && t > 3.5)
        {
            paso++;
            if (DialogueManager.Instance && DialogueManager.Instance.IsActive) DialogueManager.Instance.EndDialogue();
            string ruta = SessionState.GetString("PruebaDialogo.Asset", "");
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(string.IsNullOrEmpty(ruta) ? "Assets/Dialogos/Tutorial/Cultista_PideMuneco.asset" : ruta);
            if (DialogueManager.Instance && data) DialogueManager.Instance.StartDialogue(data);
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            File.WriteAllText(carpeta + "/dialogo_estado.txt",
                $"manager={(DialogueManager.Instance != null)} data={(data != null)} activo={(DialogueManager.Instance && DialogueManager.Instance.IsActive)} movimientoHabilitado={(pm && pm.enabled)}\n");
        }
        else if (paso == 2 && t > 4.6) { paso++; ScreenCapture.CaptureScreenshot(carpeta + "/dialogo_1.png"); }
        else if (paso == 3 && t > 7.2) { paso++; ScreenCapture.CaptureScreenshot(carpeta + "/dialogo_2.png"); }
        else if (paso == 4 && t > 10.2) { paso++; ScreenCapture.CaptureScreenshot(carpeta + "/dialogo_3.png"); }
        else if (paso == 5 && t > 15)
        {
            paso++;
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            File.AppendAllText(carpeta + "/dialogo_estado.txt",
                $"al final: activo={(DialogueManager.Instance && DialogueManager.Instance.IsActive)} movimientoHabilitado={(pm && pm.enabled)}\n");
            EditorApplication.isPlaying = false;
        }
    }
}
