using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement; // ¡NUEVO! Necesario para recargar la escena

public class UIGameController : MonoBehaviour
{
    [Header("Paneles Lobby")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject roomPanel;

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

    [Header("Menú de Pausa")] // ¡NUEVO!
    [SerializeField] private GameObject panelPausa;

    private float localTimer = 0f;
    private bool isCounting = false;

    // Variables para recordar cómo estaba el mouse
    private bool isPaused = false;
    private CursorLockMode previousLockMode;
    private bool previousCursorVisible;

    private void Start()
    {
        HandleStateChanged(GameStateManager.GameState.WaitingForPlayers);
        if (inputRespuesta != null) inputRespuesta.onSubmit.AddListener(OnSubmitAnswer);
    }

    private void OnEnable()  { GameStateManager.OnStateChangedEvent += HandleStateChanged; }
    private void OnDisable() { GameStateManager.OnStateChangedEvent -= HandleStateChanged; }

    private void HandleStateChanged(GameStateManager.GameState newState)
    {
        if (isPaused) TogglePauseMenu(); // Si estábamos en pausa, lo cerramos por seguridad

        switch (newState)
        {
            case GameStateManager.GameState.Countdown:
                if (lobbyPanel) lobbyPanel.SetActive(false);
                if (roomPanel) roomPanel.SetActive(false);
                if (panelPregunta) panelPregunta.SetActive(true);
                if (panelCountdown) panelCountdown.SetActive(true);
                if (panelRespuestas) panelRespuestas.SetActive(true);
                if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);

                if (textoPreguntaPrincipal != null && GameStateManager.Instance != null)
                    textoPreguntaPrincipal.text = GameStateManager.Instance.PreguntaActual;

                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                localTimer = 5.0f; isCounting = true;
                break;

            case GameStateManager.GameState.WaitingForBuzzer:
                isCounting = false;
                if (panelCountdown) panelCountdown.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                break;

            case GameStateManager.GameState.TypingAnswer:
                isCounting = false; 
                bool soyElGanador = (GameStateManager.Instance.BuzzerWinnerId == GameStateManager.Instance.Runner.LocalPlayer.PlayerId);

                if (soyElGanador)
                {
                    if (inputRespuesta) { inputRespuesta.gameObject.SetActive(true); inputRespuesta.Select(); inputRespuesta.ActivateInputField(); }
                    Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                }
                else
                {
                    if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
                    Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                }
                break;

            case GameStateManager.GameState.Playing:
                isCounting = false;
                if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
                break;
                
            case GameStateManager.GameState.WaitingForPlayers:
                isCounting = false;
                if (lobbyPanel) lobbyPanel.SetActive(true);
                if (panelPregunta) panelPregunta.SetActive(false);
                if (panelRespuestas) panelRespuestas.SetActive(false);
                if (panelCountdown) panelCountdown.SetActive(false);
                if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);

                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                break;
        }
    }

    private void Update()
    {
        // --- DETECTAR LA TECLA ESCAPE ---
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePauseMenu();
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

        if (GameStateManager.Instance != null && panelRespuestas != null && panelRespuestas.activeSelf)
        {
            int mask = GameStateManager.Instance.RevealedAnswersMask;
            string[] correctas = GameStateManager.Instance.RespuestasValidas;
            int[] puntos = GameStateManager.Instance.PuntosRespuestas;

            for (int i = 0; i < 8; i++)
            {
                if (casillasRespuestas[i] != null && casillasPuntos[i] != null)
                {
                    if (i < correctas.Length && (mask & (1 << i)) != 0)
                    {
                        casillasRespuestas[i].text = correctas[i];
                        casillasPuntos[i].text = puntos[i].ToString();
                    }
                    else
                    {
                        casillasRespuestas[i].text = $"--- {i + 1} ---";
                        casillasPuntos[i].text = "--";
                    }
                }
            }
        }
    }

    private void OnSubmitAnswer(string respuesta)
    {
        if (!string.IsNullOrWhiteSpace(respuesta) && GameStateManager.Instance != null)
        {
            if (GameStateManager.Instance.BuzzerWinnerId == GameStateManager.Instance.Runner.LocalPlayer.PlayerId)
            {
                GameStateManager.Instance.RPC_SubmitAnswer(respuesta, GameStateManager.Instance.Runner.LocalPlayer.PlayerId);
                inputRespuesta.text = ""; 
            }
        }
    }

    // ==========================================
    // LÓGICA DEL MENÚ DE PAUSA Y BOTONES
    // ==========================================
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
        if (GameStateManager.Instance != null && GameStateManager.Instance.Runner != null)
        {
            GameStateManager.Instance.Runner.Shutdown();
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Btn_SalirDelJuego()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}