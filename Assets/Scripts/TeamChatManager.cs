using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Fusion;
using TMPro;

public class TeamChatManager : NetworkBehaviour
{
    public static TeamChatManager Instance { get; private set; }

    [Header("UI Chat — asignar cuando se active")]
    [SerializeField] private GameObject     chatPanel;
    [SerializeField] private TMP_Text       chatHistoryText;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button         sendButton;

    private List<string> _chatHistory = new List<string>();
    private const int MAX_MESSAGES = 50;

    public override void Spawned()
    {
        Instance = this;
        if (chatPanel != null)
            chatPanel.SetActive(false);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    // TODO: Chat — implementar cuando se active
    public void SendTeamMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        string team = TeamAssigner.Instance.LocalTeam;
        // TODO: Chat — RPC_ReceiveTeamMessage(Runner.LocalPlayer, message, team);
        Debug.Log($"[TeamChat] TODO: Enviar '{message}' al equipo {team}");
    }

    public void OnClickSend()
    {
        if (chatInputField == null) return;
        string msg = chatInputField.text.Trim();
        if (string.IsNullOrEmpty(msg)) return;
        SendTeamMessage(msg);
        chatInputField.text = string.Empty;
        chatInputField.ActivateInputField();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_ReceiveTeamMessage(PlayerRef sender, string message, string team)
    {
        // TODO: Chat — descomentar al activar
        /*
        string localTeam = TeamAssigner.Instance.LocalTeam;
        if (localTeam != team) return;
        string formatted = $"[Jugador {sender.PlayerId}]: {message}";
        _chatHistory.Add(formatted);
        if (_chatHistory.Count > MAX_MESSAGES)
            _chatHistory.RemoveAt(0);
        if (chatHistoryText != null)
            chatHistoryText.text = string.Join("\n", _chatHistory);
        */
        Debug.Log($"[TeamChat] TODO: Mensaje equipo {team}: {message}");
    }

    public void SetChatVisible(bool visible)
    {
        // TODO: Chat — if (chatPanel != null) chatPanel.SetActive(visible);
        Debug.Log($"[TeamChat] TODO: SetChatVisible({visible})");
    }
}