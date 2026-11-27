using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Networking;

/// <summary>An input sample paired with the local sim tick at which it was staged.</summary>
public readonly struct StagedInput(in InputComponent input, uint stagedAtSimTick)
{
    /// <summary>The sampled input command.</summary>
    public readonly InputComponent Input = input;

    /// <summary>The local sim tick when this command was staged, used to bound command age.</summary>
    public readonly uint StagedAtSimTick = stagedAtSimTick;
}
