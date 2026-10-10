using System;

namespace WaywardBeyond.Data.Migrations;

/// <summary>Strongly-typed base for an <see cref="IMigration"/> over a specific record type <typeparamref name="T"/>. </summary>
public abstract class Migration<T> : IMigration
{
    public abstract uint FromVersion { get; }
    public abstract uint ToVersion { get; }
    public Type TargetType => typeof(T);
    public object Apply(object value) => ApplyValue((T)value)!;
    
    protected abstract T ApplyValue(T value);
}