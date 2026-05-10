using System;
using UnityEngine;

// AnimadorIA: MonoBehaviour normal (no necesita Fusion).
// La generación de preguntas ocurre en GameStateManager.Spawned().
// Los comentarios y mensajes se envían via RPC_MostrarMensajeAnimador en GameStateManager.
public class AnimadorIA : MonoBehaviour
{
    public static AnimadorIA Instance { get; private set; }

    public static event Action<string> OnMensajeChanged;
    public static event Action<bool>   OnGenerandoPreguntas;

    // Llamado desde GameStateManager para notificar el estado de generación
    public static void NotifyGenerating(bool generando)
    {
        OnGenerandoPreguntas?.Invoke(generando);
    }

    // Llamado desde GameStateManager.RPC_MostrarMensajeAnimador (se ejecuta en todos los clientes)
    public static void MostrarMensaje(string mensaje)
    {
        OnMensajeChanged?.Invoke(mensaje);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        GameStateManager.OnStateChangedEvent += HandleStateChanged;
    }

    private void OnDisable()
    {
        GameStateManager.OnStateChangedEvent -= HandleStateChanged;
    }

    // ─── Comentarios reactivos ─────────────────────────────────────

    private void HandleStateChanged(GameStateManager.GameState estado)
    {
        // Solo el host genera y distribuye comentarios
        if (GameStateManager.Instance == null || !GameStateManager.Instance.Object.HasStateAuthority) return;
        // No hablar durante el lobby
        if (!GameStateManager.Instance.IsGameStarted) return;

        switch (estado)
        {
            case GameStateManager.GameState.Countdown:
                GenerarYMostrar("nueva ronda comenzando");
                break;
            case GameStateManager.GameState.RoundEnd:
                GenerarYMostrar("ronda terminada");
                break;
            case GameStateManager.GameState.Stealing:
                GenerarYMostrar("robo de puntos");
                break;
            case GameStateManager.GameState.GameOver:
                GenerarYMostrar("ganador del juego");
                break;
        }
    }

    private bool _generandoComentario = false;

    private void GenerarYMostrar(string contexto)
    {
        if (_generandoComentario || OllamaService.Instance == null) return;
        _generandoComentario = true;

        OllamaService.Instance.GenerarComentario(contexto, mensaje =>
        {
            GameStateManager.Instance?.RPC_MostrarMensajeAnimador(mensaje);
            _generandoComentario = false;
        });
    }
}
