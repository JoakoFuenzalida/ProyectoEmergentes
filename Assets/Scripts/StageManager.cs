using UnityEngine;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("Podios Centrales (Para el botonazo)")]
    public Transform podioEquipoA;
    public Transform podioEquipoB;

    [Header("Asientos (Mesas de los equipos)")]
    public Transform[] asientosEquipoA = new Transform[4];
    public Transform[] asientosEquipoB = new Transform[4];

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }
}