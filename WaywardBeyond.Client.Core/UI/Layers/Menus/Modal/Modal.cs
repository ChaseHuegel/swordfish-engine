using System;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Modal;

internal readonly struct Modal(in string id) : IEquatable<Modal>
{
    public readonly string ID = id;
    
    public bool Equals(Modal other)
    {
        return ID == other.ID;
    }

    public override bool Equals(object? obj)
    {
        return obj is Modal modal && Equals(modal);
    }

    public override int GetHashCode()
    {
        return ID.GetHashCode();
    }
}
