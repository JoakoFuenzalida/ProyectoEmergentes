using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

public class TeamAssigner : MonoBehaviour
{
    public static TeamAssigner Instance { get; private set; }

    public const string TEAM_A = "A";
    public const string TEAM_B = "B";

    private Dictionary<PlayerRef, string> _playerTeams = new Dictionary<PlayerRef, string>();

    public string LocalTeam { get; private set; }

    // TODO: Chat — suscribir al canal del equipo cuando se asigne
    public Action<string> OnLocalTeamAssigned;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AssignTeam(PlayerRef player)
    {
        if (_playerTeams.ContainsKey(player))
        {
            if (IsLocalPlayer(player))
            {
                LocalTeam = _playerTeams[player];
                OnLocalTeamAssigned?.Invoke(LocalTeam);
            }
            return;
        }

        string team = (player.PlayerId % 2 == 1) ? TEAM_A : TEAM_B;
        _playerTeams[player] = team;

        if (IsLocalPlayer(player))
        {
            LocalTeam = team;
            OnLocalTeamAssigned?.Invoke(LocalTeam);
        }

        Debug.Log($"[TeamAssigner] Jugador {player.PlayerId} → Equipo {team}");
    }

    public void RemovePlayer(PlayerRef player)
    {
        _playerTeams.Remove(player);
    }

    public string GetTeamOf(PlayerRef player)
    {
        return _playerTeams.TryGetValue(player, out string team) ? team : string.Empty;
    }

    public List<PlayerRef> GetPlayersInTeam(string team)
    {
        var result = new List<PlayerRef>();
        foreach (var kvp in _playerTeams)
            if (kvp.Value == team) result.Add(kvp.Key);
        return result;
    }

    public bool IsLocalPlayerInTeam(string team) => LocalTeam == team;

    public void RebalanceTeams()
    {
        var players = new List<PlayerRef>(_playerTeams.Keys);
        _playerTeams.Clear();
        for (int i = 0; i < players.Count; i++)
            _playerTeams[players[i]] = (i % 2 == 0) ? TEAM_A : TEAM_B;

        var local = RoomManager.Instance.Runner.LocalPlayer;
        if (_playerTeams.TryGetValue(local, out string localTeam))
        {
            LocalTeam = localTeam;
            OnLocalTeamAssigned?.Invoke(LocalTeam);
        }
    }

    private bool IsLocalPlayer(PlayerRef player)
    {
        return RoomManager.Instance != null &&
               RoomManager.Instance.Runner != null &&
               player == RoomManager.Instance.Runner.LocalPlayer;
    }
}