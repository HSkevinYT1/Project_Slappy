using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Panel de pruebas escondido. Solo existe en el Editor y en las Development Builds (en una build normal no hay nada).
// Se abre y se cierra con la tecla de debajo de Esc (º en teclado espanol, ` en ingles) o con F9.
// Mientras esta abierto el juego queda en pausa y el cursor libre.
// - Partida: saltar el menu y la intro; saltar el tutorial (tienda, sotano, cabina y telefono) y aparecer en la estacion.
// - Zonas: abrir cualquier zona (se quita la barrera de su pasillo y las anteriores quedan completadas; se entra
//   caminando desde la estacion), completar la actual (como si la terminara) o volver a la estacion.
// - Jugador: invencible, estamina infinita, velocidad, dar/recargar la linterna, ir adonde se esta mirando.
// - Mundo: congelar a los enemigos y la velocidad del tiempo.
// Con algun truco activo sale un aviso chiquito arriba a la derecha. Se crea solo al jugar (no hay que ponerlo en la escena).
public class PanelDebug : MonoBehaviour
{
    // Los trucos siguen al recargar la escena (ej: al volver al inicio); se reinician al darle Play
    static bool estaminaInfinita, congelar;
    static float velocidad = 1f, tiempo = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        estaminaInfinita = congelar = false;
        velocidad = tiempo = 1f;
        PantallaMuerte.Invencible = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AlCargar()
    {
        if (!Debug.isDebugBuild) return;
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        Crear();
    }

    static void AlCargarEscena(Scene s, LoadSceneMode m) => Crear();

    static void Crear()
    {
        if (FindFirstObjectByType<PanelDebug>()) return;
        new GameObject("Panel Debug").AddComponent<PanelDebug>();
    }

    bool abierto;
    Rect ventana = new Rect(16f, 16f, 350f, 100f);
    // Lo que se aprieta en el panel se hace en el Update siguiente (cambiar cosas en medio del OnGUI descuadra el layout)
    System.Action pendiente;
    string mensaje = "";
    float mensajeHasta;

    readonly List<Behaviour> apagados = new List<Behaviour>();
    readonly List<Behaviour> congelados = new List<Behaviour>();
    CursorLockMode cursorAntes;
    bool visibleAntes, menuAlAbrir;
    PlayerMovement pm;
    float velocidadBase = -1f;
    float proximoCongelar;

    // ------------------------------------------------------------------ Abrir y cerrar

    void Update()
    {
        Keyboard k = Keyboard.current;
        if (k != null && (k.backquoteKey.wasPressedThisFrame || k.f9Key.wasPressedThisFrame))
        {
            if (abierto) Cerrar();
            else if (!PauseMenu.IsPaused && !PantallaMuerte.Muerto) Abrir();
        }

        if (pendiente != null)
        {
            System.Action a = pendiente;
            pendiente = null;
            a();
        }

        if (abierto)
        {
            // Algun script pudo volver a bloquear el cursor o a soltar el tiempo
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (Time.timeScale != 0f) Time.timeScale = 0f;
        }
        Trucos();
    }

    void Abrir()
    {
        abierto = true;
        cursorAntes = Cursor.lockState;
        visibleAntes = Cursor.visible;
        menuAlAbrir = MenuPrincipal.Abierto;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        apagados.Clear();
        Apagar<MouseLook>();
        Apagar<PlayerInteractor>();
        Apagar<GraspObject>();
        Apagar<PauseMenu>();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Cerrar()
    {
        if (!abierto) return;
        abierto = false;
        foreach (Behaviour b in apagados) if (b != null) b.enabled = true;
        apagados.Clear();
        AudioListener.pause = false;
        Time.timeScale = tiempo;
        if (MenuPrincipal.Abierto) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        else if (menuAlAbrir) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        else { Cursor.lockState = cursorAntes; Cursor.visible = visibleAntes; }
    }

    void OnDestroy()
    {
        if (!abierto) return;
        AudioListener.pause = false;
        Time.timeScale = tiempo;
    }

    void Apagar<T>() where T : Behaviour
    {
        foreach (T b in FindObjectsByType<T>(FindObjectsSortMode.None))
            if (b.enabled) { b.enabled = false; apagados.Add(b); }
    }

    void Aviso(string texto)
    {
        mensaje = texto;
        mensajeHasta = Time.unscaledTime + 5f;
    }

    // ------------------------------------------------------------------ Trucos que duran

    void Trucos()
    {
        if (pm == null)
        {
            pm = FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            velocidadBase = pm != null ? pm.speed : -1f;
            if (pm != null && velocidad != 1f) pm.speed = velocidadBase * velocidad;
        }
        if (pm != null && estaminaInfinita) pm.RellenarEstamina();

        if (Time.unscaledTime >= proximoCongelar)
        {
            proximoCongelar = Time.unscaledTime + 0.4f;
            Congelar();
        }
        // Si algo devolvio el tiempo a la normalidad (ej: la pausa), se vuelve a poner el elegido
        if (!abierto && tiempo != 1f && Time.timeScale == 1f && !PauseMenu.IsPaused) Time.timeScale = tiempo;
    }

    void AplicarVelocidad(float v)
    {
        velocidad = v;
        if (pm != null && velocidadBase > 0f) pm.speed = velocidadBase * v;
    }

    void Congelar()
    {
        if (congelar)
        {
            foreach (JefeIndigoAI e in FindObjectsByType<JefeIndigoAI>(FindObjectsSortMode.None)) Parar(e);
            foreach (EstatuaAI e in FindObjectsByType<EstatuaAI>(FindObjectsSortMode.None)) Parar(e);
            return;
        }
        if (congelados.Count == 0) return;
        foreach (Behaviour b in congelados)
        {
            if (b == null) continue;
            b.enabled = true;
            if (b.TryGetComponent(out NavMeshAgent a) && a.isOnNavMesh) a.isStopped = false;
        }
        congelados.Clear();
    }

    void Parar(Behaviour b)
    {
        if (!b.enabled) return;
        b.enabled = false;
        if (b.TryGetComponent(out NavMeshAgent a) && a.isOnNavMesh) { a.isStopped = true; a.velocity = Vector3.zero; }
        congelados.Add(b);
    }

    // ------------------------------------------------------------------ Partida

    bool TutorialHecho()
    {
        CabinaTeletransporte cab = FindFirstObjectByType<CabinaTeletransporte>(FindObjectsInactive.Include);
        return cab == null || cab.Usada;
    }

    void SaltarMenu()
    {
        if (!MenuPrincipal.Abierto) return;
        // Si la intro iba por la mitad, se termina bien (si no, el jugador se queda sin brazos ni cuerpo)
        IntroTienda intro = FindFirstObjectByType<IntroTienda>();
        if (intro != null && intro.Reproduciendo) intro.Terminar();
        MenuPrincipal m = FindFirstObjectByType<MenuPrincipal>();
        if (m != null) m.Empezar();
    }

    // Como si se hubiera hecho todo: sin cliente ni cultista, con la linterna, la cabina usada (sin desmayo) y el
    // telefono de la otra cabina arreglado (la puerta abierta)
    void SaltarTutorial()
    {
        SaltarMenu();
        foreach (TutorialTienda t in FindObjectsByType<TutorialTienda>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            t.StopAllCoroutines();
            t.enabled = false;
            if (t.cliente != null) t.cliente.gameObject.SetActive(false);
            if (t.cultista != null) t.cultista.gameObject.SetActive(false);
            if (t.marcoSotano != null) t.marcoSotano.Hide();
        }
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsActive) DialogueManager.Instance.EndDialogue();
        DarLinterna();

        CabinaTeletransporte cab = FindFirstObjectByType<CabinaTeletransporte>(FindObjectsInactive.Include);
        Transform j = Jugador();
        if (cab != null && !cab.Usada && j != null)
        {
            Vector3 dentro = j.position;
            if (cab.zona != null && Suelo(cab.zona.bounds.center, out Vector3 piso)) dentro = piso + Vector3.up * (Pies(j) + 0.05f);
            cab.Saltar(dentro);
        }
        foreach (PhoneMinigame ph in FindObjectsByType<PhoneMinigame>(FindObjectsSortMode.None))
            if (!ph.IsRepaired) ph.onRepaired?.Invoke();
        AbrirCabinaSalida();
        MirarSalida();
        Guardar();
        Aviso("Tutorial saltado: estás en la estación.");
    }

    static void AbrirCabinaSalida()
    {
        foreach (CabinaSalida c in FindObjectsByType<CabinaSalida>(FindObjectsSortMode.None))
            if (c.Cerrada) c.Abrir();
    }

    void MirarSalida()
    {
        Transform j = Jugador();
        CabinaSalida c = FindFirstObjectByType<CabinaSalida>();
        if (j == null || c == null || c.puerta == null) return;
        Vector3 d = c.puerta.position - j.position; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        Teleport(j.position - Vector3.up * (Pies(j) + 0.05f), Quaternion.LookRotation(d).eulerAngles.y);
    }

    // ------------------------------------------------------------------ Zonas

    // Se quita la barrera del pasillo de esa zona (y las anteriores cuentan como hechas); se entra caminando.
    // Si todavia no se paso el tutorial, se salta primero (las zonas estan en la estacion)
    void AbrirZona(int i)
    {
        ProgresoCaminos p = ProgresoCaminos.Instance;
        if (p == null || p.tramos == null || i >= p.tramos.Length) return;
        bool tutorial = !TutorialHecho();
        if (tutorial) SaltarTutorial();
        else SaltarMenu();
        p.SaltarA(i);
        Aviso(p.tramos[i].nombre + " abierta: entra desde la estación." + (tutorial ? " (Tutorial saltado.)" : ""));
    }

    void VolverEstacion()
    {
        if (!TutorialHecho()) { SaltarTutorial(); return; }
        SaltarMenu();
        CabinaTeletransporte cab = FindFirstObjectByType<CabinaTeletransporte>(FindObjectsInactive.Include);
        Transform j = Jugador();
        if (cab == null || cab.desmayo == null || cab.zona == null || j == null) { Aviso("No encontré la estación."); return; }
        // El mismo sitio de la otra cabina en el que se despierta tras el desmayo
        if (!Suelo(cab.zona.bounds.center, out Vector3 sotano)) sotano = cab.zona.bounds.center;
        Vector3 llegada = cab.desmayo.Equivalente(sotano);
        if (!Suelo(llegada + Vector3.up * 0.3f, out Vector3 piso, 0.3f)) piso = llegada;
        AbrirCabinaSalida();
        float yaw = j.eulerAngles.y;
        CabinaSalida c = FindFirstObjectByType<CabinaSalida>();
        if (c != null && c.puerta != null)
        {
            Vector3 d = c.puerta.position - piso; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) yaw = Quaternion.LookRotation(d).eulerAngles.y;
        }
        Teleport(piso, yaw);
        Aviso("De vuelta en la estación.");
    }

    // ------------------------------------------------------------------ Jugador

    Grabbable LaLinterna()
    {
        RevisionCajas rc = FindFirstObjectByType<RevisionCajas>(FindObjectsInactive.Include);
        return rc != null ? rc.linterna : null;
    }

    bool TieneLinterna()
    {
        Grabbable l = LaLinterna();
        if (l == null) return true;
        PlayerInteractor pi = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        return (Inventario.Instance != null && Inventario.Instance.IndiceDe(l) >= 0) || (pi != null && pi.Held == l);
    }

    void DarLinterna()
    {
        Grabbable l = LaLinterna();
        PlayerInteractor pi = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        if (l == null || pi == null) { if (l == null) Debug.LogWarning("[PanelDebug] No encontré la linterna (RevisionCajas > Linterna)."); return; }
        if (!TieneLinterna())
        {
            Camera cam = Camara();
            l.transform.SetParent(null, true);
            if (cam != null) l.transform.position = cam.transform.position + cam.transform.forward * 0.5f;
            l.gameObject.SetActive(true);
            pi.Hold(l);
            // En la mano y seleccionada (si el inventario la guardo en otra ranura, se elige esa)
            if (Inventario.Instance != null && pi.Held != l)
            {
                int r = Inventario.Instance.IndiceDe(l);
                if (r >= 0) Inventario.Instance.Seleccionar(r);
            }
        }
        // El punto de agarre esta en los brazos: si quedaron apagados (ej: la intro cortada a la mitad), se prenden
        if (l.transform.parent != null && !l.gameObject.activeInHierarchy && l.transform.IsChildOf(pi.transform.root))
            for (Transform t = l.transform.parent; t != null; t = t.parent)
                if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        if (!TieneLinterna() || !l.gameObject.activeInHierarchy)
            Debug.LogWarning("[PanelDebug] No pude dejarte la linterna en la mano: padre=" + (l.transform.parent ? l.transform.parent.name : "ninguno") +
                             " activa=" + l.gameObject.activeInHierarchy + " enLaMano=" + (pi.Held == l), l);
    }

    void Linterna()
    {
        bool tenia = TieneLinterna();
        DarLinterna();
        foreach (Flashlight f in FindObjectsByType<Flashlight>(FindObjectsInactive.Include, FindObjectsSortMode.None)) f.battery = f.maxBattery;
        Aviso(tenia ? "Linterna recargada." : "Ahí tienes la linterna (cargada).");
    }

    bool IrAdondeMiro()
    {
        Camera cam = Camara();
        Transform j = Jugador();
        if (cam == null || j == null) return false;
        if (!Rayo(cam.transform.position, cam.transform.forward, 200f, out RaycastHit h)) { Aviso("No hay nada adonde miras."); return false; }
        Vector3 p = h.point + h.normal * 0.5f;
        // Mirando al techo: se busca el piso desde abajo del techo (si no, apareceria encima)
        float alto = h.normal.y < -0.3f ? 0.2f : 1.2f;
        Vector3 atras = cam.transform.position - h.point; atras.y = 0f;
        atras = atras.sqrMagnitude > 0.0001f ? atras.normalized : -cam.transform.forward;
        for (float m = 0f; m <= 3f; m += 0.5f)
        {
            if (!Suelo(p + atras * m, out Vector3 suelo, alto) || !Libre(suelo)) continue;
            Teleport(suelo, j.eulerAngles.y);
            return true;
        }
        Aviso("No cabes ahí.");
        return false;
    }

    // ------------------------------------------------------------------ Utilidades

    Transform Jugador()
    {
        if (pm == null) pm = FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
        return pm != null ? pm.transform : null;
    }

    static Camera Camara()
    {
        PlayerInteractor pi = FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        Camera c = pi != null ? pi.GetComponent<Camera>() : null;
        return c != null ? c : Camera.main;
    }

    // Distancia del pivote del jugador a sus pies
    static float Pies(Transform j)
    {
        float min = float.MaxValue;
        foreach (Collider c in j.GetComponentsInChildren<Collider>())
            if (c.enabled && !c.isTrigger) min = Mathf.Min(min, c.bounds.min.y);
        return min == float.MaxValue ? 1f : j.position.y - min;
    }

    // Rayo que no choca con el jugador ni con triggers
    bool Rayo(Vector3 o, Vector3 d, float dist, out RaycastHit hit)
    {
        hit = default;
        RaycastHit[] hits = Physics.RaycastAll(o, d, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        Transform j = Jugador();
        foreach (RaycastHit h in hits)
        {
            if (j != null && h.collider.transform.IsChildOf(j)) continue;
            hit = h;
            return true;
        }
        return false;
    }

    bool Suelo(Vector3 p, out Vector3 suelo, float alto = 1.2f)
    {
        suelo = p;
        if (!Rayo(p + Vector3.up * alto, Vector3.down, alto + 8f, out RaycastHit h) || h.normal.y < 0.6f) return false;
        suelo = h.point;
        return true;
    }

    // Cabe el jugador parado ahi
    bool Libre(Vector3 suelo)
    {
        Transform j = Jugador();
        foreach (Collider c in Physics.OverlapCapsule(suelo + Vector3.up * 0.45f, suelo + Vector3.up * 1.6f, 0.28f,
                                                      Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (j == null || !c.transform.IsChildOf(j)) return false;
        return true;
    }

    void Teleport(Vector3 suelo, float yaw)
    {
        Transform j = Jugador();
        if (j == null) return;
        Vector3 pos = suelo + Vector3.up * (Pies(j) + 0.05f);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        j.SetPositionAndRotation(pos, rot);
        if (j.TryGetComponent(out Rigidbody rb))
        {
            rb.position = pos;
            rb.rotation = rot;
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }
        Physics.SyncTransforms();
        // MouseLook toma el giro nuevo del cuerpo al reactivarse
        foreach (MouseLook m in FindObjectsByType<MouseLook>(FindObjectsSortMode.None))
            if (m.enabled) { m.enabled = false; m.enabled = true; }
        Guardar();
    }

    // Si muere, reaparece aqui
    static void Guardar()
    {
        if (PantallaMuerte.Instance != null) PantallaMuerte.Instance.Guardar();
    }

    // ------------------------------------------------------------------ Dibujo

    GUIStyle sVentana, sTitulo, sSeccion, sBoton, sBotonOn, sChip, sChipOn, sTexto, sAviso, sIndicador;

    void OnGUI()
    {
        Estilos();
        float s = Mathf.Clamp(Screen.height / 900f, 0.75f, 2.5f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
        if (!abierto) { Indicador(s); return; }
        ventana.width = 350f;
        ventana = GUILayout.Window(73101, ventana, Dibujar, GUIContent.none, sVentana, GUILayout.Width(350f));
    }

    void Dibujar(int id)
    {
        GUILayout.Label("PANEL DEBUG", sTitulo);

        Seccion("Partida");
        if (MenuPrincipal.Abierto && Boton("Saltar menú e intro")) Hacer(SaltarMenu, true);
        bool hecho = TutorialHecho();
        GUI.enabled = !hecho;
        if (Boton(hecho ? "Tutorial terminado" : "Saltar tutorial (aparecer en la estación)")) Hacer(SaltarTutorial, true);
        GUI.enabled = true;

        ProgresoCaminos p = ProgresoCaminos.Instance;
        if (p != null && p.tramos != null)
        {
            Seccion("Zonas");
            string actual = p.TramoActual != null ? p.TramoActual.nombre : "todas completadas";
            GUILayout.Label("Toca: " + actual + (p.SiguienteEsperando ? "  (su puerta espera a que vuelvas)" : ""), sTexto);
            for (int i = 0; i < p.tramos.Length; i++)
            {
                if (p.tramos[i] == null) continue;
                int indice = i;
                string estado = i < p.zonaActual ? "   ·  hecha" : i == p.zonaActual ? "   ·  abierta" : "";
                if (Boton("Abrir " + p.tramos[i].nombre + estado)) Hacer(() => AbrirZona(indice), true);
            }
            GUILayout.BeginHorizontal();
            GUI.enabled = p.TramoActual != null;
            if (Boton("Completar la actual"))
                Hacer(() => { string n = p.TramoActual != null ? p.TramoActual.nombre : ""; p.CompletarZonaActual(); Aviso(n + " completada (la siguiente se abre al volver a la estación)."); }, false);
            GUI.enabled = true;
            if (Boton("Volver a la estación")) Hacer(VolverEstacion, true);
            GUILayout.EndHorizontal();
        }

        Seccion("Jugador");
        PantallaMuerte.Invencible = Interruptor("Invencible (los enemigos no matan)", PantallaMuerte.Invencible);
        estaminaInfinita = Interruptor("Estamina infinita", estaminaInfinita);
        Opciones("Velocidad", velocidad, new[] { 1f, 1.5f, 2f, 3f }, AplicarVelocidad);
        GUILayout.BeginHorizontal();
        if (Boton(TieneLinterna() ? "Recargar linterna" : "Dar linterna")) Hacer(Linterna, false);
        if (Boton("Ir adonde miro")) pendiente = () => { if (IrAdondeMiro()) Cerrar(); };
        GUILayout.EndHorizontal();
        GUI.enabled = PiedraColeccionable.Recogidas < PiedraColeccionable.Total;
        if (Boton("Dar las 4 piedras (" + PiedraColeccionable.Recogidas + "/" + PiedraColeccionable.Total + ")"))
            Hacer(() => { PiedraColeccionable.DarTodas(); Aviso("Tienes las 4 piedras: al final sale la cinemática secreta."); }, false);
        GUI.enabled = true;

        Seccion("Mundo");
        congelar = Interruptor("Congelar enemigos", congelar);
        Opciones("Tiempo", tiempo, new[] { 0.25f, 0.5f, 1f, 2f, 4f }, v => tiempo = v);

        if (mensaje != "" && Time.unscaledTime < mensajeHasta) { GUILayout.Space(4f); GUILayout.Label(mensaje, sAviso); }
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("º o F9 para cerrar. El juego está en pausa.", sTexto);
        if (GUILayout.Button("Cerrar", sChip, GUILayout.Width(70f))) pendiente = Cerrar;
        GUILayout.EndHorizontal();
    }

    void Hacer(System.Action accion, bool cerrar)
    {
        pendiente = () =>
        {
            accion();
            if (cerrar) Cerrar();
        };
    }

    void Seccion(string titulo)
    {
        GUILayout.Space(8f);
        GUILayout.Label(titulo.ToUpperInvariant(), sSeccion);
    }

    bool Boton(string texto) => GUILayout.Button(texto, sBoton);

    bool Interruptor(string texto, bool valor) => GUILayout.Button((valor ? "[ON]   " : "[  ]   ") + texto, valor ? sBotonOn : sBoton) ? !valor : valor;

    void Opciones(string texto, float actual, float[] valores, System.Action<float> elegir)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(texto, sTexto, GUILayout.Width(72f));
        foreach (float v in valores)
            if (GUILayout.Button("x" + v.ToString("0.##"), Mathf.Approximately(actual, v) ? sChipOn : sChip)) elegir(v);
        GUILayout.EndHorizontal();
    }

    // Con el panel cerrado: que trucos estan puestos (y el ultimo aviso)
    void Indicador(float s)
    {
        if (MenuPrincipal.Abierto) return;
        var partes = new List<string>();
        if (PantallaMuerte.Invencible) partes.Add("invencible");
        if (estaminaInfinita) partes.Add("estamina infinita");
        if (velocidad != 1f) partes.Add("velocidad x" + velocidad.ToString("0.##"));
        if (congelar) partes.Add("enemigos congelados");
        if (tiempo != 1f) partes.Add("tiempo x" + tiempo.ToString("0.##"));
        float ancho = Screen.width / s;
        float y = 8f;
        if (partes.Count > 0)
        {
            GUI.Label(new Rect(ancho - 612f, y, 600f, 18f), "DEBUG   " + string.Join("  ·  ", partes), sIndicador);
            y += 18f;
        }
        if (mensaje != "" && Time.unscaledTime < mensajeHasta) GUI.Label(new Rect(ancho - 612f, y, 600f, 18f), mensaje, sIndicador);
    }

    void Estilos()
    {
        if (sVentana != null) return;
        Color texto = new Color(0.9f, 0.88f, 0.85f);
        Color acento = new Color(0.95f, 0.36f, 0.3f);

        sVentana = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 12, 12) };
        sVentana.normal.background = Tex(new Color(0.055f, 0.055f, 0.065f, 0.95f));

        sTitulo = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        sTitulo.normal.textColor = acento;

        sSeccion = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 2, 2) };
        sSeccion.normal.textColor = new Color(0.55f, 0.55f, 0.58f);

        sTexto = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        sTexto.normal.textColor = new Color(0.72f, 0.71f, 0.69f);

        sAviso = new GUIStyle(sTexto);
        sAviso.normal.textColor = new Color(1f, 0.82f, 0.4f);

        sBoton = Boton(new Color(0.15f, 0.15f, 0.17f), texto, 13, TextAnchor.MiddleLeft, 28f);
        sBotonOn = Boton(new Color(0.42f, 0.11f, 0.09f), Color.white, 13, TextAnchor.MiddleLeft, 28f);
        sChip = Boton(new Color(0.15f, 0.15f, 0.17f), texto, 12, TextAnchor.MiddleCenter, 24f);
        sChipOn = Boton(new Color(0.42f, 0.11f, 0.09f), Color.white, 12, TextAnchor.MiddleCenter, 24f);

        sIndicador = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperRight };
        sIndicador.normal.textColor = new Color(1f, 0.5f, 0.42f, 0.85f);
    }

    static GUIStyle Boton(Color fondo, Color texto, int tam, TextAnchor alineado, float alto)
    {
        var s = new GUIStyle(GUI.skin.button)
        {
            fontSize = tam,
            alignment = alineado,
            fixedHeight = alto,
            padding = new RectOffset(10, 10, 4, 4),
            margin = new RectOffset(0, 0, 2, 2),
            border = new RectOffset(0, 0, 0, 0),
        };
        s.normal.background = Tex(fondo);
        s.hover.background = Tex(fondo * 1.6f + new Color(0.03f, 0.03f, 0.03f, 0f));
        s.active.background = Tex(fondo * 2.2f + new Color(0.06f, 0.06f, 0.06f, 0f));
        s.focused.background = s.normal.background;
        s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = texto;
        return s;
    }

    static Texture2D Tex(Color c)
    {
        c.a = c.a <= 0f ? 1f : Mathf.Clamp01(c.a);
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
