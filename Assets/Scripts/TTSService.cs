using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.IO;
using System.Speech.Synthesis;
using System.Threading;
#endif

/// <summary>
/// Servicio TTS con dos modos seleccionables desde el Inspector:
///   - SystemSpeech : voz local de Windows, gratis, sin internet (para pruebas)
///   - ElevenLabs   : voz de alta calidad vía API cloud (para demos)
/// </summary>
public class TTSService : MonoBehaviour
{
    public static TTSService Instance { get; private set; }

    public enum ModoTTS { SystemSpeech, ElevenLabs }

    [Header("Modo TTS")]
    [Tooltip("SystemSpeech = voz Windows gratis (pruebas). ElevenLabs = voz cloud de calidad (demo).")]
    [SerializeField] private ModoTTS modo     = ModoTTS.SystemSpeech;
    [SerializeField] private bool    ttsActivo = true;

    [Header("ElevenLabs — solo si Modo = ElevenLabs")]
    [SerializeField] private string apiKey  = "TU_API_KEY_AQUI";
    [SerializeField] private string voiceId = "pNInz6obpgDQGcFmaJgB"; // Adam (multilingual)

    [Header("Calidad de voz ElevenLabs")]
    [Range(0f, 1f)] [SerializeField] private float stability       = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float similarityBoost = 0.80f;
    [Range(0f, 1f)] [SerializeField] private float style           = 0.20f;

    private const string OUTPUT_FORMAT = "pcm_16000";
    private const int    SAMPLE_RATE   = 16000;
    private const string MODEL_ID      = "eleven_multilingual_v2";
    private const string BASE_URL      = "https://api.elevenlabs.io/v1/text-to-speech/";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─── API pública ──────────────────────────────────────────────────

    public void GenerarAudio(string texto, Action<AudioClip> onDone)
    {
        if (!ttsActivo || string.IsNullOrWhiteSpace(texto)) { onDone?.Invoke(null); return; }

        if (modo == ModoTTS.ElevenLabs)
            StartCoroutine(CoroutineElevenLabs(texto, onDone));
        else
            StartCoroutine(CoroutineSystemSpeech(texto, onDone));
    }

    // ─── System.Speech (Windows local) ───────────────────────────────

    private IEnumerator CoroutineSystemSpeech(string texto, Action<AudioClip> onDone)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        byte[]    wavData = null;
        bool      listo   = false;
        Exception error   = null;

        // Corre en un hilo secundario para no bloquear Unity
        var hilo = new Thread(() =>
        {
            try
            {
                using var synth = new SpeechSynthesizer();

                // Intentar voz en español; si no está instalada, usa la voz por defecto
                try
                {
                    synth.SelectVoiceByHints(VoiceGender.Male, VoiceAge.Adult, 0,
                        new System.Globalization.CultureInfo("es-ES"));
                }
                catch
                {
                    try { synth.SelectVoiceByHints(VoiceGender.Male); } catch { }
                }

                using var stream = new MemoryStream();
                synth.SetOutputToWaveStream(stream);
                synth.Speak(texto);
                wavData = stream.ToArray();
            }
            catch (Exception ex) { error = ex; }
            finally { listo = true; }
        });

        hilo.IsBackground = true;
        hilo.Start();

        while (!listo) yield return null; // espera sin bloquear Unity

        if (error != null)
        {
            Debug.LogError($"[TTS SystemSpeech] {error.Message}");
            onDone?.Invoke(null);
            yield break;
        }

        onDone?.Invoke(wavData != null && wavData.Length > 44 ? WavToAudioClip(wavData) : null);

#else
        Debug.LogWarning("[TTS] System.Speech solo está disponible en Windows.");
        yield return null;
        onDone?.Invoke(null);
#endif
    }

    // ─── ElevenLabs (API cloud) ───────────────────────────────────────

    private IEnumerator CoroutineElevenLabs(string texto, Action<AudioClip> onDone)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "TU_API_KEY_AQUI")
        {
            Debug.LogWarning("[TTS] API key de ElevenLabs no configurada en el Inspector.");
            onDone?.Invoke(null);
            yield break;
        }

        string url  = $"{BASE_URL}{voiceId}?output_format={OUTPUT_FORMAT}";
        byte[] body = Encoding.UTF8.GetBytes(BuildRequestJson(texto));

        using var www = new UnityWebRequest(url, "POST");
        www.uploadHandler   = new UploadHandlerRaw(body);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("xi-api-key",   apiKey);
        www.SetRequestHeader("Content-Type", "application/json");
        www.SetRequestHeader("Accept",       "audio/mpeg");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[TTS ElevenLabs] HTTP {www.responseCode}: {www.error}");
            onDone?.Invoke(null);
            yield break;
        }

        byte[] pcm = www.downloadHandler.data;
        if (pcm == null || pcm.Length < 2) { onDone?.Invoke(null); yield break; }

        onDone?.Invoke(PcmToAudioClip(pcm));
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private string BuildRequestJson(string texto)
    {
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

    // PCM 16-bit crudo (ElevenLabs) → AudioClip
    private static AudioClip PcmToAudioClip(byte[] pcm)
    {
        int     n       = pcm.Length / 2;
        float[] samples = new float[n];
        for (int i = 0; i < n; i++)
        {
            short raw  = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
            samples[i] = raw / 32768f;
        }
        var clip = AudioClip.Create("tts_clip", n, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // WAV (System.Speech) → AudioClip
    private static AudioClip WavToAudioClip(byte[] wav)
    {
        // Buscar el chunk "data" (puede venir después de otros chunks)
        int dataStart = 44;
        for (int i = 12; i < wav.Length - 8; i++)
        {
            if (wav[i] == 'd' && wav[i+1] == 'a' && wav[i+2] == 't' && wav[i+3] == 'a')
            {
                dataStart = i + 8;
                break;
            }
        }

        int channels   = wav[22] | (wav[23] << 8);
        int sampleRate = wav[24] | (wav[25] << 8) | (wav[26] << 16) | (wav[27] << 24);
        int bitDepth   = wav[34] | (wav[35] << 8);
        int bps        = bitDepth / 8;
        int n          = (wav.Length - dataStart) / bps / channels;

        float[] samples = new float[n * channels];
        for (int i = 0; i < samples.Length; i++)
        {
            int idx = dataStart + i * bps;
            samples[i] = bitDepth == 16
                ? (short)(wav[idx] | (wav[idx + 1] << 8)) / 32768f
                : (wav[idx] - 128) / 128f; // 8-bit
        }

        var clip = AudioClip.Create("tts_clip", n, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
