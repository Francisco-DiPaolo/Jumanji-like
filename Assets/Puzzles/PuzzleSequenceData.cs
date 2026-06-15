using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPuzzleSequence", menuName = "Puzzles/Puzzle Sequence")]
public class PuzzleSequenceData : ScriptableObject
{
    [SerializeField] private List<PuzzlePhaseData> phases;

    public IReadOnlyList<PuzzlePhaseData> Phases => phases;
}
