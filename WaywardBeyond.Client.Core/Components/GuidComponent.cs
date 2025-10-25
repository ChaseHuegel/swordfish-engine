using System;
using Swordfish.ECS;

namespace WaywardBeyond.Client.Core.Components;

internal struct GuidComponent(in Guid guid) : IDataComponent
{
    public readonly Guid Guid = guid;
}