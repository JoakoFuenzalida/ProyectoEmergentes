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
    [SerializeField] private Animator animatorTarget;

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
        _animator = animatorTarget;
        if (_animator == null)
        {
            var animators = GetComponentsInChildren<Animator>(true);
            if (animators.Length > 0)
            {
                _animator = animators[0];
                foreach (var a in animators)
                {
                    if (a != null && a.gameObject != gameObject && a.isActiveAndEnabled)
                    {
                        _animator = a;
                        break;
                    }
                }
            }
        }
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
                // Liberar la bandera para que OnMensajeIA pueda mostrar viñetas en juego
                _secuenciaActiva = false;
                _enPodio = false;
                TeleportarA(posEstudio);
                Ocultar(viñetaPodio);
                break;

            case GameStateManager.GameState.GameOver:
                // Cancelar cualquier viñeta activa y arrancar la secuencia de cierre
                _secuenciaActiva = false;
                DetenerViñeta();
                _enPodio = false;
                TeleportarA(posEstudio);
                IniciarSecuenciaGameOver();
                break;

            // ── Podio ────────────────────────────────────────
            case GameStateManager.GameState.WaitingForBuzzer:
                // Detener countdown si aún corre: evita que su OcultarTodas() final
                // tape las próximas viñetas y que deje _secuenciaActiva = true
                _secuenciaActiva = false;
                DetenerViñeta();
                _enPodio = true;
                TeleportarA(posPodio);
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

        // ── Textos de cada página ────────────────────────────────────────
        string[] display = {
            "¡Hola a todos!\nSoy Martín Cárcamo,\nsu presentador de hoy.",
            "¡Bienvenidos a\n¿Qué Dice Chile?\nVersión Informática!",
            $"Hoy se enfrentan:\n{eqA}\nvs {eqB}",
            "Haré preguntas como:\n'¿Qué dirían 100 informáticos\nde la PUCV sobre...?'",
            "¡Deben adivinar las\nrespuestas más populares\nde nuestra encuesta!",
            "El juego es por turnos.\nCada equipo responde\ncuando le corresponde.",
            "En el podio, el primero\nen presionar ESPACIO\nresponde la pregunta.",
            $"{lA} y {lB},\n¡al podio!\n¡Que comience el juego!"
        };
        float[] durs = { 4f, 4f, 3.8f, 4.5f, 3.8f, 3.8f, 4f, 4f };
        int total = display.Length;

        // ── Pre-fetch todos los clips en paralelo ────────────────────────
        // Todos los audios se piden a la vez para que cuando llegue la página
        // ya esté listo, eliminando el gap entre páginas.
        var clips  = new AudioClip[total];
        var listos = new bool[total];

        if (TTSService.Instance != null)
        {
            for (int i = 0; i < total; i++)
            {
                int   idx = i;
                string voz = display[idx].Replace("\n", " ").Trim();
                TTSService.Instance.GenerarAudio(voz, c => { clips[idx] = c; listos[idx] = true; });
            }
        }
        else { for (int i = 0; i < total; i++) listos[i] = true; }

        // ── Reproducir cada página al llegar su clip ─────────────────────
        for (int i = 0; i < total; i++)
        {
            if (!_secuenciaActiva) break; // abortado por cambio de estado

            // Esperar que este clip esté listo (máx 10 s)
            for (float t = 0f; !listos[i] && t < 10f; t += Time.deltaTime) yield return null;

            // Mostrar viñeta
            Activar(viñetaEquipoA, textoEquipoA, display[i]);
            Activar(viñetaEquipoB, textoEquipoB, display[i]);
            Ocultar(viñetaPodio);
            UIGameController.Instance?.ActualizarTextoAnimador(display[i]);
            SetHablando(true);

            if (clips[i] != null)
            {
                if (audioSource.clip != null && audioSource.clip.name == "tts_clip")
                { var v = audioSource.clip; audioSource.clip = null; Destroy(v); }
                audioSource.clip = clips[i];
                audioSource.Play();
                yield return new WaitForSeconds(clips[i].length + 0.4f);
                audioSource.Stop();
                Destroy(clips[i]);
                clips[i] = null;
            }
            else
            {
                yield return new WaitForSeconds(durs[i]);
            }

            OcultarTodas();
            SetHablando(false);
            yield return new WaitForSeconds(pausaEntrePaginas);
        }

        OcultarTodas();
        SetHablando(false);
        _secuenciaActiva = false;

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.Object != null &&
            GameStateManager.Instance.Object.HasStateAuthority)
            GameStateManager.Instance.TerminarIntro();
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

    // ─── Secuencia de cierre (Game Over) ─────────────────────────────

    private void IniciarSecuenciaGameOver()
    {
        if (_viñetaCoroutine != null) StopCoroutine(_viñetaCoroutine);
        _viñetaCoroutine = StartCoroutine(CoroutineGameOver());
    }

    private IEnumerator CoroutineGameOver()
    {
        _secuenciaActiva = true;

        var gsm = GameStateManager.Instance;
        if (gsm == null) { _secuenciaActiva = false; yield break; }

        string eqA    = gsm.NombreEquipoA.ToString();
        string eqB    = gsm.NombreEquipoB.ToString();
        int    pA     = gsm.ScoreA;
        int    pB     = gsm.ScoreB;
        int    rondas = gsm.CurrentRound;
        bool   empate = (pA == pB);

        string txtGanador;
        if      (empate)   txtGanador = $"Resultado increible!\nEmpate con {pA} puntos\npara ambos equipos!";
        else if (pA > pB)  txtGanador = $"El equipo {eqA}\nes el GANADOR\ncon {pA} puntos!";
        else               txtGanador = $"El equipo {eqB}\nes el GANADOR\ncon {pB} puntos!";

        string[] display = {
            $"Damos por terminadas\nlas {rondas} rondas!\nFue un gran juego!",
            $"El marcador final:\n{eqA}: {pA} puntos\n{eqB}: {pB} puntos",
            txtGanador
        };
        float[] durs = { 4f, 4.5f, 5f };
        int total = display.Length;

        // ── Pre-fetch TTS en paralelo ────────────────────────────────
        var clips  = new AudioClip[total];
        var listos = new bool[total];

        if (TTSService.Instance != null)
        {
            for (int i = 0; i < total; i++)
            {
                int    idx = i;
                string voz = display[idx].Replace("\n", " ").Trim();
                TTSService.Instance.GenerarAudio(voz, c => { clips[idx] = c; listos[idx] = true; });
            }
        }
        else { for (int i = 0; i < total; i++) listos[i] = true; }

        // ── Reproducir cada página al llegar su clip ─────────────────
        for (int i = 0; i < total; i++)
        {
            if (!_secuenciaActiva) break;

            for (float t = 0f; !listos[i] && t < 10f; t += Time.deltaTime) yield return null;

            Activar(viñetaEquipoA, textoEquipoA, display[i]);
            Activar(viñetaEquipoB, textoEquipoB, display[i]);
            Ocultar(viñetaPodio);
            UIGameController.Instance?.ActualizarTextoAnimador(display[i]);
            SetHablando(true);

            if (clips[i] != null)
            {
                if (audioSource.clip != null && audioSource.clip.name == "tts_clip")
                { var v = audioSource.clip; audioSource.clip = null; Destroy(v); }
                audioSource.clip = clips[i];
                audioSource.Play();
                yield return new WaitForSeconds(clips[i].length + 0.4f);
                audioSource.Stop();
                Destroy(clips[i]);
                clips[i] = null;
            }
            else
            {
                yield return new WaitForSeconds(durs[i]);
            }

            OcultarTodas();
            SetHablando(false);
            yield return new WaitForSeconds(pausaEntrePaginas);
        }

        OcultarTodas();
        SetHablando(false);
        _secuenciaActiva = false;

        // Mostrar el panel de ganador en el HUD una vez termina el discurso
        UIGameController.Instance?.MostrarPanelGanador();
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

        // Si el juego está en fase de suspense (IsEvaluating) Y somos el host,
        // liberar la evaluación cuando el TTS termine — así el reveal ocurre justo
        // después del "DAMELA!" y no a los 1.5 s del timer fijo.
        var gsm = GameStateManager.Instance;
        bool esHostEvaluando = gsm != null &&
                               gsm.Object != null &&
                               gsm.Object.HasStateAuthority &&
                               gsm.IsEvaluating;

        ReproducirTTS(mensaje, esHostEvaluando ? LiberarEvaluacion : (System.Action)null);
    }

    private void LiberarEvaluacion()
    {
        var gsm = GameStateManager.Instance;
        if (gsm == null || gsm.Object == null || !gsm.Object.HasStateAuthority) return;
        gsm.ReleaseEvaluation();
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

    /// <summary>
    /// Muestra una página de la viñeta sincronizada con el audio TTS.
    /// Estrategia: primero pide el audio en silencio, y cuando llega
    /// muestra el texto y arranca el audio al mismo tiempo → sin desfase.
    /// Si TTS no está disponible o hay timeout, usa duracionFallback.
    /// </summary>
    private IEnumerator Pagina(string texto, float duracionFallback)
    {
        // ── 1. Solicitar audio ANTES de mostrar nada ─────────────────
        int       myId   = ++_ttsRequestId;
        AudioClip clip   = null;
        bool      llegó  = false;

        if (TTSService.Instance != null)
        {
            string textoVoz = texto.Replace("\n", " ").Trim();
            TTSService.Instance.GenerarAudio(textoVoz, c =>
            {
                if (myId != _ttsRequestId) return; // cancelado por página más nueva
                clip  = c;
                llegó = true;
            });

            // Esperar hasta 8 s máximo (red lenta / texto largo)
            float t = 0f;
            while (!llegó && t < 8f)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        // ── 2. Mostrar viñeta + arrancar audio al mismo instante ──────
        Activar(viñetaEquipoA, textoEquipoA, texto);
        Activar(viñetaEquipoB, textoEquipoB, texto);
        Ocultar(viñetaPodio);
        UIGameController.Instance?.ActualizarTextoAnimador(texto);
        SetHablando(true);

        if (clip != null && myId == _ttsRequestId)
        {
            if (audioSource.clip != null && audioSource.clip.name == "tts_clip")
            {
                AudioClip viejo = audioSource.clip;
                audioSource.clip = null;
                Destroy(viejo);
            }
            audioSource.clip = clip;
            audioSource.Play();

            // Viñeta dura exactamente lo que habla Martín
            yield return new WaitForSeconds(clip.length + 0.4f);
        }
        else
        {
            // Sin TTS: tiempo fijo de fallback
            yield return new WaitForSeconds(duracionFallback);
        }

        // ── 3. Ocultar y pausa antes de la siguiente página ──────────
        OcultarTodas();
        SetHablando(false);
        yield return new WaitForSeconds(pausaEntrePaginas);
    }

    // ─── TTS ─────────────────────────────────────────────────────────

    /// <summary>
    /// Solicita audio TTS para el texto dado.
    /// Cuando llega, lo reproduce si no fue cancelado por un mensaje más nuevo.
    /// El texto se limpia de saltos de línea antes de enviarlo a la API.
    /// </summary>
    /// <param name="onFinished">
    /// Opcional. Se invoca cuando el audio termina de sonar (o cuando falla).
    /// Usado por el suspense para liberar la evaluación justo al acabar el "DAMELA!".
    /// </param>
    private void ReproducirTTS(string texto, System.Action onFinished = null)
    {
        if (TTSService.Instance == null)
        {
            // Sin TTS: esperar tiempo mínimo de suspense antes de liberar
            if (onFinished != null) StartCoroutine(DelayCallback(3.0f, onFinished));
            return;
        }

        // Cortar audio anterior inmediatamente al llegar mensaje nuevo
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();

        // Incrementar ID cancela cualquier petición anterior en vuelo
        int myId = ++_ttsRequestId;

        string textoVoz = texto.Replace("\n", " ").Trim();

        TTSService.Instance.GenerarAudio(textoVoz, clip =>
        {
            // Si ya llegó un mensaje más nuevo, descartamos este audio
            if (myId != _ttsRequestId) return;

            if (clip == null)
            {
                // TTS falló: esperar tiempo mínimo antes de liberar
                if (onFinished != null) StartCoroutine(DelayCallback(3.0f, onFinished));
                return;
            }

            if (audioSource != null)
            {
                if (audioSource.clip != null && audioSource.clip.name == "tts_clip")
                {
                    AudioClip viejo = audioSource.clip;
                    audioSource.clip = null;
                    Destroy(viejo);
                }
                audioSource.clip = clip;
                audioSource.Play();

                // Cuando el audio termina → disparar callback (ej: liberar evaluación)
                if (onFinished != null)
                    StartCoroutine(EsperarFinAudio(clip.length, myId, onFinished));
            }
            else { onFinished?.Invoke(); }
        });
    }

    private IEnumerator EsperarFinAudio(float duracion, int requestId, System.Action onFinished)
    {
        yield return new WaitForSeconds(duracion + 0.4f); // pequeño buffer tras el audio
        // Solo disparar si no llegó otro mensaje que canceló esta petición
        if (requestId == _ttsRequestId)
            onFinished?.Invoke();
    }

    private IEnumerator DelayCallback(float delay, System.Action callback)
    {
        yield return new WaitForSeconds(delay);
        callback?.Invoke();
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
