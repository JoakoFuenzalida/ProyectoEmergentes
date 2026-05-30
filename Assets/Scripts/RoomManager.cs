using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;
using TMPro;
using System.Threading.Tasks;

public class RoomManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static RoomManager Instance { get; private set; }

    private const int MAX_PLAYERS = 8;
    private const int MIN_PLAYERS = 2;

    [Header("UI References")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private TMP_Text       statusText;
    [SerializeField] private TMP_Text       playerCountText;
    [SerializeField] private Button         createRoomButton;
    [SerializeField] private Button         joinRoomButton;
    [SerializeField] private Button         startGameButton;
    [SerializeField] private GameObject     lobbyPanel;
    [SerializeField] private GameObject     roomPanel;

    [Header("Spawns")]
    [SerializeField] private Transform spawnPoint1;
    [SerializeField] private Transform spawnPoint2;

    [Header("Network")]
    [SerializeField] private NetworkObject playerPrefab;

    public NetworkRunner Runner { get; private set; }

    private NetworkSceneManagerDefault _sceneManager;
    private bool _isConnecting = false;
    private readonly List<int> _availableSkinIndices = new List<int>(MAX_PLAYERS);
    private readonly Dictionary<PlayerRef, int> _assignedSkins = new Dictionary<PlayerRef, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetStatus("Listo. Crea o únete a una sala.");
        
        if (startGameButton != null) startGameButton.gameObject.SetActive(false);
    }

    public async void OnClickCreateRoom()
    {
        string code = roomCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code)) code = GenerateRoomCode();
        
        // ESTA ES LA PARTE QUE YO HABÍA BORRADO POR ERROR
        roomCodeInput.text = code;
        SetStatus($"Creando sala: {code}...");
        SetButtonsInteractable(false);
        
        await StartFusionGame(GameMode.Host, code);
    }

    public async void OnClickJoinRoom()
    {
        string code = roomCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code)) { SetStatus("Ingresa un código de sala."); return; }
        
        SetStatus($"Buscando sala: {code}..."); // Cambiado de 'Uniéndose' a 'Buscando'
        SetButtonsInteractable(false);
        
        // Agregamos un pequeño delay de seguridad para asegurar que el Host ya registró la sala
        await Task.Delay(500); 
        await StartFusionGame(GameMode.Client, code);
    }

    public void OnClickStartGame()
    {
        if (Runner == null || !Runner.IsServer) return;

        if (Runner.SessionInfo.PlayerCount >= MIN_PLAYERS)
        {
            if (GameStateManager.Instance != null) GameStateManager.Instance.StartGame();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_FinalizarLobby()
    {
        if (roomPanel != null) roomPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private async Task StartFusionGame(GameMode mode, string roomCode)
    {
        if (_isConnecting) return;
        _isConnecting = true;

        if (mode == GameMode.Host) ResetSkinPool();

        // Limpiar runner anterior si existe
        if (Runner != null)
        {
            await Runner.Shutdown();
            if (Runner != null) Destroy(Runner);
            Runner = null;
        }

        // Limpiar scene manager anterior para evitar duplicados
        if (_sceneManager != null)
        {
            Destroy(_sceneManager);
            _sceneManager = null;
        }

        Runner = gameObject.AddComponent<NetworkRunner>();
        Runner.ProvideInput = true;
        Runner.AddCallbacks(this);

        _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if (sceneRef.IsValid) sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Additive);

        var result = await Runner.StartGame(new StartGameArgs
        {
            GameMode     = mode,
            SessionName  = roomCode,
            PlayerCount  = MAX_PLAYERS,
            SceneManager = _sceneManager,
            Scene        = sceneInfo
        });

        _isConnecting = false;

        if (result.Ok)
        {
            SetStatus(mode == GameMode.Host ? $"Sala creada: {roomCode}" : $"Unido a sala: {roomCode}");
            lobbyPanel.SetActive(false);
            roomPanel.SetActive(true);
            UpdatePlayerCountUI();

            if (TeamAssigner.Instance != null) TeamAssigner.Instance.AssignTeam(Runner.LocalPlayer);
        }
        else
        {
            SetStatus($"Error: {result.ShutdownReason}");
            SetButtonsInteractable(true);
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        UpdatePlayerCountUI();

        if (runner.IsServer && playerPrefab != null)
        {
            if (spawnPoint1 == null || spawnPoint2 == null) return;

            int playerIndex = runner.SessionInfo.PlayerCount - 1;
            Transform targetSpawn = (playerIndex % 2 == 0) ? spawnPoint1 : spawnPoint2;

            NetworkObject networkPlayerObject = runner.Spawn(playerPrefab, targetSpawn.position, targetSpawn.rotation, player);
            
            // LÍNEA MÁGICA: Para que reconozca los nombres en las listas
            runner.SetPlayerObject(player, networkPlayerObject);

            AssignSkinToPlayer(player, networkPlayerObject);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        UpdatePlayerCountUI();
        if (TeamAssigner.Instance != null) TeamAssigner.Instance.RemovePlayer(player);
        ReleaseSkin(player);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        SetStatus($"Desconectado: {shutdownReason}");
        lobbyPanel.SetActive(true);
        roomPanel.SetActive(false);
        SetButtonsInteractable(true);
        if (startGameButton != null) startGameButton.gameObject.SetActive(false);
    }

    // CORRECCIÓN DE ALERTAS AMARILLAS EN UNITY
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) => SetStatus("Conectado.");
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) => SetStatus($"Desconectado: {reason}");
    
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) => SetStatus($"Conexión fallida: {reason}");
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    private void UpdatePlayerCountUI()
    {
        if (Runner == null || playerCountText == null) return;
        int current = Runner.SessionInfo.PlayerCount;
        playerCountText.text = $"Jugadores: {current}/{MAX_PLAYERS}  (mínimo {MIN_PLAYERS})";
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log($"[RoomManager] {msg}");
    }

    private void SetButtonsInteractable(bool value)
    {
        if (createRoomButton) createRoomButton.interactable = value;
        if (joinRoomButton)   joinRoomButton.interactable   = value;
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        char[] code = new char[4];
        for (int i = 0; i < 4; i++) code[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        return new string(code);
    }

    private void ResetSkinPool()
    {
        _availableSkinIndices.Clear();
        _assignedSkins.Clear();
        for (int i = 0; i < MAX_PLAYERS; i++) _availableSkinIndices.Add(i);
    }

    private void AssignSkinToPlayer(PlayerRef player, NetworkObject playerObject)
    {
        var data = playerObject.GetComponent<PlayerNetworkData>();
        if (data == null) return;

        if (_assignedSkins.TryGetValue(player, out int existingSkin))
        {
            data.SkinIndex = existingSkin;
            return;
        }

        int skinIndex = TakeRandomSkinIndex();
        _assignedSkins[player] = skinIndex;
        data.SkinIndex = skinIndex;
    }

    private int TakeRandomSkinIndex()
    {
        if (_availableSkinIndices.Count == 0)
        {
            Debug.LogWarning("[RoomManager] Sin skins disponibles, se reutilizará un índice.");
            return UnityEngine.Random.Range(0, MAX_PLAYERS);
        }

        int pick = UnityEngine.Random.Range(0, _availableSkinIndices.Count);
        int skinIndex = _availableSkinIndices[pick];
        _availableSkinIndices.RemoveAt(pick);
        return skinIndex;
    }

    private void ReleaseSkin(PlayerRef player)
    {
        if (!_assignedSkins.TryGetValue(player, out int skinIndex)) return;

        _assignedSkins.Remove(player);
        if (!_availableSkinIndices.Contains(skinIndex))
            _availableSkinIndices.Add(skinIndex);
    }
}