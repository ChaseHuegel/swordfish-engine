namespace Swordfish.ECS;

public delegate void ForEach<T1>(int entity, ref T1 component1) where T1 : struct, IDataComponent;
public delegate void ForEach<T1, T2>(int entity, ref T1 component1, ref T2 component2) where T1 : struct, IDataComponent where T2 : struct, IDataComponent;