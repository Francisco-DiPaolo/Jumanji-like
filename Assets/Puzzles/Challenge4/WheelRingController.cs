using Fusion;
using UnityEngine;
using UnityEngine.Events;

public class WheelRingController : NetworkBehaviour
{
    [Header("Ring GameObjects (2 concentric rings per wheel)")]
    [SerializeField] private Transform ring0;
    [SerializeField] private Transform ring1;

    [Header("Ring Step Settings")]
    [SerializeField] private float stepAngle = 30f;
    [SerializeField] private float rotationDuration = 0.25f;
    [SerializeField] private LeanTweenType rotationEase = LeanTweenType.easeOutBack;

    [Header("Symbol Renderers")]
    [SerializeField] private Renderer[] ring0SymbolRenderers;
    [SerializeField] private Renderer[] ring1SymbolRenderers;

    [Header("Materials")]
    [SerializeField] private Material symbolDefaultMaterial;
    [SerializeField] private Material symbolSelectedMaterial;

    [Header("Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rotateClip;
    [SerializeField] private AudioClip errorClip;
    [SerializeField] private float shakeStrength = 0.08f;
    [SerializeField] private float shakeDuration = 0.35f;

    [Header("Events")]
    public UnityEvent OnSelectionChanged;

    [Networked] private int Ring0Steps { get; set; }
    [Networked] private int Ring1Steps { get; set; }

    private ChangeDetector _changeDetector;
    private bool _isAnimating;
    private string _previousCombo0;
    private string _previousCombo1;
    private bool _playerIsPresent;

    private string[] _ring0Ids;
    private string[] _ring1Ids;

    private int SelectedPosition0 => ComputePosition(Ring0Steps, ring0SymbolRenderers);
    private int SelectedPosition1 => ComputePosition(Ring1Steps, ring1SymbolRenderers);

    public string SelectedSymbolId0 => MapPositionToId(SelectedPosition0, _ring0Ids);
    public string SelectedSymbolId1 => MapPositionToId(SelectedPosition1, _ring1Ids);

    public bool PlayerIsPresent => _playerIsPresent;

    private void Awake()
    {
        _ring0Ids = CacheIds(ring0SymbolRenderers);
        _ring1Ids = CacheIds(ring1SymbolRenderers);
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
    }

    public override void Render()
    {
        bool anyChange = false;
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            anyChange = true;
            if (change == nameof(Ring0Steps)) ApplyRingRotation(ring0, Ring0Steps);
            else if (change == nameof(Ring1Steps)) ApplyRingRotation(ring1, Ring1Steps);
        }

        if (!anyChange) return;

        RefreshSelectedMaterials();

        string combo0 = SelectedSymbolId0;
        string combo1 = SelectedSymbolId1;

        if (combo0 != _previousCombo0 || combo1 != _previousCombo1)
        {
            _previousCombo0 = combo0;
            _previousCombo1 = combo1;
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
        }
        Rpc_PlayRotateSound();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayRotateSound()
    {
        if (audioSource != null && rotateClip != null)
            audioSource.PlayOneShot(rotateClip);
    }

    /// <summary>
    /// Genera todas las combinaciones posibles de ids entre los dos anillos.
    /// Con 2 anillos hay 2 combinaciones: id0+id1 y id1+id0.
    /// </summary>
    public string[] GetPossibleCombinations()
    {
        string a = SelectedSymbolId0 ?? "";
        string b = SelectedSymbolId1 ?? "";

        if (a == b) return new[] { a + b };

        return new[] { a + b, b + a };
    }

    /// <summary>
    /// Prueba todas las combinaciones posibles contra el reloj.
    /// Si alguna coincide con un símbolo activo, la devuelve en matchedId.
    /// </summary>
    public bool TryGetMatchingSymbolId(CentralClockManager clock, out string matchedId)
    {
        matchedId = null;
        if (clock == null) return false;

        foreach (var combo in GetPossibleCombinations())
        {
            if (clock.IsSymbolActive(combo))
            {
                matchedId = combo;
                return true;
            }
        }

        return false;
    }

    private void ApplyRingRotation(Transform ring, int steps)
    {
        if (ring == null) return;

        float targetAngle = steps * stepAngle;
        float startAngle = ring.localEulerAngles.z;
        if (startAngle > 180f) startAngle -= 360f;

        _isAnimating = true;
        LeanTween.cancel(ring.gameObject);

        LeanTween.value(ring.gameObject, startAngle, targetAngle, rotationDuration)
            .setEase(rotationEase)
            .setOnUpdate((float angle) => ring.localRotation = Quaternion.Euler(0f, 0f, angle))
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

    private void RefreshSelectedMaterials()
    {
        ApplySelectionToRenderers(ring0SymbolRenderers, SelectedPosition0);
        ApplySelectionToRenderers(ring1SymbolRenderers, SelectedPosition1);
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