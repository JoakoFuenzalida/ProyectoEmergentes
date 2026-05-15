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
            case GameStateManager.GameState.Intro:           GenerarBienvenida();                         break;
            case GameStateManager.GameState.Countdown:       GenerarYMostrar("nueva ronda comenzando");   break;
            case GameStateManager.GameState.WaitingForBuzzer: GenerarYMostrar("quien toca el buzzer primero"); break;
            case GameStateManager.GameState.RoundEnd:        GenerarYMostrar("ronda terminada");          break;
            case GameStateManager.GameState.Stealing:        GenerarYMostrar("robo de puntos");           break;
            case GameStateManager.GameState.GameOver:        GenerarYMostrar("ganador del juego");        break;
        }
    }

    private void GenerarBienvenida()
    {
        if (_generandoComentario) { Debug.Log("[AnimadorIA] GenerarBienvenida: ya generando, skip"); return; }
        _generandoComentario = true;

        var gsm = GameStateManager.Instance;
        string equipoA = gsm != null ? gsm.NombreEquipoA.ToString() : "A";
        string equipoB = gsm != null ? gsm.NombreEquipoB.ToString() : "B";
        string liderA  = gsm != null ? gsm.GetLiderNombre(1) : "Jugador";
        string liderB  = gsm != null ? gsm.GetLiderNombre(2) : "Jugador";

        // Mostrar mensaje inmediato mientras el LLM procesa
        string mensajeInmediato =
            $"¡Bienvenidos a los 100 Chilenos Dicen! " +
            $"Hoy {equipoA} vs {equipoB}. " +
            $"¡{liderA} y {liderB} al podio!";
        if (gsm != null) gsm.ActualizarMensajeAnimador(mensajeInmediato);
        Debug.Log($"[AnimadorIA] GenerarBienvenida START — OllamaService={(OllamaService.Instance != null ? "OK" : "NULL")}");

        if (OllamaService.Instance == null)
        {
            _generandoComentario = false;
            return;
        }

        string contexto = $"bienvenida al programa, " +
                          $"equipo {equipoA} con lider {liderA} " +
                          $"vs equipo {equipoB} con lider {liderB}, " +
                          $"invita a los lideres al podio";

        OllamaService.Instance.GenerarBienvenida(contexto, mensaje =>
        {
            Debug.Log($"[AnimadorIA] GenerarBienvenida CALLBACK → '{mensaje}'");
            if (GameStateManager.Instance != null && !string.IsNullOrEmpty(mensaje))
                GameStateManager.Instance.ActualizarMensajeAnimador(mensaje);
            _generandoComentario = false;
        });
    }

    private void GenerarYMostrar(string contexto)
    {
        if (_generandoComentario || OllamaService.Instance == null) return;
        _generandoComentario = true;

        OllamaService.Instance.GenerarComentario(contexto, mensaje =>
        {
            // Usar ActualizarMensajeAnimador para garantizar que el ChangeDetector
            // detecte el cambio aunque el texto sea igual al anterior (_mensajeVersion++)
            if (GameStateManager.Instance != null && !string.IsNullOrEmpty(mensaje))
                GameStateManager.Instance.ActualizarMensajeAnimador(mensaje);
            _generandoComentario = false;
        });
    }
}
