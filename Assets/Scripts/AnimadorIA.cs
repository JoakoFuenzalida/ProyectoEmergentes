using System;
using UnityEngine;

public class AnimadorIA : MonoBehaviour
{
    public static AnimadorIA Instance { get; private set; }

    public static event Action<string> OnMensajeChanged;
    public static event Action<bool>   OnGenerandoPreguntas;

    private bool _generandoComentario = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        GameStateManager.OnStateChangedEvent   += HandleStateChanged;
        GameStateManager.OnAnswerResultEvent   += HandleAnswerResult;
        TurnManager.OnTurnChangedEvent         += HandleTurnChanged;
    }

    private void OnDisable()
    {
        GameStateManager.OnStateChangedEvent   -= HandleStateChanged;
        GameStateManager.OnAnswerResultEvent   -= HandleAnswerResult;
        TurnManager.OnTurnChangedEvent         -= HandleTurnChanged;
    }

    // ─── API pública ─────────────────────────────────────────────────

    public static void NotifyMensaje(string mensaje)   => OnMensajeChanged?.Invoke(mensaje);
    public static void NotifyGenerating(bool generando) => OnGenerandoPreguntas?.Invoke(generando);

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
                    $"ronda {gsm.CurrentRound} de {5}, " +
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

    // ─── Resultado de respuesta (todos los clientes) ─────────────────

    // La frase inmediata ya la pone GameStateManager vía ActualizarMensajeAnimador.
    // Aquí generamos el comentario extendido del LLM (solo host).
    private void HandleAnswerResult(bool correct, int points, string playerName, string teamName)
    {
        if (GameStateManager.Instance == null || !GameStateManager.Instance.IsGameStarted) return;
        if (!GameStateManager.Instance.Object.HasStateAuthority) return;

        var gsm = GameStateManager.Instance;
        string contexto = correct
            ? $"respuesta correcta de {playerName}, gana {points} puntos para {teamName}, " +
              $"marcador {gsm.NombreEquipoA}: {gsm.ScoreA + (gsm.ActiveTeam.ToString() == "A" ? gsm.RoundScore : 0)}" +
              $" vs {gsm.NombreEquipoB}: {gsm.ScoreB + (gsm.ActiveTeam.ToString() == "B" ? gsm.RoundScore : 0)}"
            : $"respuesta incorrecta de {playerName} del equipo {teamName}, sale la X roja en el tablero";

        GenerarYMostrar(contexto);
    }

    // ─── Cambio de turno (todos los clientes, hardcoded sin LLM) ─────

    private void HandleTurnChanged(PlayerRef player)
    {
        if (GameStateManager.Instance == null || !GameStateManager.Instance.IsGameStarted) return;
        var state = GameStateManager.Instance.CurrentState;
        if (state != GameStateManager.GameState.Playing &&
            state != GameStateManager.GameState.Stealing) return;
        if (player == PlayerRef.None) return;

        var data = GameStateManager.Instance.Runner
            ?.GetPlayerObject(player)?.GetComponent<PlayerNetworkData>();
        if (data == null) return;

        string playerName = data.PlayerName.ToString();
        string teamName   = data.TeamIndex == 1
            ? GameStateManager.Instance.NombreEquipoA.ToString()
            : GameStateManager.Instance.NombreEquipoB.ToString();

        string[] frases = {
            $"¡Turno de {playerName}!\n¿Cuál es tu respuesta?",
            $"¡{playerName}, es tu momento!\n¡Dame una respuesta!",
            $"¡Vamos {playerName}!\n¿Qué dice {teamName}?"
        };
        // Determinista para todos los clientes
        int idx = (int)((uint)player.PlayerId % (uint)frases.Length);
        NotifyMensaje(frases[idx]);
    }

    // ─── LLM helpers ────────────────────────────────────────────────

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
