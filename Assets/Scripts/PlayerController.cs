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
            // BUG FIX: verificar que el AudioListener existe antes de acceder
            var audioListener = playerCamera.GetComponent<AudioListener>();
            if (audioListener != null) audioListener.enabled = false;
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

        // Contar jugadores en mi equipo para rotar turnos
        int jugadoresEnMiEquipo = 0;
        foreach (var pRef in Runner.ActivePlayers)
        {
            var d = Runner.GetPlayerObject(pRef)?.GetComponent<PlayerNetworkData>();
            if (d != null && d.TeamIndex == myData.TeamIndex) jugadoresEnMiEquipo++;
        }
        if (jugadoresEnMiEquipo == 0) jugadoresEnMiEquipo = 1;

        int turnoActual = (GameStateManager.Instance.CurrentRound - 1) % jugadoresEnMiEquipo;
        bool meTocaElPodio = (myData.SeatIndex == turnoActual);

        Transform nuevoDestino = null;

        if (newState == GameStateManager.GameState.Countdown ||
            newState == GameStateManager.GameState.WaitingForBuzzer ||
            newState == GameStateManager.GameState.TypingAnswer)
        {
            nuevoDestino = (myData.TeamIndex == 1)
                ? StageManager.Instance.podioEquipoA
                : StageManager.Instance.podioEquipoB;

            if (!meTocaElPodio)
            {
                // BUG FIX: verificar que el SeatIndex no se salga del array
                var asientos = (myData.TeamIndex == 1)
                    ? StageManager.Instance.asientosEquipoA
                    : StageManager.Instance.asientosEquipoB;

                if (asientos != null && myData.SeatIndex < asientos.Length)
                    nuevoDestino = asientos[myData.SeatIndex];
                else if (asientos != null && asientos.Length > 0)
                    nuevoDestino = asientos[0]; // Fallback al primer asiento
            }
        }
        else
        {
            // BUG FIX: misma protección de índice para el estado Playing/Stealing
            var asientos = (myData.TeamIndex == 1)
                ? StageManager.Instance.asientosEquipoA
                : StageManager.Instance.asientosEquipoB;

            if (asientos != null && myData.SeatIndex < asientos.Length)
                nuevoDestino = asientos[myData.SeatIndex];
            else if (asientos != null && asientos.Length > 0)
                nuevoDestino = asientos[0];
        }

        if (nuevoDestino == null) return;

        // El servidor mueve los cuerpos físicos
        if (Object.HasStateAuthority)
        {
            var charController = GetComponent<CharacterController>();
            var networkTransform = GetComponent<NetworkTransform>();

            if (charController != null) charController.enabled = false;

            if (networkTransform != null)
                networkTransform.Teleport(nuevoDestino.position, nuevoDestino.rotation);
            else
            {
                transform.position = nuevoDestino.position;
                transform.rotation = nuevoDestino.rotation;
            }

            if (charController != null) charController.enabled = true;
        }

        // El dueño de la cámara la acomoda
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
        if (!Object.HasInputAuthority) return;

        // Movimiento de cámara
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseX = Input.GetAxis("Mouse X") * sensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -80f, 80f);
            yRotation += mouseX;
            playerCamera.transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
        }

        // BUG FIX: Buzzer — eliminar lógica duplicada y centralizar en un método
        if (Input.GetKeyDown(KeyCode.Space))
            TryPressBuzzer();
    }

    // BUG FIX: Lógica del buzzer en un solo lugar (antes estaba duplicada en Update y HandleStateChanged)
    private void TryPressBuzzer()
    {
        if (GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameStateManager.GameState.WaitingForBuzzer) return;

        var myData = GetComponent<PlayerNetworkData>();
        if (myData == null) return;

        // Contar jugadores en mi equipo
        int jugadoresEnMiEquipo = 0;
        foreach (var pRef in Runner.ActivePlayers)
        {
            var d = Runner.GetPlayerObject(pRef)?.GetComponent<PlayerNetworkData>();
            if (d != null && d.TeamIndex == myData.TeamIndex) jugadoresEnMiEquipo++;
        }
        if (jugadoresEnMiEquipo == 0) jugadoresEnMiEquipo = 1;

        int turnoActual = (GameStateManager.Instance.CurrentRound - 1) % jugadoresEnMiEquipo;

        // Solo el jugador cuyo turno es puede tocar el buzzer
        if (myData.SeatIndex == turnoActual)
            GameStateManager.Instance.RPC_PressBuzzer(Runner.LocalPlayer.PlayerId);
    }
}