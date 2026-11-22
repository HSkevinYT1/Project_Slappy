using System.Collections.Generic;
using UnityEngine;

// Lo que dice el protagonista segun el guion (y las ayudas de las Cloacas). Solo DIALOGOS: no cambia nada del juego,
// solo escucha lo que ya pasa (eventos y estados que ya existen) y en ese momento dice la frase (ver Pensamiento).
// Cada frase se dice una sola vez por partida. Los textos se editan en el Inspector (Tools > Dialogos > Guion de ayudas
// crea este objeto en la escena; si no esta, se crea solo al jugar con los textos de abajo).
// Un texto con varias lineas (Enter) sale como varios subtitulos seguidos. Vacio = no se dice nada.
public class GuionDialogos : MonoBehaviour
{
    [Header("Tienda")]
    [TextArea] public string trasPedidoCliente = "Sí, ahora mismo se lo traigo.";
    [TextArea] public string trasPedidoCultista = "Debe estar abajo, en el sótano. Espere aquí un momento, por favor.";

    [Header("Sótano (cajas)")]
    [TextArea] public string cajaCarro = "Sigo sin entender por qué el dueño guarda estas cosas.";
    [TextArea] public string cajaCollar = "¿A quién le importa esta baratija que ni gemas tiene?";
    [TextArea] public string cajaLinterna = "Hey, todavía tiene pila. Debería sacarla de aquí, por si acaso se va la luz arriba.";
    [TextArea] public string cajasTerminadas = "Bueno, al parecer no está por estas cajas.";

    [Header("Zona Principal (al salir de la cabina)")]
    [TextArea] public string llegada = "¿En dónde demonios estoy?";
    [TextArea] public string unicoCamino = "Bueno, al parecer no tengo otro lugar a donde ir.";

    [Header("Laberinto")]
    [TextArea] public string relojLoco = "¿Qué demonios le está pasando a esta cosa?";
    [TextArea] public string trasPresentacion = "¿Qué demonios es esa cosa?";
    [TextArea] public string primeraBateria = "Al menos tengo baterías para la linterna. Me daría algo si se llegara a apagar.";
    [TextArea] public string zonaSegura = "¿Y ahora qué es este lugar? ¿Y qué es esa cosa en la pared?";
    [TextArea] public string panelCentral = "¿Necesita un código de 4 dígitos? La pregunta es para qué es esta cosa.";
    [Tooltip("Al encontrar el 1er, 2do, 3er y 4to panel con un numero")]
    [TextArea] public string[] panelesPista =
    {
        "Asumo que esto tiene algo que ver con ese código de 4 dígitos, pero esta cosa está muy dañada. Me pregunto si habrá más de estas.",
        "Aquí hay otro número. Solo faltan 2 más.",
        "Otro número. Ya solo falta uno. Me pregunto quién habrá hecho estos paneles.",
        "Y aquí está el último. Solo falta llegar otra vez a ese panel sin que esa cosa me atrape.",
    };
    [TextArea] public string codigoCorrecto = "¡Mierda! Tengo que salir de aquí antes de que esa cosa me atrape.";
    [Tooltip("A esta distancia (m) de un panel se considera encontrado")]
    public float distanciaPanel = 2.6f;

    [Header("Estación (al volver de una zona y al abrirse la siguiente)")]
    [TextArea] public string trasLaberinto = "¿Qué demonios era esa cosa?";
    [TextArea] public string abreBackrooms = "¿Por qué se abrió otra puerta?\n¿Acaso me van a hacer vivir esta mierda de nuevo?";
    [TextArea] public string trasBackrooms = "Ese gato se parecía al de mi abuela... claro, si este fuera un demonio.";
    [TextArea] public string abreCloacas = "Bueno, supongo que tendré que hacer esto otra vez.\nAl menos espero que la siguiente esté llena de colores y dulces.";
    [TextArea] public string trasCloacas = "Maldito lugar... ahora tengo los pies mojados.";
    [TextArea] public string abreZonaRoja = "Por lo que veo, esa es la última.";

    [Header("Cloacas (ayudas)")]
    [TextArea] public string rejaCerrada = "Se cerró... Tal vez haya una llave por aquí.";
    [TextArea] public string estatuaMovida = "¿Esa cosa se movió? ...Solo se mueven cuando no las estoy mirando.";
    [TextArea] public string pistaTintineo = "¿Eso que suena son... llaves?";
    [TextArea] public string pistaEstatuas = "Alguna de esas estatuas tiene que tener la llave. Mejor no perderlas de vista.";
    [TextArea] public string llaveEncontrada = "¡La llave! Ahora a la cerradura de la reja.";
    [TextArea] public string rejaAbierta = "Por fin. Hora de salir de aquí.";
    [Tooltip("Segundos encerrado sin la llave hasta cada pista")]
    public float segundosPistaTintineo = 35f, segundosPistaEstatuas = 90f;

    // ------------------------------------------------------------------ Estado

    DialogueData ultimoDialogo;
    TutorialTienda tutorial;
    RevisionCajas revision;
    readonly HashSet<CajaRevisable> cajasVistas = new HashSet<CajaRevisable>();
    Bateria[] baterias = new Bateria[0];
    PuzzleContrasena contrasena;
    readonly List<Transform> panelesPendientes = new List<Transform>();
    int panelesEncontrados;
    SalidaConLlave salida;
    float cerradaDesde = -1f;
    EstatuaAI[] estatuas = new EstatuaAI[0];
    readonly Dictionary<EstatuaAI, (EstatuaAI.State estado, Vector3 pos)> estadoEstatuas = new Dictionary<EstatuaAI, (EstatuaAI.State, Vector3)>();
    Transform jugador;
    float proximaRevision;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AlCargar()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= AlCargarEscena;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += AlCargarEscena;
        Crear();
    }

    static void AlCargarEscena(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Crear();

    // En las escenas con jugador, si no hay uno puesto a mano, se crea con los textos por defecto
    static void Crear()
    {
        if (FindFirstObjectByType<GuionDialogos>(FindObjectsInactive.Include)) return;
        if (!FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include)) return;
        new GameObject("Guion Dialogos").AddComponent<GuionDialogos>();
    }

    void Start()
    {
        // Tienda: responde despues de que el cliente y el cultista piden algo
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStarted += AlEmpezarDialogo;
            DialogueManager.Instance.OnDialogueEnded += AlTerminarDialogo;
        }
        tutorial = FindFirstObjectByType<TutorialTienda>(FindObjectsInactive.Include);

        // Sotano
        revision = FindFirstObjectByType<RevisionCajas>(FindObjectsInactive.Include);

        // Zona Principal: la cabina de llegada es la que muestra el aviso al salir
        foreach (AvisoAlSalirCabina a in FindObjectsByType<AvisoAlSalirCabina>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (a.TryGetComponent(out CabinaSalida c)) c.alAbrir.AddListener(AlLlegar);

        // Laberinto
        foreach (PresentacionEnemigo p in FindObjectsByType<PresentacionEnemigo>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            p.alTerminar.AddListener(() => Pensamiento.Decir("lab.presentacion", 0.6f, trasPresentacion));
        foreach (ZonaSeguraLianas z in FindObjectsByType<ZonaSeguraLianas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            z.alLlegar.AddListener(() => Pensamiento.Decir("lab.zonaSegura", 0.3f, zonaSegura));
        contrasena = FindFirstObjectByType<PuzzleContrasena>(FindObjectsInactive.Include);
        if (contrasena != null)
        {
            contrasena.alResolver.AddListener(() => Pensamiento.Decir("lab.codigo", contrasena.duracionCinematica + 0.4f, codigoCorrecto));
            if (contrasena.digitosPista != null)
                foreach (var d in contrasena.digitosPista) if (d != null) panelesPendientes.Add(d.transform);
        }
        baterias = FindObjectsByType<Bateria>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // Estacion: al volver de una zona y cuando se abre la siguiente
        progreso = FindFirstObjectByType<ProgresoCaminos>(FindObjectsInactive.Include);
        if (progreso != null)
        {
            progreso.AlVolver += AlVolverAEstacion;
            progreso.AlAbrir += AlAbrirZona;
        }

        // Cloacas
        salida = FindFirstObjectByType<SalidaConLlave>(FindObjectsInactive.Include);
        if (salida != null) salida.alAbrir.AddListener(() => Pensamiento.Decir("cloacas.abierta", 0.5f, rejaAbierta));
        estatuas = FindObjectsByType<EstatuaAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    void OnDestroy()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.OnDialogueStarted -= AlEmpezarDialogo;
            DialogueManager.Instance.OnDialogueEnded -= AlTerminarDialogo;
        }
        if (progreso != null)
        {
            progreso.AlVolver -= AlVolverAEstacion;
            progreso.AlAbrir -= AlAbrirZona;
        }
    }

    ProgresoCaminos progreso;

    string NombreZona(int i) => progreso != null && progreso.tramos != null && i >= 0 && i < progreso.tramos.Length && progreso.tramos[i] != null
        ? progreso.tramos[i].nombre : "";

    void AlVolverAEstacion(int zona)
    {
        switch (NombreZona(zona))
        {
            case "Laberinto": Pensamiento.Decir("estacion.trasLaberinto", 0.3f, trasLaberinto); break;
            case "Backrooms": Pensamiento.Decir("estacion.trasBackrooms", 0.3f, trasBackrooms); break;
            case "Cloacas": Pensamiento.Decir("estacion.trasCloacas", 0.3f, trasCloacas); break;
        }
    }

    void AlAbrirZona(int zona)
    {
        switch (NombreZona(zona))
        {
            case "Backrooms": Pensamiento.Decir("estacion.abreBackrooms", 0.6f, abreBackrooms); break;
            case "Cloacas": Pensamiento.Decir("estacion.abreCloacas", 0.6f, abreCloacas); break;
            case "Zona Roja": Pensamiento.Decir("estacion.abreZonaRoja", 0.6f, abreZonaRoja); break;
        }
    }

    // ------------------------------------------------------------------ Tienda

    void AlEmpezarDialogo() => ultimoDialogo = DialogueManager.Instance != null ? DialogueManager.Instance.CurrentData : null;

    void AlTerminarDialogo()
    {
        if (tutorial == null || ultimoDialogo == null) return;
        if (ultimoDialogo == tutorial.dialogoClientePide) Pensamiento.Decir("tienda.cliente", 0.2f, trasPedidoCliente);
        // Despues del cultista la camara gira hacia el sotano: se dice al terminar el giro
        else if (ultimoDialogo == tutorial.dialogoCultistaPide) Pensamiento.Decir("tienda.cultista", tutorial.duracionGiro + 0.3f, trasPedidoCultista);
    }

    // ------------------------------------------------------------------ Zona Principal

    void AlLlegar()
    {
        Pensamiento.Decir("zp.llegada", 0.8f, llegada);
        Pensamiento.Decir("zp.camino", 3.5f, unicoCamino);
    }

    // ------------------------------------------------------------------ Lo que se revisa seguido

    void Update()
    {
        if (Time.time < proximaRevision) return;
        proximaRevision = Time.time + 0.2f;
        if (jugador == null) { jugador = HUD.Jugador(); if (jugador == null) return; }

        RevisarCajas();
        RevisarLaberinto();
        RevisarCloacas();
    }

    void RevisarCajas()
    {
        if (revision == null || revision.cajas == null) return;
        foreach (CajaRevisable c in revision.cajas)
        {
            if (c == null || !c.Revisada || !cajasVistas.Add(c)) continue;
            if (revision.Completa)
            {
                // La ultima caja: sale la linterna
                Pensamiento.Decir("sotano.linterna", 1.2f, cajaLinterna);
                Pensamiento.Decir("sotano.terminadas", 0.8f, cajasTerminadas);
            }
            else if (c.objeto != null)
            {
                string n = c.objeto.name.ToLowerInvariant();
                if (n.Contains("collar")) Pensamiento.Decir("sotano.collar", 0.6f, cajaCollar);
                else if (n.Contains("car")) Pensamiento.Decir("sotano.carro", 0.6f, cajaCarro);
            }
        }
    }

    void RevisarLaberinto()
    {
        // El reloj se vuelve loco por primera vez (en la presentacion del enemigo)
        RelojJugador reloj = RelojJugador.Instance;
        if (reloj != null && reloj.Visible && reloj.Cercania > 0.25f) Pensamiento.Decir("lab.reloj", 0.4f, relojLoco);

        // Primera pila recogida
        if (!Pensamiento.YaDicho("lab.bateria"))
            foreach (Bateria b in baterias)
                if (b != null && !b.Disponible) { Pensamiento.Decir("lab.bateria", 0.3f, primeraBateria); break; }

        if (contrasena == null) return;
        // Panel central (despues de entrar a la zona segura)
        if (!Pensamiento.YaDicho("lab.panelCentral") && !contrasena.Resuelto && Cerca(CentroPanel(), distanciaPanel + 0.6f))
            Pensamiento.Decir("lab.panelCentral", 0.3f, panelCentral);
        // Paneles con numero: la frase depende de cuantos lleva encontrados
        for (int i = panelesPendientes.Count - 1; i >= 0; i--)
        {
            Transform p = panelesPendientes[i];
            if (p == null) { panelesPendientes.RemoveAt(i); continue; }
            if (!Cerca(p.position, distanciaPanel)) continue;
            panelesPendientes.RemoveAt(i);
            if (contrasena.Resuelto || panelesPista == null || panelesPista.Length == 0) continue;
            string frase = panelesPista[Mathf.Min(panelesEncontrados, panelesPista.Length - 1)];
            panelesEncontrados++;
            Pensamiento.Decir("lab.panel" + panelesEncontrados, 0.3f, frase);
        }
    }

    Vector3 CentroPanel()
    {
        if (contrasena.columnas == null || contrasena.columnas.Length == 0) return contrasena.transform.position;
        Vector3 s = Vector3.zero;
        int n = 0;
        foreach (var c in contrasena.columnas) if (c != null) { s += c.transform.position; n++; }
        return n > 0 ? s / n : contrasena.transform.position;
    }

    void RevisarCloacas()
    {
        if (salida == null || salida.Abierta) return;

        // Una estatua que se movio mientras no la veia, y ahora la ve
        foreach (EstatuaAI e in estatuas)
        {
            if (e == null) continue;
            EstatuaAI.State estado = e.CurrentState;
            if (estadoEstatuas.TryGetValue(e, out var antes) && antes.estado == EstatuaAI.State.Moving && estado == EstatuaAI.State.Frozen
                && Vector3.Distance(antes.pos, e.transform.position) > 0.8f && Cerca(e.transform.position, 14f))
                Pensamiento.Decir("cloacas.estatua", 0.3f, estatuaMovida);
            if (!estadoEstatuas.ContainsKey(e) || antes.estado != estado)
                estadoEstatuas[e] = (estado, estado == EstatuaAI.State.Moving && estadoEstatuas.ContainsKey(e) ? antes.pos : e.transform.position);
        }

        if (!salida.Cerrada) return;
        if (cerradaDesde < 0f)
        {
            cerradaDesde = Time.time;
            Pensamiento.Decir("cloacas.reja", 1f, rejaCerrada);
        }

        Grabbable llave = salida.cerradura != null ? salida.cerradura.objetoEsperado : null;
        bool tieneLlave = llave != null && Inventario.Instance != null && Inventario.Instance.IndiceDe(llave) >= 0;
        if (tieneLlave) { Pensamiento.Decir("cloacas.llave", 0.2f, llaveEncontrada); return; }
        if (Pensamiento.YaDicho("cloacas.llave")) return;   // ya la tuvo (la solto): no repetir pistas

        float encerrado = Time.time - cerradaDesde;
        if (encerrado > segundosPistaTintineo) Pensamiento.Decir("cloacas.tintineo", 0f, pistaTintineo);
        if (encerrado > segundosPistaEstatuas) Pensamiento.Decir("cloacas.estatuas", 0f, pistaEstatuas);
    }

    bool Cerca(Vector3 punto, float distancia) => jugador != null && (jugador.position - punto).sqrMagnitude <= distancia * distancia;
}
