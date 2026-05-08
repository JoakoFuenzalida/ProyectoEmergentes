using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class OllamaService : MonoBehaviour
{
    public static OllamaService Instance { get; private set; }

    [Header("Configuración Ollama")]
    [SerializeField] private string ollamaUrl = "http://localhost:11434/api/generate";
    [SerializeField] private string modelo    = "llama3.2";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── Preguntas ────────────────────────────────────────────────

    public void GenerarPreguntas(int cantidad,
                                  Action<PreguntaData[]> onComplete,
                                  Action<string> onError)
    {
        StartCoroutine(CoroutineGenerarPreguntas(cantidad, onComplete, onError));
    }

    private IEnumerator CoroutineGenerarPreguntas(int cantidad,
                                                   Action<PreguntaData[]> onComplete,
                                                   Action<string> onError)
    {
        string prompt =
            $"Eres el animador de un programa de TV estilo 100 Latinos Dijeron. " +
            $"Genera EXACTAMENTE {cantidad} preguntas sobre informatica universitaria " +
            $"(programacion, redes, sistemas operativos, bases de datos, algoritmos, etc.). " +
            $"Las respuestas deben ser lo que diran estudiantes universitarios de informatica. " +
            $"Responde SOLO con JSON valido, sin texto adicional, sin bloques de codigo, " +
            $"en este formato exacto: " +
            $"{{\"items\":[{{\"pregunta\":\"...\",\"respuestas\":[\"...\",\"...\",\"...\",\"...\",\"...\"],\"puntos\":[45,25,15,10,5]}}]}} " +
            $"Los puntos de cada pregunta deben sumar 100. Usa solo caracteres ASCII, sin tildes.";

        yield return EnviarPrompt(prompt, respuesta =>
        {
            try
            {
                string json = LimpiarJson(respuesta);
                var wrapper = JsonUtility.FromJson<PreguntasWrapper>(json);
                if (wrapper?.items == null || wrapper.items.Length == 0)
                {
                    onError?.Invoke("Ollama no devolvio preguntas validas.");
                    return;
                }

                var resultado = new PreguntaData[wrapper.items.Length];
                for (int i = 0; i < wrapper.items.Length; i++)
                {
                    resultado[i] = new PreguntaData
                    {
                        Pregunta  = wrapper.items[i].pregunta,
                        Respuestas = wrapper.items[i].respuestas,
                        Puntos    = wrapper.items[i].puntos
                    };
                }
                onComplete?.Invoke(resultado);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Error parseando preguntas: {e.Message}\nRespuesta: {respuesta}");
            }
        }, error => onError?.Invoke(error));
    }

    // ─── Comentarios del animador ─────────────────────────────────

    public void GenerarComentario(string contexto, Action<string> onComplete)
    {
        StartCoroutine(CoroutineGenerarComentario(contexto, onComplete));
    }

    private IEnumerator CoroutineGenerarComentario(string contexto, Action<string> onComplete)
    {
        string prompt =
            "Eres Martin Carcamo, el animador chileno de television. " +
            "Eres carimatico, entretenido, usas expresiones chilenas como 'po', 'cachay', 'ya!', 'brillante'. " +
            "Responde en UNA sola oracion corta (maximo 12 palabras), sin asteriscos ni formato. " +
            "Comenta esto: " + contexto;

        yield return EnviarPrompt(prompt,
            r => onComplete?.Invoke(r.Trim().Replace("*", "").Replace("\n", " ")),
            _ => onComplete?.Invoke("¡Ya po'! ¡Vamos con todo!"));
    }

    // ─── HTTP helper ──────────────────────────────────────────────

    private IEnumerator EnviarPrompt(string prompt,
                                     Action<string> onSuccess,
                                     Action<string> onError)
    {
        var body    = new OllamaRequest { model = modelo, prompt = prompt, stream = false };
        var bodyJson = JsonUtility.ToJson(body);
        byte[] bytes = Encoding.UTF8.GetBytes(bodyJson);

        using var req = new UnityWebRequest(ollamaUrl, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 120;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"Error Ollama: {req.error}");
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<OllamaResponse>(req.downloadHandler.text);
            onSuccess?.Invoke(resp.response ?? "");
        }
        catch (Exception e)
        {
            onError?.Invoke($"Error parsing Ollama response: {e.Message}");
        }
    }

    // ─── Limpieza de JSON ─────────────────────────────────────────

    private static string LimpiarJson(string raw)
    {
        // Quitar bloques de código markdown (```json ... ```)
        int start = raw.IndexOf('{');
        int end   = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
            return raw.Substring(start, end - start + 1);
        return raw.Trim();
    }

    // ─── Clases de serialización ──────────────────────────────────

    [Serializable] private class OllamaRequest
    {
        public string model;
        public string prompt;
        public bool   stream;
    }

    [Serializable] private class OllamaResponse
    {
        public string response;
        public bool   done;
    }

    [Serializable] private class PreguntaJson
    {
        public string   pregunta;
        public string[] respuestas;
        public int[]    puntos;
    }

    [Serializable] private class PreguntasWrapper
    {
        public PreguntaJson[] items;
    }
}
