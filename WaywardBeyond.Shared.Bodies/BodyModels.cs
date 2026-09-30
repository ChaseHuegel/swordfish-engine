using System.Collections.Generic;

namespace WaywardBeyond.Shared.Bodies;

/// <summary>
/// A collection of body model definitions parsed from a single <c>bodies/*.toml</c> resource.
/// </summary>
public struct BodyModels()
{
    public List<BodyModel> Bodies;
}