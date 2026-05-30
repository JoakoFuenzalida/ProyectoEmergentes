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

        // El texto debe apuntar HACIA la cámara, no en la misma dirección.
        // Copiar la rotación de la cámara hacía que se viera la cara trasera (espejado).
        Vector3 dirToCamera = _camCache.transform.position - transform.position;
        if (dirToCamera.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dirToCamera, Vector3.up);
    }

    private static Camera FindCameraActiva()
    {
        // Camera.allCameras solo incluye cámaras habilitadas
        Camera[] todas = Camera.allCameras;
        return todas.Length > 0 ? todas[0] : null;
    }
}