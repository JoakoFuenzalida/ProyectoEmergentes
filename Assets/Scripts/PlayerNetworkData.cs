using UnityEngine;
using Fusion;

public class PlayerNetworkData : NetworkBehaviour
{
    [Networked] public NetworkString<_32> PlayerName { get; set; }
    [Networked] public int TeamIndex { get; set; } // 0: Sin equipo, 1: Equipo A, 2: Equipo B
    
    // --- NUEVO: SU NÚMERO DE SILLA ---
    [Networked] public int SeatIndex { get; set; } 
    
    [Networked] public NetworkBool IsReady { get; set; }
    [Networked] public NetworkBool IsLeader { get; set; }

    public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            string miNombre = UIGameController.Instance.GetPlayerNameInput();
            if (string.IsNullOrWhiteSpace(miNombre)) miNombre = "Jugador " + Object.InputAuthority.PlayerId;
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
        
        // Asignar el primer asiento disponible de este equipo (0 al 3)
        int takenSeats = 0;
        foreach (var player in Runner.ActivePlayers)
        {
            var data = Runner.GetPlayerObject(player)?.GetComponent<PlayerNetworkData>();
            if (data != null && data.TeamIndex == team && data != this) 
            {
                takenSeats++;
            }
        }
        SeatIndex = takenSeats;
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetName(string name) { PlayerName = name; }
}