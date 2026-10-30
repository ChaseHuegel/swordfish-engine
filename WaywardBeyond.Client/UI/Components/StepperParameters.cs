using System;

namespace WaywardBeyond.Client.UI.Components;

/// <summary>Parameters for the shared discrete stepper.</summary>
internal readonly record struct StepperParameters(
    string Value = "",
    bool Enabled = false,
    Action? OnDecrease = null,
    Action? OnIncrease = null
);
