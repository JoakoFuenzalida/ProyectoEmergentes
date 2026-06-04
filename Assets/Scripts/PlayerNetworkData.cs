using UnityEngine;
using Fusion;

public class PlayerNetworkData : NetworkBehaviour
{
    [Networked] public NetworkString<_32> PlayerName { get; set; }
    [Networked] public int TeamIndex  { get; set; } // 0: Sin equipo, 1: Equipo A, 2: Equipo B
    [Networked] public int SeatIndex  { get; set; }
    [Networked] public NetworkBool IsReady   { get; set; }
    [Networked] public NetworkBool IsLeader  { get; set; }
    [Networked] public int SkinIndex  { get; set; }
    [Networked] public int Aciertos   { get; set; } // respuestas correctas en la partida actual

    [SerializeField] private PlayerSkinSelector skinSelector;
    private int _lastAppliedSkinIndex = -1;
    public override void Spawned()
    {
        if (skinSelector == null)
            skinSelector = GetComponentInChildren<PlayerSkinSelector>(true);

        if (Object.HasInputAuthority)
        {
            string miNombre = UIGameController.Instance != null
                ? UIGameController.Instance.GetPlayerNameInput()
                : "Jugador";

            if (string.IsNullOrWhiteSpace(miNombre))
                miNombre = "Jugador " + Object.InputAuthority.PlayerId;

            RPC_SetName(miNombre);

            // Asignar skin única — cada PlayerId obtiene un índice diferente automáticamente.
            // (PlayerId empieza en 1 en Fusion; el módulo garantiza que nunca salga del rango.)
            int totalSkins = (skinSelector != null && skinSelector.SkinCount > 0)
                ? skinSelector.SkinCount : 8;
            SkinIndex = (Object.InputAuthority.PlayerId - 1) % totalSkins;
        }

        ApplySkinIfNeeded();
    }

    public override void Render()
    {
        if (skinSelector == null)
            skinSelector = GetComponentInChildren<PlayerSkinSelector>(true);

        ApplySkinIfNeeded();
        if (UIGameController.Instance != null) UIGameController.Instance.RefreshRoomUI();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReady(bool ready) { IsReady = ready; }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_JoinTeam(int team)
    {
        int equipoAnterior = TeamIndex;
        TeamIndex = team;
        IsReady   = false;

        // ── Recompactar el equipo anterior ───────────────────────────────────
        // Cuando alguien deja un equipo, los SeatIndex del resto pueden quedar
        // con huecos (ej: 0, 2 en vez de 0, 1) rompiendo los turnos del podio.
        if (equipoAnterior != 0 && equipoAnterior != team)
        {
            var miembrosViejos = new System.Collections.Generic.List<PlayerNetworkData>();
            foreach (var player in Runner.ActivePlayers)
            {
                var data = Runner.GetPlayerObject(player)?.GetComponent<PlayerNetworkData>();
                if (data != null && data != this && data.TeamIndex == equipoAnterior)
                    miembrosViejos.Add(data);
            }
            // Ordenar y reasignar 0, 1, 2... sin huecos
            miembrosViejos.Sort((a, b) => a.SeatIndex.CompareTo(b.SeatIndex));
            for (int i = 0; i < miembrosViejos.Count; i++)
                miembrosViejos[i].SeatIndex = i;
        }

        // ── Asignar seat en el nuevo equipo ──────────────────────────────────
        int ocupados = 0;
        foreach (var player in Runner.ActivePlayers)
        {
            var data = Runner.GetPlayerObject(player)?.GetComponent<PlayerNetworkData>();
            if (data != null && data != this && data.TeamIndex == team)
                ocupados++;
        }
        SeatIndex = ocupados;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetName(string name) { PlayerName = name; }

    private void ApplySkinIfNeeded()
    {
        if (skinSelector == null) return;
        if (_lastAppliedSkinIndex == SkinIndex) return;

        skinSelector.SetSkinIndex(SkinIndex);
        _lastAppliedSkinIndex = SkinIndex;
    }

}