using System;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Parameters for the shared discrete stepper.</summary>
internal readonly record struct StepperParameters(
    string Value = "",
    bool Enabled = false,
    bool CanDecrease = true,
    bool CanIncrease = true,
    string Id = "",
    Action<string, int>? OnStep = null
);
