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

    [Header("Tiempos")]
    [SerializeField] private float duracionViñeta    = 10f; // para mensajes normales de juego
    [SerializeField] private float duracionPagina    = 3.8f; // tiempo por página en el intro
    [SerializeField] private float pausaEntrePaginas = 0.4f; // pausa breve entre páginas

    private Animator  _animator;
    private Coroutine _viñetaCoroutine;
    private bool      _enPodio       = false;
    private bool      _introActiva   = false; // bloquea mensajes externos durante el intro

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance  = this;
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        OcultarTodas();
        TeleportarA(posEstudio);
    }

    private void OnEnable()
    {
        GameStateManager.OnStateChangedEvent += HandleEstadoJuego;
        AnimadorIA.OnMensajeChanged           += OnMensajeIA;
    }

    private void OnDisable()
    {
        GameStateManager.OnStateChangedEvent -= HandleEstadoJuego;
        AnimadorIA.OnMensajeChanged           -= OnMensajeIA;
    }

    // ─── Teleport según estado ───────────────────────────────────────

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
                IniciarSecuenciaIntro();
                break;

            case GameStateManager.GameState.Countdown:
                // Detener intro si aún estaba corriendo
                _introActiva = false;
                _enPodio = false;
                TeleportarA(posEstudio);
                DetenerViñeta();
                break;

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

    private void TeleportarA(Transform destino)
    {
        if (destino == null) return;
        transform.position = destino.position;
        transform.rotation = destino.rotation;
    }

    // ─── Secuencia paginada de intro ─────────────────────────────────

    private void IniciarSecuenciaIntro()
    {
        if (_viñetaCoroutine != null) StopCoroutine(_viñetaCoroutine);
        _viñetaCoroutine = StartCoroutine(SecuenciaIntro());
    }

    private IEnumerator SecuenciaIntro()
    {
        _introActiva = true;

        var gsm = GameStateManager.Instance;
        string eqA    = gsm != null ? gsm.NombreEquipoA.ToString() : "Equipo A";
        string eqB    = gsm != null ? gsm.NombreEquipoB.ToString() : "Equipo B";
        string liderA = gsm != null ? gsm.GetLiderNombre(1) : "Lider A";
        string liderB = gsm != null ? gsm.GetLiderNombre(2) : "Lider B";

        // Página 1 — presentación
        yield return StartCoroutine(Pagina(
            "¡Hola a todos!\nSoy Martín Cárcamo,\nsu presentador de hoy.", 4f));

        // Página 2 — nombre del programa
        yield return StartCoroutine(Pagina(
            "¡Bienvenidos a\n¿Qué Dice Chile?\nVersión Informática!", 4f));

        // Página 3 — equipos
        yield return StartCoroutine(Pagina(
            $"Hoy se enfrentan:\n{eqA}\nvs {eqB}", 3.8f));

        // Página 4 — cómo funcionan las preguntas
        yield return StartCoroutine(Pagina(
            "Haré preguntas como:\n'¿Qué dirían 100 informáticos\nde la PUCV sobre...?'", 4.5f));

        // Página 5 — objetivo
        yield return StartCoroutine(Pagina(
            "¡Deben adivinar las\nrespuestas más populares\nde nuestra encuesta!", 3.8f));

        // Página 6 — turnos
        yield return StartCoroutine(Pagina(
            "El juego es por turnos.\nCada equipo responde\ncuando le corresponde.", 3.8f));

        // Página 7 — buzzer
        yield return StartCoroutine(Pagina(
            "En el podio, el primero\nen presionar ESPACIO\nresponde la pregunta.", 4f));

        // Página 8 — llamada al podio
        yield return StartCoroutine(Pagina(
            $"{liderA} y {liderB},\n¡al podio!\n¡Que comience el juego!", 4f));

        OcultarTodas();
        SetHablando(false);
        _introActiva = false;
    }

    // Muestra una página, espera que se lea y hace una pausa breve antes de la siguiente
    private IEnumerator Pagina(string texto, float duracion)
    {
        // Mostrar en viñetas de estudio (siempre en intro)
        Activar(viñetaEquipoA, textoEquipoA, texto);
        Activar(viñetaEquipoB, textoEquipoB, texto);
        Ocultar(viñetaPodio);

        // Sincronizar panel 2D del animador
        UIGameController.Instance?.ActualizarTextoAnimador(texto);

        SetHablando(true);
        yield return new WaitForSeconds(duracion);

        OcultarTodas();
        SetHablando(false);
        yield return new WaitForSeconds(pausaEntrePaginas);
    }

    // ─── Mensajes normales del juego (vía AnimadorIA) ────────────────

    // Solo se ejecuta si el intro ya terminó
    private void OnMensajeIA(string mensaje)
    {
        if (_introActiva) return;
        if (_viñetaCoroutine != null) StopCoroutine(_viñetaCoroutine);
        _viñetaCoroutine = StartCoroutine(CoroutineViñetaNormal(mensaje));

        // Sincronizar panel 2D
        UIGameController.Instance?.ActualizarTextoAnimador(mensaje);
    }

    private IEnumerator CoroutineViñetaNormal(string mensaje)
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

        SetHablando(true);
        yield return new WaitForSeconds(duracionViñeta);

        OcultarTodas();
        SetHablando(false);
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private void DetenerViñeta()
    {
        if (_viñetaCoroutine != null) { StopCoroutine(_viñetaCoroutine); _viñetaCoroutine = null; }
        OcultarTodas();
        SetHablando(false);
    }

    private void OcultarTodas()
    {
        Ocultar(viñetaPodio);
        Ocultar(viñetaEquipoA);
        Ocultar(viñetaEquipoB);
    }

    private void SetHablando(bool valor)
    {
        if (_animator != null) _animator.SetBool("Hablando", valor);
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
