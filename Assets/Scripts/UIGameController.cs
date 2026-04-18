using UnityEngine;
using TMPro;

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

    [Header("Tablero de 8 Casillas (Arrastrar aquí)")]
    [SerializeField] private TMP_Text[] casillasRespuestas = new TMP_Text[8];
    [SerializeField] private TMP_Text[] casillasPuntos = new TMP_Text[8];

    private float localTimer = 0f;
    private bool isCounting = false;

    private void Start()
    {
        HandleStateChanged(GameStateManager.GameState.WaitingForPlayers);
        if (inputRespuesta != null) inputRespuesta.onSubmit.AddListener(OnSubmitAnswer);
    }

    private void OnEnable()  { GameStateManager.OnStateChangedEvent += HandleStateChanged; }
    private void OnDisable() { GameStateManager.OnStateChangedEvent -= HandleStateChanged; }

    private void HandleStateChanged(GameStateManager.GameState newState)
    {
        switch (newState)
        {
            case GameStateManager.GameState.Countdown:
                if (lobbyPanel) lobbyPanel.SetActive(false);
                if (roomPanel) roomPanel.SetActive(false);
                if (panelPregunta) panelPregunta.SetActive(true);
                if (panelCountdown) panelCountdown.SetActive(true);
                if (panelRespuestas) panelRespuestas.SetActive(true); // Encendemos el tablero vacío
                if (inputRespuesta) inputRespuesta.gameObject.SetActive(false);

                // Cargar la pregunta hardcodeada
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
                
            // ... (Faltan WaitingForPlayers y GameOver que son iguales a lo que tenías, los puedes omitir o dejar apagando paneles)
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

    private void Update()
    {
        // 1. Reloj Visual
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

        // 2. ACTUALIZAR EL TABLERO MÁGICAMENTE EN TIEMPO REAL
        if (GameStateManager.Instance != null && panelRespuestas != null && panelRespuestas.activeSelf)
        {
            int mask = GameStateManager.Instance.RevealedAnswersMask;
            string[] correctas = GameStateManager.Instance.RespuestasValidas;
            int[] puntos = GameStateManager.Instance.PuntosRespuestas;

            for (int i = 0; i < 8; i++)
            {
                if (casillasRespuestas[i] != null && casillasPuntos[i] != null)
                {
                    // Si el índice existe en nuestras respuestas Y la máscara dice que está revelado...
                    if (i < correctas.Length && (mask & (1 << i)) != 0)
                    {
                        casillasRespuestas[i].text = correctas[i];
                        casillasPuntos[i].text = puntos[i].ToString();
                    }
                    else
                    {
                        // Si no, mostramos los signos de interrogación (incluso las 3 que sobran)
                        casillasRespuestas[i].text = $"--- {i + 1} ---";
                        casillasPuntos[i].text = "--";
                    }
                }
            }
        }
    }
}