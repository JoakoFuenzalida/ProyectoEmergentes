using System;
using UnityEngine;
using Fusion;

[System.Serializable]
public struct PreguntaData
{
    [TextArea(2, 3)]
    public string Pregunta;
    public string[] Respuestas;
    public int[] Puntos;
    // Sinónimos por respuesta, separados por coma. Ej: "Git,control de versiones,versionamiento"
    public string[] Sinonimos;
}

public class GameStateManager : NetworkBehaviour
{
    public static GameStateManager Instance { get; private set; }

    private const int MAX_ERRORS = 3;
    [SerializeField] private int totalRondasDefault = 5;

    public enum GameState
    {
        WaitingForPlayers, Intro, Countdown, WaitingForBuzzer, TypingAnswer, Playing, Stealing, RoundEnd, GameOver
    }

    [Header("Base de Datos de Preguntas")]
    [SerializeField] private PreguntaData[] bancoDePreguntas;
    [Networked] public int CurrentQuestionIndex { get; set; }

    // Preguntas generadas por IA — cuando están presentes, reemplazan el banco estático
    private PreguntaData[] _preguntasDinamicas;
    private PreguntaData[] BancoActivo => (_preguntasDinamicas != null && _preguntasDinamicas.Length > 0)
        ? _preguntasDinamicas : bancoDePreguntas;

    public string PreguntaActual =>
        Object.HasStateAuthority
            ? (BancoActivo != null && BancoActivo.Length > 0 ? BancoActivo[CurrentQuestionIndex].Pregunta : "Sin pregunta configurada")
            : (_netPregunta.Length > 0 ? _netPregunta.ToString() : "Cargando...");

    public string[] RespuestasValidas =>
        Object.HasStateAuthority
            ? (BancoActivo != null && BancoActivo.Length > 0 ? BancoActivo[CurrentQuestionIndex].Respuestas : new string[0])
            : ParseStringArray(_netRespuestas.ToString());

    public int[] PuntosRespuestas =>
        Object.HasStateAuthority
            ? (BancoActivo != null && BancoActivo.Length > 0 ? BancoActivo[CurrentQuestionIndex].Puntos : new int[0])
            : ParseIntArray(_netPuntos.ToString());

    [Networked] public NetworkBool IsGameStarted { get; set; }
    [Networked] public GameState CurrentState { get; private set; } = GameState.WaitingForPlayers;
    [Networked] public NetworkString<_2> ActiveTeam { get; private set; }
    [Networked] public int ErrorCount { get; private set; }
    [Networked] public int RoundScore { get; private set; }
    [Networked] public int ScoreA { get; private set; }
    [Networked] public int ScoreB { get; private set; }
    [Networked] public int CurrentRound { get; private set; } = 1;
    [Networked] public int TotalRondas  { get; set; }

    [Networked] public TickTimer Timer { get; private set; } 
    [Networked] public int BuzzerWinnerId { get; set; }

    [Networked] public NetworkString<_32> NombreEquipoA { get; set; }
    [Networked] public NetworkString<_32> NombreEquipoB { get; set; }

    [Networked] public int RevealedAnswersMask { get; set; }
    [Networked] public NetworkBool FaceOffChanceUsed { get; set; }

    [Networked] public NetworkBool IsEvaluating { get; set; }
    [Networked] public TickTimer EvaluationTimer { get; set; }
    [Networked] public NetworkBool PendingIsCorrect { get; set; }
    [Networked] public int PendingAnswerIndex { get; set; }
    [Networked] public int PendingPlayerId { get; set; }
    [Networked] private PlayerRef PendingPlayerRef { get; set; }

    // Mensaje del animador — sincronizado automáticamente a todos los clientes
    [Networked] public NetworkString<_512> MensajeAnimador { get; set; }
    // Contador de versión: fuerza detección de cambio aunque el mensaje sea igual al anterior
    [Networked] private int _mensajeVersion { get; set; }

    // ── Conductor dinámico: respuestas y resultados ──────────────────
    [Networked] public NetworkString<_64> PendingAnswerText  { get; set; }
    [Networked] public int                PendingResultPoints { get; set; }
    [Networked] private int               _answerResultVersion { get; set; }

    // Pregunta actual sincronizada — los clientes leen de aquí en vez de _preguntasDinamicas
    [Networked] private NetworkString<_256> _netPregunta   { get; set; }
    [Networked] private NetworkString<_512> _netRespuestas { get; set; }
    [Networked] private NetworkString<_128> _netPuntos     { get; set; }

    public static event Action<GameState> OnStateChangedEvent;
    public static event Action<string, int> OnScoreUpdatedEvent;
    public static event Action<int> OnErrorAddedEvent;
    public static event Action<string> OnActiveTeamChangedEvent;
    public static event Action OnTemporaryStrikeEvent; 
    public static event Action<bool> OnEvaluationStateChangedEvent;
    
    public static event Action OnTeamNamesUpdatedEvent;
    // correct, points, playerName, teamName
    public static event Action<bool, int, string, string> OnAnswerResultEvent;
    // Avisa a los clientes cuando la pregunta networked llega/cambia
    public static event Action OnPreguntaActualizadaEvent;
    // Avisa cuando TotalRondas o TurnDurationSeconds cambian (para refrescar UI de config)
    public static event Action OnConfigChangedEvent;
    // Llamado por TurnManager cuando TurnDurationSeconds cambia (los events no se pueden invocar desde fuera)
    public static void FireConfigChanged() => OnConfigChangedEvent?.Invoke();

    private ChangeDetector _changes;

    private void Awake() { if (Instance == null) Instance = this; }

    public override void Spawned()
    {
        // Siempre sobreescribir Instance: limpia referencias stale de sesiones anteriores.
        // Si Despawned() no se llamó (edge case), esto garantiza que el nuevo GSM quede activo.
        Instance = this;
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (Object.HasStateAuthority)
        {
            ActiveTeam = TeamAssigner.TEAM_A;
            NombreEquipoA = "Equipo A";
            NombreEquipoB = "Equipo B";
            TotalRondas   = totalRondasDefault;

            // El host genera preguntas IA automáticamente al entrar a la sala
            StartCoroutine(GenerarPreguntasIA());
        }
    }

    private System.Collections.IEnumerator GenerarPreguntasIA()
    {
        yield return null; // esperar un frame para que OllamaService esté listo

        if (OllamaService.Instance == null)
        {
            Debug.LogWarning("[GSM] OllamaService no encontrado en la escena. Se usarán preguntas hardcodeadas.");
            yield break;
        }

        Debug.Log("[GSM] Iniciando generación de preguntas con IA...");
        AnimadorIA.NotifyGenerating(true);

        OllamaService.Instance.GenerarPreguntas(5,
            preguntas =>
            {
                // Guardar localmente en el host — NO enviamos RPCs aquí porque los
                // clientes pueden no estar conectados todavía. La sincronización
                // ocurre en StartGame(), cuando todos ya están en la sala.
                _preguntasDinamicas = preguntas;
                AnimadorIA.NotifyGenerating(false);
                Debug.Log($"[GSM] {preguntas.Length} preguntas IA listas (se sincronizarán al arrancar).");
            },
            error =>
            {
                AnimadorIA.NotifyGenerating(false);
                Debug.LogError($"[GSM] Error generando preguntas IA: {error}. Se usarán preguntas hardcodeadas.");
            });
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Limpiar referencia estática al despawnear para que la próxima sesión pueda registrarse.
        if (Instance == this) Instance = null;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetTeamName(int teamIndex, string newName)
    {
        if (teamIndex == 1) NombreEquipoA = newName;
        else if (teamIndex == 2) NombreEquipoB = newName;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetTotalRondas(int rondas)
    {
        if (IsGameStarted) return;
        TotalRondas = Mathf.Clamp(rondas, 1, 10);
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            if (CurrentState == GameState.Intro && Timer.Expired(Runner))
            {
                Timer = TickTimer.None;
                CurrentState = GameState.Countdown;
                Timer = TickTimer.CreateFromSeconds(Runner, 5.0f);
            }

            if (CurrentState == GameState.Countdown && Timer.Expired(Runner))
            {
                Timer = TickTimer.None;
                CurrentState = GameState.WaitingForBuzzer;
            }

            if (IsEvaluating && EvaluationTimer.Expired(Runner))
            {
                EvaluationTimer = TickTimer.None;
                IsEvaluating = false;
                ApplyAnswerResult(PendingIsCorrect, PendingAnswerIndex, PendingPlayerId);
            }

            if (CurrentState == GameState.RoundEnd && Timer.Expired(Runner))
            {
                CurrentQuestionIndex = (CurrentQuestionIndex + 1) % (BancoActivo != null && BancoActivo.Length > 0 ? BancoActivo.Length : 1);
                SincronizarPreguntaActual();
                CurrentState = GameState.Countdown;
                Timer = TickTimer.CreateFromSeconds(Runner, 5.0f);
                BuzzerWinnerId = -1;
                RevealedAnswersMask = 0;
                FaceOffChanceUsed = false;
            }
        }
    }

    public override void Render()
    {
        foreach (var change in _changes.DetectChanges(this, out var previousBuffer, out var currentBuffer))
        {
            switch (change)
            {
                case nameof(CurrentState): OnStateChangedEvent?.Invoke(CurrentState); break;
                case nameof(ActiveTeam): OnActiveTeamChangedEvent?.Invoke(ActiveTeam.ToString()); break;
                case nameof(ErrorCount): OnErrorAddedEvent?.Invoke(ErrorCount); break;
                case nameof(ScoreA): OnScoreUpdatedEvent?.Invoke(TeamAssigner.TEAM_A, ScoreA); break;
                case nameof(ScoreB): OnScoreUpdatedEvent?.Invoke(TeamAssigner.TEAM_B, ScoreB); break;
                case nameof(IsEvaluating): OnEvaluationStateChangedEvent?.Invoke(IsEvaluating); break;
                
                case nameof(RoundScore):
                case nameof(NombreEquipoA):
                case nameof(NombreEquipoB):
                    OnTeamNamesUpdatedEvent?.Invoke();
                    break;

                case nameof(TotalRondas):
                    OnConfigChangedEvent?.Invoke();
                    break;

                case nameof(BuzzerWinnerId):
                    if (CurrentState == GameState.TypingAnswer || CurrentState == GameState.Stealing)
                        OnStateChangedEvent?.Invoke(CurrentState);
                    break;

                case nameof(_mensajeVersion):
                    string msg = MensajeAnimador.ToString();
                    Debug.Log($"[GSM] MensajeAnimador v{_mensajeVersion} → '{msg}' (IsServer={Runner.IsServer})");
                    if (!string.IsNullOrEmpty(msg))
                        AnimadorIA.NotifyMensaje(msg);
                    break;

                case nameof(_answerResultVersion):
                {
                    var pd    = Runner?.GetPlayerObject(PlayerRef.FromIndex(PendingPlayerId))?.GetComponent<PlayerNetworkData>();
                    string pN = pd?.PlayerName.ToString() ?? "Jugador";
                    string tN = ActiveTeam.ToString() == "A" ? NombreEquipoA.ToString() : NombreEquipoB.ToString();
                    OnAnswerResultEvent?.Invoke(PendingIsCorrect, PendingResultPoints, pN, tN);
                    break;
                }

                // Cuando _netPregunta llega/cambia en el cliente, actualizar UI
                case nameof(_netPregunta):
                    if (!string.IsNullOrEmpty(_netPregunta.ToString()))
                        OnPreguntaActualizadaEvent?.Invoke();
                    break;
            }
        }
    }
    
    // ─── Helpers públicos ────────────────────────────────────────────

    // Devuelve el nombre del jugador con SeatIndex 0 en el equipo indicado (el líder del podio)
    public string GetLiderNombre(int teamIndex)
    {
        foreach (var pRef in Runner.ActivePlayers)
        {
            var data = Runner.GetPlayerObject(pRef)?.GetComponent<PlayerNetworkData>();
            if (data != null && data.TeamIndex == teamIndex && data.SeatIndex == 0)
                return data.PlayerName.ToString();
        }
        return "Jugador";
    }

    // ─── Animador IA ─────────────────────────────────────────────────

    // Usar este método en lugar de asignar MensajeAnimador directamente.
    // El contador _mensajeVersion garantiza que Render() detecte el cambio
    // incluso si el texto es idéntico al mensaje anterior.
    public void ActualizarMensajeAnimador(string mensaje)
    {
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[GSM] ActualizarMensajeAnimador llamado sin StateAuthority — ignorado");
            return;
        }
        MensajeAnimador = mensaje.Length > 510 ? mensaje.Substring(0, 510) : mensaje;
        _mensajeVersion++;
        Debug.Log($"[GSM] ActualizarMensajeAnimador v{_mensajeVersion} → '{MensajeAnimador}'");
    }

    // ─── Preguntas dinámicas (IA) ──────────────────────────────────

    // Escribe la pregunta actual en las propiedades [Networked] para que todos los
    // clientes la reciban en el mismo snapshot que el cambio de estado.
    private void SincronizarPreguntaActual()
    {
        if (!Object.HasStateAuthority) return;
        if (BancoActivo == null || BancoActivo.Length == 0) return;

        var q = BancoActivo[CurrentQuestionIndex];
        string pregunta = q.Pregunta.Length > 255 ? q.Pregunta.Substring(0, 255) : q.Pregunta;
        string respuestas = "[\"" + string.Join("\",\"", q.Respuestas) + "\"]";
        string puntos     = "[" + string.Join(",", q.Puntos) + "]";

        _netPregunta   = pregunta;
        _netRespuestas = respuestas.Length > 511 ? respuestas.Substring(0, 511) : respuestas;
        _netPuntos     = puntos.Length > 127 ? puntos.Substring(0, 127) : puntos;
    }

    private static string[] ParseStringArray(string json)
    {
        json = json.Trim().TrimStart('[').TrimEnd(']');
        var parts = json.Split(',');
        var result = new string[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            result[i] = parts[i].Trim().Trim('"');
        return result;
    }

    private static int[] ParseIntArray(string json)
    {
        json = json.Trim().TrimStart('[').TrimEnd(']');
        var parts = json.Split(',');
        var result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            if (int.TryParse(parts[i].Trim(), out int v)) result[i] = v;
        return result;
    }

    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Anuncia al animador cuántas respuestas tiene la pregunta actual.
    /// Se llama justo antes de pasar al estado Countdown.
    /// </summary>
    private void AnunciarCantidadRespuestas(int esRonda)
    {
        if (BancoActivo == null || BancoActivo.Length == 0) return;
        int n = BancoActivo[CurrentQuestionIndex].Respuestas.Length;
        string plural = n == 1 ? "respuesta" : "respuestas";
        string msg = $"¡Ronda {esRonda}!\nEsta pregunta tiene\n{n} {plural}.";
        ActualizarMensajeAnimador(msg);
    }

    /// <summary>
    /// Llamado por AnimadorController (solo host) cuando termina la secuencia de intro.
    /// Avanza el estado a Countdown sin depender de un timer fijo.
    /// </summary>
    public void TerminarIntro()
    {
        if (!Object.HasStateAuthority) return;
        if (CurrentState != GameState.Intro) return;
        Timer = TickTimer.None;
        CurrentState = GameState.Countdown;
        Timer = TickTimer.CreateFromSeconds(Runner, 5.0f);
    }

    public void StartGame()
    {
        if (!Object.HasStateAuthority) return;

        IsGameStarted = true;
        ErrorCount = 0; RoundScore = 0; ScoreA = 0; ScoreB = 0; CurrentRound = 1;
        CurrentQuestionIndex = 0;
        BuzzerWinnerId = -1;
        RevealedAnswersMask = 0;
        FaceOffChanceUsed = false;
        IsEvaluating = false;

        // Escribir pregunta actual en [Networked] — llega a todos en el mismo snapshot
        SincronizarPreguntaActual();

        // Intro: el AnimadorController llama a TerminarIntro() cuando termina de hablar.
        // El timer de 300s es solo una red de seguridad por si algo falla.
        CurrentState = GameState.Intro;
        Timer = TickTimer.CreateFromSeconds(Runner, 300.0f);
    }

    public void RegisterCorrectAnswer(int points)
    {
        int newRoundScore = RoundScore + points;
        if (CurrentState == GameState.Stealing) AwardPointsToTeam(ActiveTeam.ToString(), newRoundScore);
        else RoundScore = newRoundScore;
    }

    public void RegisterWrongAnswer()
    {
        int newErrors = ErrorCount + 1;
        if (newErrors >= MAX_ERRORS)
        {
            string stealingTeam = ActiveTeam.ToString() == TeamAssigner.TEAM_A ? TeamAssigner.TEAM_B : TeamAssigner.TEAM_A;
            ErrorCount   = newErrors;
            ActiveTeam   = stealingTeam;
            CurrentState = GameState.Stealing;

            int otroJugadorId = -1;
            foreach (var player in Runner.ActivePlayers)
                if (player.PlayerId != BuzzerWinnerId) { otroJugadorId = player.PlayerId; break; }
            if (otroJugadorId != -1) BuzzerWinnerId = otroJugadorId;

            if (TurnManager.Instance != null) TurnManager.Instance.AdvanceTurnInTeam(stealingTeam);
        }
        else
        {
            ErrorCount = newErrors;
            if (CurrentState == GameState.Playing && TurnManager.Instance != null)
                TurnManager.Instance.AdvanceTurnInTeam(ActiveTeam.ToString());
        }
    }

    public void RegisterStealFailed()
    {
        string originalTeam = ActiveTeam.ToString() == TeamAssigner.TEAM_A ? TeamAssigner.TEAM_B : TeamAssigner.TEAM_A;
        AwardPointsToTeam(originalTeam, RoundScore);
    }

    private void AwardPointsToTeam(string team, int points)
    {
        if (team == TeamAssigner.TEAM_A) ScoreA += points;
        else ScoreB += points;

        int nextRound = CurrentRound + 1;
        if (nextRound > TotalRondas) CurrentState = GameState.GameOver;
        else
        {
            ActiveTeam = ActiveTeam.ToString() == TeamAssigner.TEAM_A ? TeamAssigner.TEAM_B : TeamAssigner.TEAM_A;
            RoundScore = 0; ErrorCount = 0; CurrentRound = nextRound; 
            CurrentState = GameState.RoundEnd;
            Timer = TickTimer.CreateFromSeconds(Runner, 4.0f); 
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ShowTemporaryStrike() { OnTemporaryStrikeEvent?.Invoke(); }

    // ¡AQUÍ ESTÁ LA FUNCIÓN RECUPERADA!
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PressBuzzer(int playerId)
    {
        if (CurrentState == GameState.WaitingForBuzzer) { BuzzerWinnerId = playerId; CurrentState = GameState.TypingAnswer; }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SubmitAnswer(string answer, int playerId)
    {
        if (CurrentState == GameState.RoundEnd || CurrentState == GameState.GameOver) return;
        if (IsEvaluating) return;

        string cleanAnswer = Normalizar(answer);

        bool isCorrect  = false;
        int answerIndex = -1;

        for (int i = 0; i < RespuestasValidas.Length; i++)
        {
            if ((RevealedAnswersMask & (1 << i)) != 0) continue; // ya revelada

            // Verificar respuesta principal
            if (cleanAnswer.Contains(Normalizar(RespuestasValidas[i])))
            {
                isCorrect = true; answerIndex = i; break;
            }

            // Verificar sinónimos (separados por '|' dentro de cada elemento)
            var banco = BancoActivo;
            if (banco != null && CurrentQuestionIndex < banco.Length &&
                banco[CurrentQuestionIndex].Sinonimos != null &&
                i < banco[CurrentQuestionIndex].Sinonimos.Length)
            {
                foreach (var sin in banco[CurrentQuestionIndex].Sinonimos[i].Split('|'))
                {
                    string cleanSin = Normalizar(sin);
                    if (!string.IsNullOrEmpty(cleanSin) && cleanAnswer.Contains(cleanSin))
                    {
                        isCorrect = true; answerIndex = i; break;
                    }
                }
            }

            if (isCorrect) break;
        }

        PendingIsCorrect   = isCorrect;
        PendingAnswerIndex  = answerIndex;
        PendingPlayerId    = playerId;

        PendingPlayerRef = PlayerRef.None;
        foreach (var pRef in Runner.ActivePlayers)
            if (pRef.PlayerId == playerId) { PendingPlayerRef = pRef; break; }

        // ── Mensaje de suspenso del conductor ─────────────────────
        PendingAnswerText = answer.Length > 63 ? answer.Substring(0, 63) : answer;
        string[] frasesSuspenso = {
            $"«{answer}»...\n¿Estará correcta?\n¡DÁMELA!",
            $"Dice «{answer}»...\n¡Veamos si acierta!",
            $"«{answer}»...\n¡Momento de la verdad!"
        };
        // Hash determinista para que todos los clientes elijan la misma frase
        int fi = (int)((uint)(answer.GetHashCode() + playerId) % (uint)frasesSuspenso.Length);
        ActualizarMensajeAnimador(frasesSuspenso[fi]);

        IsEvaluating    = true;
        // Timer de SEGURIDAD: AnimadorController lo cancela antes cuando el TTS termina.
        // 12 s cubre: ~2s LLM + ~3s TTS generación + ~4s reproducción + margen.
        EvaluationTimer = TickTimer.CreateFromSeconds(Runner, 12.0f);
    }

    /// <summary>
    /// Llamado por AnimadorController (solo HOST) cuando el TTS de suspense termina.
    /// Revela el resultado en ese instante en lugar de esperar el timer de seguridad.
    /// </summary>
    public void ReleaseEvaluation()
    {
        if (!Object.HasStateAuthority) return;
        if (!IsEvaluating) return;
        EvaluationTimer = TickTimer.None;
        IsEvaluating    = false;
        ApplyAnswerResult(PendingIsCorrect, PendingAnswerIndex, PendingPlayerId);
    }

    /// <summary>Incrementa el contador de aciertos del jugador con el PlayerId dado.</summary>
    private void IncrementarAciertos(int playerId)
    {
        foreach (var pRef in Runner.ActivePlayers)
        {
            if (pRef.PlayerId != playerId) continue;
            var data = Runner.GetPlayerObject(pRef)?.GetComponent<PlayerNetworkData>();
            if (data != null) data.Aciertos++;
            return;
        }
    }

    private static string Normalizar(string s) =>
        s.ToUpper().Trim()
         .Replace("Á","A").Replace("É","E").Replace("Í","I").Replace("Ó","O").Replace("Ú","U")
         .Replace("Ü","U").Replace("Ñ","N");

    private void ApplyAnswerResult(bool isCorrect, int answerIndex, int playerId)
    {
        // ── Resultado del conductor (mensaje inmediato + evento para LLM) ──
        int rPts = (isCorrect && answerIndex >= 0 && BancoActivo != null
                    && answerIndex < BancoActivo[CurrentQuestionIndex].Puntos.Length)
                    ? BancoActivo[CurrentQuestionIndex].Puntos[answerIndex] : 0;
        PendingResultPoints = rPts;
        string msgResult = isCorrect ? $"¡CORRECTO!\n+{rPts} puntos!" : "¡Incorrecto!\n¡X roja!";
        ActualizarMensajeAnimador(msgResult);
        _answerResultVersion++;

        // ==========================================
        // 1. FASE DE PODIO (Validamos con BuzzerWinnerId)
        // ==========================================
        if (CurrentState == GameState.TypingAnswer && BuzzerWinnerId == playerId)
        {
            if (isCorrect)
            {
                RevealedAnswersMask |= (1 << answerIndex);
                RegisterCorrectAnswer(PuntosRespuestas[answerIndex]);
                IncrementarAciertos(playerId);

                PlayerNetworkData pData = null;
                foreach (var pRef in Runner.ActivePlayers)
                {
                    if (pRef.PlayerId == playerId)
                    {
                        pData = Runner.GetPlayerObject(pRef)?.GetComponent<PlayerNetworkData>();
                        break;
                    }
                }
                ActiveTeam = (pData != null && pData.TeamIndex == 2) ? "B" : "A";
                CurrentState = GameState.Playing;

                if (TurnManager.Instance != null)
                {
                    int buzzerSeat = (pData != null) ? pData.SeatIndex : 0;
                    TurnManager.Instance.SetTurnIndex(ActiveTeam.ToString(), buzzerSeat + 1);
                    TurnManager.Instance.AdvanceTurnInTeam(ActiveTeam.ToString());
                }
            }
            else 
            {
                RPC_ShowTemporaryStrike(); 
                if (!FaceOffChanceUsed) 
                {
                    FaceOffChanceUsed = true;
                    int otroJugadorId = -1;
                    foreach (var pRef in Runner.ActivePlayers)
                    {
                        if (pRef.PlayerId != playerId) { otroJugadorId = pRef.PlayerId; break; }
                    }

                    if (otroJugadorId != -1) BuzzerWinnerId = otroJugadorId;
                    else
                    {
                        CurrentState = GameState.Playing;
                        if (TurnManager.Instance != null)
                        {
                            TurnManager.Instance.ResetTurnIndices();
                            TurnManager.Instance.AdvanceTurnInTeam(ActiveTeam.ToString());
                        }
                    }
                } 
                else
                {
                    CurrentState = GameState.Playing;
                    if (TurnManager.Instance != null)
                    {
                        TurnManager.Instance.ResetTurnIndices();
                        TurnManager.Instance.AdvanceTurnInTeam(ActiveTeam.ToString());
                    }
                }
            }
        }
        
        // ==========================================
        // 2. FASE DE MESA (Validamos con TurnManager)
        // ==========================================
        else if (CurrentState == GameState.Playing)
        {
            bool esTurnoValido = TurnManager.Instance != null &&
                                 TurnManager.Instance.ActivePlayer != PlayerRef.None &&
                                 TurnManager.Instance.ActivePlayer == PendingPlayerRef;

            if (esTurnoValido)
            {
                if (isCorrect)
                {
                    RevealedAnswersMask |= (1 << answerIndex);
                    RegisterCorrectAnswer(PuntosRespuestas[answerIndex]);
                    IncrementarAciertos(playerId);

                    int allAnswersMask = (1 << RespuestasValidas.Length) - 1;
                    if (RevealedAnswersMask == allAnswersMask)
                    {
                        AwardPointsToTeam(ActiveTeam.ToString(), RoundScore);
                    }
                    else
                    {
                        // Si acertó pero aún quedan respuestas, pasamos el turno al siguiente del equipo
                        if (TurnManager.Instance != null) TurnManager.Instance.AdvanceTurnInTeam(ActiveTeam.ToString());
                    }
                }
                else
                {
                    // Esto registrará la X roja de los Strikes
                    RegisterWrongAnswer();
                }
            }
        }

        // ==========================================
        // 3. FASE DE ROBO (Validamos con TurnManager)
        // ==========================================
        else if (CurrentState == GameState.Stealing)
        {
            bool esTurnoValido = TurnManager.Instance != null &&
                                 TurnManager.Instance.ActivePlayer != PlayerRef.None &&
                                 TurnManager.Instance.ActivePlayer == PendingPlayerRef;

            if (esTurnoValido)
            {
                if (isCorrect)
                {
                    RevealedAnswersMask |= (1 << answerIndex);
                    AwardPointsToTeam(ActiveTeam.ToString(), RoundScore + PuntosRespuestas[answerIndex]);
                    IncrementarAciertos(playerId);
                }
                else
                {
                    RPC_ShowTemporaryStrike();
                    RegisterStealFailed();
                }
            }
        }
    }

}
