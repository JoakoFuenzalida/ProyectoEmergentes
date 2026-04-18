using UnityEngine;
using Fusion;

public class PlayerController : NetworkBehaviour
{
    public Camera playerCamera;
    public float sensitivity = 2f;
    
    private float xRotation = 0f;
    private float yRotation = 0f;

    public override void Spawned()
    {
        // Si NO soy yo, apago la cámara del otro
        if (!Object.HasInputAuthority)
        {
            playerCamera.enabled = false;
            playerCamera.GetComponent<AudioListener>().enabled = false;
        }
    }

    void Update()
    {
        if (Object.HasInputAuthority)
        {
            // 1. --- CONTROL DE CÁMARA ---
            // Solo movemos la cabeza si el mouse está bloqueado
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float mouseX = Input.GetAxis("Mouse X") * sensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

                xRotation -= mouseY;
                xRotation = Mathf.Clamp(xRotation, -80f, 80f); 

                yRotation += mouseX;
                playerCamera.transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
            }

            // 2. --- EL BOTONAZO (BARRA ESPACIADORA) ---
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.WaitingForBuzzer)
                {
                    Debug.Log("¡PRESIONÉ ESPACIO!");
                    
                    // Solo mandamos nuestro número de jugador (PlayerId)
                    int miID = Runner.LocalPlayer.PlayerId;
                    GameStateManager.Instance.RPC_PressBuzzer(miID);
                }
            }
        }
    }
}