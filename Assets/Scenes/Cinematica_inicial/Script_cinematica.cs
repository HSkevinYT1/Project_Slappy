using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;


public class Script_cinematica : MonoBehaviour
{
    //Delaracion de variables 
    public PlayableDirector director;
    public float duracionCinematica;
    float tiempoTranscurrido;

    public GameObject CinematicaObj;
    public GameObject GameplayObj;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        duracionCinematica = (float)director.duration;
    }

    // Update is called once per frame
    void Update()
    {
      tiempoTranscurrido += Time.deltaTime;

        if (tiempoTranscurrido >= duracionCinematica)
        {
            CinematicaObj.SetActive(false);
            GameplayObj.SetActive(true);
            Destroy(this);
        }  
    }
}
