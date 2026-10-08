using UnityEngine;

// Controla las manecillas usando un tiempo ficticio del juego.
public class ClockController : MonoBehaviour
{
    // Manecilla de las horas.
    public Transform hourHand;

    // Manecilla de los minutos.
    public Transform minuteHand;

    // Hora en la que comienza el reloj.
    // Ejemplo: 8 = 8:00, 14 = 14:00.
    [Range(0f, 24f)]
    public float gameHour = 8f;

    // Cuántos segundos REALES tardan en pasar 24 horas del juego.
    public float dayDuration = 120f;

    void Update()
    {
        // Calcula cuánto debe avanzar el tiempo del juego.
        gameHour += (24f / dayDuration) * Time.deltaTime;

        // Cuando llegue a 24 horas, vuelve a 0.
        if (gameHour >= 24f)
        {
            gameHour -= 24f;
        }


        // Convierte la hora de 24 horas a un reloj de 12 horas.
        float hour12 = gameHour % 12f;

        // Obtiene los minutos actuales.
        float minutes = (gameHour * 60f) % 60f;


        // Mueve la manecilla de minutos.
        minuteHand.localRotation =
            Quaternion.Euler(0f, -minutes * 6f, 0f);

        // Mueve la manecilla de horas.
        hourHand.localRotation =
            Quaternion.Euler(0f, -hour12 * 30f, 0f);
    }
}