﻿using UnityEngine;

/// <summary>
/// Class which contains all of the "tweakables" for the game
/// </summary>
[System.Serializable]
public class GameSettings
{
    public bool PushAwayOtherPlayers = true;
    public float BlowbackRadius = 9f;
    public float BlowbackSpeed = 30f;
    public float BlowbackStunTime = 0.1f;
    public float SlowMoFactor = 0.4f;
    public float PitchShiftTime = 0.3f;
    public float SlowedPitch = 0.5f;
    public float GoalShakeAmount = 1.5f;
    public float GoalShakeDuration = .4f;
    public bool RespectSoundEffectSlowMo = true;
    public int WinningScore = 4;
    public int RequiredWinMargin = 1;
}
