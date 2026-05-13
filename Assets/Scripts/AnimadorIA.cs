using System;
using System.Collections;
using UnityEngine;
using Fusion;

// AnimadorIA como NetworkBehaviour para que los comentarios se sincronicen
// via propiedad [Networked], garantizando que cualquier cliente los reciba.
public class AnimadorIA : NetworkBehaviour
{
    public static AnimadorIA Instance { get; private set; }

    [Networked(OnChanged = nameof(OnMensajeChanged_Fusion))]
    public NetworkString<_512> MensajeRed { get; set; }

    public static event Action<string> OnMensajeChanged;
    public static event Action<bool>   OnGenerandoPreguntas;

    private bool _generandoComentario = false;

    public override void Spawned()
    {
        Instance = this;
        GameStateManager.OnStateChangedEvent += HandleStateChanged;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
        GameStateManager.OnStateChangedEvent -= HandleStateChanged;
    }

    // Fusion llama esto en TODOS los clientes cuando MensajeRed cambia
    private static void OnMensajeChanged_Fusion(Changed<AnimadorIA> changed)
    {
        string msg = changed.Behaviour.MensajeRed.ToString();
        if (!string.IsNullOrEmpty(msg))
            OnMensajeChanged?.Invoke(msg);
    }

    // ─── API estática usada por GameStateManager ──────────────────

    public static void NotifyGenerating(bool generando)
        => OnGenerandoPreguntas?.Invoke(generando);

    // ─── Comentarios reactivos (solo host) ────────────────────────

    private void HandleStateChanged(GameStateManager.GameState estado)
    {
        if (!Object.HasStateAuthority) return;
        if (GameStateManager.Instance == null || !GameStateManager.Instance.IsGameStarted) return;

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

    private void GenerarYMostrar(string contexto)
    {
        if (_generandoComentario || OllamaService.Instance == null) return;
        _generandoComentario = true;

        OllamaService.Instance.GenerarComentario(contexto, mensaje =>
        {
            if (!string.IsNullOrEmpty(mensaje) && Object.HasStateAuthority)
            {
                MensajeRed = mensaje.Length > 510
                    ? mensaje.Substring(0, 510)
                    : mensaje;
            }
            _generandoComentario = false;
        });
    }
}
