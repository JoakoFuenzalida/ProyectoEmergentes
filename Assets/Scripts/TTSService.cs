using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Convierte texto a audio usando la API de ElevenLabs.
/// Solicita PCM 16-bit crudo (sin contenedor) para decodificarlo
/// directamente a AudioClip sin dependencias externas.
/// </summary>
public class TTSService : MonoBehaviour
{
    public static TTSService Instance { get; private set; }

    [Header("ElevenLabs — Configuración")]
    [SerializeField] private string apiKey   = "TU_API_KEY_AQUI";
    [SerializeField] private string voiceId  = "pNInz6obpgDQGcFmaJgB"; // Adam (multilingual)

    [Header("Calidad de voz")]
    [Range(0f, 1f)] [SerializeField] private float stability        = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float similarityBoost  = 0.80f;
    [Range(0f, 1f)] [SerializeField] private float style            = 0.20f;

    // PCM 16-bit mono a 16 kHz — fácil de decodificar, sin librerías extra
    private const string OUTPUT_FORMAT = "pcm_16000";
    private const int    SAMPLE_RATE   = 16000;
    private const string MODEL_ID      = "eleven_multilingual_v2";
    private const string BASE_URL      = "https://api.elevenlabs.io/v1/text-to-speech/";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── API pública ─────────────────────────────────────────────────

    /// <summary>
    /// Genera audio para el texto dado. onDone recibe el AudioClip listo
    /// (o null si hubo error). Se llama desde el hilo principal de Unity.
    /// </summary>
    public void GenerarAudio(string texto, Action<AudioClip> onDone)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "TU_API_KEY_AQUI")
        {
            Debug.LogWarning("[TTS] API key no configurada. Agrega tu key de ElevenLabs en el Inspector.");
            onDone?.Invoke(null);
            return;
        }

        if (string.IsNullOrWhiteSpace(texto)) { onDone?.Invoke(null); return; }

        StartCoroutine(GenerarAudioCoroutine(texto, onDone));
    }

    // ─── Coroutine principal ──────────────────────────────────────────

    private IEnumerator GenerarAudioCoroutine(string texto, Action<AudioClip> onDone)
    {
        string url = $"{BASE_URL}{voiceId}?output_format={OUTPUT_FORMAT}";

        // Cuerpo JSON de la petición
        string json = BuildRequestJson(texto);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

        using var www = new UnityWebRequest(url, "POST");
        www.uploadHandler   = new UploadHandlerRaw(bodyBytes);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("xi-api-key",    apiKey);
        www.SetRequestHeader("Content-Type",  "application/json");
        www.SetRequestHeader("Accept",        "audio/mpeg");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[TTS] Error HTTP {www.responseCode}: {www.error}\n{www.downloadHandler.text}");
            onDone?.Invoke(null);
            yield break;
        }

        byte[] pcmData = www.downloadHandler.data;
        if (pcmData == null || pcmData.Length < 2)
        {
            Debug.LogError("[TTS] Respuesta vacía de ElevenLabs.");
            onDone?.Invoke(null);
            yield break;
        }

        // Decodificar PCM 16-bit little-endian → float[]
        AudioClip clip = PcmToAudioClip(pcmData);
        onDone?.Invoke(clip);
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private string BuildRequestJson(string texto)
    {
        // Escapamos comillas para no romper el JSON manual
        string safe = texto.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $@"{{
  ""text"": ""{safe}"",
  ""model_id"": ""{MODEL_ID}"",
  ""voice_settings"": {{
    ""stability"": {stability.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},
    ""similarity_boost"": {similarityBoost.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},
    ""style"": {style.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},
    ""use_speaker_boost"": true
  }}
}}";
    }

    private static AudioClip PcmToAudioClip(byte[] pcmData)
    {
        int sampleCount = pcmData.Length / 2; // 2 bytes por muestra (16-bit)
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            // Little-endian 16-bit signed → float [-1, 1]
            short raw = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
            samples[i] = raw / 32768f;
        }

        AudioClip clip = AudioClip.Create("tts_clip", sampleCount, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
