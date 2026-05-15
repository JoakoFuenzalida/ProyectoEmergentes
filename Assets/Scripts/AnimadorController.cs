using System.Collections;
using UnityEngine;
using TMPro;

public class AnimadorController : MonoBehaviour
{
    public static AnimadorController Instance { get; private set; }

    [Header("Posiciones de teleport")]
    [SerializeField] private Transform posPodio;
    [SerializeField] private Transform posEstudio;

    [Header("Viñeta — Podio (una sola, mirando al frente)")]
    [SerializeField] private GameObject viñetaPodio;
    [SerializeField] private TMP_Text   textoPodio;

    [Header("Viñeta — Estudio (una por equipo, rotaciones opuestas)")]
    [SerializeField] private GameObject viñetaEquipoA;
    [SerializeField] private TMP_Text   textoEquipoA;
    [SerializeField] private GameObject viñetaEquipoB;
    [SerializeField] private TMP_Text   textoEquipoB;

    [SerializeField] private float duracionViñeta = 12f;

    private Animator  _animator;
    private Coroutine _viñetaCoroutine;
    private bool      _enPodio = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance  = this;
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        Ocultar(viñetaPodio);
        Ocultar(viñetaEquipoA);
        Ocultar(viñetaEquipoB);
        TeleportarA(posEstudio);
    }

    private void OnEnable()
    {
        GameStateManager.OnStateChangedEvent += HandleEstadoJuego;
        AnimadorIA.OnMensajeChanged           += MostrarViñeta;
    }

    private void OnDisable()
    {
        GameStateManager.OnStateChangedEvent -= HandleEstadoJuego;
        AnimadorIA.OnMensajeChanged           -= MostrarViñeta;
    }

    // ─── Teleport según estado ───────────────────────────────────

    private void HandleEstadoJuego(GameStateManager.GameState estado)
    {
        switch (estado)
        {
            case GameStateManager.GameState.WaitingForBuzzer:
            case GameStateManager.GameState.TypingAnswer:
                _enPodio = true;
                TeleportarA(posPodio);
                Ocultar(viñetaEquipoA);
                Ocultar(viñetaEquipoB);
                break;

            case GameStateManager.GameState.Intro:
                _enPodio = false;
                TeleportarA(posEstudio);
                Ocultar(viñetaPodio);
                MostrarViñetaIntro();
                break;

            case GameStateManager.GameState.Countdown:
            case GameStateManager.GameState.Playing:
            case GameStateManager.GameState.Stealing:
            case GameStateManager.GameState.RoundEnd:
            case GameStateManager.GameState.GameOver:
                _enPodio = false;
                TeleportarA(posEstudio);
                Ocultar(viñetaPodio);
                break;
        }
    }

    private void MostrarViñetaIntro()
    {
        var gsm = GameStateManager.Instance;
        if (gsm == null) return;

        string equipoA = gsm.NombreEquipoA.ToString();
        string equipoB = gsm.NombreEquipoB.ToString();
        string liderA  = gsm.GetLiderNombre(1);
        string liderB  = gsm.GetLiderNombre(2);

        string mensaje =
            $"¡Bienvenidos a los 100 Chilenos Dicen!\n" +
            $"{equipoA} vs {equipoB}\n" +
            $"¡{liderA} y {liderB} al podio!";

        MostrarViñeta(mensaje);
    }

    private void TeleportarA(Transform destino)
    {
        if (destino == null) return;
        transform.position = destino.position;
        transform.rotation = destino.rotation;
    }

    // ─── Viñetas según posición ──────────────────────────────────

    private void MostrarViñeta(string mensaje)
    {
        Debug.Log($"[AnimadorController] MostrarViñeta '{mensaje}' | enPodio={_enPodio} | " +
                  $"viñetaPodio={viñetaPodio != null} textoPodio={textoPodio != null} | " +
                  $"viñetaA={viñetaEquipoA != null} textoA={textoEquipoA != null} | " +
                  $"viñetaB={viñetaEquipoB != null} textoB={textoEquipoB != null}");
        if (_viñetaCoroutine != null) StopCoroutine(_viñetaCoroutine);
        _viñetaCoroutine = StartCoroutine(CoroutineViñeta(mensaje));
    }

    private IEnumerator CoroutineViñeta(string mensaje)
    {
        if (_enPodio)
        {
            Activar(viñetaPodio,   textoPodio,   mensaje);
            Ocultar(viñetaEquipoA);
            Ocultar(viñetaEquipoB);
        }
        else
        {
            Activar(viñetaEquipoA, textoEquipoA, mensaje);
            Activar(viñetaEquipoB, textoEquipoB, mensaje);
            Ocultar(viñetaPodio);
        }

        if (_animator != null) _animator.SetBool("Hablando", true);

        yield return new WaitForSeconds(duracionViñeta);

        Ocultar(viñetaPodio);
        Ocultar(viñetaEquipoA);
        Ocultar(viñetaEquipoB);
        if (_animator != null) _animator.SetBool("Hablando", false);
    }

    private void Activar(GameObject canvas, TMP_Text texto, string mensaje)
    {
        if (canvas == null || texto == null) return;
        texto.text = mensaje;
        canvas.SetActive(true);
    }

    private void Ocultar(GameObject canvas)
    {
        if (canvas != null) canvas.SetActive(false);
    }
}