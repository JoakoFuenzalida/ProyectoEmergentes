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

    [SerializeField] private Transform spawnPoint1;
    [SerializeField] private Transform spawnPoint2;

    [Header("Network")]
    [SerializeField] private NetworkObject playerPrefab;

    public NetworkRunner Runner { get; private set; }

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
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(false);
    }

    // ── Botones ────────────────────────────────────────────────────

    public async void OnClickCreateRoom()
    {
        string code = roomCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
            code = GenerateRoomCode();
        roomCodeInput.text = code;
        SetStatus($"Creando sala: {code}...");
        SetButtonsInteractable(false);
        await StartFusionGame(GameMode.Host, code);
    }

    public async void OnClickJoinRoom()
    {
        string code = roomCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Ingresa un código de sala.");
            return;
        }
        SetStatus($"Uniéndose a sala: {code}...");
        SetButtonsInteractable(false);
        await StartFusionGame(GameMode.Client, code);
    }

    public void OnClickStartGame()
    {
        Debug.Log("--- RASTREO: 1. Botón presionado ---");
        
        if (Runner == null || !Runner.IsServer) 
        {
            Debug.LogWarning("RASTREO: Falló. No hay Runner o NO soy el servidor.");
            return;
        }

        Debug.Log("RASTREO: 2. Soy el servidor y tengo autoridad.");

        if (Runner.SessionInfo.PlayerCount >= MIN_PLAYERS)
        {
            Debug.Log("RASTREO: 3. Hay suficientes jugadores.");
            
            if (GameStateManager.Instance != null)
            {
                Debug.Log("RASTREO: 4. El GameStateManager existe. Llamando a StartGame()...");
                GameStateManager.Instance.StartGame();
            }
            else 
            {
                Debug.LogError("RASTREO: ¡ERROR! GameStateManager.Instance es NULL. El script no está en la escena o no se inició.");
            }
        }
        else
        {
            Debug.LogWarning($"RASTREO: Faltan jugadores. Solo hay {Runner.SessionInfo.PlayerCount}.");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_FinalizarLobby()
    {
        // Desactivar los paneles que tapan la vista
        if (roomPanel != null) roomPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);

        // Bloquear el cursor para que el POV del PlayerController funcione
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("Lobby cerrado y mouse bloqueado para todos los jugadores.");
    }

    // ── Red ────────────────────────────────────────────────────────

    private async Task StartFusionGame(GameMode mode, string roomCode)
    {
        if (Runner != null)
        {
            await Runner.Shutdown();
            Runner = null;
        }

        Runner = gameObject.AddComponent<NetworkRunner>();
        Runner.ProvideInput = true;
        Runner.AddCallbacks(this);

        // --- NUEVO: Empaquetar la escena al estilo Fusion 2 ---
        var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        
        if (sceneRef.IsValid) 
        {
            // Se añade la escena al paquete de información
            sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Additive); 
        }
        // -------------------------------------------------------

        var result = await Runner.StartGame(new StartGameArgs
        {
            GameMode     = mode,
            SessionName  = roomCode,
            PlayerCount  = MAX_PLAYERS,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
            Scene        = sceneInfo // ¡Aquí le pasamos el paquete ya armado!
        });

        if (result.Ok)
        {
            SetStatus(mode == GameMode.Host ? $"Sala creada: {roomCode}" : $"Unido a sala: {roomCode}");
            lobbyPanel.SetActive(false);
            roomPanel.SetActive(true);
            UpdatePlayerCountUI();
            if (startGameButton != null)
                startGameButton.gameObject.SetActive(Runner.IsServer);
            
            // Asignar el equipo asegurándonos de que el TeamAssigner exista
            if (TeamAssigner.Instance != null)
            {
                TeamAssigner.Instance.AssignTeam(Runner.LocalPlayer);
            }
            else
            {
                Debug.LogWarning("RASTREO: Falta el componente TeamAssigner en la escena.");
            }
        }
        else
        {
            SetStatus($"Error: {result.ShutdownReason}");
            SetButtonsInteractable(true);
        }
    }

    // ── INetworkRunnerCallbacks ────────────────────────────────────

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
{
    Debug.Log($"[RoomManager] Jugador entró: {player}");
    UpdatePlayerCountUI();

    // Solo el Servidor (Host) tiene el poder de spawnear objetos
    if (runner.IsServer && playerPrefab != null)
    {
        // SEGURIDAD: Si olvidaste asignar los puntos en el Inspector, avisar en consola
        if (spawnPoint1 == null || spawnPoint2 == null)
        {
            Debug.LogError("¡Mita! Te falta arrastrar los SpawnPoints al RoomManager en el Inspector.");
            return;
        }

        // Decidir el punto de spawn. 
        // Si es el primero entra al 1, si no, al 2.
        int playerIndex = runner.SessionInfo.PlayerCount - 1;
        
        // El comando 'index % 2' hace que si entra un 3ero, vuelva al lugar 1 y no tire error
        Transform targetSpawn = (playerIndex % 2 == 0) ? spawnPoint1 : spawnPoint2;

        Debug.Log($"Spawneando jugador {player.PlayerId} en {targetSpawn.name}");

        // El comando mágico que crea al jugador en la red
        runner.Spawn(playerPrefab, targetSpawn.position, targetSpawn.rotation, player);
    }
}

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[RoomManager] Jugador salió: {player}");
        UpdatePlayerCountUI();
        TeamAssigner.Instance.RemovePlayer(player);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        SetStatus($"Desconectado: {shutdownReason}");
        lobbyPanel.SetActive(true);
        roomPanel.SetActive(false);
        SetButtonsInteractable(true);
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(false);
    }

    public void OnConnectedToServer(NetworkRunner runner)    => SetStatus("Conectado.");
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
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) => SetStatus($"Desconectado: {reason}");
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    // ── Helpers ────────────────────────────────────────────────────

    private void UpdatePlayerCountUI()
    {
        if (Runner == null || playerCountText == null) return;
        int current = Runner.SessionInfo.PlayerCount;
        playerCountText.text = $"Jugadores: {current}/{MAX_PLAYERS}  (mínimo {MIN_PLAYERS})";
        if (startGameButton != null && Runner.IsServer)
            startGameButton.interactable = current >= MIN_PLAYERS;
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
        for (int i = 0; i < 4; i++)
            code[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        return new string(code);
    }
}