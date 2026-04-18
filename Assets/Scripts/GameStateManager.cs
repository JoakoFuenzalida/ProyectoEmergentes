using System;
using UnityEngine;
using Fusion;

public class GameStateManager : NetworkBehaviour
{
    public static GameStateManager Instance { get; private set; }

    private const int MAX_ERRORS   = 3;
    private const int TOTAL_ROUNDS = 5;

    public enum GameState
    {
        WaitingForPlayers, Countdown, WaitingForBuzzer, TypingAnswer, Playing, Stealing, RoundEnd, GameOver
    }

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

    // --- DATOS DE LA PREGUNTA HARDCODEADA ---
    public readonly string PreguntaActual = "Le preguntamos a 100 chilenos: Menciona algo que NUNCA puede faltar en un asado de día domingo.";
    public readonly string[] RespuestasValidas = { "CARNE", "CHORIPAN", "PEBRE", "CERVEZA", "CARBON" };
    public readonly int[] PuntosRespuestas = { 42, 28, 15, 10, 5 };

    [Networked] public int RevealedAnswersMask { get; set; }
    [Networked] public NetworkBool FaceOffChanceUsed { get; set; }

    public static event Action<GameState> OnStateChangedEvent;
    public static event Action<string, int> OnScoreUpdatedEvent;
    public static event Action<int> OnErrorAddedEvent;
    public static event Action<string> OnActiveTeamChangedEvent;

    private ChangeDetector _changes;

    private void Awake() { if (Instance == null) Instance = this; }

    public override void Spawned()
    {
        if (Instance == null) Instance = this;
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
        if (Object.HasStateAuthority) ActiveTeam = TeamAssigner.TEAM_A;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority && CurrentState == GameState.Countdown)
        {
            if (Timer.Expired(Runner))
            {
                Timer = TickTimer.None; 
                CurrentState = GameState.WaitingForBuzzer;
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
            }
        }
    }
    
    public void StartGame()
    {
        if (!Object.HasStateAuthority) return;
        
        IsGameStarted = true; 
        CurrentState = GameState.Countdown;
        Timer = TickTimer.CreateFromSeconds(Runner, 5.0f);
        BuzzerWinnerId = -1; 
        RevealedAnswersMask = 0; 
        FaceOffChanceUsed = false; 
        
        ErrorCount = 0; RoundScore = 0; ScoreA = 0; ScoreB = 0; CurrentRound = 1;
    }

    // --- FUNCIONES ANTIGUAS RESTAURADAS ---
    public void RegisterCorrectAnswer(int points)
    {
        if (!Object.HasStateAuthority) return;
        if (CurrentState != GameState.Playing && CurrentState != GameState.Stealing) return;

        int newRoundScore = RoundScore + points;
        if (CurrentState == GameState.Stealing)
            AwardPointsToTeam(ActiveTeam.ToString(), newRoundScore);
        else
            RoundScore = newRoundScore;
    }

    public void RegisterWrongAnswer()
    {
        if (!Object.HasStateAuthority) return;
        if (CurrentState != GameState.Playing) return;

        int newErrors = ErrorCount + 1;
        if (newErrors >= MAX_ERRORS)
        {
            string stealingTeam = ActiveTeam.ToString() == TeamAssigner.TEAM_A
                ? TeamAssigner.TEAM_B : TeamAssigner.TEAM_A;
            ErrorCount   = newErrors;
            ActiveTeam   = stealingTeam;
            CurrentState = GameState.Stealing;
            Debug.Log($"[GameStateManager] 3 errores → Equipo {stealingTeam} intenta robar.");
            if (TurnManager.Instance != null) TurnManager.Instance.AdvanceTurnInTeam(stealingTeam);
        }
        else
        {
            ErrorCount = newErrors;
        }
    }

    public void RegisterStealFailed()
    {
        if (!Object.HasStateAuthority) return;
        if (CurrentState != GameState.Stealing) return;
        string originalTeam = ActiveTeam.ToString() == TeamAssigner.TEAM_A
            ? TeamAssigner.TEAM_B : TeamAssigner.TEAM_A;
        AwardPointsToTeam(originalTeam, RoundScore);
    }

    private void AwardPointsToTeam(string team, int points)
    {
        if (team == TeamAssigner.TEAM_A) ScoreA += points;
        else ScoreB += points;

        int nextRound = CurrentRound + 1;
        if (nextRound > TOTAL_ROUNDS)
        {
            CurrentState = GameState.GameOver;
        }
        else
        {
            string nextTeam = ActiveTeam.ToString() == TeamAssigner.TEAM_A
                ? TeamAssigner.TEAM_B : TeamAssigner.TEAM_A;
            RoundScore   = 0;
            ErrorCount   = 0;
            CurrentRound = nextRound;
            ActiveTeam   = nextTeam;
            CurrentState = GameState.RoundEnd;
        }
    }

    // --- LÓGICA DE BOTONAZO Y RESPUESTAS ---
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PressBuzzer(int playerId)
    {
        if (CurrentState == GameState.WaitingForBuzzer)
        {
            BuzzerWinnerId = playerId;
            CurrentState = GameState.TypingAnswer;
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SubmitAnswer(string answer, int playerId)
    {
        if (CurrentState == GameState.TypingAnswer && BuzzerWinnerId == playerId)
        {
            string cleanAnswer = answer.ToUpper().Trim()
                .Replace("Á", "A").Replace("É", "E").Replace("Í", "I").Replace("Ó", "O").Replace("Ú", "U");

            bool isCorrect = false;
            int answerIndex = -1;

            for (int i = 0; i < RespuestasValidas.Length; i++)
            {
                if (cleanAnswer.Contains(RespuestasValidas[i]))
                {
                    if ((RevealedAnswersMask & (1 << i)) == 0)
                    {
                        isCorrect = true;
                        answerIndex = i;
                        break;
                    }
                }
            }

            if (isCorrect)
            {
                RevealedAnswersMask |= (1 << answerIndex);
                RegisterCorrectAnswer(PuntosRespuestas[answerIndex]);
                CurrentState = GameState.Playing; 
            }
            else
            {
                if (!FaceOffChanceUsed)
                {
                    FaceOffChanceUsed = true;
                    int otroJugadorId = -1;
                    
                    foreach (var player in Runner.ActivePlayers)
                    {
                        if (player.PlayerId != playerId) { otroJugadorId = player.PlayerId; break; }
                    }
                    
                    if (otroJugadorId != -1)
                    {
                        BuzzerWinnerId = otroJugadorId; 
                        CurrentState = GameState.WaitingForBuzzer; 
                        CurrentState = GameState.TypingAnswer; 
                    }
                }
                else
                {
                    CurrentState = GameState.Playing;
                }
            }
        }
    }
}