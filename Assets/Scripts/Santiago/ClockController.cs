using UnityEngine;

// Controla el reloj normal y el modo detector de entidad.
public class ClockController : MonoBehaviour
{
    // -------------------------
    // MANECILLAS
    // -------------------------

    public Transform hourHand;
    public Transform minuteHand;


    // -------------------------
    // MODO RELOJ NORMAL
    // -------------------------

    // Hora inicial del juego.
    [Range(0f, 24f)]
    public float gameHour = 8f;

    // Cuántos segundos reales tarda un día completo de 24 horas.
    public float dayDuration = 120f;


    // -------------------------
    // MODO DETECTOR DE ENTIDAD
    // -------------------------

    // Referencia al Enemy.
    public Transform entity;

    // Distancia a la que empieza la detección.
    public float maxDetectionDistance = 20f;

    // Distancia considerada muy cercana.
    public float minDetectionDistance = 2f;

    // Velocidad mínima de giro de las agujas.
    public float minSpinSpeed = 10f;

    // Velocidad máxima de giro de las agujas.
    public float maxSpinSpeed = 600f;


    // false = reloj normal
    // true = detector de entidad
    private bool entityMode = false;


    void Update()
    {
        // Si está activado el modo entidad...
        if (entityMode)
        {
            EntityDetector();
        }
        else
        {
            // Si no, funciona como reloj normal.
            NormalClock();
        }
    }


    // -------------------------
    // RELOJ NORMAL
    // -------------------------

    private void NormalClock()
    {
        // Hace avanzar la hora del juego.
        gameHour += (24f / dayDuration) * Time.deltaTime;

        // Cuando llega a 24 horas, vuelve a 0.
        if (gameHour >= 24f)
        {
            gameHour -= 24f;
        }

        // Convierte la hora a formato de 12 horas.
        float hour12 = gameHour % 12f;

        // Calcula los minutos.
        float minutes = (gameHour * 60f) % 60f;


        // Mueve la manecilla de minutos.
        minuteHand.localRotation =
            Quaternion.Euler(0f, -minutes * 6f, 0f);

        // Mueve la manecilla de horas.
        hourHand.localRotation =
            Quaternion.Euler(0f, -hour12 * 30f, 0f);
    }


    // -------------------------
    // DETECTOR DE ENTIDAD
    // -------------------------

    private void EntityDetector()
    {
        // Seguridad por si todavía no se ha conectado el Enemy.
        if (entity == null)
        {
            return;
        }


        // Calcula la distancia entre el reloj y la entidad.
        float distance = Vector3.Distance(
            transform.position,
            entity.position
        );


        // Convierte la distancia en un valor entre 0 y 1.
        //
        // 0 = entidad lejos
        // 1 = entidad muy cerca
        float proximity = Mathf.InverseLerp(
            maxDetectionDistance,
            minDetectionDistance,
            distance
        );


        // Calcula la velocidad de giro según la cercanía.
        float spinSpeed = Mathf.Lerp(
            minSpinSpeed,
            maxSpinSpeed,
            proximity
        );


        // Gira continuamente la manecilla de minutos.
        minuteHand.Rotate(
            0f,
            -spinSpeed * Time.deltaTime,
            0f
        );


        // La manecilla de horas también gira,
        // pero más lentamente.
        hourHand.Rotate(
            0f,
            -(spinSpeed * 0.4f) * Time.deltaTime,
            0f
        );
    }


    // -------------------------
    // CAMBIO DE MODO
    // -------------------------

    // Esta función es llamada desde TeleportTrigger.
    public void ActivateEntityMode()
    {
        entityMode = true;
    }
}