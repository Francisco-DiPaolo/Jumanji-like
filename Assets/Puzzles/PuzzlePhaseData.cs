using System;

[Serializable]
public class SubControllerPhaseData
{
    public string correctButtonId;
    public string incorrectButtonId1;
    public string incorrectButtonId2;
}

[Serializable]
public class PuzzlePhaseData
{
    public SubControllerPhaseData sub1;
    public SubControllerPhaseData sub2;
    public SubControllerPhaseData sub3;
}
