using UnityEngine;
using TMPro;

/// <summary>
/// Muestra el nickname de red del jugador flotando sobre su cabeza.
/// Maneja el billboard internamente usando la cámara del jugador LOCAL
/// (HasInputAuthority), lo que garantiza orientación correcta en todos los clientes
/// sin depender del orden no determinista de Camera.allCameras.
/// </summary>
public class PlayerNameTag : MonoBehaviour
{
    [SerializeField] private TMP_Text textoNombre;

    private PlayerNetworkData _data;
    private string            _ultimoNombre = "";
    private Camera            _localCam;

    private void Awake()
    {
        _data = GetComponentInParent<PlayerNetworkData>();
        if (textoNombre == null)
            textoNombre = GetComponentInChildren<TMP_Text>();

        // Si el diseñador dejó BillboardCanvas en este mismo GO, lo desactivamos
        // para que no compita con nuestra rotación billboard.
        var bc = GetComponent<BillboardCanvas>();
        if (bc != null) bc.enabled = false;
    }

    private void LateUpdate()
    {
        if (_data == null || textoNombre == null) return;

        // ── Actualizar texto ─────────────────────────────────────
        string nombre = _data.PlayerName.ToString();
        if (nombre != _ultimoNombre)
        {
            _ultimoNombre = nombre;
            textoNombre.text = nombre;
        }

        // ── Billboard: apuntar hacia la cámara del jugador local ─
        Camera cam = GetLocalCamera();
        if (cam == null) return;

        // TMP 3D muestra la cara legible en la dirección -Z local,
        // por eso usamos el vector invertido: +Z apunta AWAY de la cámara
        // y -Z (la cara del texto) queda mirando hacia ella.
        Vector3 dirToCamera = cam.transform.position - transform.position;
        if (dirToCamera.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(-dirToCamera, Vector3.up);
    }

    /// <summary>
    /// Busca y cachea la cámara del jugador con InputAuthority en esta máquina.
    /// FindObjectsByType solo se ejecuta cuando el caché es inválido.
    /// </summary>
    private Camera GetLocalCamera()
    {
        if (_localCam != null && _localCam.isActiveAndEnabled)
            return _localCam;

        // Buscar el PlayerController cuyo objeto Fusion es local
        foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (pc.Object != null && pc.Object.HasInputAuthority)
            {
                _localCam = pc.playerCamera;
                return _localCam;
            }
        }

        // Fallback: primera cámara activa (antes de que Fusion spawnie jugadores)
        Camera[] all = Camera.allCameras;
        return all.Length > 0 ? all[0] : null;
    }
}
