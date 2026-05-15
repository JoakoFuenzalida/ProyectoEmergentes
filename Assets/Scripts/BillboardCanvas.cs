using UnityEngine;

public class BillboardCanvas : MonoBehaviour
{
    private Camera _camCache;

    private void LateUpdate()
    {
        // Camera.main falla cuando la cámara global se desactiva en FPS.
        // Buscamos la primera cámara habilitada en la escena.
        if (_camCache == null || !_camCache.isActiveAndEnabled)
            _camCache = FindCameraActiva();

        if (_camCache == null) return;

        // Copiar la rotación de la cámara activa → viñeta siempre paralela a la pantalla
        transform.rotation = _camCache.transform.rotation;
    }

    private static Camera FindCameraActiva()
    {
        // Camera.allCameras solo incluye cámaras habilitadas
        Camera[] todas = Camera.allCameras;
        return todas.Length > 0 ? todas[0] : null;
    }
}