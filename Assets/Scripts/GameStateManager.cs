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

    private const int MAX_ERRORS   = 3;
    private const int TOTAL_ROUNDS = 5;

    public enum GameState
    {
        WaitingForPlayers, Countdown, WaitingForBuzzer, TypingAnswer, Playing, Stealing, RoundEnd, GameOver
    }

    [Header("Base de Datos de Preguntas")]
    [SerializeField] private PreguntaData[] bancoDePreguntas;
    [Networked] public int CurrentQuestionIndex { get; set; }

    // Preguntas generadas por IA — cuando están presentes, reemplazan el banco estático
    private PreguntaData[] _preguntasDinamicas;
    private PreguntaData[] BancoActivo => (_preguntasDinamicas != null && _preguntasDinamicas.Length > 0)
        ? _preguntasDinamicas : bancoDePreguntas;

    public string PreguntaActual    => (BancoActivo != null && BancoActivo.Length > 0) ? BancoActivo[CurrentQuestionIndex].Pregunta   : "Sin pregunta configurada";
    public string[] RespuestasValidas => (BancoActivo != null && BancoActivo.Length > 0) ? BancoActivo[CurrentQuestionIndex].Respuestas : new string[0];
    public int[] PuntosRespuestas   => (BancoActivo != null && BancoActivo.Length > 0) ? BancoActivo[CurrentQuestionIndex].Puntos     : new int[0];

    [Networked] public NetworkBool IsGameStarted { get; set; }
    [Networked] public GameState CurrentState { get; private set; } = GameState.WaitingForPlayers;
    [Networked] public NetworkString<_2> ActiveTeam { get; private set; }
    [Networked] public int ErrorCount { get; private set; }
    [Networked] public int RoundScore { get; private set; }
    [Networked] public int ScoreA { get; private set; }
    [Networked] public int ScoreB { get; private set; }
    [Networked] public int CurrentRound { get; private set; } = 1;

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

    public static event Action<GameState> OnStateChangedEvent;
    public static event Action<string, int> OnScoreUpdatedEvent;
    public static event Action<int> OnErrorAddedEvent;
    public static event Action<string> OnActiveTeamChangedEvent;
    public static event Action OnTemporaryStrikeEvent; 
    public static event Action<bool> OnEvaluationStateChangedEvent;
    
    public static event Action OnTeamNamesUpdatedEvent; 

    private ChangeDetector _changes;

    private void Awake() { if (Instance == null) Instance = this; }

    public override void Spawned()
    {
        if (Instance == null) Instance = this;
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
        
        if (Object.HasStateAuthority) 
        {
            ActiveTeam = TeamAssigner.TEAM_A;
            NombreEquipoA = "Equipo A"; 
            NombreEquipoB = "Equipo B";
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetTeamName(int teamIndex, string newName)
    {
        if (teamIndex == 1) NombreEquipoA = newName;
        else if (teamIndex == 2) NombreEquipoB = newName;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
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

                case nameof(BuzzerWinnerId): 
                    if (CurrentState == GameState.TypingAnswer || CurrentState == GameState.Stealing) 
                    {
                        OnStateChangedEvent?.Invoke(CurrentState); 
                    }
                    break;
            }
        }
    }
    
    // ─── Preguntas dinámicas (IA) ──────────────────────────────────

    // El host llama esto con las preguntas generadas por Ollama.
    // Se serializa cada pregunta como JSON y se envía via RPC a todos los clientes.
    public void RPC_CargarPreguntasDinamicas(PreguntaData[] preguntas)
    {
        if (!Object.HasStateAuthority) return;

        RPC_LimpiarPreguntasDinamicas(preguntas.Length);

        for (int i = 0; i < preguntas.Length; i++)
        {
            var p = preguntas[i];
            string respuestasJson = "[\"" + string.Join("\",\"", p.Respuestas) + "\"]";
            string puntosJson     = "[" + string.Join(",", p.Puntos) + "]";
            // Sinónimos: cada elemento es "sin1|sin2|sin3" separado por |
            string sinonimosJson  = (p.Sinonimos != null && p.Sinonimos.Length > 0)
                ? "[\"" + string.Join("\",\"", p.Sinonimos) + "\"]"
                : "[]";
            RPC_AgregarPreguntaDinamica(i, p.Pregunta, respuestasJson, puntosJson, sinonimosJson);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_LimpiarPreguntasDinamicas(int cantidad)
    {
        _preguntasDinamicas = new PreguntaData[cantidad];
        CurrentQuestionIndex = 0;
        Debug.Log($"[GSM] Banco dinámico preparado para {cantidad} preguntas.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AgregarPreguntaDinamica(int indice, string pregunta,
                                              string respuestasJson, string puntosJson,
                                              string sinonimosJson)
    {
        if (_preguntasDinamicas == null || indice >= _preguntasDinamicas.Length) return;

        _preguntasDinamicas[indice] = new PreguntaData
        {
            Pregunta   = pregunta,
            Respuestas = ParseStringArray(respuestasJson),
            Puntos     = ParseIntArray(puntosJson),
            Sinonimos  = sinonimosJson == "[]" ? new string[0] : ParseStringArray(sinonimosJson)
        };
        Debug.Log($"[GSM] Pregunta {indice} cargada: {pregunta}");
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

    public void StartGame()
    {
        if (!Object.HasStateAuthority) return;
        IsGameStarted = true; 
        CurrentState = GameState.Countdown;
        Timer = TickTimer.CreateFromSeconds(Runner, 5.0f);
        BuzzerWinnerId = -1; 
        RevealedAnswersMask = 0; 
        FaceOffChanceUsed = false; 
        IsEvaluating = false;
        ErrorCount = 0; RoundScore = 0; ScoreA = 0; ScoreB = 0; CurrentRound = 1;
        CurrentQuestionIndex = 0; 
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

            if (TurnManager.Instance != null) TurnManager.Instance.RPC_AdvanceTurnInTeam(stealingTeam);
        }
        else ErrorCount = newErrors;
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
        if (nextRound > TOTAL_ROUNDS) CurrentState = GameState.GameOver;
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

        PendingIsCorrect  = isCorrect;
        PendingAnswerIndex = answerIndex;
        PendingPlayerId   = playerId;
        IsEvaluating      = true;
        EvaluationTimer   = TickTimer.CreateFromSeconds(Runner, 1.5f);
    }

    private static string Normalizar(string s) =>
        s.ToUpper().Trim()
         .Replace("Á","A").Replace("É","E").Replace("Í","I").Replace("Ó","O").Replace("Ú","U")
         .Replace("Ü","U").Replace("Ñ","N");

    private void ApplyAnswerResult(bool isCorrect, int answerIndex, int playerId)
    {
        // ==========================================
        // 1. FASE DE PODIO (Validamos con BuzzerWinnerId)
        // ==========================================
        if (CurrentState == GameState.TypingAnswer && BuzzerWinnerId == playerId)
        {
            if (isCorrect) 
            { 
                RevealedAnswersMask |= (1 << answerIndex); 
                RegisterCorrectAnswer(PuntosRespuestas[answerIndex]); 
                
                var pData = Runner.GetPlayerObject(PlayerRef.FromIndex(playerId))?.GetComponent<PlayerNetworkData>();
                ActiveTeam = (pData != null && pData.TeamIndex == 2) ? "B" : "A";
                CurrentState = GameState.Playing; 

                if (TurnManager.Instance != null) 
                {
                    TurnManager.Instance.ResetTurnIndices();
                    TurnManager.Instance.RPC_AdvanceTurnInTeam(ActiveTeam.ToString());
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
                    else CurrentState = GameState.Playing;
                } 
                else 
                {
                    CurrentState = GameState.Playing;
                    if (TurnManager.Instance != null) TurnManager.Instance.RPC_AdvanceTurnInTeam(ActiveTeam.ToString());
                }
            }
        }
        
        // ==========================================
        // 2. FASE DE MESA (Validamos con TurnManager)
        // ==========================================
        else if (CurrentState == GameState.Playing)
        {
            bool esTurnoValido = (TurnManager.Instance != null &&
                                  TurnManager.Instance.ActivePlayer != PlayerRef.None &&
                                  TurnManager.Instance.ActivePlayer.PlayerId == playerId)
                                 || BuzzerWinnerId == playerId;

            if (esTurnoValido)
            {
                if (isCorrect) 
                { 
                    RevealedAnswersMask |= (1 << answerIndex); 
                    RegisterCorrectAnswer(PuntosRespuestas[answerIndex]); 
                    
                    int allAnswersMask = (1 << RespuestasValidas.Length) - 1; 
                    if (RevealedAnswersMask == allAnswersMask) 
                    {
                        AwardPointsToTeam(ActiveTeam.ToString(), RoundScore); 
                    }
                    else
                    {
                        // Si acertó pero aún quedan respuestas, pasamos el turno al siguiente del equipo
                        if (TurnManager.Instance != null) TurnManager.Instance.RPC_AdvanceTurnInTeam(ActiveTeam.ToString());
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
            bool esTurnoValido = (TurnManager.Instance != null &&
                                  TurnManager.Instance.ActivePlayer != PlayerRef.None &&
                                  TurnManager.Instance.ActivePlayer.PlayerId == playerId)
                                 || BuzzerWinnerId == playerId;

            if (esTurnoValido)
            {
                if (isCorrect) 
                { 
                    RevealedAnswersMask |= (1 << answerIndex); 
                    AwardPointsToTeam(ActiveTeam.ToString(), RoundScore + PuntosRespuestas[answerIndex]); 
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
