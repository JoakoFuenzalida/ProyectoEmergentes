using System;
using UnityEngine;

// AnimadorIA: MonoBehaviour simple.
// El mensaje se sincroniza via GameStateManager.MensajeAnimador ([Networked]),
// que Fusion entrega automáticamente a todos los clientes.
public class AnimadorIA : MonoBehaviour
{
    public static AnimadorIA Instance { get; private set; }

    public static event Action<string> OnMensajeChanged;
    public static event Action<bool>   OnGenerandoPreguntas;

    private bool _generandoComentario = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()  => GameStateManager.OnStateChangedEvent += HandleStateChanged;
    private void OnDisable() => GameStateManager.OnStateChangedEvent -= HandleStateChanged;

    // Llamado por GameStateManager.OnMensajeAnimadorChanged en TODOS los clientes
    public static void NotifyMensaje(string mensaje)
    {
        Debug.Log($"[AnimadorIA] NotifyMensaje → '{mensaje}' | suscriptores={OnMensajeChanged?.GetInvocationList()?.Length ?? 0}");
        OnMensajeChanged?.Invoke(mensaje);
    }

    // Llamado por GameStateManager cuando inicia/termina la generación de preguntas
    public static void NotifyGenerating(bool generando) => OnGenerandoPreguntas?.Invoke(generando);

    // ─── Comentarios reactivos (solo host) ────────────────────────

    private void HandleStateChanged(GameStateManager.GameState estado)
    {
        if (GameStateManager.Instance == null || !GameStateManager.Instance.Object.HasStateAuthority) return;
        if (!GameStateManager.Instance.IsGameStarted) return;

        switch (estado)
        {
            case GameStateManager.GameState.Countdown:  GenerarYMostrar("nueva ronda comenzando"); break;
            case GameStateManager.GameState.RoundEnd:   GenerarYMostrar("ronda terminada");        break;
            case GameStateManager.GameState.Stealing:   GenerarYMostrar("robo de puntos");         break;
            case GameStateManager.GameState.GameOver:   GenerarYMostrar("ganador del juego");      break;
        }
    }

    private void GenerarYMostrar(string contexto)
    {
        if (_generandoComentario || OllamaService.Instance == null) return;
        _generandoComentario = true;

        OllamaService.Instance.GenerarComentario(contexto, mensaje =>
        {
            // Escribir en la propiedad [Networked] de GameStateManager —
            // Fusion la replica automáticamente a todos los clientes.
            if (GameStateManager.Instance != null && !string.IsNullOrEmpty(mensaje))
            {
                GameStateManager.Instance.MensajeAnimador = mensaje.Length > 510
                    ? mensaje.Substring(0, 510)
                    : mensaje;
            }
            _generandoComentario = false;
        });
    }
}
