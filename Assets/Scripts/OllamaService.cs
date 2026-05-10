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
            "Genera UNA pregunta estilo 'Que dice la gente' sobre informatica universitaria.\n" +
            "La pregunta debe ser del tipo: 'Nombre un/una...' o 'Cual es el/la mas...'.\n" +
            "Las respuestas son las 5 cosas mas comunes que diria la gente.\n\n" +
            "Responde SOLO con este JSON (sin markdown, sin explicacion):\n" +
            "{\"pregunta\":\"Nombre un lenguaje de programacion popular\"," +
            "\"respuestas\":[\"Python\",\"JavaScript\",\"Java\",\"C++\",\"PHP\"]," +
            "\"puntos\":[40,25,15,12,8]}\n\n" +
            "Reglas: solo letras ASCII (sin tildes ni enyes), exactamente 5 respuestas, " +
            "los puntos deben sumar 100, respuestas de 1 a 4 palabras cada una.\n" +
            "Genera una pregunta DIFERENTE al ejemplo.";

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

        string[] sinonimos = GenerarSinonimos(respuestas.ToArray());

        Debug.Log($"[OllamaService] OK — {pregunta} | {string.Join(" / ", respuestas)}");

        return new PreguntaData
        {
            Pregunta   = pregunta,
            Respuestas = respuestas.ToArray(),
            Puntos     = puntos,
            Sinonimos  = sinonimos
        };
    }

    // ─── Sinónimos automáticos ─────────────────────────────────────
    // Para cada respuesta, busca en el diccionario y agrega cada palabra
    // de respuestas multi-palabra como sinónimo adicional.

    private static readonly Dictionary<string, string> _sinoDict =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Lenguajes
        {"JavaScript",          "JS|java script"},
        {"Python",              "py|python3"},
        {"Java",                "jdk|java se"},
        {"C++",                 "cplusplus|cpp|c plus plus"},
        {"C#",                  "csharp|c sharp"},
        {"C",                   "lenguaje c|ansi c"},
        {"PHP",                 "php7|php8"},
        {"Ruby",                "rb|ruby on rails"},
        {"Swift",               "swift ios"},
        {"Kotlin",              "kotlin android"},
        {"Go",                  "golang"},
        {"Rust",                "rustlang"},
        {"TypeScript",          "TS|type script"},
        {"Scala",               "scala jvm"},
        {"R",                   "lenguaje r|r studio"},
        {"MATLAB",              "matlab simulink"},
        // Web / protocolos
        {"HTML",                "html5|hypertext markup"},
        {"CSS",                 "css3|estilos|cascading"},
        {"SQL",                 "sequel|structured query"},
        {"NoSQL",               "no sql"},
        {"REST",                "restful|rest api"},
        {"HTTP",                "hypertext transfer|http1"},
        {"HTTPS",               "http seguro|http ssl|tls"},
        {"TCP",                 "tcp ip|transmission control"},
        {"UDP",                 "user datagram"},
        {"IP",                  "internet protocol"},
        {"DNS",                 "domain name system"},
        {"URL",                 "enlace|link|direccion web|uri"},
        {"API",                 "interfaz|application programming interface"},
        // Bases de datos
        {"MySQL",               "my sql"},
        {"PostgreSQL",          "postgres"},
        {"MongoDB",             "mongo"},
        {"SQLite",              "sqlite db"},
        {"Redis",               "redis cache"},
        {"Base de datos",       "BD|DB|database|BBDD|bbdd"},
        // Sistemas operativos
        {"Linux",               "gnu linux|unix|tux"},
        {"Windows",             "microsoft windows|win"},
        {"macOS",               "mac os|osx|apple os"},
        {"Android",             "android os"},
        {"iOS",                 "iphone os|apple mobile"},
        {"Sistema Operativo",   "SO|OS|operating system"},
        // Estructuras de datos
        {"Array",               "arreglo|vector|lista indexada"},
        {"Lista",               "list|linked list|lista enlazada"},
        {"Pila",                "stack|lifo"},
        {"Cola",                "queue|fifo"},
        {"Arbol",               "tree|arbol binario|arbol avl"},
        {"Grafo",               "graph|red nodos"},
        {"Hash",                "hashmap|tabla hash|diccionario"},
        {"Heap",                "monticulo|priority queue"},
        // Conceptos
        {"Recursion",           "recursividad|recursivo"},
        {"POO",                 "programacion orientada objetos|OOP|orientado objetos"},
        {"OOP",                 "POO|orientado objetos|programacion objetos"},
        {"Algoritmo",           "algorithm|procedimiento"},
        {"Compilador",          "compiler|compilar"},
        {"Interprete",          "interpreter"},
        {"Framework",           "marco trabajo"},
        {"Libreria",            "library|lib|biblioteca"},
        {"Variable",            "var"},
        {"Funcion",             "function|metodo|method"},
        {"Clase",               "class|objeto"},
        {"Interfaz",            "interface|contrato"},
        {"Herencia",            "inheritance|extends"},
        {"Polimorfismo",        "polymorphism"},
        {"Encapsulamiento",     "encapsulation|encapsulacion"},
        // Hardware / infra
        {"RAM",                 "memoria ram|memoria volatil"},
        {"CPU",                 "procesador|unidad procesamiento"},
        {"GPU",                 "tarjeta grafica|procesador grafico"},
        {"SSD",                 "disco solido|solid state drive"},
        {"HDD",                 "disco duro|hard drive"},
        // DevOps / cloud
        {"Git",                 "github|gitlab|control versiones|vcs"},
        {"Docker",              "contenedor|container"},
        {"Kubernetes",          "k8s|kube"},
        {"AWS",                 "amazon web services|amazon cloud"},
        {"Azure",               "microsoft azure"},
        {"GCP",                 "google cloud|google cloud platform"},
        // Seguridad / redes
        {"Firewall",            "cortafuegos|muro fuego"},
        {"Cifrado",             "encriptacion|encryption|cifrar"},
        {"Red",                 "network|red computadores"},
        {"Servidor",            "server|host"},
        {"Cliente",             "client|browser"},
    };

    private static string[] GenerarSinonimos(string[] respuestas)
    {
        var resultado = new string[respuestas.Length];
        for (int i = 0; i < respuestas.Length; i++)
        {
            string resp = respuestas[i];
            _sinoDict.TryGetValue(resp, out string syns);
            syns = syns ?? "";

            // Agregar cada palabra de respuestas multi-palabra (>2 letras) como sinónimo
            var palabras = resp.Split(' ');
            if (palabras.Length > 1)
            {
                foreach (var p in palabras)
                    if (p.Length > 2 && !syns.Contains(p))
                        syns += (syns.Length > 0 ? "|" : "") + p;
            }

            resultado[i] = syns;
        }
        return resultado;
    }

    // ─── Clases de serialización HTTP ─────────────────────────────

    [Serializable] private class OllamaOptions  { public int num_predict = 1024; }
    [Serializable] private class OllamaRequest  { public string model; public string prompt; public bool stream; public OllamaOptions options; }
    [Serializable] private class OllamaResponse { public string response; public bool done; }
}
