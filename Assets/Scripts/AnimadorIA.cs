using System;
using System.Collections;
using UnityEngine;
using Fusion;

public class AnimadorIA : NetworkBehaviour
{
    public static AnimadorIA Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private float duracionMensaje = 5f;
    [SerializeField] private int   cantidadPreguntas = 5;

    [Networked] private NetworkString<_512> MensajeActual { get; set; }

    public static event Action<string> OnMensajeChanged;
    public static event Action<bool>   OnGenerandoPreguntas;

    private ChangeDetector _changes;
    private bool _generandoComentario = false;

    public override void Spawned()
    {
        Instance = this;
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

        GameStateManager.OnStateChangedEvent    += HandleStateChanged;
        GameStateManager.OnErrorAddedEvent      += HandleErrorAdded;
        TurnManager.OnTurnChangedEvent          += HandleTurnChanged;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
        GameStateManager.OnStateChangedEvent    -= HandleStateChanged;
        GameStateManager.OnErrorAddedEvent      -= HandleErrorAdded;
        TurnManager.OnTurnChangedEvent          -= HandleTurnChanged;
    }

    public override void Render()
    {
        foreach (var change in _changes.DetectChanges(this, out _, out _))
        {
            if (change == nameof(MensajeActual))
                OnMensajeChanged?.Invoke(MensajeActual.ToString());
        }
    }

    // ─── Generación de preguntas (solo host) ──────────────────────

    public void GenerarPreguntas()
    {
        if (!Object.HasStateAuthority) return;
        if (OllamaService.Instance == null)
        {
            Debug.LogWarning("[AnimadorIA] OllamaService no encontrado en la escena.");
            return;
        }

        OnGenerandoPreguntas?.Invoke(true);
        MostrarMensaje("Estoy preparando las preguntas, un momento po'...");

        OllamaService.Instance.GenerarPreguntas(cantidadPreguntas,
            preguntas =>
            {
                GameStateManager.Instance?.RPC_CargarPreguntasDinamicas(preguntas);
                OnGenerandoPreguntas?.Invoke(false);
                MostrarMensaje($"¡Ya po'! Tengo {preguntas.Length} preguntas listas. ¡Empecemos!");
            },
            error =>
            {
                OnGenerandoPreguntas?.Invoke(false);
                Debug.LogError($"[AnimadorIA] {error}");
                MostrarMensaje("Hubo un problema con la IA po', usaremos preguntas del banco.");
            });
    }

    // ─── Comentarios reactivos (solo host) ───────────────────────

    private void HandleStateChanged(GameStateManager.GameState estado)
    {
        if (!Object.HasStateAuthority) return;

        switch (estado)
        {
            case GameStateManager.GameState.Countdown:
                GenerarYMostrar("el juego acaba de iniciar una nueva ronda");
                break;
            case GameStateManager.GameState.WaitingForBuzzer:
                GenerarYMostrar("los jugadores esperan para apretar el buzzer");
                break;
            case GameStateManager.GameState.RoundEnd:
                GenerarYMostrar("la ronda terminó y los puntos fueron otorgados");
                break;
            case GameStateManager.GameState.GameOver:
                GenerarYMostrar("el juego terminó y hay un equipo ganador");
                break;
            case GameStateManager.GameState.Stealing:
                GenerarYMostrar("el equipo contrario tiene la chance de robar los puntos");
                break;
        }
    }

    private void HandleErrorAdded(int errores)
    {
        if (!Object.HasStateAuthority) return;
        string ctx = errores >= 3
            ? "el equipo acumuló 3 errores y pierde el control"
            : $"el equipo tuvo un error, ya van {errores}";
        GenerarYMostrar(ctx);
    }

    private void HandleTurnChanged(PlayerRef jugador)
    {
        if (!Object.HasStateAuthority) return;
        // Solo comentar cada 2 cambios de turno para no ser repetitivo
        if (UnityEngine.Random.value > 0.5f)
            GenerarYMostrar("el turno pasó al siguiente jugador del equipo");
    }

    // ─── Infraestructura de mensajes ──────────────────────────────

    private void GenerarYMostrar(string contexto)
    {
        if (_generandoComentario || OllamaService.Instance == null) return;
        _generandoComentario = true;

        OllamaService.Instance.GenerarComentario(contexto, mensaje =>
        {
            MostrarMensaje(mensaje);
            _generandoComentario = false;
        });
    }

    private void MostrarMensaje(string mensaje)
    {
        if (!Object.HasStateAuthority) return;
        // Truncar a 511 chars (límite de NetworkString<_512>)
        if (mensaje.Length > 510) mensaje = mensaje.Substring(0, 510);
        MensajeActual = mensaje;
    }

    // Llamado público para mensajes directos desde el host sin Ollama
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_MostrarMensajeDirecto(string mensaje)
    {
        OnMensajeChanged?.Invoke(mensaje);
    }
}
