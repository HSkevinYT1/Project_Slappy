using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Volume global que usan los triggers ZonaVolumen. Fuera de las areas no aplica nada (peso 0),
// asi la tienda y la zona principal se ven igual que antes.
// Al cambiar de area se desvanece el perfil anterior y aparece el nuevo.
[RequireComponent(typeof(Volume))]
public class VolumenZonas : MonoBehaviour
{
    // Segundos que tarda en desvanecerse o aparecer un perfil
    public float transicion = 1f;

    private Volume volume;
    // Areas en las que esta el jugador. Si se pisan (la Zona Roja es enorme), manda la mas pequena
    private readonly List<ZonaVolumen> zonas = new List<ZonaVolumen>();

    public VolumeProfile PerfilDeseado
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
            return elegida != null ? elegida.perfil : null;
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
        VolumeProfile deseado = PerfilDeseado;
        float paso = transicion > 0f ? Time.deltaTime / transicion : 1f;

        if (volume.sharedProfile != deseado)
        {
            // Primero se apaga el perfil actual y despues se cambia
            volume.weight = Mathf.MoveTowards(volume.weight, 0f, paso);
            if (volume.weight <= 0f || volume.sharedProfile == null) volume.sharedProfile = deseado;
        }
        else if (deseado != null)
        {
            volume.weight = Mathf.MoveTowards(volume.weight, 1f, paso);
        }
    }
}
