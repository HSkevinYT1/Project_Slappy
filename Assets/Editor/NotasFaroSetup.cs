using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Zona Roja con las notas del guion (NotasCompletas.pdf). Cada nota da una pista de hacia donde ir y, al leerla, la
// brujula apunta alli (MisionFaro.DestinoBrujula). Se leen en orden: la siguiente aparece al leer la anterior.
// - La bitacora del faro, en la mesa del piso 1: se lee primero (la del farero, y al final la letra de uno de los
//   chicos, que avisa que dejo sus notas afuera). Con ella aparece la nota 1.
// - Nota 1 (Dia 1): clavada en un arbol del borde del bosque, a 16-30 m de la salida del faro (la puerta del piso 1):
//   se alcanza a ver desde la puerta, pero no esta pegada a ella. Dice: "el noreste de la playa".
// - Nota 2 (Dia 30): en la playa NORESTE de la isla, sobre una caja. Dice: "al noroeste, al borde del lago".
// - Nota 3 (Dia 60): en el borde del lago, en la parte mas cercana al faro (en el arbol mas cercano, o en un poste).
//   Dice: "un cobertizo cerca de la playa, en el este, con un deposito subterraneo".
// - Las notas no brillan de lejos: solo de cerca (MisionFaro.distanciaBrillo) y el papel es viejo, apagado.
// Al pie de cada nota sale hacia donde marca la brujula (la direccion de su pista: "La brújula marca: NORESTE").
// - El bidon de gasolina: dentro de ese cobertizo (playa ESTE, al sur del faro), junto a la trampilla abierta.
// Norte del mapa = +Z (como en ZonaRojaSetup: la Tienda queda al norte de la isla), este = +X.
// Todos los materiales con Smoothness 0 (la linterna se reflejaba).
// Se aplica sola una vez; rehacer: Tools > Zona Roja > Notas, brujula y gasolina (rehacer). Despues guarda la escena (Ctrl+S).
[InitializeOnLoad]
public static class NotasFaroSetup
{
    const string Carpeta = "Assets/Guion";
    const string Glow = "Assets/Materials/GlowMaterial.mat";
    // v2: nota 1 afuera (vista desde la salida del faro), nota 3 en el borde del lago mas cercano al faro y el rumbo
    // de la brujula al pie de cada nota
    // v3: vuelve la bitacora, nota 1 en un arbol mas lejos de la puerta, nota 3 en un arbol del lago, papel apagado
    const string Marca = "_notas pdf v3";
    static readonly string[] MarcasViejas = { "_notas pdf v1", "_notas pdf v2" };
    const string TituloBitacora = "Bitácora del faro";
    const string TextoBitacora =
        "Registro del farero.\n\n" +
        "12 de marzo. — El combustible llegó tarde otra vez. La lámpara aguanta, pero no sé por cuántas noches más.\n\n" +
        "3 de abril. — Volví a escuchar pasos alrededor del faro. Cuando la luz barre el bosque, a veces hay algo de pie entre los árboles. Cuando vuelve a pasar, ya no está.\n\n" +
        "18 de abril. — No salgo de la luz. Guardé lo que quedaba de combustible lejos del faro, por si la lámpara se incendia.\n\n" +
        "21 de abril. — Está más cerca.\n\n" +
        "(Más abajo, con otra letra:)\n\n" +
        "Encontramos esta bitácora al llegar. Del farero no hay rastro. Voy a ir dejando lo que escribo afuera, donde se pueda encontrar, por si alguien llega después de nosotros. La primera hoja está cerca, saliendo por la puerta, entre los árboles.";
    const string RumboBitacora = "La brújula marca: OESTE";
    const string NombreCobertizo = "Cobertizo de la playa";
    const string NombreCaja = "Caja de la playa (nota 2)";
    const string NombrePoste = "Poste del lago (nota 3)";
    const string NombrePosteSalida = "Poste de la salida (nota 1)";
    const float RadioFaro = 9f;   // radio exterior del faro (ZonaRojaSetup)

    static readonly string[] Titulos = { "Nota 1 — Día 1", "Nota 2 — Día 30", "Nota 3 — Día 60" };
    // Hacia donde manda cada nota (lo que dice su pista): sale al pie de la hoja
    static readonly string[] Rumbos = { "La brújula marca: NORESTE", "La brújula marca: NOROESTE", "La brújula marca: ESTE" };
    static readonly string[] Textos =
    {
        "No sé dónde estamos. Tampoco recuerdo cómo llegamos aquí.\n\n" +
        "Hace un momento estaba con mis amigos en mi casa. Ninguno sabe qué ocurrió. No tenemos teléfonos, nuestras pertenencias desaparecieron y no recordamos cómo terminamos en esta isla.\n\n" +
        "A lo lejos vimos un faro. Fue nuestra única esperanza, así que caminamos hasta él. Por suerte, todavía funciona. Encontramos algunas provisiones y decidimos refugiarnos aquí mientras esperamos que alguien venga a buscarnos.\n\n" +
        "Hay algo extraño en este lugar. Desde las ventanas se puede ver un bosque que rodea prácticamente toda la isla. Anoche escuchamos algo caminando entre los árboles. Pensamos que sería algún animal, pero los pasos eran demasiado pesados.\n\n" +
        "Tim dice haber visto una silueta entre los árboles. Era muy alta, demasiado alta para ser una persona.\n\n" +
        "Mañana iremos a revisar el noreste de la playa. Quizás encontremos algún barco, herramientas o cualquier cosa que nos pueda servir para salir de aquí. También vimos una pequeña construcción cerca de la costa.",

        "Han pasado treinta días. Nadie ha venido.\n\n" +
        "Ya no contamos los días esperando un rescate. Simplemente intentamos sobrevivir.\n\n" +
        "La criatura sigue ahí. Ahora estamos seguros de que no es un animal. Es alta, extremadamente delgada y parece tener brazos que casi llegan al suelo. Nunca hemos conseguido verla con claridad, pero siempre está cerca.\n\n" +
        "Descubrimos algo: no se acerca mientras estamos dentro del alcance de la luz del faro. Una noche, intentamos caminar hacia los árboles y aquella cosa apareció entre ellos. Tuvimos que correr de regreso.\n\n" +
        "Las provisiones empiezan a escasear. También hemos notado que la luz parpadea algunas noches. No sabemos cuánto tiempo podrá seguir funcionando.\n\n" +
        "Hoy discutimos sobre revisar las instalaciones del faro. Quizás encontremos alguna herramienta o podamos entender por qué la luz falla tanto. Hay una pequeña habitación en la parte superior que todavía no hemos revisado.\n\n" +
        "Diego fue a revisar el bosque, la parte iluminada.\n" +
        "Tim se quedó en el faro.\n" +
        "Y yo fui al noroeste, cerca del centro al borde del lago.\n\n" +
        "No me gusta estar solo.",

        "No sé cuánto tiempo más podré seguir escribiendo.\n\n" +
        "Ahora estoy solo.\n\n" +
        "En una de las salidas de Tim algo pasó. Ni siquiera estoy seguro de haberlo entendido. Diego se volvió loco y se suicidó. Voy por el mismo camino. Todavía escucho pasos dentro del faro, aunque esté completamente solo.\n\n" +
        "La luz sigue funcionando, pero cada vez dura menos antes de apagarse por unos segundos. Esos segundos son suficientes para escuchar cómo algo se acerca.\n\n" +
        "Anoche vi su silueta al otro lado del cristal. No se movía. Simplemente estaba allí, observándome.\n\n" +
        "Creo que sabe que solo quedo yo.\n\n" +
        "He estado buscando alguna manera de escapar. Recordé que, cuando llegamos, vimos un pequeño cobertizo cerca de la playa, en el este. Uno de los chicos mencionó que había un depósito subterráneo debajo de él.\n\n" +
        "Creo que allí guardaban combustible. Si todavía queda gasolina, podría ser nuestra única oportunidad de sobrevivir, puede que la puerta se abra.\n\n" +
        "No quiero salir. No quiero volver a verlo.\n\n" +
        "Pero tampoco puedo quedarme aquí para siempre.\n\n" +
        "La luz volvió a apagarse.",
    };

    static NotasFaroSetup()
    {
        // Despues de NovedadesGuionSetup (crea la Mision Faro) y de BrujulaSetup
        EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall +=
            () => EditorApplication.delayCall += () => EditorApplication.delayCall += () => EditorApplication.delayCall += AutoAplicar;
    }

    static void AutoAplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!ZonaRoja(out Transform zr)) return;
        Transform mt = zr.Find("Mision Faro");
        if (!mt || mt.Find(Marca)) return;
        // Ya estaba la version vieja: se quita lo que puso y se rehace
        Quitar(zr);
        Aplicar(zr);
    }

    [MenuItem("Tools/Zona Roja/Notas, brújula y gasolina (rehacer)")]
    static void Menu()
    {
        if (!ZonaRoja(out Transform zr) || !zr.Find("Mision Faro")) { EditorUtility.DisplayDialog("Zona Roja", "No encontre la \"Zona Roja/Mision Faro\" en la escena.", "OK"); return; }
        Quitar(zr);
        Aplicar(zr);
    }

    static void Quitar(Transform zr)
    {
        var nombres = new List<string> { NombreCobertizo, NombreCaja, NombrePoste, NombrePosteSalida, "Mision Faro/" + Marca };
        foreach (string m in MarcasViejas) nombres.Add("Mision Faro/" + m);
        foreach (string n in nombres)
        {
            Transform t = zr.Find(n);
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }
    }

    static void Aplicar(Transform zr)
    {
        Physics.SyncTransforms();
        MisionFaro mision = zr.Find("Mision Faro").GetComponent<MisionFaro>();
        Transform faro = zr.Find("Faro"), isla = zr.Find("Isla"), lago = zr.Find("Lago"), mesa = zr.Find("Mesa del diario");
        Collider suelo = isla ? isla.GetComponent<Collider>() : null;
        if (!mision || !faro || !suelo) { Debug.LogWarning("[Notas faro] Falta algo de la Zona Roja (Mision Faro, Faro o Isla con collider)."); return; }
        float y0 = isla.position.y;
        Vector3 centro = new Vector3(isla.position.x, y0, isla.position.z);

        // ---- Materiales (todos sin brillo)
        // Papel viejo y apagado (blanco se veia desde lejos con la linterna)
        Material papel = Mat("PapelViejo", new Color(0.5f, 0.46f, 0.36f));
        Material cuero = Mat("Cuero", new Color(0.28f, 0.09f, 0.06f));
        Material madera = Mat("Madera", new Color(0.32f, 0.2f, 0.11f));
        Material maderaVieja = Mat("MaderaVieja", new Color(0.24f, 0.17f, 0.11f));
        Material hueco = Mat("HuecoNegro", new Color(0.01f, 0.01f, 0.01f));
        Material oxido = Mat("BidonOxidado", new Color(0.33f, 0.13f, 0.07f));
        Material glow = AssetDatabase.LoadAssetAtPath<Material>(Glow);
        foreach (string viejo in new[] { "Cuero", "BidonRojo", "BidonTapa", "BrujulaDial", "BrujulaRojo", "BrujulaBlanco", "BrujulaNegro", "BrujulaLaton" })
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(Carpeta + "/" + viejo + ".mat");
            if (!m) continue;
            MunecaModeloSetup.SinBrillo(m);
            // El laton muy metalico y sin brillo queda negro
            if (viejo == "BrujulaLaton" && m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.35f);
        }

        // ---- Las notas viejas fuera (el diario y las 3 notas del bosque)
        Transform grupo = zr.Find("Notas del farero");
        if (!grupo) { grupo = new GameObject("Notas del farero").transform; Undo.RegisterCreatedObjectUndo(grupo.gameObject, "Notas"); grupo.SetParent(zr, false); }
        var viejas = new List<GameObject>();
        foreach (Transform t in grupo) viejas.Add(t.gameObject);
        foreach (GameObject g in viejas) Undo.DestroyObjectImmediate(g);
        foreach (string n in new[] { "Diario", "Nota 1", "Bitacora" })
        {
            Transform t = mesa ? mesa.Find(n) : null;
            if (t) Undo.DestroyObjectImmediate(t.gameObject);
        }
        var log = new List<string>();
        Transform bosque = zr.Find("Bosque");

        // ---- La bitacora: en la mesa del piso 1 (donde estaba el diario)
        Lectura bitacora = null;
        if (mesa)
        {
            var b = new GameObject("Bitacora");
            Undo.RegisterCreatedObjectUndo(b, "Bitacora");
            b.transform.SetParent(mesa, false);
            b.transform.localPosition = new Vector3(0.05f, 0.81f, 0f);
            b.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
            Cubo(b.transform, "Tapa", Vector3.zero, new Vector3(0.24f, 0.045f, 0.32f), cuero, false);
            Cubo(b.transform, "Hojas", new Vector3(0.008f, 0f, 0f), new Vector3(0.23f, 0.035f, 0.305f), papel, false);
            b.AddComponent<BoxCollider>().size = new Vector3(0.3f, 0.12f, 0.38f);
            b.AddComponent<Interactable>();
            bitacora = b.AddComponent<Lectura>();
            bitacora.titulo = TituloBitacora;
            bitacora.texto = TextoBitacora;
            bitacora.prompt = "Leer bitácora";
            bitacora.pie = RumboBitacora;
            log.Add("bitácora en la mesa del piso 1");
        }
        else Debug.LogWarning("[Notas faro] No encontre la \"Mesa del diario\": va sin bitácora (la nota 1 queda disponible desde el principio).");

        // ---- Nota 1: clavada en un arbol del borde del bosque, delante de la salida del faro (la puerta del piso 1,
        //      del lado de la isla): se alcanza a ver desde la puerta, pero lejos (16-30 m)
        Transform entrada = zr.Find("Entrada Pasillo");
        Vector3 haciaPasillo = entrada ? entrada.position - faro.position : faro.right;
        haciaPasillo.y = 0f;
        haciaPasillo = haciaPasillo.sqrMagnitude > 0.01f ? haciaPasillo.normalized : Vector3.right;
        Vector3 afuera = -haciaPasillo;   // la puerta del piso 1 da al lado contrario del pasillo (hacia la isla)
        Vector3 puerta = new Vector3(faro.position.x, y0, faro.position.z) + afuera * RadioFaro;
        if (Tierra(suelo, puerta + afuera * 1.5f, y0, out Vector3 sp, true)) puerta.y = sp.y;
        Vector3 ojos = puerta + afuera * 0.6f + Vector3.up * 1.6f;
        var n1 = Nota(grupo, "Nota 1", papel, madera, 0);
        Transform arbol1 = null;
        float mejor = float.MaxValue;
        if (bosque)
            foreach (Transform a in bosque)
            {
                if (!a.TryGetComponent(out CapsuleCollider cc)) continue;
                Vector3 d = a.position - puerta; d.y = 0f;
                float m = d.magnitude;
                if (m < 16f || m > 30f || Vector3.Angle(afuera, d) > 40f) continue;
                // El lado del tronco que da a la puerta, a la altura de la nota: que se vea desde la puerta
                Vector3 cara = new Vector3(a.position.x, a.position.y + 1.75f, a.position.z) - d / m * (cc.radius * a.lossyScale.x + 0.05f);
                if (Physics.Linecast(ojos, cara, out RaycastHit hv, ~0, QueryTriggerInteraction.Ignore) && hv.collider != cc) continue;
                float puntos = Mathf.Abs(m - 22f) + Vector3.Angle(afuera, d) * 0.1f;
                if (puntos < mejor) { mejor = puntos; arbol1 = a; }
            }
        if (arbol1 != null) Clavar(n1.transform, arbol1, puerta);
        else
        {
            // Sin arbol a la vista: en un poste a 14 m
            Debug.LogWarning("[Notas faro] No encontre un arbol a la vista frente a la salida del faro: la nota 1 va en un poste, muevela si hace falta.");
            Vector3 p = puerta + afuera * 14f;
            if (Tierra(suelo, p, y0, out Vector3 s, true)) p = s;
            var posteSalida = Raiz(zr, NombrePosteSalida, p, Quaternion.LookRotation(-afuera));
            Cubo(posteSalida, "Poste", new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, 1.4f, 0.12f), maderaVieja, true);
            n1.transform.SetPositionAndRotation(posteSalida.TransformPoint(new Vector3(0f, 1.2f, 0.064f)), posteSalida.rotation);
        }
        log.Add($"Nota 1 {(arbol1 != null ? "clavada en un árbol" : "en un poste")} a {Plano(n1.transform.position - puerta):0} m de la salida del faro");

        // ---- Nota 2: playa noreste, en una caja
        Vector3 ne = (Vector3.forward + Vector3.right).normalized;
        Vector3 mitadCaja = new Vector3(0.45f, 0.4f, 0.45f);
        if (!Sitio(suelo, centro, ne, y0, mitadCaja, new[] { 7f, 10f, 13f, 5f, 16f }, new[] { 0f, 8f, -8f, 16f, -16f, 24f, -24f }, faro, 22f,
                   out Vector3 pCaja, out Quaternion gCaja, out float costaNE))
            Debug.LogWarning("[Notas faro] No encontre un sitio libre en la playa noreste: la caja quedo donde se pudo, muevela si hace falta.");
        var caja = Raiz(zr, NombreCaja, pCaja, gCaja);
        Cubo(caja, "Caja", new Vector3(0f, 0.3f, 0f), new Vector3(0.62f, 0.6f, 0.62f), maderaVieja, true);
        Cubo(caja, "Tabla", new Vector3(0.05f, 0.62f, 0f), new Vector3(0.7f, 0.04f, 0.18f), madera, false).transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
        Cubo(caja, "Tabla rota", new Vector3(-0.7f, 0.03f, 0.35f), new Vector3(1.1f, 0.05f, 0.16f), maderaVieja, false).transform.localRotation = Quaternion.Euler(0f, 38f, 4f);
        Obstaculo(caja.Find("Caja").gameObject);
        var n2 = Nota(grupo, "Nota 2", papel, madera, 1);
        // Clavada en la cara de la caja que da hacia el centro de la isla
        n2.transform.SetPositionAndRotation(caja.TransformPoint(new Vector3(0f, 0.36f, 0.312f)), caja.rotation);
        log.Add($"Nota 2 en la playa {Rumbo(pCaja - centro)} ({pCaja.x:0}, {pCaja.z:0}), a {Plano(pCaja - centro):0} m del centro (costa a {costaNE:0} m)");

        // ---- Nota 3: borde del lago, en la parte mas cercana al faro, en un poste
        Vector3 pPoste = centro;
        Quaternion gPoste = Quaternion.identity;
        if (lago)
        {
            Vector3 alCentro = faro.position - lago.position; alCentro.y = 0f;
            alCentro = alCentro.sqrMagnitude > 0.01f ? alCentro.normalized : Vector3.forward;
            bool ok = false;
            foreach (float ang in new[] { 0f, 12f, -12f, 24f, -24f, 36f, -36f })
            {
                Vector3 d = Quaternion.Euler(0f, ang, 0f) * alCentro;
                // Desde el lago hacia afuera: el primer suelo firme (ya fuera de la cuenca), un par de metros mas
                for (float r = 12f; r < 50f && !ok; r += 1f)
                {
                    Vector3 p = lago.position + d * r;
                    if (!Tierra(suelo, p, y0, out Vector3 s)) continue;
                    s = lago.position + d * (r + 2f);
                    if (!Tierra(suelo, s, y0, out s)) break;
                    if (!Libre(s, new Vector3(0.3f, 0.8f, 0.3f), Quaternion.identity, suelo)) break;
                    pPoste = s;
                    // Mirando hacia el faro (el lado por donde se llega al lago)
                    gPoste = Quaternion.LookRotation(d);
                    ok = true;
                }
                if (ok) break;
            }
            if (!ok) Debug.LogWarning("[Notas faro] No encontre un sitio libre al borde del lago: el poste quedo donde se pudo, muevelo si hace falta.");
        }
        else Debug.LogWarning("[Notas faro] No encontre \"Zona Roja/Lago\".");
        var n3 = Nota(grupo, "Nota 3", papel, madera, 2);
        // En el arbol mas cercano a ese punto del borde (si hay uno a menos de 7 m); si no, en un poste
        Transform arbol3 = null;
        float d3 = 7f;
        if (bosque)
            foreach (Transform a in bosque)
            {
                if (!a.GetComponent<CapsuleCollider>()) continue;
                float dd = Plano(a.position - pPoste);
                if (dd < d3) { d3 = dd; arbol3 = a; }
            }
        if (arbol3 != null) Clavar(n3.transform, arbol3, pPoste + gPoste * Vector3.forward * 3f);
        else
        {
            var poste = Raiz(zr, NombrePoste, pPoste, gPoste);
            Cubo(poste, "Poste", new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, 1.4f, 0.12f), maderaVieja, true);
            n3.transform.SetPositionAndRotation(poste.TransformPoint(new Vector3(0f, 1.2f, 0.064f)), poste.rotation);
        }
        log.Add($"Nota 3 al borde del lago, del lado del faro, {(arbol3 != null ? "clavada en un árbol" : "en un poste")} ({n3.transform.position.x:0}, {n3.transform.position.z:0}), a {Plano(n3.transform.position - faro.position):0} m del faro");

        // ---- El cobertizo de la playa este (al sur del faro), con el deposito y el bidon
        Vector3 mitadCob = new Vector3(1.9f, 1.3f, 1.7f);
        Vector3 este = Quaternion.Euler(0f, 22f, 0f) * Vector3.right;   // un poco al sur del este (el faro esta justo al este)
        if (!Sitio(suelo, centro, este, y0, mitadCob, new[] { 8f, 11f, 14f, 6f, 18f }, new[] { 0f, 6f, 12f, -6f, 18f, 24f, -12f, 30f }, faro, 22f,
                   out Vector3 pCob, out Quaternion gCob, out float costaE))
            Debug.LogWarning("[Notas faro] No encontre un sitio libre en la playa este: el cobertizo quedo donde se pudo, muevelo si hace falta.");
        Transform cob = Cobertizo(zr, pCob, gCob, suelo, y0, madera, maderaVieja, hueco, oxido, out Vector3 dondeBidon, out Quaternion giroBidon);
        log.Add($"cobertizo con el bidón en la playa {Rumbo(pCob - centro)} ({pCob.x:0}, {pCob.z:0}), a {Plano(pCob - faro.position):0} m del faro");

        Transform bidon = zr.Find("Bidon de gasolina");
        if (bidon)
        {
            Undo.RecordObject(bidon, "Bidon");
            bidon.SetPositionAndRotation(dondeBidon, giroBidon);
            if (bidon.TryGetComponent(out Rigidbody rb)) { Undo.RecordObject(rb, "Bidon"); rb.linearVelocity = Vector3.zero; }
        }
        else Debug.LogWarning("[Notas faro] No encontre \"Zona Roja/Bidon de gasolina\".");

        // ---- La mision: primero la bitacora, despues las notas en orden (brillan solo de cerca: MisionFaro)
        Undo.RecordObject(mision, "Mision Faro");
        mision.diario = bitacora;
        mision.notas = new[] { n1.GetComponent<Lectura>(), n2.GetComponent<Lectura>(), n3.GetComponent<Lectura>() };
        mision.alVerNota = "¿Una nota?";
        mision.alVerDiario = "¿Una bitácora?";
        foreach (Lectura l in mision.notas) Brillo(l.gameObject, glow);
        if (bitacora) Brillo(bitacora.gameObject, glow);
        EditorUtility.SetDirty(mision);

        new GameObject(Marca).transform.SetParent(mision.transform, false);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(zr.gameObject.scene);
        Debug.Log("[Notas faro] Zona Roja con las notas del guion: " + string.Join("; ", log) +
                  ". La brújula apunta a lo que dice la última nota leída. Se puede mover todo. Guarda la escena (Ctrl+S).", mision);
    }

    // ------------------------------------------------------------------ Piezas

    static GameObject Nota(Transform padre, string nombre, Material papel, Material clavo, int i)
    {
        var n = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(n, nombre);
        n.transform.SetParent(padre, false);
        Cubo(n.transform, "Papel", new Vector3(0f, 0f, 0.006f), new Vector3(0.24f, 0.32f, 0.01f), papel, false).transform.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 5f);
        if (clavo)
            Cubo(n.transform, "Clavo", new Vector3(0f, 0.12f, 0.014f), new Vector3(0.018f, 0.012f, 0.018f), clavo, false, PrimitiveType.Cylinder)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var bc = n.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0f, 0.04f);
        bc.size = new Vector3(0.32f, 0.4f, 0.08f);
        n.AddComponent<Interactable>();
        var l = n.AddComponent<Lectura>();
        l.titulo = Titulos[i];
        l.texto = Textos[i];
        l.prompt = "Leer nota";
        l.pie = Rumbos[i];
        return n;
    }

    static Transform Cobertizo(Transform zr, Vector3 pos, Quaternion giro, Collider suelo, float y0, Material madera, Material vieja,
                               Material hueco, Material oxido, out Vector3 dondeBidon, out Quaternion giroBidon)
    {
        const float ancho = 3.2f, fondo = 2.8f, alto = 2.3f, puerta = 1.1f, grosor = 0.1f;
        // El piso a la altura del punto mas alto del terreno debajo (no se hunde en ningun lado)
        float piso = pos.y;
        foreach (float x in new[] { -ancho / 2f, ancho / 2f })
            foreach (float z in new[] { -fondo / 2f, fondo / 2f })
                if (Tierra(suelo, pos + giro * new Vector3(x, 0f, z), y0 - 5f, out Vector3 s, true)) piso = Mathf.Max(piso, s.y);
        var c = Raiz(zr, NombreCobertizo, new Vector3(pos.x, piso + 0.05f, pos.z), giro);

        Cubo(c, "Piso", new Vector3(0f, 0.05f, 0f), new Vector3(ancho, 0.1f, fondo), vieja, true);
        foreach (float x in new[] { -ancho / 2f + 0.06f, ancho / 2f - 0.06f })
            foreach (float z in new[] { -fondo / 2f + 0.06f, fondo / 2f - 0.06f })
                Cubo(c, "Pilote", new Vector3(x, -0.45f, z), new Vector3(0.14f, 1f, 0.14f), vieja, false);
        float yMuro = 0.1f + alto / 2f;
        var muros = new List<GameObject>
        {
            Cubo(c, "Pared fondo", new Vector3(0f, yMuro, -fondo / 2f + grosor / 2f), new Vector3(ancho, alto, grosor), madera, true),
            Cubo(c, "Pared izquierda", new Vector3(-ancho / 2f + grosor / 2f, yMuro, 0f), new Vector3(grosor, alto, fondo), madera, true),
            Cubo(c, "Pared derecha", new Vector3(ancho / 2f - grosor / 2f, yMuro, 0f), new Vector3(grosor, alto, fondo), madera, true),
        };
        // Frente con la puerta (abierta, sin hoja: se cayo) mirando hacia el centro de la isla
        float lado = (ancho - puerta) / 2f;
        muros.Add(Cubo(c, "Frente izquierdo", new Vector3(-(puerta / 2f + lado / 2f), yMuro, fondo / 2f - grosor / 2f), new Vector3(lado, alto, grosor), madera, true));
        muros.Add(Cubo(c, "Frente derecho", new Vector3(puerta / 2f + lado / 2f, yMuro, fondo / 2f - grosor / 2f), new Vector3(lado, alto, grosor), madera, true));
        Cubo(c, "Dintel", new Vector3(0f, 0.1f + 2.05f + (alto - 2.05f) / 2f, fondo / 2f - grosor / 2f), new Vector3(puerta, alto - 2.05f, grosor), madera, true);
        Cubo(c, "Puerta caida", new Vector3(0.9f, 0.04f, fondo / 2f + 0.9f), new Vector3(1f, 0.05f, 1.9f), vieja, false).transform.localRotation = Quaternion.Euler(0f, 18f, 3f);
        foreach (GameObject m in muros) Obstaculo(m);
        Cubo(c, "Techo", new Vector3(0f, 0.1f + alto + 0.12f, 0f), new Vector3(ancho + 0.4f, 0.08f, fondo + 0.5f), vieja, true).transform.localRotation = Quaternion.Euler(7f, 0f, 0f);

        // El deposito subterraneo: la trampilla abierta sobre un hueco negro, con la escalera que baja
        Vector3 h = new Vector3(-0.65f, 0f, -0.45f);
        Cubo(c, "Deposito (hueco)", h + new Vector3(0f, 0.102f, 0f), new Vector3(0.9f, 0.006f, 0.9f), hueco, false);
        Cubo(c, "Marco trampilla", h + new Vector3(0f, 0.11f, 0.47f), new Vector3(1f, 0.02f, 0.06f), madera, false);
        Cubo(c, "Marco trampilla", h + new Vector3(0f, 0.11f, -0.47f), new Vector3(1f, 0.02f, 0.06f), madera, false);
        var bisagra = new GameObject("Trampilla").transform;
        bisagra.SetParent(c, false);
        bisagra.localPosition = h + new Vector3(0f, 0.12f, -0.45f);
        bisagra.localRotation = Quaternion.Euler(-110f, 0f, 0f);
        Cubo(bisagra, "Tapa", new Vector3(0f, 0.02f, 0.45f), new Vector3(0.9f, 0.04f, 0.9f), madera, false);
        Cubo(bisagra, "Argolla", new Vector3(0f, 0.05f, 0.8f), new Vector3(0.08f, 0.02f, 0.08f), oxido, false, PrimitiveType.Cylinder);
        foreach (float x in new[] { -0.25f, 0.25f })
            Cubo(c, "Escalera", h + new Vector3(x, 0.32f, -0.1f), new Vector3(0.05f, 0.5f, 0.05f), vieja, false);
        Cubo(c, "Escalon", h + new Vector3(0f, 0.45f, -0.1f), new Vector3(0.5f, 0.04f, 0.04f), vieja, false);

        // Restos: bidones vacios y una caja (lo que quedaba del deposito)
        Cubo(c, "Bidon vacio", new Vector3(1.05f, 0.33f, -1.0f), new Vector3(0.3f, 0.44f, 0.16f), oxido, false).transform.localRotation = Quaternion.Euler(0f, -20f, 0f);
        Cubo(c, "Bidon vacio", new Vector3(1.0f, 0.2f, -0.45f), new Vector3(0.3f, 0.16f, 0.44f), oxido, false).transform.localRotation = Quaternion.Euler(0f, 60f, 90f);
        Cubo(c, "Caja", new Vector3(1.0f, 0.35f, 0.55f), new Vector3(0.5f, 0.5f, 0.5f), vieja, true);

        // El bidon (el de la mision): en el piso, al lado de la trampilla
        dondeBidon = c.TransformPoint(h + new Vector3(0.95f, 0.1f + 0.25f, 0.15f));
        giroBidon = c.rotation * Quaternion.Euler(0f, 35f, 0f);
        return c;
    }

    // Clava la nota en el tronco del arbol, del lado que da hacia "desde", a 1.75 m del suelo (algo alto: no se ve tan facil)
    static void Clavar(Transform nota, Transform arbol, Vector3 desde)
    {
        var cc = arbol.GetComponent<CapsuleCollider>();
        Vector3 fuera = desde - arbol.position; fuera.y = 0f;
        fuera = fuera.sqrMagnitude > 0.01f ? fuera.normalized : arbol.forward;
        Vector3 eje = new Vector3(arbol.position.x, arbol.position.y + 1.75f, arbol.position.z);
        Vector3 punto = eje + fuera * (cc ? cc.radius * arbol.lossyScale.x : 0.2f), normal = fuera;
        if (cc)
            foreach (RaycastHit h in Physics.RaycastAll(eje + fuera * 3f, -fuera, 3f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider == cc) { punto = h.point; normal = new Vector3(h.normal.x, 0f, h.normal.z).normalized; break; }
        nota.SetPositionAndRotation(punto + normal * 0.004f, Quaternion.LookRotation(normal, Vector3.up));
    }

    // Los muros y la caja no dejan pasar al enemigo (el NavMesh es del terreno, sin el cobertizo)
    static void Obstaculo(GameObject g)
    {
        var o = g.AddComponent<NavMeshObstacle>();
        o.carving = true;
        o.shape = NavMeshObstacleShape.Box;
        o.center = Vector3.zero;
        o.size = Vector3.one;
    }

    static Transform Raiz(Transform zr, string nombre, Vector3 pos, Quaternion giro)
    {
        var go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, nombre);
        go.transform.SetParent(zr, true);
        go.transform.SetPositionAndRotation(pos, giro);
        return go.transform;
    }

    static void Brillo(GameObject go, Material glow)
    {
        var h = go.GetComponent<Highlightable>();
        if (!h) h = go.AddComponent<Highlightable>();
        if (glow == null) return;
        var so = new SerializedObject(h);
        SerializedProperty p = so.FindProperty("outlineMaterial");
        if (p != null) { p.objectReferenceValue = glow; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    // ------------------------------------------------------------------ Terreno

    // Suelo de la isla en (x, z): firme y por encima del agua (cualquiera = sin pedir que este alto ni plano)
    static bool Tierra(Collider suelo, Vector3 p, float y0, out Vector3 punto, bool cualquiera = false)
    {
        punto = p;
        if (!suelo.Raycast(new Ray(new Vector3(p.x, y0 + 80f, p.z), Vector3.down), out RaycastHit h, 200f)) return false;
        punto = h.point;
        return cualquiera || (h.point.y > y0 - 0.25f && h.normal.y > 0.85f);
    }

    // Distancia desde el centro hasta la costa en esa direccion (el ultimo suelo firme)
    static float Costa(Collider suelo, Vector3 centro, Vector3 dir, float y0)
    {
        float ultima = 0f;
        for (float r = 5f; r < 200f; r += 1f)
        {
            if (Tierra(suelo, centro + dir * r, y0, out _)) ultima = r;
            else if (ultima > 0f && r - ultima > 4f) break;   // ya es la playa / el mar
        }
        return ultima;
    }

    // Un sitio libre cerca de la costa en esa direccion (probando angulos y metros tierra adentro), mirando al centro
    static bool Sitio(Collider suelo, Vector3 centro, Vector3 dir, float y0, Vector3 mitad, float[] adentro, float[] angulos,
                      Transform faro, float lejosFaro, out Vector3 sitio, out Quaternion giro, out float costa)
    {
        sitio = centro + dir * 30f;
        giro = Quaternion.LookRotation(-dir);
        costa = 0f;
        foreach (float ang in angulos)
        {
            Vector3 d = Quaternion.Euler(0f, ang, 0f) * dir;
            float c = Costa(suelo, centro, d, y0);
            if (c <= 0f) continue;
            if (costa <= 0f) { costa = c; sitio = centro + d * Mathf.Max(0f, c - adentro[0]); }
            foreach (float a in adentro)
            {
                Vector3 p = centro + d * (c - a);
                if (!Tierra(suelo, p, y0, out Vector3 s)) continue;
                if (faro && Plano(s - faro.position) < lejosFaro) continue;
                Vector3 alCentro = centro - s; alCentro.y = 0f;
                Quaternion q = Quaternion.LookRotation(alCentro.sqrMagnitude > 0.01f ? alCentro.normalized : -d);
                // Terreno parejo debajo (las esquinas a menos de 40 cm)
                bool parejo = true;
                foreach (float x in new[] { -mitad.x, mitad.x })
                    foreach (float z in new[] { -mitad.z, mitad.z })
                        if (!Tierra(suelo, s + q * new Vector3(x, 0f, z), y0, out Vector3 e, true) || Mathf.Abs(e.y - s.y) > 0.4f) parejo = false;
                if (!parejo || !Libre(s, mitad, q, suelo)) continue;
                sitio = s;
                giro = q;
                costa = c;
                return true;
            }
        }
        if (Tierra(suelo, sitio, y0, out Vector3 s2, true)) sitio = s2;
        return false;
    }

    // Nada (arboles, rocas) en la caja que ocupa, sin contar el suelo
    static bool Libre(Vector3 pie, Vector3 mitad, Quaternion q, Collider suelo)
    {
        foreach (Collider c in Physics.OverlapBox(pie + Vector3.up * (mitad.y + 0.2f), mitad, q, ~0, QueryTriggerInteraction.Ignore))
            if (c != suelo) return false;
        return true;
    }

    static string Rumbo(Vector3 v)
    {
        string[] r = { "norte", "noreste", "este", "sureste", "sur", "suroeste", "oeste", "noroeste" };
        float a = Mathf.Repeat(Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg, 360f);   // 0 = norte (+Z), 90 = este (+X)
        return r[Mathf.RoundToInt(a / 45f) % 8];
    }

    static float Plano(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    // ------------------------------------------------------------------ Utilidades

    static GameObject Cubo(Transform padre, string nombre, Vector3 pos, Vector3 tam, Material m, bool collider, PrimitiveType tipo = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = tam;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (m) go.GetComponent<Renderer>().sharedMaterial = m;
        return go;
    }

    // Material de Assets/Guion (si existe se usa ese), siempre con Smoothness 0
    static Material Mat(string nombre, Color color)
    {
        string ruta = Carpeta + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, ruta);
        }
        MunecaModeloSetup.SinBrillo(m);
        return m;
    }

    static bool ZonaRoja(out Transform t)
    {
        t = null;
        foreach (GameObject g in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == "Zona Roja") { t = g.transform; return true; }
        GameObject zr = GameObject.Find("Zona Roja");
        if (zr) t = zr.transform;
        return t != null;
    }
}
