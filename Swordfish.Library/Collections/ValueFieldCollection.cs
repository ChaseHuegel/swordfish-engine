using System.Collections.Generic;
using Swordfish.Library.Types;

namespace Swordfish.Library.Collections;

public class ValueFieldCollection : ValueFieldCollection<string>
{
}

public class ValueFieldCollection<TIdentifier>
{
    private readonly Dictionary<TIdentifier, ValueField<TIdentifier>> _items = new();

    public bool Contains(TIdentifier identifier)
    {
        return _items.ContainsKey(identifier);
    }

    public float ValueOf(TIdentifier identifier)
    {
        return _items.TryGetValue(identifier, out ValueField<TIdentifier> attribute) ? attribute.Value : 0f;
    }

    public float MaxValueOf(TIdentifier identifier)
    {
        return _items.TryGetValue(identifier, out ValueField<TIdentifier> attribute) ? attribute.MaxValue : 0f;
    }

    public float CalculatePercentOf(TIdentifier identifier)
    {
        return _items.TryGetValue(identifier, out ValueField<TIdentifier> attribute) ? attribute.CalculatePercent() : 0f;
    }

    public ValueField<TIdentifier> Get(TIdentifier identifier)
    {
        return _items.TryGetValue(identifier, out ValueField<TIdentifier> attribute) ? attribute : null;
    }

    public ValueField<TIdentifier> Add(TIdentifier identifier, float value, float max = 0f)
    {
        if (!_items.ContainsKey(identifier))
        {
            var attribute = new ValueField<TIdentifier>(identifier, value, max);
            _items.Add(identifier, attribute);
            return attribute;
        }

        return null;
    }

    public bool TryAdd(TIdentifier identifier, float value, float max = 0f)
    {
        if (_items.ContainsKey(identifier))
        {
            return false;
        }
        else
        {
            _items.Add(identifier, new ValueField<TIdentifier>(identifier, value, max));
            return true;
        }
    }

    public ValueField<TIdentifier> AddOrUpdate(TIdentifier identifier, float value, float max = 0f)
    {
        if (_items.TryGetValue(identifier, out ValueField<TIdentifier> field))
        {
            field.MaxValue = max;
            field.Value = value;
            return field;
        }
        else
        {
            field = new ValueField<TIdentifier>(identifier, value, max);
            _items.Add(identifier, field);
            return field;
        }
    }

    public bool Remove(TIdentifier name)
    {
        return _items.Remove(name);
    }

    public ValueField<TIdentifier> GetOrAdd(TIdentifier name, float value, float max = 0f)
    {
        return Get(name) ?? Add(name, value, max);
    }
}