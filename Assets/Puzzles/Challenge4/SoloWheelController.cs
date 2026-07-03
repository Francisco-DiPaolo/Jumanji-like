using Fusion;
using UnityEngine;
using UnityEngine.Events;

public class SoloWheelController : NetworkBehaviour
{
    [Header("Rings (3 concentric rings)")]
    [SerializeField] private Transform ring0;
    [SerializeField] private Transform ring1;
    [SerializeField] private Transform ring2;

    [Header("Ring Step Settings")]
    [SerializeField] private float stepAngle = 30f;
    [SerializeField] private float rotationDuration = 0.25f;
    [SerializeField] private LeanTweenType rotationEase = LeanTweenType.easeOutBack;

    [Header("Symbol Renderers")]
    [SerializeField] private Renderer[] ring0SymbolRenderers;
    [SerializeField] private Renderer[] ring1SymbolRenderers;
    [SerializeField] private Renderer[] ring2SymbolRenderers;

    [Header("Materials")]
    [SerializeField] private Material symbolDefaultMaterial;
    [SerializeField] private Material symbolSelectedMaterial;

    [Header("Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rotateClip;
    [SerializeField] private AudioClip errorClip;
    [SerializeField] private float shakeStrength = 0.08f;
    [SerializeField] private float shakeDuration = 0.35f;

    [Tooltip("Objeto opcional que se apagará al completarse el puzzle")]
    public GameObject objectToDisableOnResolve;

    [Header("Events")]
    public UnityEvent OnSelectionChanged;
    public UnityEvent OnResolved;

    [Networked] private int Ring0Steps { get; set; }
    [Networked] private int Ring1Steps { get; set; }
    [Networked] private int Ring2Steps { get; set; }
    [Networked] public NetworkBool IsResolved { get; private set; }

    private ChangeDetector _changeDetector;
    private bool _isAnimating;
    private string _previousSelected0;
    private string _previousSelected1;
    private string _previousSelected2;
    private bool _playerIsPresent;

    private string[] _ring0Ids;
    private string[] _ring1Ids;
    private string[] _ring2Ids;

    private int SelectedPosition0 => ComputePosition(Ring0Steps, ring0SymbolRenderers);
    private int SelectedPosition1 => ComputePosition(Ring1Steps, ring1SymbolRenderers);
    private int SelectedPosition2 => ComputePosition(Ring2Steps, ring2SymbolRenderers);

    public string SelectedSymbolId0 => MapPositionToId(SelectedPosition0, _ring0Ids);
    public string SelectedSymbolId1 => MapPositionToId(SelectedPosition1, _ring1Ids);
    public string SelectedSymbolId2 => MapPositionToId(SelectedPosition2, _ring2Ids);

    public bool PlayerIsPresent => _playerIsPresent;

    private void Awake()
    {
        _ring0Ids = CacheIds(ring0SymbolRenderers);
        _ring1Ids = CacheIds(ring1SymbolRenderers);
        _ring2Ids = CacheIds(ring2SymbolRenderers);
    }

    private string[] CacheIds(Renderer[] renderers)
    {
        if (renderers == null) return new string[0];
        var ids = new string[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var identity = renderers[i] != null ? renderers[i].GetComponent<SymbolIdentity>() : null;
            if (identity == null)
            {
                Debug.LogError($"Falta SymbolIdentity en {renderers[i]?.name}", this);
                ids[i] = null;
                continue;
            }
            ids[i] = identity.SymbolId;
        }
        return ids;
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        if (IsResolved)
        {
            if (objectToDisableOnResolve != null) objectToDisableOnResolve.SetActive(false);
        }
    }

    public override void Render()
    {
        bool anyChange = false;
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            anyChange = true;
            if (change == nameof(Ring0Steps)) ApplyRotation(ring0, Ring0Steps);
            else if (change == nameof(Ring1Steps)) ApplyRotation(ring1, Ring1Steps);
            else if (change == nameof(Ring2Steps)) ApplyRotation(ring2, Ring2Steps);

            if (change == nameof(IsResolved) && IsResolved) 
            {
                TriggerSuccessFeedback();
                OnResolved?.Invoke();
                if (objectToDisableOnResolve != null) objectToDisableOnResolve.SetActive(false);
            }
        }

        if (!anyChange) return;

        RefreshMaterials();

        string id0 = SelectedSymbolId0;
        string id1 = SelectedSymbolId1;
        string id2 = SelectedSymbolId2;

        if (id0 != _previousSelected0 || id1 != _previousSelected1 || id2 != _previousSelected2)
        {
            _previousSelected0 = id0;
            _previousSelected1 = id1;
            _previousSelected2 = id2;
            OnSelectionChanged?.Invoke();
        }
    }

    public void RotateRing(int ringIndex, int direction)
    {
        if (_isAnimating) return;
        Rpc_RequestRotate(ringIndex, direction);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void Rpc_RequestRotate(int ringIndex, int direction)
    {
        switch (ringIndex)
        {
            case 0: Ring0Steps += direction; break;
            case 1: Ring1Steps += direction; break;
            case 2: Ring2Steps += direction; break;
        }
        Rpc_PlayRotateSound();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayRotateSound()
    {
        if (audioSource != null && rotateClip != null)
            audioSource.PlayOneShot(rotateClip);
    }

    public void MarkResolved()
    {
        if (!HasStateAuthority) return;
        IsResolved = true;
    }

    public bool MatchesClock(CentralClockManager clock)
    {
        if (clock == null) return false;

        var selected = new System.Collections.Generic.HashSet<string>
        {
            SelectedSymbolId0,
            SelectedSymbolId1,
            SelectedSymbolId2
        };

        if (selected.Count != 3) return false;

        return clock.IsSymbolActive(SelectedSymbolId0)
            && clock.IsSymbolActive(SelectedSymbolId1)
            && clock.IsSymbolActive(SelectedSymbolId2);
    }

    public void TryResolve(CentralClockManager clock)
    {
        if (!HasStateAuthority) return;

        if (MatchesClock(clock))
        {
            MarkResolved();
        }
        else
        {
            Rpc_TriggerErrorFeedback();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_TriggerErrorFeedback()
    {
        TriggerErrorFeedback();
    }

    private void ApplyRotation(Transform ringTransform, int steps)
    {
        if (ringTransform == null) return;

        float targetAngle = steps * stepAngle;
        float startAngle = ringTransform.localEulerAngles.z;
        if (startAngle > 180f) startAngle -= 360f;

        _isAnimating = true;
        LeanTween.cancel(ringTransform.gameObject);

        LeanTween.value(ringTransform.gameObject, startAngle, targetAngle, rotationDuration)
            .setEase(rotationEase)
            .setOnUpdate((float angle) => ringTransform.localRotation = Quaternion.Euler(0f,-90, angle))
            .setOnComplete(() => _isAnimating = false);
    }

    private int ComputePosition(int steps, Renderer[] renderers)
    {
        int count = renderers != null ? renderers.Length : 1;
        return ((steps % count) + count) % count;
    }

    private string MapPositionToId(int position, string[] ids)
    {
        if (ids == null || position < 0 || position >= ids.Length) return null;
        return ids[position];
    }

    private void RefreshMaterials()
    {
        ApplySelectionToRenderers(ring0SymbolRenderers, SelectedPosition0);
        ApplySelectionToRenderers(ring1SymbolRenderers, SelectedPosition1);
        ApplySelectionToRenderers(ring2SymbolRenderers, SelectedPosition2);
    }

    private void ApplySelectionToRenderers(Renderer[] renderers, int selectedPosition)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].material = i == selectedPosition ? symbolSelectedMaterial : symbolDefaultMaterial;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement>(out _))
            _playerIsPresent = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerMovement>(out _))
            _playerIsPresent = false;
    }

    public void TriggerErrorFeedback()
    {
        if (audioSource != null && errorClip != null)
            audioSource.PlayOneShot(errorClip);

        Vector3 originalPos = transform.localPosition;
        LeanTween.cancel(gameObject);
        LeanTween.value(gameObject, 0f, 1f, shakeDuration)
            .setOnUpdate(t =>
            {
                float offset = Mathf.Sin(t * Mathf.PI * 8f) * shakeStrength * (1f - t);
                transform.localPosition = originalPos + new Vector3(offset, 0f, 0f);
            })
            .setOnComplete(() => transform.localPosition = originalPos);
    }

    public void TriggerSuccessFeedback()
    {
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, Vector3.one * 1.05f, 0.1f)
            .setEase(LeanTweenType.easeOutBack)
            .setLoopPingPong(1);
    }
}