using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public class OllamaService : MonoBehaviour
{
    public static OllamaService Instance { get; private set; }

    [Header("Configuración Groq")]
    [SerializeField] private string groqUrl = "https://api.groq.com/openai/v1/chat/completions";
    // llama-3.1-8b-instant: 30,000 TPM (vs 12,000 del 70b) — calidad similar para JSON estructurado
    [SerializeField] private string modelo  = "llama-3.1-8b-instant";

    // ─── API Key: cargada desde archivo externo (NO en el código ni en el Inspector) ──
    // El archivo `groq.env` se busca en este orden y NO se versiona en Git (ver .gitignore).
    //   1) Al lado del .exe (para builds distribuidos)
    //   2) En la raíz del proyecto Unity (para desarrollo en Editor)
    //   3) En %APPDATA%/QueDiceChile/groq.env (fallback persistente)
    // Formato del archivo: una sola línea con la API key, sin comillas ni espacios.
    private string apiKey = "";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        CargarApiKey();
    }

    /// <summary>
    /// Busca el archivo `groq.env` en varias ubicaciones y carga la API key.
    /// Si no encuentra el archivo, loguea un error claro y el servicio queda inactivo.
    /// </summary>
    private void CargarApiKey()
    {
        string[] rutasBusqueda = {
            Path.Combine(Application.dataPath, "..", "groq.env"),                  // Raíz del proyecto (Editor)
            Path.Combine(Path.GetDirectoryName(Application.dataPath), "groq.env"), // Al lado del .exe (Build)
            Path.Combine(Application.persistentDataPath, "groq.env")               // %APPDATA% (fallback)
        };

        foreach (string ruta in rutasBusqueda)
        {
            try
            {
                if (File.Exists(ruta))
                {
                    apiKey = File.ReadAllText(ruta).Trim();
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        Debug.Log($"[OllamaService] API key cargada desde: {ruta}");
                        return;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning($"[OllamaService] Error leyendo {ruta}: {e.Message}"); }
        }

        Debug.LogError("[OllamaService] No se encontró 'groq.env' en ninguna ruta. Crea el archivo con tu API key. Rutas buscadas:\n" +
                       string.Join("\n", rutasBusqueda));
    }

    // ─── Preguntas: 1 por llamada ─────────────────────────────────

    // Temas curriculares (basados en la malla de Ing. Civil Informatica PUCV)
    // + temas culturales para variedad. Mezcla 11 + 6 = 17 temas.
    private static readonly string[] _temas =
    {
        // ── CURRICULARES ──
        "Estructura de Datos",
        "Base de Datos",
        "Redes de Computadores",
        "Inteligencia Artificial",
        "Hardware y Sistemas Operativos",
        "Ingenieria Web y Movil",
        "Ciberseguridad",
        "Automatas y Compiladores",
        "Analisis y Diseno de Algoritmos",
        "Administracion de Proyectos Informaticos",
        "Experiencia del Usuario",
        "comandos basicos de Git o GitHub",
        "herramientas modernas de desarrollo de software",

        // ── CULTURALES ──
        "snack o bebida que consume un programador codeando",
        "juego o videojuego favorito de un informatico",
        "aplicacion que todo informatico tiene instalada",
        "excusa que da un informatico para no entregar el proyecto",
        "distraccion favorita de un programador cuando deberia estar trabajando",
        "frase tipica que dice un programador frustrado",
    };

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
        var lista      = new List<PreguntaData>();
        var previas    = new List<string>();          // se pasan al prompt para evitar repeticion

        // Mezclar temas y tomar los primeros 'cantidad'
        var temasMezclados = new List<string>(_temas);
        for (int k = temasMezclados.Count - 1; k > 0; k--)
        {
            int j = UnityEngine.Random.Range(0, k + 1);
            (temasMezclados[k], temasMezclados[j]) = (temasMezclados[j], temasMezclados[k]);
        }

        const int MIN_PREGUNTAS_ACEPTABLE = 5;
        const float PAUSA_ENTRE_REQUESTS  = 1.5f;
        // Esperas progresivas para reintentos: 10s, 30s, 60s (la ventana de TPM es de 60s)
        float[] esperasReintento = { 10f, 30f, 60f };

        for (int i = 0; i < cantidad; i++)
        {
            string tema     = temasMezclados[i % temasMezclados.Count];
            PreguntaData resultado = default;
            string errorMsg        = null;

            yield return StartCoroutine(CoroutineGenerarUnaPregunta(
                tema, previas,
                p => resultado = p,
                e => errorMsg  = e));

            // ── Reintentos múltiples con espera progresiva ──
            int intento = 0;
            while (errorMsg != null && intento < esperasReintento.Length)
            {
                float espera = esperasReintento[intento];
                Debug.LogWarning($"[OllamaService] Pregunta {i+1} intento {intento+1} fallo: {errorMsg}. Esperando {espera}s antes de reintentar...");
                yield return new WaitForSeconds(espera);

                errorMsg = null;
                yield return StartCoroutine(CoroutineGenerarUnaPregunta(
                    tema, previas,
                    p => resultado = p,
                    e => errorMsg  = e));
                intento++;
            }

            if (errorMsg != null)
            {
                // Después de 3 reintentos sigue fallando
                if (lista.Count >= MIN_PREGUNTAS_ACEPTABLE)
                {
                    Debug.LogWarning($"[OllamaService] Todos los reintentos fallaron, pero ya tenemos {lista.Count} preguntas. Cortando aqui.");
                    break;
                }
                onError?.Invoke($"Pregunta {i+1} (tras {esperasReintento.Length} reintentos): {errorMsg}");
                yield break;
            }

            lista.Add(resultado);
            previas.Add(resultado.Pregunta);
            Debug.Log($"[OllamaService] Pregunta {i+1}/{cantidad} OK: {resultado.Pregunta}");

            if (i < cantidad - 1) yield return new WaitForSeconds(PAUSA_ENTRE_REQUESTS);
        }

        onComplete?.Invoke(lista.ToArray());
    }

    private IEnumerator CoroutineGenerarUnaPregunta(string tema,
                                                     List<string> previas,
                                                     Action<PreguntaData> onComplete,
                                                     Action<string> onError)
    {
        // Construir lista de preguntas ya generadas para evitar repetición
        string listaPrevias = previas.Count > 0
            ? "Preguntas YA generadas en esta partida (NO repetir ni hacer preguntas similares):\n" +
              string.Join("\n", previas.ConvertAll(p => "- " + p)) + "\n\n"
            : "";

        string prompt =
            "Eres el presentador de un concurso chileno tipo \"Que dice Chile\" / \"Family Feud\".\n\n" +
            "Los jugadores son estudiantes de Ingenieria Civil Informatica de la PUCV " +
            "(Pontificia Universidad Catolica de Valparaiso), Chile.\n\n" +
            $"TEMA: {tema}\n\n" +
            "Genera UNA pregunta que un estudiante de informatica PUCV responda FACILMENTE.\n" +
            "La pregunta debe ser de uno de estos tipos:\n" +
            "- \"Nombre un/una...\"\n" +
            "- \"Cual es el/la mas popular...\"\n" +
            "- \"Que hace un programador/informatico cuando...\" (situacional, con respuestas CORTAS y PREDECIBLES — no abierta)\n" +
            "- \"Diga/Mencione un/una...\"\n\n" +
            "REGLAS OBLIGATORIAS:\n" +
            "- ENTRE 4 Y 7 RESPUESTAS — IDEAL 5 o 6, evita siempre 4 si hay mas respuestas populares\n" +
            "- Usa 6 o 7 cuando la pregunta tiene MUCHAS respuestas faciles (ej: lenguajes, comandos Git, IAs)\n" +
            "- Las respuestas deben ser TERMINOS POPULARES Y MODERNOS (2024-2025), NO legacy\n" +
            "- PROHIBIDO incluir tecnologia obsoleta: SVN, Mercurial, CVS, Notepad clasico, PHP4, Flash, etc.\n" +
            "- Pensa: \"que diria un estudiante de informatica de la PUCV en 2025?\"\n" +
            "- NO inventes respuestas obscuras para rellenar el banco\n" +
            "- Cada respuesta vale al menos 8 puntos (NO hay respuestas de 2-5 puntos)\n" +
            "- Los puntos suman EXACTAMENTE 100, en orden decreciente\n" +
            "- Distribuciones sugeridas:\n" +
            "    4 resp: 40-25-20-15\n" +
            "    5 resp: 30-25-20-15-10\n" +
            "    6 resp: 25-22-18-15-12-8\n" +
            "    7 resp: 22-20-17-14-12-8-7\n" +
            "- Respuestas de 1 a 3 palabras\n" +
            "- TODO en español, solo ASCII (sin tildes, sin enyes)\n\n" +
            "EJEMPLOS DE BUENAS PREGUNTAS:\n\n" +
            "{\"pregunta\":\"Nombre una estructura de datos clasica\",\"respuestas\":[\"Array\",\"Lista\",\"Pila\",\"Arbol\",\"Hash\"],\"puntos\":[30,25,20,15,10]}\n\n" +
            "{\"pregunta\":\"Nombre un IDE moderno para programar\",\"respuestas\":[\"VS Code\",\"IntelliJ\",\"PyCharm\",\"Cursor\",\"WebStorm\"],\"puntos\":[35,22,18,15,10]}\n\n" +
            "{\"pregunta\":\"Que come o bebe un programador codeando a las 3am\",\"respuestas\":[\"Energetica\",\"Pizza\",\"Cafe\",\"Bebida\"],\"puntos\":[35,30,20,15]}\n\n" +
            "{\"pregunta\":\"Nombre una IA o LLM de uso general\",\"respuestas\":[\"Claude\",\"ChatGPT\",\"Gemini\",\"Grok\",\"DeepSeek\"],\"puntos\":[30,25,20,15,10]}\n\n" +
            "{\"pregunta\":\"Mencione un comando basico de Git\",\"respuestas\":[\"commit\",\"push\",\"pull\",\"clone\",\"merge\",\"add\",\"branch\"],\"puntos\":[22,20,17,14,12,8,7]}\n\n" +
            listaPrevias +
            "Responde SOLO con este JSON exacto (sin markdown, sin texto extra):\n" +
            "{\"pregunta\":\"...\",\"respuestas\":[\"...\"],\"puntos\":[...]}";

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
        },
        e => onError?.Invoke(e)));
    }

    // ─── Bienvenida del conductor (mensaje más largo, para el Intro) ──

    public void GenerarBienvenida(string contexto, Action<string> onComplete)
    {
        StartCoroutine(CoroutineGenerarBienvenida(contexto, onComplete));
    }

    private IEnumerator CoroutineGenerarBienvenida(string contexto, Action<string> onComplete)
    {
        string prompt =
            "Eres Martin Carcamo, animador chileno de television, conductor del programa '100 Chilenos Dicen'. " +
            "Escribe UNA frase entusiasta en español (maximo 25 palabras) para dar la bienvenida al programa con este contexto: " +
            contexto + ". Solo la frase, sin comillas, sin asteriscos.";

        yield return StartCoroutine(EnviarPrompt(prompt, r =>
        {
            string c = r.Trim().Replace("*", "").Replace("\"", "").Replace("\n", " ");
            onComplete?.Invoke(c.Length >= 5 ? c.Substring(0, Mathf.Min(250, c.Length)) : FraseBienvenidaFallback());
        },
        _ => onComplete?.Invoke(FraseBienvenidaFallback())));
    }

    private static string FraseBienvenidaFallback() =>
        "¡Bienvenidos a los 100 Chilenos Dicen! ¡Que comience el juego!";

    // ─── Comentarios del animador ─────────────────────────────────

    public void GenerarComentario(string contexto, Action<string> onComplete)
    {
        StartCoroutine(CoroutineGenerarComentario(contexto, onComplete));
    }

    private IEnumerator CoroutineGenerarComentario(string contexto, Action<string> onComplete)
    {
        string prompt =
            "Eres Martin Carcamo, animador chileno de television. " +
            "Escribe UNA frase corta y entusiasta (maximo 8 palabras) en español para este momento del juego: " +
            contexto + ". Solo la frase, sin comillas, sin asteriscos.";

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

    // ─── Suspense antes de revelar respuesta ─────────────────────────

    public void GenerarFraseSuspense(string nombre, string respuesta, Action<string> onComplete)
    {
        StartCoroutine(CoroutineGenerarFraseSuspense(nombre, respuesta, onComplete));
    }

    private IEnumerator CoroutineGenerarFraseSuspense(string nombre, string respuesta,
                                                       Action<string> onComplete)
    {
        string prompt =
            "Eres Martin Carcamo, animador chileno de television del programa '100 Chilenos Dicen'. " +
            $"El jugador {nombre} acaba de responder {respuesta}. " +
            "Genera UNA sola frase (maximo 12 palabras) que genere maximo suspenso antes de revelar si es correcta. " +
            "Menciona el nombre del jugador y su respuesta. Termina SIEMPRE con la palabra DAMELA. " +
            "Solo la frase, sin comillas, sin signos de puntuacion especiales, sin explicaciones.";

        yield return StartCoroutine(EnviarPrompt(prompt, r =>
        {
            string c = r.Trim().Replace("*", "").Replace("\"", "").Replace("\n", " ");
            onComplete?.Invoke(c.Length >= 5
                ? c.Substring(0, Mathf.Min(200, c.Length))
                : FraseSuspenseFallback(nombre, respuesta));
        },
        _ => onComplete?.Invoke(FraseSuspenseFallback(nombre, respuesta))));
    }

    private static string FraseSuspenseFallback(string nombre, string respuesta) =>
        $"{nombre} dice {respuesta}... DAMELA!";

    // ─── HTTP helper (formato Groq / OpenAI) ─────────────────────

    private IEnumerator EnviarPrompt(string prompt,
                                     Action<string> onSuccess,
                                     Action<string> onError)
    {
        var body = new GroqRequest
        {
            model       = modelo,
            messages    = new GroqMessage[] { new GroqMessage { role = "user", content = prompt } },
            max_tokens  = 1024,
            temperature = 0.7f,
            stream      = false
        };

        string bodyJson = JsonUtility.ToJson(body);
        byte[] bytes    = Encoding.UTF8.GetBytes(bodyJson);

        using var req = new UnityWebRequest(groqUrl, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        req.timeout = 60;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"HTTP {req.responseCode}: {req.error}");
            yield break;
        }

        try
        {
            // Extraer el campo "content" del primer choice via regex
            // (más robusto que JsonUtility para JSON anidado con arrays)
            string responseText = req.downloadHandler.text;
            var match = Regex.Match(responseText, @"""content""\s*:\s*""((?:[^""\\]|\\.)*)""");

            if (!match.Success)
            {
                onError?.Invoke($"No se encontró 'content' en: {responseText.Substring(0, Mathf.Min(300, responseText.Length))}");
                yield break;
            }

            // Desescapar el contenido (el modelo devuelve JSON como string escapado)
            string content = match.Groups[1].Value;
            content = content.Replace("\\n", "\n")
                             .Replace("\\r", "")
                             .Replace("\\t", "\t")
                             .Replace("\\\"", "\"")
                             .Replace("\\\\", "\\");

            onSuccess?.Invoke(content);
        }
        catch (Exception e)
        {
            onError?.Invoke($"Response parse error: {e.Message}");
        }
    }

    // ─── Extractor robusto por regex ──────────────────────────────

    private static PreguntaData? ExtraerPregunta(string raw)
    {
        // 1) Decodificar escapes \uXXXX
        raw = Regex.Replace(raw, @"\\u([0-9a-fA-F]{4})",
            m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

        // 2) Limpiar saltos de línea y comillas escapadas
        raw = raw.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        raw = raw.Replace("\\\"", "'").Replace("\\\\", "\\");

        // 3) Extraer "pregunta":"..."
        var mP = Regex.Match(raw, @"""pregunta""\s*:\s*""([^""]*)""");
        if (!mP.Success) return null;
        string pregunta = mP.Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(pregunta)) return null;

        // 4) Extraer "respuestas":[...] (con fallback para JSON truncado)
        var mR = Regex.Match(raw, @"""respuestas""\s*:\s*\[([^\]]*)\]");
        if (!mR.Success)
            mR = Regex.Match(raw, @"""respuestas""\s*:\s*\[([^\]]*)");
        if (!mR.Success) return null;

        // 5) Extraer cada string del array
        var respuestas = new List<string>();
        foreach (Match m in Regex.Matches(mR.Groups[1].Value, @"""([^""]*)"""))
            respuestas.Add(m.Groups[1].Value.Trim());

        if (respuestas.Count < 3) return null;
        if (respuestas.Count > 8) respuestas = respuestas.GetRange(0, 8);

        // 6) Extraer "puntos":[...]
        var mPuntos = Regex.Match(raw, @"""puntos""\s*:\s*\[([0-9,\s]+)\]");
        int[] puntos = GenerarPuntosDefault(respuestas.Count);
        if (mPuntos.Success)
        {
            var pList = new List<int>();
            foreach (var token in mPuntos.Groups[1].Value.Split(','))
                if (int.TryParse(token.Trim(), out int v)) pList.Add(v);
            if (pList.Count >= respuestas.Count)
                puntos = pList.GetRange(0, respuestas.Count).ToArray();
        }

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

    // Distribución decreciente de 100 puntos según cantidad de respuestas
    private static int[] GenerarPuntosDefault(int count)
    {
        float[] pesos = { 0.35f, 0.25f, 0.16f, 0.11f, 0.07f, 0.04f, 0.02f, 0.00f };
        int[] puntos = new int[count];
        int total = 0;
        for (int i = 0; i < count - 1; i++)
        {
            puntos[i] = Mathf.Max(1, Mathf.RoundToInt(100f * pesos[i]));
            total += puntos[i];
        }
        puntos[count - 1] = Mathf.Max(1, 100 - total);
        return puntos;
    }

    // ─── Sinónimos automáticos ─────────────────────────────────────

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
        // (Polimorfismo y Encapsulamiento están más abajo con sus typos)
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
        // ── IDEs ──
        {"VS Code",             "vscode|visual studio code|code editor"},
        {"IntelliJ",            "intellij idea|idea ide"},
        {"PyCharm",             "py charm|pycharm ide"},
        {"Eclipse",             "eclipse ide"},
        {"NetBeans",            "net beans"},
        {"Visual Studio",       "vs ide|microsoft visual studio"},
        {"Sublime",             "sublime text"},
        {"Atom",                "atom editor"},
        // ── Frameworks web/mobile ──
        {"React",               "reactjs|react js|react native"},
        {"Angular",             "angularjs|angular js"},
        {"Vue",                 "vuejs|vue js"},
        {"Django",              "django python"},
        {"Laravel",             "laravel php"},
        {"Spring",              "spring boot|java spring"},
        {"Node",                "nodejs|node js"},
        {"Flutter",             "flutter dart"},
        {"Express",             "expressjs|express js"},
        {"Next",                "nextjs|next js"},
        // ── IA/LLM ──
        {"ChatGPT",             "chat gpt|gpt|gpt4|gpt 4"},
        {"Claude",              "anthropic|anthropic claude"},
        {"Gemini",              "google gemini|gemini google|bard"},
        {"Grok",                "grok xai|grok elon|grok x"},
        {"DeepSeek",            "deep seek|deepsek|deepsick"},
        {"Llama",               "llama meta|meta llama"},
        {"Copilot",             "github copilot|gh copilot"},
        {"Redes Neuronales",    "red neuronal|neural network|nn"},
        {"Machine Learning",    "ml|aprendizaje automatico|aprendizaje maquina"},
        {"Deep Learning",       "dl|aprendizaje profundo"},
        // ── Snacks / Bebidas ──
        {"Ramen",               "fideos|fideos instantaneos|maruchan|sopa instantanea"},
        {"Pizza",               "pizzeria|pizza slice"},
        {"Cafe",                "cafecito|coffee|espresso|cafe negro"},
        {"Bebida",              "coca|coca cola|gaseosa|refresco|coke"},
        {"Energetica",          "red bull|monster|bebida energetica|energizante|energy drink"},
        {"Doritos",             "papas|chips|papas fritas|snacks"},
        {"Completo",            "hot dog|perro caliente"},
        {"Empanada",            "empanadas"},
        // ── Apps / Distracciones ──
        {"YouTube",             "you tube|yt|youtub"},
        {"Discord",             "disc|discord chat"},
        {"Reddit",              "reddit forum|reddit foro"},
        {"TikTok",              "tik tok|tic toc"},
        {"Twitch",              "twitch tv|streaming twitch"},
        {"Instagram",           "insta|ig"},
        {"WhatsApp",            "wsp|whats|whatsap|whatsap"},
        {"Stack Overflow",      "stackoverflow|stack|stackoverflow forum"},
        {"GitHub",              "git hub|github"},
        {"Spotify",             "spotify musica"},
        {"Netflix",             "netflix series"},
        // ── Conceptos OOP / abstractos comunes (typos frecuentes) ──
        {"Polimorfismo",        "polimorfism|polimorfisno|polimorfiso|polymorphism"},
        {"Encapsulamiento",     "encapsulacion|encapsulado|encapsulament"},
        {"Abstraccion",         "abstract|abstracion"},
        // ── UX/UI (para Experiencia del Usuario) ──
        {"Usabilidad",          "usability|facil de usar"},
        {"Wireframe",           "wireframes|esqueleto|maqueta"},
        {"Prototipo",           "prototype|prototipado"},
        {"Figma",               "figma diseno"},
        {"Adobe XD",            "xd|adobe experience"},
        // ── Gestión de proyectos ──
        {"Scrum",               "scrum agile|metodologia scrum"},
        {"Agile",               "agil|metodologia agil"},
        {"Kanban",              "kanban board|tablero kanban"},
        {"Jira",                "jira atlassian"},
        {"Trello",              "trello board"},
        {"Sprint",              "sprint scrum|ciclo sprint"},
        // ── Videojuegos populares ──
        {"League of Legends",   "lol|league|legends"},
        {"Minecraft",           "mine craft|minecraf"},
        {"Counter Strike",      "counter strike|cs|csgo|cs go|cs2"},
        {"Valorant",            "valo|valorant riot"},
        {"Fortnite",            "fortnaite|fortnight"},
        {"DotA",                "dota 2|dota2"},
    };

    private static string[] GenerarSinonimos(string[] respuestas)
    {
        var resultado = new string[respuestas.Length];
        for (int i = 0; i < respuestas.Length; i++)
        {
            string resp = respuestas[i];
            _sinoDict.TryGetValue(resp, out string syns);
            syns = syns ?? "";

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

    // ─── Clases de serialización HTTP (Groq / OpenAI) ─────────────

    [Serializable] private class GroqMessage { public string role; public string content; }
    [Serializable] private class GroqRequest
    {
        public string       model;
        public GroqMessage[] messages;
        public int          max_tokens;
        public float        temperature;
        public bool         stream;
    }
}
