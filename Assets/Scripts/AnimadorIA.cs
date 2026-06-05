using System;
using UnityEngine;
using Fusion;

public class AnimadorIA : MonoBehaviour
{
    public static AnimadorIA Instance { get; private set; }

    public static event Action<string> OnMensajeChanged;
    public static event Action<bool>   OnGenerandoPreguntas;

    // True SOLO durante la invocación de NotifyMensaje(_pendingTurnMessage) en Update().
    // AnimadorController lo lee para saber si debe arrancar el timer cuando termine el TTS.
    public static bool PendingTimerStart { get; private set; } = false;

    private bool      _generandoComentario    = false;
    private bool      _teamAnnouncedThisRound = false;
    private string    _pendingTurnMessage     = null;

    // Polling: último ActivePlayer que ya procesamos — evita disparar dos veces el mismo turno.
    private PlayerRef _lastKnownActivePlayer  = PlayerRef.None;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Update() corre DESPUÉS de todos los Render() de Fusion en el mismo frame,
    // por eso tanto el polling como el dispatch del mensaje van aquí.
    private void Update()
    {
        // ── Polling de ActivePlayer ───────────────────────────────────
        // El evento TurnManager.OnTurnChangedEvent a veces no llega a este MonoBehaviour
        // (comportamiento conocido de Fusion en ciertas versiones). El polling es el fallback
        // definitivo: lee el valor networked directamente, igual que UIGameController.
        if (TurnManager.Instance != null &&
            GameStateManager.Instance != null &&
            GameStateManager.Instance.IsGameStarted)
        {
            PlayerRef current = TurnManager.Instance.ActivePlayer;
            if (current != _lastKnownActivePlayer)
            {
                _lastKnownActivePlayer = current;
                if (current != PlayerRef.None)
                    BuildPendingTurnMessage(current);
            }
        }

        // ── Dispatch del mensaje de turno ─────────────────────────────
        if (_pendingTurnMessage != null)
        {
            var gsm = GameStateManager.Instance;
            if (gsm == null ||
                (gsm.CurrentState != GameStateManager.GameState.Playing &&
                 gsm.CurrentState != GameStateManager.GameState.Stealing))
            {
                _pendingTurnMessage = null;
                return;
            }

            if (AnimadorController.Instance != null && AnimadorController.Instance.MensajeEnCurso)
                return;

            PendingTimerStart = true;
            NotifyMensaje(_pendingTurnMessage);
            PendingTimerStart = false;
            _pendingTurnMessage = null;
        }
    }

    private void OnEnable()
    {
        GameStateManager.OnStateChangedEvent            += HandleStateChanged;
        GameStateManager.OnStateChangedEvent            += ResetRoundFlags;
        GameStateManager.OnAnswerResultEvent            += HandleAnswerResult;
        GameStateManager.OnEvaluationStateChangedEvent  += HandleEvaluationChanged;
        // Suscripción al evento como mecanismo secundario (el polling es el primario).
        TurnManager.OnTurnChangedEvent                  += HandleTurnChangedEvent;
    }

    private void OnDisable()
    {
        GameStateManager.OnStateChangedEvent            -= HandleStateChanged;
        GameStateManager.OnStateChangedEvent            -= ResetRoundFlags;
        GameStateManager.OnAnswerResultEvent            -= HandleAnswerResult;
        GameStateManager.OnEvaluationStateChangedEvent  -= HandleEvaluationChanged;
        TurnManager.OnTurnChangedEvent                  -= HandleTurnChangedEvent;
    }

    // ─── API pública ─────────────────────────────────────────────────

    public static void NotifyMensaje(string mensaje)    => OnMensajeChanged?.Invoke(mensaje);
    public static void NotifyGenerating(bool generando) => OnGenerandoPreguntas?.Invoke(generando);

    // ─── Reset por ronda ─────────────────────────────────────────────

    private void ResetRoundFlags(GameStateManager.GameState state)
    {
        if (state == GameStateManager.GameState.Countdown)
        {
            _teamAnnouncedThisRound = false;
            _lastKnownActivePlayer  = PlayerRef.None; // permite re-anunciar el primer turno de cada ronda
        }
    }

    // ─── Evento secundario de TurnManager ────────────────────────────
    // Si el evento llega, actualiza el tracker para que el polling no duplique el disparo.

    private void HandleTurnChangedEvent(PlayerRef player)
    {
        if (player == PlayerRef.None) return;
        if (GameStateManager.Instance == null || !GameStateManager.Instance.IsGameStarted) return;

        // Sincronizar el tracker: si el evento llega primero, el polling lo saltará.
        if (player == _lastKnownActivePlayer) return; // ya procesado por el polling
        _lastKnownActivePlayer = player;
        BuildPendingTurnMessage(player);
    }

    // ─── Lógica de mensaje de turno ──────────────────────────────────

    private void BuildPendingTurnMessage(PlayerRef player)
    {
        if (GameStateManager.Instance == null) return;

        var data = GameStateManager.Instance.Runner
            ?.GetPlayerObject(player)?.GetComponent<PlayerNetworkData>();
        if (data == null) return;

        string playerName = data.PlayerName.ToString();
        string teamName   = data.TeamIndex == 1
            ? GameStateManager.Instance.NombreEquipoA.ToString()
            : GameStateManager.Instance.NombreEquipoB.ToString();

        if (!_teamAnnouncedThisRound)
        {
            _teamAnnouncedThisRound = true;
            string pregunta = GameStateManager.Instance.PreguntaActual ?? "";
            string preguntaLinea = !string.IsNullOrEmpty(pregunta) ? $"\n{pregunta}" : "";
            _pendingTurnMessage = $"¡{teamName} tiene la palabra!\nTurno de {playerName}!{preguntaLinea}";
        }
        else
        {
            string[] frases = {
                $"Turno de {playerName}!\n¿Cuál es tu respuesta?",
                $"{playerName}, es tu momento!\n¡Dame una respuesta!",
                $"¡Vamos {playerName}!\n¿Qué dice {teamName}?"
            };
            int idx = (int)((uint)player.PlayerId % (uint)frases.Length);
            _pendingTurnMessage = frases[idx];
        }

        Debug.Log($"[AnimadorIA] Turno detectado → '{_pendingTurnMessage.Substring(0, Mathf.Min(60, _pendingTurnMessage.Length))}'");
    }

    // ─── Estado del juego (solo host, vía LLM) ───────────────────────

    private void HandleStateChanged(GameStateManager.GameState estado)
    {
        if (GameStateManager.Instance == null || !GameStateManager.Instance.Object.HasStateAuthority) return;
        if (!GameStateManager.Instance.IsGameStarted) return;

        var gsm = GameStateManager.Instance;

        switch (estado)
        {
            case GameStateManager.GameState.Intro:
                GenerarBienvenida();
                break;

            case GameStateManager.GameState.Countdown:
                GenerarYMostrar(
                    $"ronda {gsm.CurrentRound} de 5, " +
                    $"lean bien la pregunta que ya esta arriba, preparense");
                break;

            case GameStateManager.GameState.WaitingForBuzzer:
                GenerarYMostrar("quien sera el primero en tocar el buzzer presionando espacio");
                break;

            case GameStateManager.GameState.Stealing:
            {
                string robando = gsm.ActiveTeam.ToString() == "A"
                    ? gsm.NombreEquipoA.ToString() : gsm.NombreEquipoB.ToString();
                GenerarYMostrar(
                    $"el equipo {robando} tiene una sola oportunidad para robar " +
                    $"todos los puntos de la ronda");
                break;
            }

            case GameStateManager.GameState.RoundEnd:
                GenerarYMostrar(
                    $"fin de ronda {gsm.CurrentRound}, " +
                    $"{gsm.NombreEquipoA}: {gsm.ScoreA} puntos, " +
                    $"{gsm.NombreEquipoB}: {gsm.ScoreB} puntos");
                break;

            case GameStateManager.GameState.GameOver:
            {
                string ganador = gsm.ScoreA >= gsm.ScoreB
                    ? gsm.NombreEquipoA.ToString() : gsm.NombreEquipoB.ToString();
                int pts = Mathf.Max(gsm.ScoreA, gsm.ScoreB);
                GenerarYMostrar(
                    $"fin del juego, {ganador} gano con {pts} puntos totales, " +
                    $"felicitar al equipo ganador");
                break;
            }
        }
    }

    // ─── Suspense durante la evaluación (solo host) ──────────────────

    private void HandleEvaluationChanged(bool isEvaluating)
    {
        if (!isEvaluating) return;
        if (GameStateManager.Instance == null || !GameStateManager.Instance.Object.HasStateAuthority) return;
        if (!GameStateManager.Instance.IsGameStarted) return;

        var gsm = GameStateManager.Instance;
        string nombre = gsm.Runner
            ?.GetPlayerObject(PlayerRef.FromIndex(gsm.PendingPlayerId))
            ?.GetComponent<PlayerNetworkData>()?.PlayerName.ToString() ?? "Jugador";
        string respuesta = gsm.PendingAnswerText.ToString();

        GenerarFraseSuspense(nombre, respuesta);
    }

    // ─── Resultado de respuesta (solo host) ──────────────────────────

    private void HandleAnswerResult(bool correct, int points, string playerName, string teamName)
    {
        if (GameStateManager.Instance == null || !GameStateManager.Instance.IsGameStarted) return;
        if (!GameStateManager.Instance.Object.HasStateAuthority) return;

        _generandoComentario = false;

        string contexto = correct
            ? $"respuesta correcta de {playerName}, grita CORRECTO con mucho entusiasmo, " +
              $"suma {points} puntos para {teamName}"
            : $"respuesta incorrecta de {playerName} del equipo {teamName}, " +
              $"reacciona con decepcion con un Ohhhh, sale la X roja en el tablero";

        GenerarYMostrar(contexto);
    }

    // ─── LLM helpers ─────────────────────────────────────────────────

    private void GenerarBienvenida()
    {
        if (_generandoComentario) return;
        _generandoComentario = true;

        var gsm = GameStateManager.Instance;
        string equipoA = gsm != null ? gsm.NombreEquipoA.ToString() : "A";
        string equipoB = gsm != null ? gsm.NombreEquipoB.ToString() : "B";
        string liderA  = gsm != null ? gsm.GetLiderNombre(1) : "Jugador";
        string liderB  = gsm != null ? gsm.GetLiderNombre(2) : "Jugador";

        if (OllamaService.Instance == null) { _generandoComentario = false; return; }

        string contexto = $"bienvenida al programa, equipo {equipoA} con lider {liderA} " +
                          $"vs equipo {equipoB} con lider {liderB}, invita a los lideres al podio";

        OllamaService.Instance.GenerarBienvenida(contexto, mensaje =>
        {
            if (GameStateManager.Instance != null && !string.IsNullOrEmpty(mensaje))
                GameStateManager.Instance.ActualizarMensajeAnimador(mensaje);
            _generandoComentario = false;
        });
    }

    private void GenerarFraseSuspense(string nombre, string respuesta)
    {
        if (_generandoComentario || OllamaService.Instance == null) return;
        _generandoComentario = true;

        OllamaService.Instance.GenerarFraseSuspense(nombre, respuesta, mensaje =>
        {
            if (GameStateManager.Instance != null && !string.IsNullOrEmpty(mensaje))
                GameStateManager.Instance.ActualizarMensajeAnimador(mensaje);
            _generandoComentario = false;
        });
    }

    private void GenerarYMostrar(string contexto)
    {
        if (_generandoComentario || OllamaService.Instance == null) return;
        _generandoComentario = true;

        OllamaService.Instance.GenerarComentario(contexto, mensaje =>
        {
            if (GameStateManager.Instance != null && !string.IsNullOrEmpty(mensaje))
                GameStateManager.Instance.ActualizarMensajeAnimador(mensaje);
            _generandoComentario = false;
        });
    }
}
