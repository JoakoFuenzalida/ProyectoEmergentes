using UnityEngine;
using TMPro;
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
    [SerializeField] private TMP_Text textoCodigoRoom;

    [Header("RoomPanel: Configuración de Partida")]
    [SerializeField] private TMP_Text   textoConfigRondas;    // muestra "Rondas: 5"
    [SerializeField] private TMP_Text   textoConfigTiempo;    // muestra "Turno: 30sg"
    [SerializeField] private GameObject btnEditarRondas;      // botón "Editar" rondas  (solo host)
    [SerializeField] private GameObject btnEditarTiempo;      // botón "Editar" tiempo  (solo host)
    [SerializeField] private GameObject panelOpcionesRondas;  // panel con opciones 1 / 3 / 5
    [SerializeField] private GameObject panelOpcionesTiempo;  // panel con opciones 10 / 15 / 30sg

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

    [Header("Tablero de 8 Casillas (HUD 2D)")]
    [SerializeField] private TMP_Text[] casillasRespuestas = new TMP_Text[8];
    [SerializeField] private TMP_Text[] casillasPuntos = new TMP_Text[8];

    [Header("Tablero 3D Family Feud")]
    [SerializeField] private GameObject tableroPanel3D;             // contenedor de textos 3D (hijo de la pantalla)
    [SerializeField] private TMP_Text[] tableroRespuestas3D = new TMP_Text[8]; // textos de respuestas
    [SerializeField] private TMP_Text[] tableroPuntos3D     = new TMP_Text[8]; // textos de puntos
    [SerializeField] private TMP_Text   tableroPregunta3D;          // texto de la pregunta (opcional)

    [Header("Temporizador de turno")]
    [SerializeField] private GameObject panelTiempoTurno;        // panel contenedor (se muestra/oculta completo)
    [SerializeField] private TMP_Text   textoTiempoTurno;        // número de segundos restantes
    [SerializeField] private UnityEngine.UI.Slider sliderTiempo; // barra de progreso (opcional)
    [SerializeField] private Color colorNormal  = Color.white;
    [SerializeField] private Color colorUrgente = Color.red;
    [SerializeField] private float umbralUrgente = 10f;

    [Header("Panel Puntos Equipo")]
    [SerializeField] private GameObject panelPuntosEquipo; // panel contenedor de mis puntos (se muestra/oculta completo)

    [Header("Panel Fin de Juego (Game Over)")]
    [SerializeField] private GameObject panelGameOver;
    [SerializeField] private TMP_Text   textoGanadorFinal;   // "Ganador: Equipo X" o "Empate!"
    [SerializeField] private TMP_Text   textoMarcadorFinal;  // puntos finales — "EquipoA: X pts  |  EquipoB: Y pts"
    [SerializeField] private TMP_Text   textoTablaEquipoA;   // tabla izquierda: jugadores Equipo A con aciertos
    [SerializeField] private TMP_Text   textoTablaEquipoB;   // tabla derecha:   jugadores Equipo B con aciertos

    [Header("Menú de Pausa y Strikes")]
    [SerializeField] private GameObject panelPausa;
    [SerializeField] private GameObject panelStrikeGrande;
    [SerializeField] private TMP_Text textoStrikeGrande;
    [SerializeField] private GameObject[] iconosX = new GameObject[3];

    [Header("Animador IA")]
    [SerializeField] private GameObject panelAnimador;
    [SerializeField] private TMP_Text   textoAnimador;
    [SerializeField] private TMP_Text   textoEstadoGeneracion; // opcional: muestra "Generando preguntas..."

    [Header("Audio UI")]
    [SerializeField] private AudioSource audioSourceUI;
    [SerializeField] private AudioClip   dingCorrectClip;  // Assets/Audio/dingCorrect.mp3
    [SerializeField] private AudioClip   wrongAnswerClip;  // arrastra el clip wrong-answer-buzzer aquí

    [Header("Start Screen")]
    [SerializeField] private GameObject panelStartScreen;
    [SerializeField] private TMP_Text   textoPresionaTecla;
    [SerializeField] private AudioSource musicaInicioSource;
    [SerializeField] private AudioClip  musicaInicioClip;
    [Range(0f, 1f)] [SerializeField] private float musicaInicioVolumen = 0.35f;
    [SerializeField] private float musicaInicioFadeSegundos = 2f;
    [Range(0f, 1f)] [SerializeField] private float musicaLobbyVolumen = 0.12f;
    [SerializeField] private float musicaLobbyFadeSegundos = 1.2f;

    [Header("Música de Juego")]
    [SerializeField] private AudioSource musicaJuegoSource;    // AudioSource para SongMainJuego
    [SerializeField] private AudioClip   musicaJuegoClip;      // SongMainJuego.mp3
    [Range(0f, 1f)] [SerializeField] private float musicaJuegoVolumen = 0.12f;
    [SerializeField] private AudioSource musicaSuspensoSource; // AudioSource para song10sg
    [SerializeField] private AudioClip   musicaSuspensoClip;   // song10sg.mp3
    [Range(0f, 1f)] [SerializeField] private float musicaSuspensoVolumen = 0.20f;

    private float localTimer = 0f;
    private bool isCounting = false;
    private int  _prevRevealedMask = 0; // detecta nuevas respuestas reveladas para el ding
    private bool isPaused = false;
    private bool _suspensoActivo = false;
    private bool _prevAnnouncingTurn = false;
    private bool _tableroMostrarPlaceholders = false; // false = slots vacíos, true = muestra "--- X ---"
    private CursorLockMode previousLockMode;
    private bool previousCursorVisible;

    // Bloquea el botón arrancar hasta que las preguntas IA estén listas
    private bool _preguntasListas = false;

    // ── BUG FIX: Guardamos si ES nuestro turno para el Update ─────
    private bool _esMiTurnoActual = false;
    // Reintento de buzzer: cuando se gana el buzzer, reintentar ActualizarControlInput
    // durante N frames para cubrir retrasos de sincronización de red
    private int _buzzerRetryFrames = 0;
    private bool _startScreenActive = false;
    private GameStateManager.GameState _queuedState = GameStateManager.GameState.WaitingForPlayers;
    private Coroutine _fadeMusicCoroutine;

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

        // Asegurar que el panel del animador empiece oculto — se muestra al iniciar partida
        if (panelAnimador != null) panelAnimador.SetActive(false);

        if (textoNombreJugadorLobby != null && string.IsNullOrWhiteSpace(textoNombreJugadorLobby.text))
            textoNombreJugadorLobby.text = "Jugador";

        _queuedState = GameStateManager.GameState.WaitingForPlayers;
        HandleStateChanged(GameStateManager.GameState.WaitingForPlayers);
        if (panelStartScreen != null) ActivarStartScreen();
    }

    private void OnEnable()
    {
        GameStateManager.OnStateChangedEvent           += HandleStateChanged;
        GameStateManager.OnErrorAddedEvent             += HandleErrorAdded;
        GameStateManager.OnTemporaryStrikeEvent        += HandleTemporaryStrike;
        GameStateManager.OnEvaluationStateChangedEvent += HandleEvaluationStateChanged;
        GameStateManager.OnTeamNamesUpdatedEvent       += RefreshRoomUI;
        GameStateManager.OnPreguntaActualizadaEvent    += HandlePreguntaActualizada;
        TurnManager.OnTurnChangedEvent                 += HandleTurnChanged;
        AnimadorIA.OnMensajeChanged                    += MostrarBurbujaAnimador;
        AnimadorIA.OnGenerandoPreguntas                += HandleGenerandoPreguntas;
        RoomManager.OnDisconnectedEvent                += HandleDisconnected;
        GameStateManager.OnConfigChangedEvent          += ActualizarConfigUI;
    }

    private void OnDisable()
    {
        GameStateManager.OnStateChangedEvent           -= HandleStateChanged;
        GameStateManager.OnErrorAddedEvent             -= HandleErrorAdded;
        GameStateManager.OnTemporaryStrikeEvent        -= HandleTemporaryStrike;
        GameStateManager.OnEvaluationStateChangedEvent -= HandleEvaluationStateChanged;
        GameStateManager.OnTeamNamesUpdatedEvent       -= RefreshRoomUI;
        GameStateManager.OnPreguntaActualizadaEvent    -= HandlePreguntaActualizada;
        TurnManager.OnTurnChangedEvent                 -= HandleTurnChanged;
        AnimadorIA.OnMensajeChanged                    -= MostrarBurbujaAnimador;
        AnimadorIA.OnGenerandoPreguntas                -= HandleGenerandoPreguntas;
        RoomManager.OnDisconnectedEvent                -= HandleDisconnected;
        GameStateManager.OnConfigChangedEvent          -= ActualizarConfigUI;
    }

    // ─── Disconnect ──────────────────────────────────────────────────

    /// <summary>
    /// Llamado cuando RoomManager termina el shutdown (salida intencional o kick del host).
    /// Resetea toda la UI del juego al estado de lobby inicial.
    /// </summary>
    private void HandleDisconnected()
    {
        // Detener timers locales y flags para evitar que el Update siga procesando
        isCounting       = false;
        _buzzerRetryFrames = 0;
        _esMiTurnoActual = false;

        // Forzar estado de lobby (oculta paneles de juego, muestra lobbyPanel)
        HandleStateChanged(GameStateManager.GameState.WaitingForPlayers);
    }

    // ─── Animador IA ─────────────────────────────────────────────────

    private void MostrarBurbujaAnimador(string mensaje)
    {
        if (panelAnimador == null || textoAnimador == null) return;
        StopCoroutine(nameof(LimpiarMensajeCoroutine));
        textoAnimador.text = mensaje;
        panelAnimador.SetActive(true);        // por si el padre estaba inactivo
        StartCoroutine(nameof(LimpiarMensajeCoroutine));
    }

    // Cuando la pregunta networked llega al cliente (puede ser más tarde que el cambio de estado)
    private void HandlePreguntaActualizada()
    {
        if (GameStateManager.Instance == null) return;
        var estado = GameStateManager.Instance.CurrentState;
        // Solo actualizar si estamos en una fase donde la pregunta debe verse
        if (estado == GameStateManager.GameState.Countdown  ||
            estado == GameStateManager.GameState.WaitingForBuzzer ||
            estado == GameStateManager.GameState.TypingAnswer ||
            estado == GameStateManager.GameState.Playing    ||
            estado == GameStateManager.GameState.Stealing)
        {
            string pregunta = GameStateManager.Instance.PreguntaActual;
            if (!string.IsNullOrEmpty(pregunta) && pregunta != "Cargando...")
            {
                if (textoPreguntaPrincipal != null) textoPreguntaPrincipal.text = pregunta;
                if (tableroPregunta3D     != null) tableroPregunta3D.text      = pregunta;
            }
        }
    }

    // Llamado directamente por AnimadorController (intro y mensajes del juego)
    // para mantener panel 2D sincronizado con las viñetas 3D
    public void ActualizarTextoAnimador(string mensaje)
    {
        if (textoAnimador != null) textoAnimador.text = mensaje;
        if (panelAnimador != null && !panelAnimador.activeSelf)
            panelAnimador.SetActive(true);
    }

    // No oculta el panel — solo borra el texto después de un rato para
    // que el panel de Martín quede limpio esperando el siguiente comentario
    private IEnumerator LimpiarMensajeCoroutine()
    {
        yield return new WaitForSeconds(9f);
        if (textoAnimador != null) textoAnimador.text = "";
    }

    private void HandleGenerandoPreguntas(bool generando)
    {
        _preguntasListas = !generando;

        if (textoEstadoGeneracion != null)
        {
            textoEstadoGeneracion.gameObject.SetActive(generando);
            textoEstadoGeneracion.text = generando
                ? "Martin esta preparando las preguntas... (puede tardar un minuto)"
                : "";
        }

        // Ocultar/mostrar botón arrancar según estado de carga
        if (btnArrancarPartida != null && GameStateManager.Instance != null
            && GameStateManager.Instance.Runner != null
            && GameStateManager.Instance.Runner.IsServer)
        {
            if (generando)
                btnArrancarPartida.SetActive(false);
            else
                RefreshRoomUI(); // re-evalúa con _preguntasListas = true
        }
    }

    private void HandleTurnChanged(PlayerRef nuevoJugadorActivo)
    {
        StartCoroutine(ActualizarInputConReintentoCoroutine());
    }

    // ══════════════════════════════════════════════════════════════
    //  Lobby UI
    // ══════════════════════════════════════════════════════════════

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
            textoNombreJugadorLobby.text = nuevoNombre;
        inputEdicionNombreLobby.gameObject.SetActive(false);
    }

    public string GetPlayerNameInput()
    {
        return textoNombreJugadorLobby != null ? textoNombreJugadorLobby.text : "Jugador";
    }

    public void RefreshRoomUI()
    {
        if (GameStateManager.Instance == null || GameStateManager.Instance.Runner == null) return;

        // Mostrar el tablero 3D en cuanto hay sala activa (lobby de sala y durante el juego)
        if (tableroPanel3D != null && !tableroPanel3D.activeSelf)
        {
            tableroPanel3D.SetActive(true);
            _tableroMostrarPlaceholders = false; // slots vacíos hasta que empiece la partida
        }

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
            btnArrancarPartida.SetActive(todosListos && jugadoresConEquipo >= 2 && _preguntasListas);

        ActualizarConfigUI();
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

    public void Btn_ToggleReady() { GetMyPlayerData()?.RPC_SetReady(!GetMyPlayerData().IsReady); }
    public void Btn_UnirseEquipoA() { GetMyPlayerData()?.RPC_JoinTeam(1); }
    public void Btn_UnirseEquipoB() { GetMyPlayerData()?.RPC_JoinTeam(2); }

    // ── Editar rondas (host) ──────────────────────────────────────
    public void Btn_EditarRondas()
    {
        if (panelOpcionesRondas == null) return;
        bool abriendo = !panelOpcionesRondas.activeSelf;
        panelOpcionesRondas.SetActive(abriendo);
        // Cerrar el otro panel si estaba abierto
        if (abriendo && panelOpcionesTiempo != null) panelOpcionesTiempo.SetActive(false);
    }

    public void Btn_SetRondas1() { AplicarRondas(1); }
    public void Btn_SetRondas3() { AplicarRondas(3); }
    public void Btn_SetRondas5() { AplicarRondas(5); }

    private void AplicarRondas(int rondas)
    {
        if (GameStateManager.Instance != null) GameStateManager.Instance.RPC_SetTotalRondas(rondas);
        if (panelOpcionesRondas != null) panelOpcionesRondas.SetActive(false);
    }

    // ── Editar tiempo de turno (host) ─────────────────────────────
    public void Btn_EditarTiempo()
    {
        if (panelOpcionesTiempo == null) return;
        bool abriendo = !panelOpcionesTiempo.activeSelf;
        panelOpcionesTiempo.SetActive(abriendo);
        // Cerrar el otro panel si estaba abierto
        if (abriendo && panelOpcionesRondas != null) panelOpcionesRondas.SetActive(false);
    }

    public void Btn_SetTiempo10() { AplicarTiempo(10); }
    public void Btn_SetTiempo15() { AplicarTiempo(15); }
    public void Btn_SetTiempo30() { AplicarTiempo(30); }

    private void AplicarTiempo(int segundos)
    {
        if (TurnManager.Instance != null) TurnManager.Instance.RPC_SetTurnDuration(segundos);
        if (panelOpcionesTiempo != null) panelOpcionesTiempo.SetActive(false);
    }

    private void ActualizarConfigUI()
    {
        if (GameStateManager.Instance == null || GameStateManager.Instance.Runner == null) return;
        bool esHost      = GameStateManager.Instance.Runner.IsServer;
        bool juegoActivo = GameStateManager.Instance.IsGameStarted;

        if (textoConfigRondas != null)
            textoConfigRondas.text = $"Rondas: {GameStateManager.Instance.TotalRondas}";

        if (textoConfigTiempo != null && TurnManager.Instance != null)
            textoConfigTiempo.text = $"Turno: {TurnManager.Instance.TurnDurationSeconds}sg";

        // El botón "Editar" solo lo ve el host antes de empezar
        bool puedeEditar = esHost && !juegoActivo;
        if (btnEditarRondas != null) btnEditarRondas.SetActive(puedeEditar);
        if (btnEditarTiempo != null) btnEditarTiempo.SetActive(puedeEditar);

        // Si el juego empieza, cerrar cualquier panel de opciones abierto
        if (juegoActivo)
        {
            if (panelOpcionesRondas != null) panelOpcionesRondas.SetActive(false);
            if (panelOpcionesTiempo != null) panelOpcionesTiempo.SetActive(false);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Helpers de datos
    // ══════════════════════════════════════════════════════════════

    private PlayerNetworkData GetPlayerData(PlayerRef player)
    {
        var networkObj = GameStateManager.Instance?.Runner?.GetPlayerObject(player);
        return networkObj != null ? networkObj.GetComponent<PlayerNetworkData>() : null;
    }

    private PlayerNetworkData GetMyPlayerData()
    {
        return (GameStateManager.Instance == null || GameStateManager.Instance.Runner == null)
            ? null
            : GetPlayerData(GameStateManager.Instance.Runner.LocalPlayer);
    }

    // ══════════════════════════════════════════════════════════════
    //  Handlers de eventos
    // ══════════════════════════════════════════════════════════════

    private void HandleTemporaryStrike()
    {
        if (textoStrikeGrande != null) textoStrikeGrande.text = "X";
        // Reproducir sonido de fallo (también se usa cuando se acaba el tiempo)
        if (audioSourceUI != null && wrongAnswerClip != null)
            audioSourceUI.PlayOneShot(wrongAnswerClip);
        StartCoroutine(MostrarStrikeTemporal());
    }

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
        for (int i = 0; i < iconosX.Length; i++)
            if (iconosX[i] != null) iconosX[i].SetActive(i < totalErrores);
    }

    private IEnumerator MostrarStrikeTemporal()
    {
        if (panelStrikeGrande != null)
        {
            panelStrikeGrande.SetActive(true);
            yield return new WaitForSeconds(1.5f);
            panelStrikeGrande.SetActive(false);
        }
    }

    private void HandleEvaluationStateChanged(bool isEvaluating)
    {
        if (isEvaluating)
        {
            if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Respuesta revelada → detener suspenso y reanudar música de juego
            DetenerSuspenso();
            if (GameStateManager.Instance != null)
                HandleStateChanged(GameStateManager.Instance.CurrentState);
        }
    }

    private void HandleStateChanged(GameStateManager.GameState newState)
    {
        if (isPaused) TogglePauseMenu();
        bool isServerReady = GameStateManager.Instance != null &&
                             GameStateManager.Instance.Object != null &&
                             GameStateManager.Instance.Object.IsValid;
        if (isServerReady && GameStateManager.Instance.IsEvaluating) return;
        if (_startScreenActive)
        {
            _queuedState = newState;
            return;
        }

        switch (newState)
        {
            case GameStateManager.GameState.Intro:
                IniciarMusicaJuego();
                if (tableroPanel3D) tableroPanel3D.SetActive(false); // aún no hay pregunta en Intro
                if (lobbyPanel) lobbyPanel.SetActive(false);
                if (roomPanel) roomPanel.SetActive(false);
                if (panelPregunta) panelPregunta.SetActive(false);
                if (panelRespuestas) panelRespuestas.SetActive(false);
                if (panelCountdown) panelCountdown.SetActive(false);
                if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
                if (panelAnimador != null) panelAnimador.SetActive(true);
                if (textoAnimador != null && GameStateManager.Instance != null)
                {
                    string eqA = GameStateManager.Instance.NombreEquipoA.ToString();
                    string eqB = GameStateManager.Instance.NombreEquipoB.ToString();
                    textoAnimador.text = $"¡Bienvenidos a los 100 Chilenos Dicen!\n{eqA} vs {eqB}";
                }
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _esMiTurnoActual = false;
                break;

            case GameStateManager.GameState.Countdown:
                _prevRevealedMask = 0;
                _tableroMostrarPlaceholders = false; // slots vacíos hasta que el animador termine
                if (tableroPanel3D) tableroPanel3D.SetActive(true);
                if (lobbyPanel) lobbyPanel.SetActive(false);
                if (roomPanel) roomPanel.SetActive(false);
                if (panelPregunta) panelPregunta.SetActive(true);
                if (panelCountdown) panelCountdown.SetActive(true);
                if (panelRespuestas) panelRespuestas.SetActive(true);
                if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
                if (labelPuntosRonda != null) labelPuntosRonda.SetActive(true);
                if (valorPuntosRonda != null) valorPuntosRonda.gameObject.SetActive(true);
                if (panelPuntosEquipo != null) panelPuntosEquipo.SetActive(true);
                else { if (labelMisPuntos != null) labelMisPuntos.SetActive(true); if (valorMisPuntos != null) valorMisPuntos.gameObject.SetActive(true); }
                if (textoTurnoHUD != null) textoTurnoHUD.gameObject.SetActive(true);
                if (textoPreguntaPrincipal != null && GameStateManager.Instance != null)
                    textoPreguntaPrincipal.text = GameStateManager.Instance.PreguntaActual;
                // Mostrar panel del animador al inicio de cada ronda
                if (panelAnimador != null) panelAnimador.SetActive(true);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                localTimer = 5.0f;
                isCounting = true;
                _esMiTurnoActual = false; // BUG FIX: resetear en countdown
                _buzzerRetryFrames = 0;
                break;

            case GameStateManager.GameState.WaitingForBuzzer:
                _tableroMostrarPlaceholders = true; // animador terminó → revelar "--- X ---"
                isCounting = false;
                _buzzerRetryFrames = 0;
                if (panelCountdown) panelCountdown.SetActive(false);
                if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _esMiTurnoActual = false;
                break;

            case GameStateManager.GameState.RoundEnd:
                isCounting = false;
                _buzzerRetryFrames = 0;
                if (panelCountdown) panelCountdown.SetActive(false);
                if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                _esMiTurnoActual = false;
                break;

            case GameStateManager.GameState.GameOver:
                DetenerSuspenso();
                if (musicaJuegoSource != null) musicaJuegoSource.Stop();
                if (tableroPanel3D) tableroPanel3D.SetActive(false);
                isCounting = false;
                _buzzerRetryFrames = 0;
                _esMiTurnoActual = false;
                if (panelCountdown) panelCountdown.SetActive(false);
                if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
                if (panelTiempoTurno != null) panelTiempoTurno.SetActive(false);
                if (sliderTiempo != null) sliderTiempo.gameObject.SetActive(false);
                // El cursor se desbloquea para que el botón "Volver al lobby" sea clickeable
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                // El panel de ganador se muestra DESPUÉS de que AnimadorController
                // termina su secuencia de cierre → llama a MostrarPanelGanador()
                break;

            case GameStateManager.GameState.TypingAnswer:
            case GameStateManager.GameState.Playing:
            case GameStateManager.GameState.Stealing:
                isCounting = false;
                if (newState == GameStateManager.GameState.TypingAnswer)
                    _buzzerRetryFrames = 30; // reintentar hasta 30 frames (~0.5s) para cubrir latencia de red
                StartCoroutine(ActualizarInputConReintentoCoroutine());
                break;

            case GameStateManager.GameState.WaitingForPlayers:
                DetenerSuspenso();
                if (musicaJuegoSource != null) musicaJuegoSource.Stop();
                BajarMusicaParaLobby();
                if (tableroPanel3D) tableroPanel3D.SetActive(false);
                _tableroMostrarPlaceholders = false;
                isCounting = false;
                _buzzerRetryFrames = 0;
                if (lobbyPanel) lobbyPanel.SetActive(true);
                if (panelPregunta) panelPregunta.SetActive(false);
                if (panelRespuestas) panelRespuestas.SetActive(false);
                if (panelCountdown) panelCountdown.SetActive(false);
                if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
                if (labelPuntosRonda != null) labelPuntosRonda.SetActive(false);
                if (valorPuntosRonda != null) valorPuntosRonda.gameObject.SetActive(false);
                if (panelPuntosEquipo != null) panelPuntosEquipo.SetActive(false);
                else { if (labelMisPuntos != null) labelMisPuntos.SetActive(false); if (valorMisPuntos != null) valorMisPuntos.gameObject.SetActive(false); }
                if (textoTurnoHUD != null) textoTurnoHUD.gameObject.SetActive(false);
                // Ocultar timer de turno completamente para que no bloquee clicks en el lobby
                if (panelTiempoTurno != null) panelTiempoTurno.SetActive(false);
                if (textoTiempoTurno != null) textoTiempoTurno.gameObject.SetActive(false);
                if (sliderTiempo != null) sliderTiempo.gameObject.SetActive(false);
                // Ocultar panel de fin de juego
                if (panelGameOver != null) panelGameOver.SetActive(false);
                // Cerrar paneles de opciones de configuración
                if (panelOpcionesRondas != null) panelOpcionesRondas.SetActive(false);
                if (panelOpcionesTiempo != null) panelOpcionesTiempo.SetActive(false);
                // Ocultar panel del animador en el lobby
                if (panelAnimador != null) panelAnimador.SetActive(false);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _esMiTurnoActual = false;
                break;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  BUG FIX: ActualizarControlInput sin red de emergencia
    // ══════════════════════════════════════════════════════════════

    private IEnumerator ActualizarInputConReintentoCoroutine()
    {
        ActualizarControlInput();
        yield return null;
        ActualizarControlInput();
    }

    private void ActualizarControlInput()
    {
        if (GameStateManager.Instance == null || GameStateManager.Instance.Runner == null ||
            GameStateManager.Instance.Object == null || !GameStateManager.Instance.Object.IsValid) return;

        bool esMiTurno = false;
        string nombreTurno = "Esperando...";
        var currentState = GameStateManager.Instance.CurrentState;
        var myPlayer = GameStateManager.Instance.Runner.LocalPlayer;

        // ── Fase de Podio (Buzzer) ─────────────────────────────
        if (currentState == GameStateManager.GameState.TypingAnswer)
        {
            int buzzerId = GameStateManager.Instance.BuzzerWinnerId;
            esMiTurno = (buzzerId == myPlayer.PlayerId);
            var dataWinner = GetPlayerData(PlayerRef.FromIndex(buzzerId));
            if (dataWinner != null) nombreTurno = dataWinner.PlayerName.ToString();
        }
        // ── Fase de Mesa y Robo ────────────────────────────────
        else if (currentState == GameStateManager.GameState.Playing ||
                 currentState == GameStateManager.GameState.Stealing)
        {
            if (TurnManager.Instance != null && TurnManager.Instance.ActivePlayer != PlayerRef.None)
            {
                // Solo el jugador con turno asignado por TurnManager puede responder
                esMiTurno = TurnManager.Instance.IsLocalPlayersTurn;
                var dataActive = GetPlayerData(TurnManager.Instance.ActivePlayer);
                if (dataActive != null) nombreTurno = dataActive.PlayerName.ToString();
            }
            // Si TurnManager aún no tiene jugador asignado, nadie responde
            // (evita mostrar el input al ganador del buzzer cuando ya no es su turno)
        }

        // ── Actualizar HUD ─────────────────────────────────────
        if (textoTurnoHUD != null) textoTurnoHUD.text = "Turno de: " + nombreTurno;

        // ── Guardar estado para el Update ──────────────────────
        _esMiTurnoActual = esMiTurno;

        bool estadoPermiteInput = (currentState == GameStateManager.GameState.TypingAnswer ||
                                   currentState == GameStateManager.GameState.Playing ||
                                   currentState == GameStateManager.GameState.Stealing)
                                  // No mostrar input mientras el animador anuncia el turno
                                  && !GameStateManager.Instance.IsAnnouncingTurn;

        if (esMiTurno && estadoPermiteInput)
        {
            if (inputRespuesta && !inputRespuesta.gameObject.activeSelf)
            {
                inputRespuesta.gameObject.SetActive(true);
                inputRespuesta.text = "";
                inputRespuesta.Select();
                inputRespuesta.ActivateInputField();
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            if (inputRespuesta && inputRespuesta.gameObject.activeSelf)
                inputRespuesta.gameObject.SetActive(false);
            if (estadoPermiteInput)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Update
    // ══════════════════════════════════════════════════════════════

    private void Update()
    {
        if (_startScreenActive)
        {
            if (Input.anyKeyDown) DesactivarStartScreen();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) TogglePauseMenu();

        // Detectar cambios en IsAnnouncingTurn para refrescar visibilidad del input
        var gsmAnuncio = GameStateManager.Instance;
        if (gsmAnuncio != null && gsmAnuncio.Object != null && gsmAnuncio.Object.IsValid)
        {
            bool anunciando = gsmAnuncio.IsAnnouncingTurn;
            if (anunciando != _prevAnnouncingTurn)
            {
                _prevAnnouncingTurn = anunciando;
                ActualizarControlInput();
            }
        }

        // Polling de seguridad para el buzzer: reintenta ActualizarControlInput() cada frame
        // hasta _buzzerRetryFrames veces, para cubrir cualquier retraso de sincronización de red.
        if (_buzzerRetryFrames > 0)
        {
            _buzzerRetryFrames--;
            bool gsValid = GameStateManager.Instance != null &&
                           GameStateManager.Instance.Object != null &&
                           GameStateManager.Instance.Object.IsValid;
            if (gsValid &&
                GameStateManager.Instance.CurrentState == GameStateManager.GameState.TypingAnswer &&
                GameStateManager.Instance.BuzzerWinnerId != -1 &&
                !GameStateManager.Instance.IsEvaluating)
            {
                ActualizarControlInput();
            }
        }

        if (inputRespuesta != null && inputRespuesta.gameObject.activeInHierarchy)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (!string.IsNullOrWhiteSpace(inputRespuesta.text))
                    OnSubmitAnswer(inputRespuesta.text);
            }

            if (!inputRespuesta.isFocused && !isPaused && _esMiTurnoActual)
            {
                inputRespuesta.ActivateInputField();
            }
        }

        if (isCounting && panelCountdown != null)
        {
            localTimer -= Time.deltaTime;
            TMP_Text texto = panelCountdown.GetComponent<TMP_Text>();
            if (texto != null)
            {
                int segundos = Mathf.CeilToInt(localTimer);
                texto.text = segundos <= 0 ? "¡PREPÁRATE!" : segundos.ToString();
            }
        }

        // ── Temporizador de turno ──────────────────────────────────────
        ActualizarTimerUI();

        // ── Tablero 3D Family Feud ─────────────────────────────────────
        if (tableroPanel3D != null && tableroPanel3D.activeSelf)
            ActualizarTablero3D();

        // Actualización visual del tablero
        bool isServerReady = GameStateManager.Instance != null &&
                             GameStateManager.Instance.Object != null &&
                             GameStateManager.Instance.Object.IsValid;

        if (isServerReady && panelRespuestas != null && panelRespuestas.activeSelf)
        {
            int mask          = GameStateManager.Instance.RevealedAnswersMask;

            // Reproducir ding cuando se revela una nueva respuesta correcta
            // (funciona tanto en juego como en la revelación al final de ronda)
            int nuevasBits = mask & ~_prevRevealedMask;
            if (nuevasBits != 0 && audioSourceUI != null && dingCorrectClip != null)
                audioSourceUI.PlayOneShot(dingCorrectClip);
            _prevRevealedMask = mask;
            string[] correctas = GameStateManager.Instance.RespuestasValidas;
            int[] puntos       = GameStateManager.Instance.PuntosRespuestas;
            int totalResp      = (correctas != null) ? correctas.Length : 0;

            for (int i = 0; i < 8; i++)
            {
                if (casillasRespuestas[i] == null || casillasPuntos[i] == null) continue;

                // Mostrar la fila solo si hay una respuesta real para ese índice
                bool filaActiva = i < totalResp;

                // Intentar ocultar el padre (la fila completa) para que el layout se reajuste
                var filaGO = casillasRespuestas[i].transform.parent?.gameObject;
                if (filaGO != null && filaGO != panelRespuestas)
                    filaGO.SetActive(filaActiva);
                else
                {
                    // Fallback: ocultar los textos directamente
                    casillasRespuestas[i].gameObject.SetActive(filaActiva);
                    casillasPuntos[i].gameObject.SetActive(filaActiva);
                }

                if (!filaActiva) continue;

                if (i < correctas.Length && (mask & (1 << i)) != 0)
                {
                    casillasRespuestas[i].text = correctas[i];
                    casillasPuntos[i].text     = puntos[i].ToString();
                }
                else
                {
                    casillasRespuestas[i].text = $"--- {i + 1} ---";
                    casillasPuntos[i].text     = "--";
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Temporizador de turno
    // ══════════════════════════════════════════════════════════════

    private void ActualizarTimerUI()
    {
        // Sin sesión activa → ocultar todo (evita que el panel bloquee clicks en el lobby)
        if (GameStateManager.Instance == null || TurnManager.Instance == null)
        {
            if (panelTiempoTurno != null) panelTiempoTurno.SetActive(false);
            if (textoTiempoTurno != null && panelTiempoTurno == null)
                textoTiempoTurno.gameObject.SetActive(false);
            if (sliderTiempo != null) sliderTiempo.gameObject.SetActive(false);
            return;
        }

        var estado = GameStateManager.Instance.CurrentState;
        bool activo = estado == GameStateManager.GameState.Playing    ||
                      estado == GameStateManager.GameState.Stealing   ||
                      estado == GameStateManager.GameState.TypingAnswer;

        // Panel contenedor (si existe) — lo muestra/oculta completo
        if (panelTiempoTurno != null) panelTiempoTurno.SetActive(activo);
        // Texto y slider SIEMPRE se gestionan de forma explícita:
        // si el texto fue ocultado explícitamente (ej. en MostrarPanelGanador),
        // activar el panel padre no lo muestra automáticamente en Unity.
        if (textoTiempoTurno != null) textoTiempoTurno.gameObject.SetActive(activo);
        if (sliderTiempo     != null) sliderTiempo.gameObject.SetActive(activo);

        // Cuando el timer deja de estar activo (RoundEnd, Lobby, etc.) → detener suspenso
        if (!activo)
        {
            DetenerSuspenso();
            return;
        }

        float tiempoRestante  = TurnManager.Instance.TurnTimeLeft;
        float normalizado     = TurnManager.Instance.TurnTimeNormalized;
        bool  urgente         = tiempoRestante <= umbralUrgente;
        Color color           = urgente ? colorUrgente : colorNormal;

        // ── Música de suspenso ────────────────────────────────────────
        bool isEval = GameStateManager.Instance != null && GameStateManager.Instance.IsEvaluating;
        if (urgente && !_suspensoActivo)
            IniciarSuspenso();
        else if (!urgente && _suspensoActivo && !isEval)
            DetenerSuspenso();

        // Texto con segundos restantes
        if (textoTiempoTurno != null)
        {
            textoTiempoTurno.text  = Mathf.CeilToInt(tiempoRestante).ToString();
            textoTiempoTurno.color = color;
        }

        // Barra de progreso (se vacía conforme pasa el tiempo)
        if (sliderTiempo != null)
        {
            sliderTiempo.value = normalizado;
            // Colorear el fill de la barra si tiene Image asignada
            var fill = sliderTiempo.fillRect?.GetComponent<UnityEngine.UI.Image>();
            if (fill != null) fill.color = color;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Submit de respuesta
    // ══════════════════════════════════════════════════════════════

    private void OnSubmitAnswer(string respuesta)
    {
        if (string.IsNullOrWhiteSpace(respuesta) ||
            GameStateManager.Instance == null ||
            GameStateManager.Instance.Runner == null) return;

        GameStateManager.Instance.RPC_SubmitAnswer(respuesta, GameStateManager.Instance.Runner.LocalPlayer.PlayerId);

        if (inputRespuesta != null)
        {
            inputRespuesta.text = "";
            inputRespuesta.gameObject.SetActive(false);
        }

        // BUG FIX: Resetear flag de turno al enviar respuesta
        // evita que el Update reactive el campo mientras el servidor evalúa
        _esMiTurnoActual = false;
    }

    // ══════════════════════════════════════════════════════════════
    //  Start Screen
    // ══════════════════════════════════════════════════════════════

    private void ActivarStartScreen()
    {
        _startScreenActive = true;
        if (panelStartScreen != null)
        {
            panelStartScreen.SetActive(true);
            panelStartScreen.transform.SetAsLastSibling();
        }

        if (textoPresionaTecla != null && string.IsNullOrWhiteSpace(textoPresionaTecla.text))
            textoPresionaTecla.text = "Presiona cualquier tecla para comenzar";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        IniciarMusicaInicio();
    }

    private void DesactivarStartScreen()
    {
        _startScreenActive = false;
        if (panelStartScreen != null) panelStartScreen.SetActive(false);
        BajarMusicaParaLobby();
        HandleStateChanged(_queuedState);
    }

    private void IniciarMusicaInicio()
    {
        if (musicaInicioSource == null) return;
        if (musicaInicioClip != null) musicaInicioSource.clip = musicaInicioClip;
        if (musicaInicioSource.clip == null) return;

        musicaInicioSource.loop = true;
        if (_fadeMusicCoroutine != null) StopCoroutine(_fadeMusicCoroutine);
        musicaInicioSource.volume = 0f;
        if (!musicaInicioSource.isPlaying) musicaInicioSource.Play();
        _fadeMusicCoroutine = StartCoroutine(FadeMusicaCoroutine(musicaInicioVolumen, musicaInicioFadeSegundos));
    }

    private void BajarMusicaParaLobby()
    {
        if (musicaInicioSource == null || musicaInicioSource.clip == null) return;
        if (!musicaInicioSource.isPlaying) musicaInicioSource.Play();
        if (_fadeMusicCoroutine != null) StopCoroutine(_fadeMusicCoroutine);
        _fadeMusicCoroutine = StartCoroutine(FadeMusicaCoroutine(musicaLobbyVolumen, musicaLobbyFadeSegundos));
    }

    private IEnumerator FadeMusicaCoroutine(float targetVolume, float seconds)
    {
        if (musicaInicioSource == null) yield break;
        if (seconds <= 0f)
        {
            musicaInicioSource.volume = targetVolume;
            yield break;
        }

        float startVolume = musicaInicioSource.volume;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            musicaInicioSource.volume = Mathf.Lerp(startVolume, targetVolume, t / seconds);
            yield return null;
        }
        musicaInicioSource.volume = targetVolume;
    }

    // ══════════════════════════════════════════════════════════════
    //  Tablero 3D Family Feud
    // ══════════════════════════════════════════════════════════════

    private void ActualizarTablero3D()
    {
        bool gsOk = GameStateManager.Instance != null &&
                    GameStateManager.Instance.Object != null &&
                    GameStateManager.Instance.Object.IsValid;
        if (!gsOk) return;

        int      mask      = GameStateManager.Instance.RevealedAnswersMask;
        string[] correctas = GameStateManager.Instance.RespuestasValidas;
        int[]    puntos    = GameStateManager.Instance.PuntosRespuestas;
        int      totalResp = correctas != null ? correctas.Length : 0;

        for (int i = 0; i < 8; i++)
        {
            if (tableroRespuestas3D[i] == null) continue;

            // Mostrar / ocultar fila según cantidad real de respuestas
            bool filaActiva = i < totalResp;
            var filaGO = tableroRespuestas3D[i].transform.parent?.gameObject;
            if (filaGO != null && filaGO != tableroPanel3D)
                filaGO.SetActive(filaActiva);

            if (!filaActiva) continue;

            if (i < correctas.Length && (mask & (1 << i)) != 0)
            {
                // Respuesta revelada
                tableroRespuestas3D[i].text = correctas[i];
                if (tableroPuntos3D[i] != null) tableroPuntos3D[i].text = puntos[i].ToString();
            }
            else if (_tableroMostrarPlaceholders)
            {
                // Animador ya anunció — mostrar casilla oculta numerada
                tableroRespuestas3D[i].text = $"--- {i + 1} ---";
                if (tableroPuntos3D[i] != null) tableroPuntos3D[i].text = "--";
            }
            else
            {
                // Countdown: slots completamente vacíos
                tableroRespuestas3D[i].text = "";
                if (tableroPuntos3D[i] != null) tableroPuntos3D[i].text = "";
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Música de juego y suspenso
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Detiene la música de lobby y arranca SongMainJuego al mismo volumen.
    /// Se llama al pasar al estado Intro (inicio de partida).
    /// </summary>
    private void IniciarMusicaJuego()
    {
        // Detener música de lobby/inicio
        if (_fadeMusicCoroutine != null) StopCoroutine(_fadeMusicCoroutine);
        if (musicaInicioSource != null) musicaInicioSource.Stop();

        if (musicaJuegoSource == null || musicaJuegoClip == null) return;
        musicaJuegoSource.clip   = musicaJuegoClip;
        musicaJuegoSource.volume = musicaJuegoVolumen;
        musicaJuegoSource.loop   = true;
        if (!musicaJuegoSource.isPlaying) musicaJuegoSource.Play();
    }

    /// <summary>
    /// Pausa SongMainJuego y arranca la música de suspenso (últimos 10 s).
    /// </summary>
    private void IniciarSuspenso()
    {
        if (_suspensoActivo) return;
        _suspensoActivo = true;

        if (musicaJuegoSource != null && musicaJuegoSource.isPlaying)
            musicaJuegoSource.Pause();

        if (musicaSuspensoSource != null && musicaSuspensoClip != null)
        {
            musicaSuspensoSource.clip   = musicaSuspensoClip;
            musicaSuspensoSource.volume = musicaSuspensoVolumen;
            musicaSuspensoSource.loop   = false;
            if (!musicaSuspensoSource.isPlaying) musicaSuspensoSource.Play();
        }
    }

    /// <summary>
    /// Detiene la música de suspenso y reanuda SongMainJuego.
    /// Se llama al revelar la respuesta o al cambiar de ronda.
    /// </summary>
    private void DetenerSuspenso()
    {
        if (!_suspensoActivo) return;
        _suspensoActivo = false;

        if (musicaSuspensoSource != null && musicaSuspensoSource.isPlaying)
            musicaSuspensoSource.Stop();

        if (musicaJuegoSource != null && !musicaJuegoSource.isPlaying
            && musicaJuegoSource.clip != null)
            musicaJuegoSource.UnPause();
    }

    // ══════════════════════════════════════════════════════════════
    //  Panel de Fin de Juego
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Llamado por AnimadorController cuando termina la secuencia de cierre.
    /// Muestra el panel de ganador con los marcadores finales.
    /// </summary>
    public void MostrarPanelGanador()
    {
        if (panelGameOver == null) return;

        var gsm = GameStateManager.Instance;
        if (gsm == null || gsm.Runner == null) return;

        string eqA = gsm.NombreEquipoA.ToString();
        string eqB = gsm.NombreEquipoB.ToString();
        int    pA  = gsm.ScoreA;
        int    pB  = gsm.ScoreB;

        // ── Título: ganador o empate ──────────────────────────────────
        if (textoGanadorFinal != null)
        {
            if      (pA > pB) textoGanadorFinal.text = eqA;
            else if (pB > pA) textoGanadorFinal.text = eqB;
            else              textoGanadorFinal.text  = "¡Empate!";
        }

        // ── Marcador general (línea simple de puntos) ─────────────────
        if (textoMarcadorFinal != null)
            textoMarcadorFinal.text = $"{eqA}: {pA} pts     |     {eqB}: {pB} pts";

        // ── Recolectar jugadores y sus aciertos ───────────────────────
        var jugA = new System.Collections.Generic.List<(string nombre, int aciertos)>();
        var jugB = new System.Collections.Generic.List<(string nombre, int aciertos)>();

        foreach (var pRef in gsm.Runner.ActivePlayers)
        {
            var data = gsm.Runner.GetPlayerObject(pRef)?.GetComponent<PlayerNetworkData>();
            if (data == null) continue;
            if      (data.TeamIndex == 1) jugA.Add((data.PlayerName.ToString(), data.Aciertos));
            else if (data.TeamIndex == 2) jugB.Add((data.PlayerName.ToString(), data.Aciertos));
        }

        // Ordenar por aciertos (mayor primero)
        jugA.Sort((a, b) => b.aciertos.CompareTo(a.aciertos));
        jugB.Sort((a, b) => b.aciertos.CompareTo(a.aciertos));

        // ── Tabla Equipo A (izquierda) ────────────────────────────────
        if (textoTablaEquipoA != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(eqA);
            sb.AppendLine("──────────────");
            if (jugA.Count == 0) sb.AppendLine("Sin jugadores");
            else foreach (var (nombre, aciertos) in jugA)
                sb.AppendLine($"{nombre}\n  {aciertos} acierto{(aciertos != 1 ? "s" : "")}");
            textoTablaEquipoA.text = sb.ToString();
        }

        // ── Tabla Equipo B (derecha) ──────────────────────────────────
        if (textoTablaEquipoB != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(eqB);
            sb.AppendLine("──────────────");
            if (jugB.Count == 0) sb.AppendLine("Sin jugadores");
            else foreach (var (nombre, aciertos) in jugB)
                sb.AppendLine($"{nombre}\n  {aciertos} acierto{(aciertos != 1 ? "s" : "")}");
            textoTablaEquipoB.text = sb.ToString();
        }

        // ── Ocultar todo el HUD de juego ─────────────────────────────
        if (panelPregunta    != null) panelPregunta.SetActive(false);
        if (panelRespuestas  != null) panelRespuestas.SetActive(false);
        if (panelCountdown   != null) panelCountdown.SetActive(false);
        if (panelTiempoTurno != null) panelTiempoTurno.SetActive(false);
        if (sliderTiempo     != null) sliderTiempo.gameObject.SetActive(false);
        if (textoTiempoTurno != null) textoTiempoTurno.gameObject.SetActive(false);
        if (textoTurnoHUD    != null) textoTurnoHUD.gameObject.SetActive(false);
        if (labelPuntosRonda != null) labelPuntosRonda.SetActive(false);
        if (valorPuntosRonda != null) valorPuntosRonda.gameObject.SetActive(false);
        if (panelPuntosEquipo != null) panelPuntosEquipo.SetActive(false);
        else
        {
            if (labelMisPuntos != null) labelMisPuntos.SetActive(false);
            if (valorMisPuntos != null) valorMisPuntos.gameObject.SetActive(false);
        }
        if (panelStrikeGrande != null) panelStrikeGrande.SetActive(false);
        foreach (var icono in iconosX) if (icono != null) icono.SetActive(false);
        if (inputRespuesta != null) inputRespuesta.gameObject.SetActive(false);

        panelGameOver.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ══════════════════════════════════════════════════════════════
    //  Pausa y salida
    // ══════════════════════════════════════════════════════════════

    public void TogglePauseMenu()
    {
        if (panelPausa == null) return;
        isPaused = !isPaused;
        panelPausa.SetActive(isPaused);
        if (isPaused)
        {
            previousLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = previousLockMode;
            Cursor.visible = previousCursorVisible;
        }
    }

    public void Btn_SalirAlLobby()
    {
        // Cerrar menú de pausa si está abierto
        if (isPaused) TogglePauseMenu();

        if (RoomManager.Instance != null)
        {
            // SalirAlLobby hace Runner.Shutdown() async → OnShutdown → RetornarAlLobby
            // → OnDisconnectedEvent → HandleDisconnected() aquí en UIGameController.
            // NO recargamos la escena: los paneles se ocultan/muestran via eventos.
            RoomManager.Instance.SalirAlLobby();
        }
        else
        {
            // Fallback: sin RoomManager, resetear UI directamente
            HandleStateChanged(GameStateManager.GameState.WaitingForPlayers);
        }
    }

    public void Btn_SalirDelJuego()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}