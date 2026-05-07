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

        Transform nuevoDestino = null;

        if (newState == GameStateManager.GameState.Countdown ||
            newState == GameStateManager.GameState.WaitingForBuzzer ||
            newState == GameStateManager.GameState.TypingAnswer)
        {
            // FIX: Calcular turno dentro del equipo PROPIO independientemente
            // Antes había un solo turno global que solo mandaba UN jugador al podio en total
            // Ahora cada equipo calcula su propio jugador de turno por separado
            // → resultado: un jugador de cada equipo sube a su podio simultáneamente
            int jugadoresEnMiEquipo = ContarJugadoresEnEquipo(myData.TeamIndex);
            int turnoEnMiEquipo = (GameStateManager.Instance.CurrentRound - 1) % jugadoresEnMiEquipo;
            bool meTocoElPodio = (myData.SeatIndex == turnoEnMiEquipo);

            if (meTocoElPodio)
            {
                // Subir al podio de MI equipo
                nuevoDestino = (myData.TeamIndex == 1)
                    ? StageManager.Instance.podioEquipoA
                    : StageManager.Instance.podioEquipoB;
            }
            else
            {
                // Ir al asiento correspondiente
                nuevoDestino = ObtenerAsiento(myData.TeamIndex, myData.SeatIndex);
            }
        }
        else
        {
            // En cualquier otro estado todos van a sus asientos
            nuevoDestino = ObtenerAsiento(myData.TeamIndex, myData.SeatIndex);
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

        // Buzzer
        if (Input.GetKeyDown(KeyCode.Space))
            TryPressBuzzer();
    }

    private void TryPressBuzzer()
    {
        if (GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameStateManager.GameState.WaitingForBuzzer) return;

        var myData = GetComponent<PlayerNetworkData>();
        if (myData == null) return;

        int jugadoresEnMiEquipo = ContarJugadoresEnEquipo(myData.TeamIndex);
        int turnoEnMiEquipo = (GameStateManager.Instance.CurrentRound - 1) % jugadoresEnMiEquipo;

        if (myData.SeatIndex == turnoEnMiEquipo)
            GameStateManager.Instance.RPC_PressBuzzer(Runner.LocalPlayer.PlayerId);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private int ContarJugadoresEnEquipo(int teamIndex)
    {
        int count = 0;
        foreach (var pRef in Runner.ActivePlayers)
        {
            var d = Runner.GetPlayerObject(pRef)?.GetComponent<PlayerNetworkData>();
            if (d != null && d.TeamIndex == teamIndex) count++;
        }
        return Mathf.Max(count, 1);
    }

    private Transform ObtenerAsiento(int teamIndex, int seatIndex)
    {
        var asientos = (teamIndex == 1)
            ? StageManager.Instance.asientosEquipoA
            : StageManager.Instance.asientosEquipoB;

        if (asientos == null || asientos.Length == 0) return null;
        return asientos[Mathf.Clamp(seatIndex, 0, asientos.Length - 1)];
    }
}