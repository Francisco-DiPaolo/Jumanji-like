using Fusion;
using UnityEngine;
using UnityEngine.Events;

public class PlayerWheelStation : NetworkBasicInteraction
{
    [Header("Wheel Reference")]
    [SerializeField] private WheelRingController wheelController;

    [Header("Player Assignment")]
    [SerializeField] private int stationIndex;

    [Header("Input — which ring the current interaction targets")]
    private int _targetRingIndex;

    [Header("Events")]
    public UnityEvent OnPlayerArrived;
    public UnityEvent OnPlayerLeft;

    private bool _playerIsPresent;

    public int StationIndex => stationIndex;
    public WheelRingController Wheel => wheelController;

    public override void Select()
    {
        base.Select();
        if (!_playerIsPresent) return;
        wheelController?.RotateRing(_targetRingIndex, 1);
    }

    public void RotateLeft()
    {
        if (!_playerIsPresent) return;
        wheelController?.RotateRing(_targetRingIndex, -1);
    }

    public void RotateRight()
    {
        if (!_playerIsPresent) return;
        wheelController?.RotateRing(_targetRingIndex, 1);
    }

    public void CycleTargetRing()
    {
        _targetRingIndex = (_targetRingIndex + 1) % 2;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement>(out _))
        {
            _playerIsPresent = true;
            OnPlayerArrived?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement>(out _))
        {
            _playerIsPresent = false;
            _targetRingIndex = 0;
            OnPlayerLeft?.Invoke();
        }
    }
}
