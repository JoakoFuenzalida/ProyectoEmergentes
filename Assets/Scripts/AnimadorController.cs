using System.Collections;
using UnityEngine;
using TMPro;
using Fusion;

public class AnimadorController : MonoBehaviour
{
    public static AnimadorController Instance { get; private set; }

    [Header("Posiciones de teleport")]
    [SerializeField] private Transform posPodio;
    [SerializeField] private Transform posEstudio;

    [Header("Viñeta — Podio (una sola, mirando al frente)")]
    [SerializeField] private GameObject viñetaPodio;
    [SerializeField] private TMP_Text   textoPodio;

    [Header("Viñeta — Estudio (una por equipo, rotaciones opuestas)")]
    [SerializeField] private GameObject viñetaEquipoA;
    [SerializeField] private TMP_Text   textoEquipoA;
    [SerializeField] private GameObject viñetaEquipoB;
    [SerializeField] private TMP_Text   textoEquipoB;

    [Header("Tiempos")]
    [SerializeField] private float duracionViñeta    = 9f;  // mensajes normales de juego
    [SerializeField] private float duracionPagina    = 3.8f; // tiempo por página en intro
    [SerializeField] private float pausaEntrePaginas = 0.4f; // pausa breve entre páginas

    [Header("Audio TTS")]
    [SerializeField] private AudioSource audioSource;

    private Animator  _animator;
    private Coroutine _viñetaCoroutine;
    private bool      _enPodio         = false;
    private bool      _secuenciaActiva = false; // bloquea mensajes externos durante intro/countdown

    // Versión de petición TTS: si llega audio de una petición vieja, se descarta
    private int _ttsRequestId = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance  = this;
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        OcultarTodas();
        TeleportarA(posEstudio);

        // Configurar AudioSource si no fue asignado en el Inspector
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnEnable()
    {
        GameStateManager.OnStateChangedEvent += HandleEstadoJuego;
        AnimadorIA.OnMensajeChanged           += OnMensajeIA;
    }

    private void OnDisable()
    {
        GameStateManager.OnStateChangedEvent -= HandleEstadoJuego;
        AnimadorIA.OnMensajeChanged           -= OnMensajeIA;
    }

    // ─── Teleport y reacciones según estado ──────────────────────────

    private void HandleEstadoJuego(GameStateManager.GameState estado)
    {
        switch (estado)
        {
            // ── Mesas / Estudio ──────────────────────────────
            case GameStateManager.GameState.Intro:
                _enPodio = false;
                TeleportarA(posEstudio);
                Ocultar(viñetaPodio);
                IniciarSecuenciaIntro();
                break;

            case GameStateManager.GameState.Countdown:
                _secuenciaActiva = false; // cierra intro si estaba corriendo
                _enPodio = false;
                TeleportarA(posEstudio);
                DetenerViñeta();
                IniciarCountdown();
                break;

            case GameStateManager.GameState.Playing:
            case GameStateManager.GameState.Stealing:
            case GameStateManager.GameState.RoundEnd:
            case GameStateManager.GameState.GameOver:
                _enPodio = false;
                TeleportarA(posEstudio);
                Ocultar(viñetaPodio);
                break;

            // ── Podio ────────────────────────────────────────
            case GameStateManager.GameState.WaitingForBuzzer:
                _enPodio = true;
                TeleportarA(posPodio);
                Ocultar(viñetaEquipoA);
                Ocultar(viñetaEquipoB);
                break;

            case GameStateManager.GameState.TypingAnswer:
                _enPodio = true;
                TeleportarA(posPodio);
                Ocultar(viñetaEquipoA);
                Ocultar(viñetaEquipoB);
                MostrarBuzzerWinner();
                break;
        }
    }

    private void TeleportarA(Transform destino)
    {
        if (destino == null) return;
        transform.position = destino.position;
        transform.rotation = destino.rotation;
    }

    // ─── Secuencia paginada de intro ─────────────────────────────────

    private void IniciarSecuenciaIntro()
    {
        if (_viñetaCoroutine != null) StopCoroutine(_viñetaCoroutine);
        _viñetaCoroutine = StartCoroutine(SecuenciaIntro());
    }

    private IEnumerator SecuenciaIntro()
    {
        _secuenciaActiva = true;

        var gsm    = GameStateManager.Instance;
        string eqA = gsm != null ? gsm.NombreEquipoA.ToString() : "Equipo A";
        string eqB = gsm != null ? gsm.NombreEquipoB.ToString() : "Equipo B";
        string lA  = gsm != null ? gsm.GetLiderNombre(1) : "Lider A";
        string lB  = gsm != null ? gsm.GetLiderNombre(2) : "Lider B";

        yield return StartCoroutine(Pagina("¡Hola a todos!\nSoy Martín Cárcamo,\nsu presentador de hoy.", 4f));
        yield return StartCoroutine(Pagina("¡Bienvenidos a\n¿Qué Dice Chile?\nVersión Informática!", 4f));
        yield return StartCoroutine(Pagina($"Hoy se enfrentan:\n{eqA}\nvs {eqB}", 3.8f));
        yield return StartCoroutine(Pagina("Haré preguntas como:\n'¿Qué dirían 100 informáticos\nde la PUCV sobre...?'", 4.5f));
        yield return StartCoroutine(Pagina("¡Deben adivinar las\nrespuestas más populares\nde nuestra encuesta!", 3.8f));
        yield return StartCoroutine(Pagina("El juego es por turnos.\nCada equipo responde\ncuando le corresponde.", 3.8f));
        yield return StartCoroutine(Pagina("En el podio, el primero\nen presionar ESPACIO\nresponde la pregunta.", 4f));
        yield return StartCoroutine(Pagina($"{lA} y {lB},\n¡al podio!\n¡Que comience el juego!", 4f));

        OcultarTodas();
        SetHablando(false);
        _secuenciaActiva = false;
    }

    // ─── Anuncio de ronda en Countdown ───────────────────────────────

    private void IniciarCountdown()
    {
        if (_viñetaCoroutine != null) StopCoroutine(_viñetaCoroutine);
        _viñetaCoroutine = StartCoroutine(CoroutineCountdown());
    }

    private IEnumerator CoroutineCountdown()
    {
        _secuenciaActiva = true;

        var gsm  = GameStateManager.Instance;
        int ronda = gsm != null ? gsm.CurrentRound : 1;

        yield return StartCoroutine(Pagina(
            $"¡Ronda {ronda}!\nLean la pregunta arriba.\n¡Prepárense para el buzzer!", 4f));

        OcultarTodas();
        SetHablando(false);
        _secuenciaActiva = false;
    }

    // ─── Buzzer winner ────────────────────────────────────────────────

    private void MostrarBuzzerWinner()
    {
        var gsm = GameStateManager.Instance;
        if (gsm == null) return;

        var data = gsm.Runner
            ?.GetPlayerObject(PlayerRef.FromIndex(gsm.BuzzerWinnerId))
            ?.GetComponent<PlayerNetworkData>();

        string playerName = data?.PlayerName.ToString() ?? "Jugador";
        string msg = $"¡{playerName} tiene el buzzer!\n¡Escribe tu respuesta!";

        if (_viñetaCoroutine != null) StopCoroutine(_viñetaCoroutine);
        _viñetaCoroutine = StartCoroutine(CoroutineViñetaNormal(msg));
        UIGameController.Instance?.ActualizarTextoAnimador(msg);
        ReproducirTTS(msg);
    }

    // ─── Mensajes del juego llegados via AnimadorIA ───────────────────

    private void OnMensajeIA(string mensaje)
    {
        if (_secuenciaActiva) return; // no interrumpir intro ni countdown
        if (_viñetaCoroutine != null) StopCoroutine(_viñetaCoroutine);
        _viñetaCoroutine = StartCoroutine(CoroutineViñetaNormal(mensaje));
        UIGameController.Instance?.ActualizarTextoAnimador(mensaje);
        ReproducirTTS(mensaje);
    }

    private IEnumerator CoroutineViñetaNormal(string mensaje)
    {
        if (_enPodio)
        {
            Activar(viñetaPodio,   textoPodio,   mensaje);
            Ocultar(viñetaEquipoA);
            Ocultar(viñetaEquipoB);
        }
        else
        {
            Activar(viñetaEquipoA, textoEquipoA, mensaje);
            Activar(viñetaEquipoB, textoEquipoB, mensaje);
            Ocultar(viñetaPodio);
        }
        SetHablando(true);
        yield return new WaitForSeconds(duracionViñeta);
        OcultarTodas();
        SetHablando(false);
    }

    // ─── Página individual (intro y countdown) ───────────────────────

    private IEnumerator Pagina(string texto, float duracionFallback)
    {
        Activar(viñetaEquipoA, textoEquipoA, texto);
        Activar(viñetaEquipoB, textoEquipoB, texto);
        Ocultar(viñetaPodio);
        UIGameController.Instance?.ActualizarTextoAnimador(texto);
        SetHablando(true);

        // Espera a que el audio llegue y termine — la viñeta dura lo que habla Martín
        yield return StartCoroutine(ReproducirYEsperar(texto, duracionFallback));

        OcultarTodas();
        SetHablando(false);
        yield return new WaitForSeconds(pausaEntrePaginas);
    }

    // ─── TTS bloqueante (para páginas de intro/countdown) ────────────

    /// <summary>
    /// Solicita audio TTS, espera a que llegue y luego espera a que termine de reproducirse.
    /// Si TTS tarda más de (duracionFallback + 5s) o falla, usa el tiempo fijo como respaldo.
    /// </summary>
    private IEnumerator ReproducirYEsperar(string texto, float duracionFallback)
    {
        if (TTSService.Instance == null)
        {
            yield return new WaitForSeconds(duracionFallback);
            yield break;
        }

        int myId = ++_ttsRequestId;
        string textoVoz = texto.Replace("\n", " ").Trim();

        AudioClip clip   = null;
        bool      llegó  = false;

        TTSService.Instance.GenerarAudio(textoVoz, c =>
        {
            if (myId != _ttsRequestId) return; // cancelado por viñeta más nueva
            clip  = c;
            llegó = true;
        });

        // Esperar hasta que llegue el audio o se agote el timeout
        float timeout = duracionFallback + 5f;
        float t = 0f;
        while (!llegó && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (clip != null && myId == _ttsRequestId)
        {
            // Liberar clip anterior
            if (audioSource.clip != null && audioSource.clip.name == "tts_clip")
            {
                AudioClip viejo = audioSource.clip;
                audioSource.clip = null;
                Destroy(viejo);
            }
            audioSource.clip = clip;
            audioSource.Play();

            // Esperar exactamente lo que dura el audio + pausa breve al final
            yield return new WaitForSeconds(clip.length + 0.3f);
        }
        else
        {
            // Fallback: TTS no llegó a tiempo, duración fija
            Debug.LogWarning($"[TTS] Timeout esperando audio para: \"{textoVoz.Substring(0, Mathf.Min(40, textoVoz.Length))}...\"");
            yield return new WaitForSeconds(duracionFallback);
        }
    }

    // ─── TTS ─────────────────────────────────────────────────────────

    /// <summary>
    /// Solicita audio TTS para el texto dado.
    /// Cuando llega, lo reproduce si no fue cancelado por un mensaje más nuevo.
    /// El texto se limpia de saltos de línea antes de enviarlo a la API.
    /// </summary>
    private void ReproducirTTS(string texto)
    {
        if (TTSService.Instance == null) return;

        // Incrementar ID cancela cualquier petición anterior en vuelo
        int myId = ++_ttsRequestId;

        // Limpiar formato visual (\n) que no debe leerlo la voz
        string textoVoz = texto.Replace("\n", " ").Trim();

        TTSService.Instance.GenerarAudio(textoVoz, clip =>
        {
            // Si ya llegó un mensaje más nuevo, descartamos este audio
            if (clip == null || myId != _ttsRequestId) return;

            if (audioSource != null)
            {
                // Liberar el clip anterior para no acumular memoria
                if (audioSource.clip != null && audioSource.clip.name == "tts_clip")
                {
                    AudioClip viejo = audioSource.clip;
                    audioSource.clip = null;
                    Destroy(viejo);
                }

                audioSource.clip = clip;
                audioSource.Play();
            }
        });
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private void DetenerViñeta()
    {
        if (_viñetaCoroutine != null) { StopCoroutine(_viñetaCoroutine); _viñetaCoroutine = null; }
        OcultarTodas();
        SetHablando(false);
    }

    private void OcultarTodas()
    {
        Ocultar(viñetaPodio);
        Ocultar(viñetaEquipoA);
        Ocultar(viñetaEquipoB);
    }

    private void SetHablando(bool valor)
    {
        if (_animator != null) _animator.SetBool("Hablando", valor);
    }

    private void Activar(GameObject canvas, TMP_Text texto, string mensaje)
    {
        if (canvas == null || texto == null) return;
        texto.text = mensaje;
        canvas.SetActive(true);
    }

    private void Ocultar(GameObject canvas)
    {
        if (canvas != null) canvas.SetActive(false);
    }
}
