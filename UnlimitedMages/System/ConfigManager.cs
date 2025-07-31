using BepInEx.Logging;
using UnityEngine;
using UnlimitedMages.Components;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.System;

public class ConfigManager : MonoBehaviour, IModComponent
{
    public static ConfigManager? Instance { get; private set; }

    public int TeamSize { get; private set; } = GameConstants.Game.MinimumTeamSize;

    public bool IsConfigReady { get; private set; }

    public void Initialize(ManualLogSource log)
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    public void FinalizeConfig(int teamSize)
    {
        if (IsConfigReady)
        {
            if (teamSize == TeamSize) return;
        }

        TeamSize = teamSize;
        IsConfigReady = true;
        UnlimitedMagesPlugin.Log?.LogInfo($"Config finalized. Team Size: {TeamSize}");

        EventBus.Publish(new ConfigReadyEvent(TeamSize));
    }

    public void Reset()
    {
        UnlimitedMagesPlugin.Log?.LogInfo("Resetting session configuration.");
        IsConfigReady = false;
        TeamSize = GameConstants.Game.MinimumTeamSize;
    }
}