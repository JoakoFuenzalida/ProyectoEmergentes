using UnityEngine;
using Fusion;

public class PlayerController : NetworkBehaviour
{
    public Camera playerCamera;
    public float sensitivity = 2f;
    
    private float xRotation = 0f;
    private float yRotation = 0f;

    private Transform destinoActual = null; 

    public override void Spawned()
    {
        if (!Object.HasInputAuthority)
        {
            playerCamera.enabled = false;
            playerCamera.GetComponent<AudioListener>().enabled = false;
        }
        
        GameStateManager.OnStateChangedEvent += HandleStateChanged;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        GameStateManager.OnStateChangedEvent -= HandleStateChanged;
    }

    private void HandleStateChanged(GameStateManager.GameState newState)
    {
        if (StageManager.Instance == null) return;

        var myData = GetComponent<PlayerNetworkData>();
        if (myData == null || myData.TeamIndex == 0) return;

        // --- LA MAGIA DINÁMICA ---
        // Contamos cuántos jugadores hay en mi equipo para rotar los turnos correctamente
        int jugadoresEnMiEquipo = 0;
        foreach (var pRef in Runner.ActivePlayers)
        {
            var d = Runner.GetPlayerObject(pRef)?.GetComponent<PlayerNetworkData>();
            if (d != null && d.TeamIndex == myData.TeamIndex) jugadoresEnMiEquipo++;
        }
        if (jugadoresEnMiEquipo == 0) jugadoresEnMiEquipo = 1; // Seguridad

        // Ahora rotamos el turno según los jugadores que de verdad existen
        int turnoActual = (GameStateManager.Instance.CurrentRound - 1) % jugadoresEnMiEquipo;
        bool meTocaElPodio = (myData.SeatIndex == turnoActual);

        Transform nuevoDestino = null;

        if (newState == GameStateManager.GameState.Countdown || 
            newState == GameStateManager.GameState.WaitingForBuzzer || 
            newState == GameStateManager.GameState.TypingAnswer)
        {
            nuevoDestino = (myData.TeamIndex == 1) ? StageManager.Instance.podioEquipoA : StageManager.Instance.podioEquipoB;
            if (!meTocaElPodio)
            {
                nuevoDestino = (myData.TeamIndex == 1) ? StageManager.Instance.asientosEquipoA[myData.SeatIndex] : StageManager.Instance.asientosEquipoB[myData.SeatIndex];
            }
        }
        else
        {
            nuevoDestino = (myData.TeamIndex == 1) ? StageManager.Instance.asientosEquipoA[myData.SeatIndex] : StageManager.Instance.asientosEquipoB[myData.SeatIndex];
        }

        if (nuevoDestino == null) return;

        // 1. EL SERVIDOR MUEVE LOS CUERPOS FÍSICOS Y LOS ROTA CORRECTAMENTE
        if (Object.HasStateAuthority)
        {
            var charController = GetComponent<CharacterController>();
            var networkTransform = GetComponent<NetworkTransform>();

            if (charController != null) charController.enabled = false; 

            if (networkTransform != null) 
            {
                networkTransform.Teleport(nuevoDestino.position, nuevoDestino.rotation);
            }
            else 
            {
                transform.position = nuevoDestino.position;
                transform.rotation = nuevoDestino.rotation;
            }

            if (charController != null) charController.enabled = true; 
        }

        // 2. EL DUEÑO DE LA CÁMARA LA ACOMODA (Solo si se movió de lugar)
        if (Object.HasInputAuthority)
        {
            if (destinoActual != nuevoDestino)
            {
                destinoActual = nuevoDestino; 
                
                xRotation = 0f;
                yRotation = 0f; 
                playerCamera.transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
            }
        }
    }

    void Update()
    {
        if (Object.HasInputAuthority)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float mouseX = Input.GetAxis("Mouse X") * sensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

                xRotation -= mouseY;
                xRotation = Mathf.Clamp(xRotation, -80f, 80f); 

                yRotation += mouseX;
                playerCamera.transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameStateManager.GameState.WaitingForBuzzer)
                {
                    var myData = GetComponent<PlayerNetworkData>();
                    if (myData != null)
                    {
                        // Repetimos la lógica del conteo para el botón
                        int jugadoresEnMiEquipo = 0;
                        foreach (var pRef in Runner.ActivePlayers)
                        {
                            var d = Runner.GetPlayerObject(pRef)?.GetComponent<PlayerNetworkData>();
                            if (d != null && d.TeamIndex == myData.TeamIndex) jugadoresEnMiEquipo++;
                        }
                        if (jugadoresEnMiEquipo == 0) jugadoresEnMiEquipo = 1;

                        int turnoActual = (GameStateManager.Instance.CurrentRound - 1) % jugadoresEnMiEquipo;
                        
                        if (myData.SeatIndex == turnoActual)
                        {
                            GameStateManager.Instance.RPC_PressBuzzer(Runner.LocalPlayer.PlayerId);
                        }
                    }
                }
            }
        }
    }
}