using UnityEngine;

/// <summary>
/// Reproduce el buzzer de respuesta incorrecta.
/// Adjunta este componente a cualquier GameObject de la escena,
/// arrastra el clip en el Inspector y ajusta el volumen.
/// </summary>
public class WrongAnswerAudio : MonoBehaviour
{
    [Header("Clip de audio")]
    [SerializeField] private AudioClip buzzerClip;

    [Header("Volumen  (0 = silencio, 1 = máximo)")]
    [Range(0f, 1f)]
    [SerializeField] private float volumen = 0.4f;

    private AudioSource _src;

    private void Awake()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake  = false;
        _src.spatialBlend = 0f;   // 2D — mismo volumen en toda la escena
        _src.loop         = false;
    }

    private void OnEnable()  => GameStateManager.OnAnswerResultEvent += OnResultado;
    private void OnDisable() => GameStateManager.OnAnswerResultEvent -= OnResultado;

    private void OnResultado(bool correct, int points, string playerName, string teamName)
    {
        if (!correct && buzzerClip != null)
            _src.PlayOneShot(buzzerClip, volumen);
    }
}
