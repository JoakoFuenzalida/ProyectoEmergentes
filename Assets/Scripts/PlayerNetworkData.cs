using UnityEngine;
using Fusion;

public class PlayerNetworkData : NetworkBehaviour
{
    [Networked] public NetworkString<_32> PlayerName { get; set; }
    [Networked] public int TeamIndex  { get; set; } // 0: Sin equipo, 1: Equipo A, 2: Equipo B
    [Networked] public int SeatIndex  { get; set; }
    [Networked] public NetworkBool IsReady   { get; set; }
    [Networked] public NetworkBool IsLeader  { get; set; }
    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            string miNombre = UIGameController.Instance != null
                ? UIGameController.Instance.GetPlayerNameInput()
                : "Jugador";

            if (string.IsNullOrWhiteSpace(miNombre))
                miNombre = "Jugador " + Object.InputAuthority.PlayerId;

            RPC_SetName(miNombre);
        }
    }

    public override void Render()
    {
        if (UIGameController.Instance != null) UIGameController.Instance.RefreshRoomUI();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReady(bool ready) { IsReady = ready; }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_JoinTeam(int team)
    {
        TeamIndex = team;

        // BUG FIX: Contar asientos ocupados de forma segura
        // Antes podía haber race condition si dos jugadores entraban al mismo equipo simultáneamente
        // porque ambos leían el mismo valor antes de que el otro escribiera
        int takenSeats = 0;
        foreach (var player in Runner.ActivePlayers)
        {
            var data = Runner.GetPlayerObject(player)?.GetComponent<PlayerNetworkData>();
            // BUG FIX: excluir jugadores sin equipo asignado (TeamIndex == 0)
            // y al propio jugador para no contar su asiento anterior
            if (data != null && data != this && data.TeamIndex == team && data.SeatIndex >= 0)
                takenSeats = Mathf.Max(takenSeats, data.SeatIndex + 1);
        }

        SeatIndex = takenSeats;
        IsReady = false; // BUG FIX: resetear ready al cambiar de equipo
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetName(string name) { PlayerName = name; }

}