using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Servicio TTS con dos modos seleccionables desde el Inspector:
///   - SystemSpeech : invoca PowerShell (gratis, local, sin internet) para pruebas con 40 usuarios
///   - ElevenLabs   : API cloud de alta calidad para demos
/// </summary>
public class TTSService : MonoBehaviour
{
    public static TTSService Instance { get; private set; }

    public enum ModoTTS { SystemSpeech, ElevenLabs }

    [Header("Modo TTS")]
    [Tooltip("SystemSpeech = voz Windows gratis (pruebas). ElevenLabs = voz cloud de calidad (demo).")]
    [SerializeField] private ModoTTS modo      = ModoTTS.SystemSpeech;
    [SerializeField] private bool    ttsActivo = true;

    [Header("ElevenLabs — solo si Modo = ElevenLabs")]
    [SerializeField] private string apiKey  = "TU_API_KEY_AQUI";
    [SerializeField] private string voiceId = "pNInz6obpgDQGcFmaJgB";

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

    // ─── System.Speech vía PowerShell ────────────────────────────────
    // PowerShell tiene acceso a .NET Framework completo (incluido System.Speech)
    // aunque Unity use .NET Standard. Se genera un WAV temporal y se carga.

    private IEnumerator CoroutineSystemSpeech(string texto, Action<AudioClip> onDone)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        string wavPath    = Path.Combine(Application.temporaryCachePath, "tts_temp.wav");
        string scriptPath = Path.Combine(Application.temporaryCachePath, "tts_script.ps1");

        // Escribir script a archivo evita problemas de escape con caracteres especiales
        string script = BuildPowerShellScript(texto, wavPath);
        File.WriteAllText(scriptPath, script, new UTF8Encoding(false));

        // Ejecutar PowerShell en hilo secundario para no bloquear Unity
        bool      listo = false;
        Exception error = null;

        var hilo = new System.Threading.Thread(() =>
        {
            try
            {
                var psi = new ProcessStartInfo("powershell.exe")
                {
                    Arguments      = $"-NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                p.WaitForExit();
            }
            catch (Exception ex) { error = ex; }
            finally { listo = true; }
        });
        hilo.IsBackground = true;
        hilo.Start();

        while (!listo) yield return null;

        if (error != null)
        {
            UnityEngine.Debug.LogError($"[TTS SystemSpeech] Error al lanzar PowerShell: {error.Message}");
            onDone?.Invoke(null);
            yield break;
        }

        if (!File.Exists(wavPath))
        {
            UnityEngine.Debug.LogWarning("[TTS SystemSpeech] No se generó el WAV. ¿Hay voces instaladas en Windows?");
            onDone?.Invoke(null);
            yield break;
        }

        // Cargar el WAV generado como AudioClip
        string uri = new Uri(wavPath).AbsoluteUri;
        using var www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV);
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
            onDone?.Invoke(DownloadHandlerAudioClip.GetContent(www));
        else
        {
            UnityEngine.Debug.LogError($"[TTS SystemSpeech] Error cargando WAV: {www.error}");
            onDone?.Invoke(null);
        }
#else
        UnityEngine.Debug.LogWarning("[TTS] SystemSpeech solo disponible en Windows.");
        yield return null;
        onDone?.Invoke(null);
#endif
    }

    [Header("System.Speech — Voz local")]
    [Tooltip("Locale de la voz Windows. Ejemplos: es-ES, es-MX, es-AR. Deja vacío para voz por defecto.")]
    [SerializeField] private string localeSpeech = "es-ES";

    private string BuildPowerShellScript(string texto, string wavPath)
    {
        // Usamos here-string de PowerShell (@'...'@) para evitar escapes
        string wavPathPs = wavPath.Replace("\\", "\\\\");
        string localeCode = string.IsNullOrWhiteSpace(localeSpeech) ? "" : localeSpeech.Trim();

        string selectVoice = string.IsNullOrEmpty(localeCode)
            ? "try { $synth.SelectVoiceByHints('Male') } catch {}"
            : $"try {{ $synth.SelectVoiceByHints('Male', 'Adult', 0, [System.Globalization.CultureInfo]::new('{localeCode}')) }} catch {{ try {{ $synth.SelectVoiceByHints('Male') }} catch {{}} }}";

        return
$@"Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
{selectVoice}
$synth.SetOutputToWaveFile('{wavPathPs}')
$synth.Speak(@'
{texto}
'@)
$synth.Dispose()
";
    }

    // ─── ElevenLabs ───────────────────────────────────────────────────

    private IEnumerator CoroutineElevenLabs(string texto, Action<AudioClip> onDone)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "TU_API_KEY_AQUI")
        {
            UnityEngine.Debug.LogWarning("[TTS] API key de ElevenLabs no configurada en el Inspector.");
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
            UnityEngine.Debug.LogError($"[TTS ElevenLabs] HTTP {www.responseCode}: {www.error}");
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
}
