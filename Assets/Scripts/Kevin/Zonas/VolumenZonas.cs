using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Volume global que usan los triggers ZonaVolumen. Fuera de las areas no aplica nada (peso 0),
// asi la tienda y la zona principal se ven igual que antes.
// Al cambiar de area se desvanece el perfil anterior y aparece el nuevo.
[RequireComponent(typeof(Volume))]
public class VolumenZonas : MonoBehaviour
{
    // Segundos que tarda en desvanecerse o aparecer un perfil (el efecto entra despacio, con curva suave)
    public float transicion = 5f;
    // Niebla que se enciende solo dentro de las areas que la usan (ZonaVolumen.niebla)
    public FogVolume niebla;

    private Volume volume;
    // 0..1 lineal; el peso del Volume es su version suavizada
    private float avance;
    // Areas en las que esta el jugador. Si se pisan (la Zona Roja es enorme), manda la mas pequena
    private readonly List<ZonaVolumen> zonas = new List<ZonaVolumen>();

    public VolumeProfile PerfilDeseado => ZonaActual != null ? ZonaActual.perfil : null;

    // Area en la que esta el jugador (si se pisan, la mas pequena)
    public ZonaVolumen ZonaActual
    {
        get
        {
            ZonaVolumen elegida = null;
            float menor = float.MaxValue;
            foreach (ZonaVolumen z in zonas)
            {
                Vector3 s = z.GetComponent<BoxCollider>().bounds.size;
                float tamano = s.x * s.y * s.z;
                if (tamano < menor) { menor = tamano; elegida = z; }
            }
            return elegida;
        }
    }

    void Awake()
    {
        volume = GetComponent<Volume>();
        volume.isGlobal = true;
        volume.weight = 0f;
        volume.sharedProfile = null;
    }

    public void Entrar(ZonaVolumen zona)
    {
        zonas.Remove(zona);
        zonas.Add(zona);
    }

    public void Salir(ZonaVolumen zona)
    {
        zonas.Remove(zona);
    }

    void Update()
    {
        ZonaVolumen zona = ZonaActual;
        VolumeProfile deseado = zona != null ? zona.perfil : null;
        float paso = transicion > 0f ? Time.deltaTime / transicion : 1f;

        if (volume.sharedProfile != deseado)
        {
            // Primero se apaga el perfil actual y despues se cambia
            avance = Mathf.MoveTowards(avance, 0f, paso * 2f);
            if (avance <= 0f || volume.sharedProfile == null) volume.sharedProfile = deseado;
        }
        else if (deseado != null)
        {
            avance = Mathf.MoveTowards(avance, 1f, paso);
        }
        else avance = Mathf.MoveTowards(avance, 0f, paso * 2f);
        volume.weight = avance * avance * (3f - 2f * avance);

        // Niebla: solo en las areas que la usan (no en la tienda, la zona principal ni el Backrooms)
        if (niebla != null)
        {
            bool quiere = zona != null && zona.niebla;
            if (quiere && !niebla.IsActive) niebla.Activate();
            else if (!quiere && niebla.IsActive) niebla.Deactivate();
        }
    }
}
