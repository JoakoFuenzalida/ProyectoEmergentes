using System;
using System.Collections;
using System.Collections.Generic;
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
        Debug.Log("[OllamaService] Awake — singleton listo.");
    }

    // ─── Preguntas: 1 por llamada para evitar JSON truncado ───────

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
        var lista = new List<PreguntaData>();

        for (int i = 0; i < cantidad; i++)
        {
            PreguntaData resultado = default;
            string errorMsg        = null;

            yield return StartCoroutine(CoroutineGenerarUnaPregunta(
                p => resultado = p,
                e => errorMsg  = e));

            if (errorMsg != null)
            {
                onError?.Invoke($"Pregunta {i+1}: {errorMsg}");
                yield break;
            }

            lista.Add(resultado);
            Debug.Log($"[OllamaService] Pregunta {i+1}/{cantidad} OK: {resultado.Pregunta}");
        }

        onComplete?.Invoke(lista.ToArray());
    }

    private IEnumerator CoroutineGenerarUnaPregunta(Action<PreguntaData> onComplete,
                                                     Action<string> onError)
    {
        string prompt =
            "Generate exactly 1 trivia question about university computer science " +
            "(programming, algorithms, networks, OS, databases). " +
            "Respond ONLY with valid JSON, no extra text, no markdown:\n" +
            "{\"pregunta\":\"Name a programming language\"," +
            "\"respuestas\":[\"Python\",\"Java\",\"JavaScript\",\"C\",\"C++\"]," +
            "\"puntos\":[40,25,20,10,5]}\n" +
            "Rules: exactly 5 respuestas, puntos sum to 100, " +
            "pregunta and respuestas in Spanish, ASCII only, no accents.";

        bool done = false;

        yield return StartCoroutine(EnviarPrompt(prompt, raw =>
        {
            Debug.Log($"[OllamaService] Raw pregunta: {raw}");
            try
            {
                string json = LimpiarJson(raw);
                var p = JsonUtility.FromJson<PreguntaJson>(json);

                if (p == null || string.IsNullOrEmpty(p.pregunta) || p.respuestas == null)
                {
                    onError?.Invoke($"JSON inválido: {raw.Substring(0, Mathf.Min(120, raw.Length))}");
                }
                else
                {
                    onComplete?.Invoke(new PreguntaData
                    {
                        Pregunta   = p.pregunta,
                        Respuestas = p.respuestas,
                        Puntos     = (p.puntos != null && p.puntos.Length > 0)
                                        ? p.puntos : new[] { 40, 25, 20, 10, 5 },
                        Sinonimos  = p.sinonimos ?? new string[0]
                    });
                }
            }
            catch (Exception e)
            {
                onError?.Invoke($"Parse error: {e.Message} | {raw.Substring(0, Mathf.Min(120, raw.Length))}");
            }
            done = true;
        },
        e => { onError?.Invoke(e); done = true; }));
    }

    // ─── Comentarios del animador ─────────────────────────────────

    public void GenerarComentario(string contexto, Action<string> onComplete)
    {
        StartCoroutine(CoroutineGenerarComentario(contexto, onComplete));
    }

    private IEnumerator CoroutineGenerarComentario(string contexto, Action<string> onComplete)
    {
        string prompt =
            "You are Martin Carcamo, a famous Chilean TV host. " +
            "Write ONE short enthusiastic phrase (max 8 words) in Spanish for this game moment: " +
            contexto + ". Only the phrase, no quotes, no asterisks.";

        yield return StartCoroutine(EnviarPrompt(prompt, r =>
        {
            string c = r.Trim().Replace("*", "").Replace("\"", "").Replace("\n", " ");
            onComplete?.Invoke((c.Length >= 3 && c.Length <= 80) ? c : FraseFallback(contexto));
        },
        _ => onComplete?.Invoke(FraseFallback(contexto))));
    }

    private static string FraseFallback(string ctx)
    {
        if (ctx.Contains("ronda"))   return "¡Nueva ronda! ¡A darle con todo po'!";
        if (ctx.Contains("robo"))    return "¡El robo! ¿Cachay la respuesta?";
        if (ctx.Contains("ganador")) return "¡Brillante po'! ¡Asi se juega!";
        string[] g = {
            "¡Ya po'! ¡Vamos con todo!",
            "¡Increible po'! ¡Sigamos!",
            "¡Asi se hace, campeon!",
            "¡Que nivel po'! ¡Impresionante!"
        };
        return g[UnityEngine.Random.Range(0, g.Length)];
    }

    // ─── HTTP helper ──────────────────────────────────────────────

    private IEnumerator EnviarPrompt(string prompt,
                                     Action<string> onSuccess,
                                     Action<string> onError)
    {
        var body     = new OllamaRequest { model = modelo, prompt = prompt, stream = false };
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
            onError?.Invoke($"HTTP {req.responseCode}: {req.error}");
            yield break;
        }

        try
        {
            var resp = JsonUtility.FromJson<OllamaResponse>(req.downloadHandler.text);
            onSuccess?.Invoke(resp.response ?? "");
        }
        catch (Exception e)
        {
            onError?.Invoke($"Response parse error: {e.Message}");
        }
    }

    // ─── Limpieza de JSON ─────────────────────────────────────────

    private static string LimpiarJson(string raw)
    {
        int start = raw.IndexOf('{');
        int end   = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
            raw = raw.Substring(start, end - start + 1);
        else
            raw = raw.Trim();

        // JsonUtility no soporta \uXXXX — convertir a caracteres reales antes de parsear
        raw = System.Text.RegularExpressions.Regex.Replace(
            raw,
            @"\\u([0-9a-fA-F]{4})",
            m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

        return raw;
    }

    // ─── Clases de serialización ──────────────────────────────────

    [Serializable] private class OllamaRequest  { public string model; public string prompt; public bool stream; }
    [Serializable] private class OllamaResponse { public string response; public bool done; }

    [Serializable] private class PreguntaJson
    {
        public string   pregunta;
        public string[] respuestas;
        public string[] sinonimos;
        public int[]    puntos;
    }
}
