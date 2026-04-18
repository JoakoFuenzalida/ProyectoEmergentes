using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Networked] public PlayerRef ActivePlayer  { get; private set; }
    [Networked] public float     TurnTimeLeft  { get; private set; }

    [Header("Configuración de turno")]
    [SerializeField] private float turnDurationSeconds = 30f;

    private Dictionary<string, int> _teamTurnIndex = new Dictionary<string, int>
    {
        { TeamAssigner.TEAM_A, 0 },
        { TeamAssigner.TEAM_B, 0 }
    };

    private bool _timerRunning = false;
    private ChangeDetector _changes;

    public bool IsLocalPlayersTurn =>
        Runner != null && ActivePlayer == Runner.LocalPlayer;

    public float TurnTimeNormalized =>
        turnDurationSeconds > 0 ? Mathf.Clamp01(TurnTimeLeft / turnDurationSeconds) : 0f;

    // TODO: Chat — habilitar input de chat solo para el equipo activo al cambiar turno
    public static event Action<PlayerRef> OnTurnChangedEvent;

    public override void Spawned()
    {
        Instance = this;
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
        GameStateManager.OnStateChangedEvent      += HandleStateChanged;
        GameStateManager.OnActiveTeamChangedEvent += HandleTeamChanged;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
        GameStateManager.OnStateChangedEvent      -= HandleStateChanged;
        GameStateManager.OnActiveTeamChangedEvent -= HandleTeamChanged;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || !_timerRunning) return;
        TurnTimeLeft -= Runner.DeltaTime;
        if (TurnTimeLeft <= 0f)
        {
            _timerRunning = false;
            TurnTimeLeft  = 0f;
            Debug.Log("[TurnManager] Tiempo agotado → respuesta incorrecta.");
            GameStateManager.Instance.RegisterWrongAnswer();
        }
    }

    // Render() corre en todos los clientes — detecta cambios de turno
    public override void Render()
    {
        foreach (var change in _changes.DetectChanges(this, out var previousBuffer, out var currentBuffer))
        {
            switch (change)
            {
                case nameof(ActivePlayer):
                    Debug.Log($"[TurnManager] Turno → {ActivePlayer}");
                    OnTurnChangedEvent?.Invoke(ActivePlayer);
                    break;
            }
        }
    }

    public void AdvanceTurnInTeam(string team)
    {
        if (!Object.HasStateAuthority) return;

        List<PlayerRef> teamPlayers = TeamAssigner.Instance.GetPlayersInTeam(team);
        if (teamPlayers.Count == 0)
        {
            Debug.LogWarning($"[TurnManager] No hay jugadores en equipo {team}");
            return;
        }

        int idx = _teamTurnIndex[team] % teamPlayers.Count;
        _teamTurnIndex[team] = idx + 1;

        ActivePlayer  = teamPlayers[idx];
        TurnTimeLeft  = turnDurationSeconds;
        _timerRunning = true;

        Debug.Log($"[TurnManager] Turno → Jugador {ActivePlayer} (Equipo {team})");
    }

    public void ResetTurnIndices()
    {
        _teamTurnIndex[TeamAssigner.TEAM_A] = 0;
        _teamTurnIndex[TeamAssigner.TEAM_B] = 0;
    }

    private void HandleStateChanged(GameStateManager.GameState newState)
    {
        if (!Object.HasStateAuthority) return;
        switch (newState)
        {
            case GameStateManager.GameState.Playing:
                ResetTurnIndices();
                AdvanceTurnInTeam(GameStateManager.Instance.ActiveTeam.ToString());
                break;
            case GameStateManager.GameState.Stealing:
                AdvanceTurnInTeam(GameStateManager.Instance.ActiveTeam.ToString());
                break;
            case GameStateManager.GameState.RoundEnd:
            case GameStateManager.GameState.GameOver:
                _timerRunning = false;
                TurnTimeLeft  = 0f;
                break;
        }
    }

    private void HandleTeamChanged(string newTeam)
    {
        Debug.Log($"[TurnManager] Equipo activo cambió a: {newTeam}");
    }
}