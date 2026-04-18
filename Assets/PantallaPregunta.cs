using UnityEngine;

using TMPro;

public class PantallaPregunta : MonoBehaviour
{
    [Header("Pantalla")]
    public TextMeshProUGUI textPregunta;
    public TextMeshProUGUI[] filasRespuestas;

    [Header("Datos hardcodeados")]
    string pregunta = "¿Nombre algo que la gente hace en la mañana?";
    
    string[] respuestas = {
        "Tomar desayuno",
        "Ducharse",
        "Ver el celular",
        "Cepillarse los dientes",
        "Hacer ejercicio",
        "Tomar café"
    };

    int[] puntajes = { 38, 27, 18, 10, 5, 2 };

    void Start()
    {
        if (textPregunta == null || filasRespuestas == null || filasRespuestas.Length == 0)
        {
            Debug.LogWarning("PantallaPregunta: Referencias no asignadas, saltando Start()");
            return;
        }
        // Muestra la pregunta
        textPregunta.text = pregunta;

        // Muestra cada fila con su puntaje
        for (int i = 0; i < filasRespuestas.Length; i++)
        {
            filasRespuestas[i].text = (i + 1) + ".  " + respuestas[i] + "  •••  " + puntajes[i];
        }
    }
}