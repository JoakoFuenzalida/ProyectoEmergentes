using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement; 
using System.Collections; 
using Fusion;

public class UIGameController : MonoBehaviour
{
    public static UIGameController Instance { get; private set; }

    [Header("Paneles Principales")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject roomPanel;

    [Header("Lobby Inicial: Edición de Nombre")]
    [SerializeField] private TMP_Text textoNombreJugadorLobby;       
    [SerializeField] private TMP_InputField inputEdicionNombreLobby; 

    [Header("RoomPanel: Listas y Equipos")]
    [SerializeField] private TMP_Text textoListaSinEquipo;      
    [SerializeField] private TMP_Text textoListaEquipoA;        
    [SerializeField] private TMP_Text textoListaEquipoB;
    [SerializeField] private TMP_Text textoNombreEquipoA;    
    [SerializeField] private TMP_Text textoNombreEquipoB;
    [SerializeField] private TMP_Text textoMiNombre; 
    [SerializeField] private TMP_InputField inputEdicionEquipoA; 
    [SerializeField] private TMP_InputField inputEdicionEquipoB;
    [SerializeField] private GameObject btnArrancarPartida;     
    [SerializeField] private GameObject btnListo;
    [SerializeField] private TMP_Text textoCodigoRoom; // Solo mostrará el código puro

    [Header("Set 3D: Pantallas de Equipos")]
    [SerializeField] private TMP_Text pantalla3DEquipoA;
    [SerializeField] private TMP_Text pantalla3DEquipoB;

    [Header("Set 3D: Pantallas de Puntaje")]
    [SerializeField] private TMP_Text pantallaPuntosRonda;  
    [SerializeField] private TMP_Text pantallaPuntosTotalA; 
    [SerializeField] private TMP_Text pantallaPuntosTotalB; 

    [Header("HUD 2D: Mi Puntaje y Turno")]
    [SerializeField] private TMP_Text PuntosRondaPozo; 
    [SerializeField] private TMP_Text MisPuntos;       
    [SerializeField] private TMP_Text textoTurnoHUD; 
    [SerializeField] private GameObject labelPuntosRonda;
    [SerializeField] private TMP_Text valorPuntosRonda;   
    [SerializeField] private GameObject labelMisPuntos;   
    [SerializeField] private TMP_Text valorMisPuntos;     

    [Header("Paneles Juego")]
    [SerializeField] private GameObject panelPregunta;
    [SerializeField] private GameObject panelRespuestas;

    [Header("Elementos de Respuesta")]
    [SerializeField] private GameObject panelCountdown; 
    [SerializeField] private TMP_InputField inputRespuesta;
    [SerializeField] private TMP_Text textoPreguntaPrincipal;

    [Header("Tablero de 8 Casillas")]
    [SerializeField] private TMP_Text[] casillasRespuestas = new TMP_Text[8];
    [SerializeField] private TMP_Text[] casillasPuntos = new TMP_Text[8];

    [Header("Menú de Pausa y Strikes")] 
    [SerializeField] private GameObject panelPausa;
    [SerializeField] private GameObject panelStrikeGrande; 
    [SerializeField] private TMP_Text textoStrikeGrande;   
    [SerializeField] private GameObject[] iconosX = new GameObject[3]; 

    private float localTimer = 0f;
    private bool isCounting = false;
    private bool isPaused = false;
    private CursorLockMode previousLockMode;
    private bool previousCursorVisible;

    private void Awake() 
    { 
        if (Instance == null) Instance = this; 
    }

    private void Start()
    {
        if (inputRespuesta != null) inputRespuesta.onSubmit.AddListener(OnSubmitAnswer);
        if (panelStrikeGrande != null) panelStrikeGrande.SetActive(false);
        
        if (inputEdicionNombreLobby != null) inputEdicionNombreLobby.gameObject.SetActive(false);
        if (inputEdicionEquipoA != null) inputEdicionEquipoA.gameObject.SetActive(false);
        if (inputEdicionEquipoB != null) inputEdicionEquipoB.gameObject.SetActive(false);

        if (textoNombreJugadorLobby != null && string.IsNullOrWhiteSpace(textoNombreJugadorLobby.text))
        {
            textoNombreJugadorLobby.text = "Jugador";
        }

        HandleStateChanged(GameStateManager.GameState.WaitingForPlayers);
    }

    private void OnEnable()  
    { 
        GameStateManager.OnStateChangedEvent += HandleStateChanged; 
        GameStateManager.OnErrorAddedEvent += HandleErrorAdded; 
        GameStateManager.OnTemporaryStrikeEvent += HandleTemporaryStrike; 
        GameStateManager.OnEvaluationStateChangedEvent += HandleEvaluationStateChanged; 
        GameStateManager.OnTeamNamesUpdatedEvent += RefreshRoomUI;
        TurnManager.OnTurnChangedEvent += HandleTurnChanged;
    }

    private void OnDisable() 
    { 
        GameStateManager.OnStateChangedEvent -= HandleStateChanged; 
        GameStateManager.OnErrorAddedEvent -= HandleErrorAdded; 
        GameStateManager.OnTemporaryStrikeEvent -= HandleTemporaryStrike; 
        GameStateManager.OnEvaluationStateChangedEvent -= HandleEvaluationStateChanged; 
        GameStateManager.OnTeamNamesUpdatedEvent -= RefreshRoomUI;
        TurnManager.OnTurnChangedEvent -= HandleTurnChanged;
    }

    private void HandleTurnChanged(PlayerRef nuevoJugadorActivo)
    {
        ActualizarControlInput();
    }

    public void Btn_AbrirEdicionNombreLobby()
    {
        if (inputEdicionNombreLobby != null && textoNombreJugadorLobby != null)
        {
            inputEdicionNombreLobby.gameObject.SetActive(true);
            inputEdicionNombreLobby.text = textoNombreJugadorLobby.text;
            inputEdicionNombreLobby.Select();
            inputEdicionNombreLobby.ActivateInputField();
        }
    }

    public void OnGuardarNombreLobby()
    {
        if (inputEdicionNombreLobby == null) return;
        string nuevoNombre = inputEdicionNombreLobby.text;

        if (!string.IsNullOrWhiteSpace(nuevoNombre) && textoNombreJugadorLobby != null)
        {
            textoNombreJugadorLobby.text = nuevoNombre;
        }
        inputEdicionNombreLobby.gameObject.SetActive(false);
    }

    public string GetPlayerNameInput() 
    { 
        return textoNombreJugadorLobby != null ? textoNombreJugadorLobby.text : "Jugador"; 
    }

    public void RefreshRoomUI()
    {
        if (GameStateManager.Instance == null || GameStateManager.Instance.Runner == null) return;

        if (textoCodigoRoom != null) 
            textoCodigoRoom.text = GameStateManager.Instance.Runner.SessionInfo.Name;

        if (pantallaPuntosRonda != null) pantallaPuntosRonda.text = GameStateManager.Instance.RoundScore.ToString();
        if (pantallaPuntosTotalA != null) pantallaPuntosTotalA.text = GameStateManager.Instance.ScoreA.ToString();
        if (pantallaPuntosTotalB != null) pantallaPuntosTotalB.text = GameStateManager.Instance.ScoreB.ToString();
        
        if (textoNombreEquipoA != null) textoNombreEquipoA.text = GameStateManager.Instance.NombreEquipoA.ToString();
        if (textoNombreEquipoB != null) textoNombreEquipoB.text = GameStateManager.Instance.NombreEquipoB.ToString();
        if (pantalla3DEquipoA != null) pantalla3DEquipoA.text = GameStateManager.Instance.NombreEquipoA.ToString();
        if (pantalla3DEquipoB != null) pantalla3DEquipoB.text = GameStateManager.Instance.NombreEquipoB.ToString();

        if (valorPuntosRonda != null) valorPuntosRonda.text = GameStateManager.Instance.RoundScore.ToString();

        var myData = GetMyPlayerData();
        if (myData != null)
        {
            if (textoMiNombre != null) textoMiNombre.text = myData.PlayerName.ToString();
            if (valorMisPuntos != null)
            {
                int puntos = (myData.TeamIndex == 1) ? GameStateManager.Instance.ScoreA : GameStateManager.Instance.ScoreB;
                valorMisPuntos.text = puntos.ToString();
            }
        }

        string unassigned = ""; string teamA = ""; string teamB = "";
        bool todosListos = true; int jugadoresConEquipo = 0;

        foreach (var playerRef in GameStateManager.Instance.Runner.ActivePlayers)
        {
            var data = GetPlayerData(playerRef);
            if (data == null) continue;

            string status = data.IsReady ? "<color=green>[OK]</color>" : "<color=red>[...]</color>";
            string nombre = string.IsNullOrWhiteSpace(data.PlayerName.ToString()) ? "Jugador" : data.PlayerName.ToString();
            string line = $"{nombre} {status}\n";

            if (data.TeamIndex == 0) unassigned += line;
            else if (data.TeamIndex == 1) { teamA += line; jugadoresConEquipo++; }
            else if (data.TeamIndex == 2) { teamB += line; jugadoresConEquipo++; }

            if (!data.IsReady || data.TeamIndex == 0) todosListos = false;
        }

        if (textoListaSinEquipo != null) textoListaSinEquipo.text = unassigned;
        if (textoListaEquipoA != null) textoListaEquipoA.text = teamA;
        if (textoListaEquipoB != null) textoListaEquipoB.text = teamB;
        
        if (GameStateManager.Instance.Runner.IsServer && btnArrancarPartida != null)
        {
            btnArrancarPartida.SetActive(todosListos && jugadoresConEquipo >= 2);
        }
    }

    public void Btn_AbrirEdicionEquipoA() 
    { 
        if (inputEdicionEquipoA != null && GameStateManager.Instance != null) 
        { 
            inputEdicionEquipoA.gameObject.SetActive(true); 
            inputEdicionEquipoA.text = GameStateManager.Instance.NombreEquipoA.ToString(); 
            inputEdicionEquipoA.Select(); 
            inputEdicionEquipoA.ActivateInputField();
        } 
    }

    public void Btn_AbrirEdicionEquipoB() 
    { 
        if (inputEdicionEquipoB != null && GameStateManager.Instance != null) 
        { 
            inputEdicionEquipoB.gameObject.SetActive(true); 
            inputEdicionEquipoB.text = GameStateManager.Instance.NombreEquipoB.ToString(); 
            inputEdicionEquipoB.Select(); 
            inputEdicionEquipoB.ActivateInputField();
        } 
    }

    public void OnGuardarNombreEquipoA()
    {
        if (inputEdicionEquipoA == null) return;
        string nuevoNombre = inputEdicionEquipoA.text;
        if (!string.IsNullOrWhiteSpace(nuevoNombre) && GameStateManager.Instance != null) 
            GameStateManager.Instance.RPC_SetTeamName(1, nuevoNombre);
        if (inputEdicionEquipoA != null) inputEdicionEquipoA.gameObject.SetActive(false);
    }

    public void OnGuardarNombreEquipoB()
    {
        if (inputEdicionEquipoB == null) return;
        string nuevoNombre = inputEdicionEquipoB.text;
        if (!string.IsNullOrWhiteSpace(nuevoNombre) && GameStateManager.Instance != null) 
            GameStateManager.Instance.RPC_SetTeamName(2, nuevoNombre);
        if (inputEdicionEquipoB != null) inputEdicionEquipoB.gameObject.SetActive(false);
    }

    public void Btn_ToggleReady() { var myData = GetMyPlayerData(); if (myData != null) myData.RPC_SetReady(!myData.IsReady); }
    public void Btn_UnirseEquipoA() { GetMyPlayerData()?.RPC_JoinTeam(1); }
    public void Btn_UnirseEquipoB() { GetMyPlayerData()?.RPC_JoinTeam(2); }

    private PlayerNetworkData GetPlayerData(PlayerRef player) 
    { 
        var networkObj = GameStateManager.Instance.Runner.GetPlayerObject(player); 
        return networkObj != null ? networkObj.GetComponent<PlayerNetworkData>() : null; 
    }

    private PlayerNetworkData GetMyPlayerData() 
    { 
        return (GameStateManager.Instance == null || GameStateManager.Instance.Runner == null) ? null : GetPlayerData(GameStateManager.Instance.Runner.LocalPlayer); 
    }

    private void HandleTemporaryStrike() { if (textoStrikeGrande != null) textoStrikeGrande.text = "X"; StartCoroutine(MostrarStrikeTemporal()); }

    private void HandleErrorAdded(int totalErrores) 
    { 
        if (totalErrores > 0) 
        { 
            if (textoStrikeGrande != null) 
            { 
                string equis = ""; 
                for (int i = 0; i < totalErrores; i++) equis += "X  "; 
                textoStrikeGrande.text = equis.Trim(); 
            } 
            StartCoroutine(MostrarStrikeTemporal()); 
        } 
        for (int i = 0; i < iconosX.Length; i++) if (iconosX[i] != null) iconosX[i].SetActive(i < totalErrores); 
    }

    private IEnumerator MostrarStrikeTemporal() { if (panelStrikeGrande != null) { panelStrikeGrande.SetActive(true); yield return new WaitForSeconds(1.5f); panelStrikeGrande.SetActive(false); } }

    private void HandleEvaluationStateChanged(bool isEvaluating) 
    { 
        if (isEvaluating) { if (inputRespuesta) inputRespuesta.gameObject.SetActive(false); Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; } 
        else if (GameStateManager.Instance != null) HandleStateChanged(GameStateManager.Instance.CurrentState); 
    }

    private void HandleStateChanged(GameStateManager.GameState newState)
    {
        if (isPaused) TogglePauseMenu(); 
        bool isServerReady = (GameStateManager.Instance != null && GameStateManager.Instance.Object != null && GameStateManager.Instance.Object.IsValid);
        if (isServerReady && GameStateManager.Instance.IsEvaluating) return; 

        switch (newState)
        {
            case GameStateManager.GameState.Countdown:
                if (lobbyPanel) lobbyPanel.SetActive(false); if (roomPanel) roomPanel.SetActive(false);
                if (panelPregunta) panelPregunta.SetActive(true); if (panelCountdown) panelCountdown.SetActive(true);
                if (panelRespuestas) panelRespuestas.SetActive(true); if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
                if (labelPuntosRonda != null) labelPuntosRonda.SetActive(true); if (valorPuntosRonda != null) valorPuntosRonda.gameObject.SetActive(true);
                if (labelMisPuntos != null) labelMisPuntos.SetActive(true); if (valorMisPuntos != null) valorMisPuntos.gameObject.SetActive(true);
                if (textoTurnoHUD != null) textoTurnoHUD.gameObject.SetActive(true);
                if (textoPreguntaPrincipal != null && GameStateManager.Instance != null) textoPreguntaPrincipal.text = GameStateManager.Instance.PreguntaActual;
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; localTimer = 5.0f; isCounting = true;
                break;

            case GameStateManager.GameState.WaitingForBuzzer:
            case GameStateManager.GameState.RoundEnd:
            case GameStateManager.GameState.GameOver:
                isCounting = false; if (panelCountdown) panelCountdown.SetActive(false);
                if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                break;

            case GameStateManager.GameState.TypingAnswer:
            case GameStateManager.GameState.Playing:
            case GameStateManager.GameState.Stealing:
                isCounting = false; 
                ActualizarControlInput(); 
                break;

            case GameStateManager.GameState.WaitingForPlayers:
                isCounting = false; if (lobbyPanel) lobbyPanel.SetActive(true);
                if (panelPregunta) panelPregunta.SetActive(false); if (panelRespuestas) panelRespuestas.SetActive(false);
                if (panelCountdown) panelCountdown.SetActive(false); if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
                if (labelPuntosRonda != null) labelPuntosRonda.SetActive(false); if (valorPuntosRonda != null) valorPuntosRonda.gameObject.SetActive(false);
                if (labelMisPuntos != null) labelMisPuntos.SetActive(false); if (valorMisPuntos != null) valorMisPuntos.gameObject.SetActive(false);
                if (textoTurnoHUD != null) textoTurnoHUD.gameObject.SetActive(false);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                break;
        }
    }

    private void ActualizarControlInput()
    {
        if (GameStateManager.Instance == null || GameStateManager.Instance.Runner == null || 
            GameStateManager.Instance.Object == null || !GameStateManager.Instance.Object.IsValid) return;

        bool esMiTurno = false;
        string nombreTurno = "Calculando...";
        var currentState = GameStateManager.Instance.CurrentState;
        var myPlayer = GameStateManager.Instance.Runner.LocalPlayer;

        if (currentState == GameStateManager.GameState.TypingAnswer)
        {
            esMiTurno = (GameStateManager.Instance.BuzzerWinnerId == myPlayer.PlayerId);
            var dataWinner = GetPlayerData(PlayerRef.FromIndex(GameStateManager.Instance.BuzzerWinnerId));
            if (dataWinner != null) nombreTurno = dataWinner.PlayerName.ToString();
        }
        else if (currentState == GameStateManager.GameState.Playing || currentState == GameStateManager.GameState.Stealing)
        {
            // Leemos nuestros datos locales
            var myData = GetMyPlayerData();
            string myTeamStr = (myData != null && myData.TeamIndex == 2) ? "B" : "A";
            string activeTeamStr = GameStateManager.Instance.ActiveTeam.ToString();

            // Verificamos el servidor (TurnManager)
            if (TurnManager.Instance != null && TurnManager.Instance.ActivePlayer != PlayerRef.None)
            {
                esMiTurno = (TurnManager.Instance.ActivePlayer == myPlayer);
                var dataActive = GetPlayerData(TurnManager.Instance.ActivePlayer);
                if (dataActive != null) nombreTurno = dataActive.PlayerName.ToString();
            }
            else 
            {
                // RED DE EMERGENCIA: Si el TurnManager falló, forzamos el turno si nuestro equipo está jugando
                if (activeTeamStr.Contains(myTeamStr)) 
                {
                    esMiTurno = true;
                    nombreTurno = myData != null ? myData.PlayerName.ToString() : "Tú";
                }
                else 
                {
                    nombreTurno = "Esperando rival...";
                }
            }
        }

        if (textoTurnoHUD != null) textoTurnoHUD.text = "Turno de: " + nombreTurno;

        // Encendemos el Input agresivamente
        if (esMiTurno && (currentState == GameStateManager.GameState.TypingAnswer || currentState == GameStateManager.GameState.Playing || currentState == GameStateManager.GameState.Stealing))
        {
            if (inputRespuesta && !inputRespuesta.gameObject.activeSelf) 
            { 
                inputRespuesta.gameObject.SetActive(true); 
                inputRespuesta.Select(); 
                inputRespuesta.ActivateInputField(); 
            }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        else if (!esMiTurno)
        {
            if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePauseMenu();

        // --- NUEVO: DETECCIÓN FORZADA DE ENTER ---
        if (inputRespuesta != null && inputRespuesta.gameObject.activeInHierarchy)
        {
            // Si presionas Enter y hay texto, enviamos manualmente
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!string.IsNullOrWhiteSpace(inputRespuesta.text))
                {
                    OnSubmitAnswer(inputRespuesta.text);
                }
            }

            // Mantenemos el foco si el mouse hizo clic fuera por error
            if (!inputRespuesta.isFocused && !isPaused)
            {
                inputRespuesta.ActivateInputField();
            }
        }
        // -----------------------------------------

        if (isCounting && panelCountdown != null) 
        { 
            localTimer -= Time.deltaTime; 
            TMP_Text texto = panelCountdown.GetComponent<TMP_Text>(); 
            if (texto != null) { int segundos = Mathf.CeilToInt(localTimer); texto.text = segundos <= 0 ? "¡PREPÁRATE!" : segundos.ToString(); } 
        }

        bool isServerReady = (GameStateManager.Instance != null && GameStateManager.Instance.Object != null && GameStateManager.Instance.Object.IsValid);

        if (isServerReady && !isPaused)
        {
            var st = GameStateManager.Instance.CurrentState;
            // Quitamos el llamado constante en Update para no saturar, 
            // ya que ahora los eventos (HandleStateChanged y HandleTurnChanged) se encargan de esto.
        }

        // Actualización visual del tablero (Esto se queda igual)
        if (isServerReady && panelRespuestas != null && panelRespuestas.activeSelf) 
        { 
            int mask = GameStateManager.Instance.RevealedAnswersMask; 
            string[] correctas = GameStateManager.Instance.RespuestasValidas; 
            int[] puntos = GameStateManager.Instance.PuntosRespuestas; 
            
            for (int i = 0; i < 8; i++) 
            { 
                if (casillasRespuestas[i] != null && casillasPuntos[i] != null) 
                { 
                    if (i < correctas.Length && (mask & (1 << i)) != 0) { casillasRespuestas[i].text = correctas[i]; casillasPuntos[i].text = puntos[i].ToString(); } 
                    else { casillasRespuestas[i].text = $"--- {i + 1} ---"; casillasPuntos[i].text = "--"; } 
                } 
            } 
        }
    }

    private void OnSubmitAnswer(string respuesta) 
    { 
        if (string.IsNullOrWhiteSpace(respuesta) || GameStateManager.Instance == null || GameStateManager.Instance.Runner == null) return;

        // Ya hicimos todo el trabajo pesado para mostrar el InputField.
        // Si el jugador llegó hasta aquí y pudo escribir, tiene permiso.
        
        // 1. Enviamos la respuesta al servidor
        GameStateManager.Instance.RPC_SubmitAnswer(respuesta, GameStateManager.Instance.Runner.LocalPlayer.PlayerId); 
        
        // 2. Limpiamos el texto inmediatamente
        if (inputRespuesta != null)
        {
            inputRespuesta.text = ""; 
            
            // 3. Ocultamos el input temporalmente mientras el servidor evalúa (1.5s)
            // Esto evita que mandes "mantequilla" 3 veces seguidas por accidente.
            inputRespuesta.gameObject.SetActive(false);
        }
    }

    public void TogglePauseMenu() 
    { 
        if (panelPausa == null) return; 
        isPaused = !isPaused; panelPausa.SetActive(isPaused); 
        if (isPaused) { previousLockMode = Cursor.lockState; previousCursorVisible = Cursor.visible; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } 
        else { Cursor.lockState = previousLockMode; Cursor.visible = previousCursorVisible; } 
    }

    public void Btn_SalirAlLobby() { if (GameStateManager.Instance != null && GameStateManager.Instance.Runner != null) GameStateManager.Instance.Runner.Shutdown(); SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
    
    public void Btn_SalirDelJuego() 
    { 
        Application.Quit(); 
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; 
#endif
    }
}