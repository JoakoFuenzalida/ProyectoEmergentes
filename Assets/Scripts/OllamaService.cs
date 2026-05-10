using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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

    // ─── Preguntas: 1 por llamada ─────────────────────────────────

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
            "Output ONLY a JSON object, no markdown, no explanation.\n" +
            "Schema: {\"pregunta\":\"...\",\"respuestas\":[\"A\",\"B\",\"C\",\"D\",\"E\"],\"puntos\":[40,25,20,10,5]}\n" +
            "Rules: topic=university computer science, language=Spanish, " +
            "ASCII only (no accents/tildes/enyes), exactly 5 respuestas, puntos sum=100.\n" +
            "Example: {\"pregunta\":\"Cual es el lenguaje mas usado en desarrollo web?\"," +
            "\"respuestas\":[\"JavaScript\",\"Python\",\"Java\",\"PHP\",\"Ruby\"]," +
            "\"puntos\":[40,25,20,10,5]}";

        bool done = false;

        yield return StartCoroutine(EnviarPrompt(prompt, raw =>
        {
            try
            {
                var p = ExtraerPregunta(raw);
                if (p == null)
                    onError?.Invoke($"No se encontraron campos en: {raw.Substring(0, Mathf.Min(200, raw.Length))}");
                else
                    onComplete?.Invoke(p.Value);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Excepcion extrayendo pregunta: {e.Message}");
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
        var body     = new OllamaRequest { model = modelo, prompt = prompt, stream = false, options = new OllamaOptions() };
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

    // ─── Extractor robusto por regex (sin JsonUtility) ────────────
    // JsonUtility falla con acentos, \uXXXX y otros chars no-ASCII.
    // Este extractor busca los campos directamente con regex.

    private static PreguntaData? ExtraerPregunta(string raw)
    {
        // 1) Decodificar escapes \uXXXX a caracteres reales
        raw = Regex.Replace(raw, @"\\u([0-9a-fA-F]{4})",
            m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

        // 2) Eliminar saltos de línea/tabs internos y comillas escapadas
        raw = raw.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        raw = raw.Replace("\\\"", "'");   // \" → ' para no romper los límites de string
        raw = raw.Replace("\\\\", "\\");  // \\ → \

        // 3) Extraer "pregunta":"..."  — toma todo hasta la próxima " no escapada
        var mP = Regex.Match(raw, @"""pregunta""\s*:\s*""([^""]*)""");
        if (!mP.Success) return null;
        string pregunta = mP.Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(pregunta)) return null;

        // 4) Extraer bloque "respuestas":[...]
        var mR = Regex.Match(raw, @"""respuestas""\s*:\s*\[([^\]]*)\]");
        if (!mR.Success) return null;

        // 5) Extraer cada string dentro del bloque
        var respuestas = new List<string>();
        foreach (Match m in Regex.Matches(mR.Groups[1].Value, @"""([^""]*)"""))
            respuestas.Add(m.Groups[1].Value.Trim());

        if (respuestas.Count < 3) return null;

        // 6) Extraer "puntos":[n,n,n,n,n]
        int[] puntos = new[] { 40, 25, 20, 10, 5 };
        var mPuntos = Regex.Match(raw, @"""puntos""\s*:\s*\[([0-9,\s]+)\]");
        if (mPuntos.Success)
        {
            var pList = new List<int>();
            foreach (var token in mPuntos.Groups[1].Value.Split(','))
                if (int.TryParse(token.Trim(), out int v)) pList.Add(v);
            if (pList.Count >= 3) puntos = pList.ToArray();
        }

        // 7) Normalizar a 5 respuestas y 5 puntos
        while (respuestas.Count < 5) respuestas.Add("Otro");
        if (respuestas.Count > 5) respuestas = respuestas.GetRange(0, 5);
        if (puntos.Length < 5) puntos = new[] { 40, 25, 20, 10, 5 };

        Debug.Log($"[OllamaService] OK — {pregunta} | {string.Join(" / ", respuestas)}");

        return new PreguntaData
        {
            Pregunta   = pregunta,
            Respuestas = respuestas.ToArray(),
            Puntos     = puntos,
            Sinonimos  = new string[0]
        };
    }

    // ─── Clases de serialización HTTP ─────────────────────────────

    [Serializable] private class OllamaOptions  { public int num_predict = 1024; }
    [Serializable] private class OllamaRequest  { public string model; public string prompt; public bool stream; public OllamaOptions options; }
    [Serializable] private class OllamaResponse { public string response; public bool done; }
}
