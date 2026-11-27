using System.Collections.Concurrent;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using Swordfish.Settings;

namespace Swordfish.Graphics.Particles;

/// <summary>Simulates pooled world-space particles from emitters and bursts and publishes snapshots for rendering.</summary>
public sealed class ParticleSystem : IEntitySystem, IParticleService
{
    private const float MAX_DELTA_SECONDS = 0.1f;
    private const float HALF_DIAGONAL_FACTOR = 0.71f;
    private const int MAX_MISSING_EFFECT_CACHE = 256;
    internal const int MAX_PENDING_BURSTS = 256;
    private const float MIN_BATCH_CELL_SIZE = 1f;

    private readonly IAssetDatabase<ParticleEffect> _effectDatabase;
    private readonly RenderSettings _renderSettings;
    private readonly Random _random;
    private readonly ILogger<ParticleSystem>? _logger;
    private readonly SnapshotGate _snapshotLock = new();
    private readonly ConcurrentQueue<QueuedBurst> _pendingBursts = new();

    private readonly Dictionary<Uuid, EmitterRuntimeState> _emitterStates = [];
    private readonly Stack<EmitterRuntimeState> _emitterStatePool = new();
    private readonly HashSet<Uuid> _seenEmitters = [];
    private readonly List<Uuid> _staleEmitters = [];
    private readonly Dictionary<ParticleEffect, int> _burstActiveCounts = [];
    private readonly Dictionary<string, ParticleEffect> _resolvedEffectCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missingEffectIds = new(StringComparer.Ordinal);

    private readonly Dictionary<BatchKey, List<int>> _batchBuckets = [];
    private readonly Stack<List<int>> _bucketPool = new();
    private readonly List<BatchKey> _batchOrder = [];

    private LiveParticle[] _particles = [];
    private int _activeCount;
    private volatile int _activeParticleCount;
    private long _clearGeneration;
    private long _lastProcessedClearGeneration;

    private ParticleRenderSnapshot _writeSnapshot = new();
    private ParticleRenderSnapshot _readySnapshot = new();
    private ParticleRenderSnapshot _readSnapshot = new();
    private bool _hasReadySnapshot;

    /// <summary>Constructs the particle simulation system.</summary>
    public ParticleSystem(
        in IAssetDatabase<ParticleEffect> effectDatabase,
        in RenderSettings renderSettings,
        Random? random = null,
        ILogger<ParticleSystem>? logger = null
    ) {
        _effectDatabase = effectDatabase;
        _renderSettings = renderSettings;
        _random = random ?? Random.Shared;
        _logger = logger;
    }

    /// <inheritdoc/>
    public int ActiveParticleCount => _activeParticleCount;

    /// <inheritdoc/>
    public void EmitBurst(in ParticleBurstRequest request)
    {
        if (!_renderSettings.Particles.Enabled.Get())
        {
            return;
        }

        Vector3 position = request.Position;
        Vector3 direction = request.Direction;
        if (request.Effect == null || !IsFinite(in position) || !IsFinite(in direction))
        {
            return;
        }

        if (_pendingBursts.Count >= MAX_PENDING_BURSTS)
        {
            return;
        }

        long generation = Volatile.Read(ref _clearGeneration);
        _pendingBursts.Enqueue(new QueuedBurst(request, generation));
    }

    /// <inheritdoc/>
    public Result EmitBurst(string effectId, Vector3 position, Vector3 direction, int? count = null)
    {
        return EmitBurst(effectId, position, direction, count, speedScale: 1f, sizeScale: 1f, tintRgba: 0u);
    }

    /// <inheritdoc/>
    public Result EmitBurst(
        string effectId,
        Vector3 position,
        Vector3 direction,
        int? count,
        float speedScale,
        float sizeScale,
        uint tintRgba = 0u
    ) {
        if (string.IsNullOrWhiteSpace(effectId))
        {
            return Result.FromFailure("Particle effect id cannot be empty.");
        }

        if (!IsFinite(in position) || !IsFinite(in direction))
        {
            return Result.FromFailure("Particle burst position and direction must be finite.");
        }

        if (!float.IsFinite(speedScale) || speedScale < 0f)
        {
            return Result.FromFailure("Particle burst speedScale must be finite and non-negative.");
        }

        if (!float.IsFinite(sizeScale) || sizeScale <= 0f)
        {
            return Result.FromFailure("Particle burst sizeScale must be finite and positive.");
        }

        if (count.HasValue && count.Value <= 0)
        {
            return Result.FromFailure("Particle burst count must be positive when specified.");
        }

        Result<ParticleEffect> effectResult = _effectDatabase.Get(effectId);
        if (!effectResult.Success)
        {
            return Result.FromFailure(effectResult.Message);
        }

        EmitBurst(new ParticleBurstRequest(effectResult.Value, position, direction, count, speedScale, sizeScale, tintRgba));
        return Result.FromSuccess();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        Interlocked.Increment(ref _clearGeneration);
        while (_pendingBursts.TryDequeue(out _))
        {
        }
    }

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        long clearGeneration = Volatile.Read(ref _clearGeneration);
        if (clearGeneration != _lastProcessedClearGeneration)
        {
            ResetSimulationState();
            _lastProcessedClearGeneration = clearGeneration;
        }

        if (!_renderSettings.Particles.Enabled.Get())
        {
            ResetSimulationState();
            while (_pendingBursts.TryDequeue(out _))
            {
            }

            PublishSnapshot();
            return;
        }

        float dt = float.IsFinite(delta) && delta > 0f ? MathF.Min(delta, MAX_DELTA_SECONDS) : 0f;
        int maxGlobalParticles = Math.Max(0, _renderSettings.Particles.MaxParticles.Get());
        float density = ClampDensity(_renderSettings.Particles.Density.Get());

        AdvanceLiveParticles(dt);
        ProcessPendingBursts(maxGlobalParticles, density);
        ProcessEmitters(dt, store, maxGlobalParticles, density);
        PublishSnapshot();
    }

    /// <summary>Acquires the newest published render snapshot on the render thread.</summary>
    internal ParticleRenderSnapshot AcquireRenderSnapshot()
    {
        lock (_snapshotLock)
        {
            if (_hasReadySnapshot)
            {
                (_readSnapshot, _readySnapshot) = (_readySnapshot, _readSnapshot);
                _hasReadySnapshot = false;
            }

            return _readSnapshot;
        }
    }

    private void ResetSimulationState()
    {
        if (_activeCount > 0)
        {
            Array.Clear(_particles, 0, _activeCount);
            _activeCount = 0;
        }

        _activeParticleCount = 0;
        _burstActiveCounts.Clear();

        foreach (EmitterRuntimeState state in _emitterStates.Values)
        {
            state.Reset();
            _emitterStatePool.Push(state);
        }

        _emitterStates.Clear();
        _resolvedEffectCache.Clear();
        _missingEffectIds.Clear();
    }

    private void AdvanceLiveParticles(float dt)
    {
        var i = 0;
        while (i < _activeCount)
        {
            ref LiveParticle particle = ref _particles[i];
            particle.Age += dt;

            if (particle.Age >= particle.Lifetime)
            {
                DecrementActiveCount(in particle);
                int lastIndex = --_activeCount;
                if (i < lastIndex)
                {
                    _particles[i] = _particles[lastIndex];
                }

                _particles[lastIndex] = default;
                continue;
            }

            if (dt > 0f)
            {
                particle.Velocity += particle.Gravity * dt;
                if (particle.Drag > 0f)
                {
                    particle.Velocity *= MathF.Exp(-particle.Drag * dt);
                }

                particle.Position += particle.Velocity * dt;
                particle.Rotation += particle.AngularVelocity * dt;
            }

            i++;
        }
    }

    private void DecrementActiveCount(in LiveParticle particle)
    {
        if (particle.EmitterOwner != Uuid.Null)
        {
            if (_emitterStates.TryGetValue(particle.EmitterOwner, out EmitterRuntimeState? state) && state.ActiveCount > 0)
            {
                state.ActiveCount--;
            }

            return;
        }

        if (_burstActiveCounts.TryGetValue(particle.Effect, out int count))
        {
            if (count <= 1)
            {
                _burstActiveCounts.Remove(particle.Effect);
            }
            else
            {
                _burstActiveCounts[particle.Effect] = count - 1;
            }
        }
    }

    private void ProcessPendingBursts(int maxGlobalParticles, float density)
    {
        while (_pendingBursts.TryDequeue(out QueuedBurst queued))
        {
            if (queued.Generation > _lastProcessedClearGeneration)
            {
                ResetSimulationState();
                _lastProcessedClearGeneration = Volatile.Read(ref _clearGeneration);
            }

            if (queued.Generation < _lastProcessedClearGeneration)
            {
                continue;
            }

            ParticleBurstRequest request = queued.Request;
            ParticleEffect effect = request.Effect;
            int requestedCount = request.Count ?? effect.BurstCount;
            if (requestedCount <= 0 || density <= 0f || maxGlobalParticles <= _activeCount)
            {
                continue;
            }

            float speedScale = float.IsFinite(request.SpeedScale) && request.SpeedScale >= 0f ? request.SpeedScale : 1f;
            float sizeScale = float.IsFinite(request.SizeScale) && request.SizeScale > 0f ? request.SizeScale : 1f;

            _burstActiveCounts.TryGetValue(effect, out int currentEffectActive);
            int availableEffect = Math.Max(0, effect.MaxParticles - currentEffectActive);
            int availableGlobal = Math.Max(0, maxGlobalParticles - _activeCount);
            int maxAvailable = Math.Min(availableEffect, availableGlobal);
            int count = ScaleBurstCount(requestedCount, density, maxAvailable);

            Vector3 position = request.Position;
            Vector3 direction = request.Direction;
            for (var i = 0; i < count; i++)
            {
                SpawnParticle(
                    effect,
                    Uuid.Null,
                    null,
                    in position,
                    in direction,
                    speedScale,
                    sizeScale,
                    request.TintRgba,
                    maxGlobalParticles
                );
            }
        }
    }

    private void ProcessEmitters(float dt, DataStore store, int maxGlobalParticles, float density)
    {
        _seenEmitters.Clear();
        EmitAction action = new()
        {
            Owner = this,
            Delta = dt,
            MaxGlobalParticles = maxGlobalParticles,
            Density = density,
        };
        store.Query<TransformComponent, ParticleEmitterComponent, EmitAction>(dt, ref action);

        if (_emitterStates.Count == 0)
        {
            return;
        }

        _staleEmitters.Clear();
        foreach (KeyValuePair<Uuid, EmitterRuntimeState> entry in _emitterStates)
        {
            if (!_seenEmitters.Contains(entry.Key) && entry.Value.ActiveCount <= 0)
            {
                _staleEmitters.Add(entry.Key);
            }
        }

        for (var i = 0; i < _staleEmitters.Count; i++)
        {
            Uuid owner = _staleEmitters[i];
            if (_emitterStates.Remove(owner, out EmitterRuntimeState? state))
            {
                state.Reset();
                _emitterStatePool.Push(state);
            }
        }
    }

    private void EmitForEntity(
        Uuid owner,
        bool muted,
        in TransformComponent transform,
        in ParticleEmitterComponent emitter,
        float dt,
        int maxGlobalParticles,
        float density
    ) {
        _seenEmitters.Add(owner);

        ParticleEffect? effect = muted || !emitter.Playing ? null : ResolveEmitterEffect(in emitter);
        if (effect == null || !float.IsFinite(emitter.RateScale) || emitter.RateScale <= 0f || density <= 0f)
        {
            if (_emitterStates.TryGetValue(owner, out EmitterRuntimeState? existing))
            {
                existing.Accumulator = 0f;
            }

            return;
        }

        double baseRate = effect.Rate * (double)emitter.RateScale;
        if (!double.IsFinite(baseRate) || baseRate <= 0d)
        {
            return;
        }

        double effectiveRate = baseRate * density;

        if (!IsFinite(in transform.Position) || !IsFinite(in emitter.Offset) || !IsFinite(in emitter.LocalDirection))
        {
            return;
        }

        Vector3 spawnOrigin = transform.Position + Vector3.Transform(emitter.Offset, transform.Orientation);
        Vector3 localDir = emitter.LocalDirection.LengthSquared() > 1e-6f ? emitter.LocalDirection : Vector3.UnitY;
        Vector3 worldDir = Vector3.Transform(localDir, transform.Orientation);
        if (!IsFinite(in spawnOrigin) || !IsFinite(in worldDir))
        {
            return;
        }

        EmitterRuntimeState state = GetOrCreateEmitterState(owner);
        double maxAccumulator = effect.MaxParticles + 1d;
        double increment = dt > 0f ? dt * effectiveRate : 0d;
        double nextAccumulator = double.IsFinite(increment)
            ? Math.Min(state.Accumulator + increment, maxAccumulator)
            : maxAccumulator;
        state.Accumulator = (float)nextAccumulator;

        int toSpawn = (int)state.Accumulator;
        if (toSpawn <= 0)
        {
            return;
        }

        state.Accumulator -= toSpawn;

        int availableEmitter = Math.Max(0, effect.MaxParticles - state.ActiveCount);
        int availableGlobal = Math.Max(0, maxGlobalParticles - _activeCount);
        int spawnCount = Math.Min(toSpawn, Math.Min(availableEmitter, availableGlobal));
        if (spawnCount <= 0)
        {
            return;
        }

        for (var i = 0; i < spawnCount; i++)
        {
            if (!SpawnParticle(effect, owner, state, in spawnOrigin, in worldDir, speedScale: 1f, sizeScale: 1f, emitter.TintRgba, maxGlobalParticles))
            {
                break;
            }
        }
    }

    private ParticleEffect? ResolveEmitterEffect(in ParticleEmitterComponent emitter)
    {
        if (emitter.Effect != null)
        {
            return emitter.Effect;
        }

        string? effectId = emitter.EffectId;
        if (string.IsNullOrEmpty(effectId))
        {
            return null;
        }

        if (_resolvedEffectCache.TryGetValue(effectId, out ParticleEffect? cached))
        {
            return cached;
        }

        if (_missingEffectIds.Contains(effectId))
        {
            return null;
        }

        Result<ParticleEffect> result = _effectDatabase.Get(effectId);
        if (result.Success && result.Value != null)
        {
            _resolvedEffectCache[effectId] = result.Value;
            return result.Value;
        }

        if (_missingEffectIds.Count < MAX_MISSING_EFFECT_CACHE)
        {
            _missingEffectIds.Add(effectId);
            _logger?.LogWarning("Unknown particle effect id \"{EffectId}\": {Message}", effectId, result.Message);
        }

        return null;
    }

    private EmitterRuntimeState GetOrCreateEmitterState(Uuid owner)
    {
        if (_emitterStates.TryGetValue(owner, out EmitterRuntimeState? state))
        {
            return state;
        }

        state = _emitterStatePool.Count > 0 ? _emitterStatePool.Pop() : new EmitterRuntimeState();
        state.Reset();
        _emitterStates[owner] = state;
        return state;
    }

    private bool SpawnParticle(
        ParticleEffect effect,
        Uuid owner,
        EmitterRuntimeState? emitterState,
        in Vector3 origin,
        in Vector3 direction,
        float speedScale,
        float sizeScale,
        uint tintRgba,
        int maxGlobalParticles
    ) {
        if (_activeCount >= maxGlobalParticles)
        {
            return false;
        }

        EnsureParticleCapacity(_activeCount + 1, maxGlobalParticles);

        Vector3 spawnPos = origin;
        if (effect.SpawnRadius > 0f)
        {
            Vector3 offsetDir = SampleSphereDirection(_random);
            float radius = effect.SpawnRadius * MathF.Cbrt((float)_random.NextDouble());
            spawnPos += offsetDir * radius;
        }

        Vector3 velocityDir = SampleConeDirection(in direction, effect.SpreadDegrees, _random);
        float speed = effect.Speed.Sample(_random) * speedScale;
        float lifetime = MathF.Max(0.001f, effect.Lifetime.Sample(_random));
        float startSize = effect.StartSize.Sample(_random) * sizeScale;
        float endSize = effect.EndSize.Sample(_random) * sizeScale;

        Vector4 startColor = effect.StartColor;
        if (effect.SecondaryStartColor.HasValue)
        {
            float mix = (float)_random.NextDouble();
            startColor = Vector4.Lerp(effect.StartColor, effect.SecondaryStartColor.Value, mix);
        }

        Vector4 endColor = effect.EndColor;
        if (tintRgba != 0u)
        {
            Vector4 tint = ParticleEffect.UnpackTintLinear(tintRgba);
            startColor *= tint;
            endColor *= tint;
        }

        ref LiveParticle particle = ref _particles[_activeCount++];
        particle.Effect = effect;
        particle.EmitterOwner = owner;
        particle.Position = spawnPos;
        particle.Velocity = velocityDir * speed;
        particle.Gravity = effect.Gravity;
        particle.StartColor = startColor;
        particle.EndColor = endColor;
        particle.Age = 0f;
        particle.Lifetime = lifetime;
        particle.StartSize = startSize;
        particle.EndSize = endSize;
        particle.Drag = effect.Drag;
        particle.Rotation = effect.InitialRotationDegrees.Sample(_random) * MathS.DEGREES_TO_RADIANS;
        particle.AngularVelocity = effect.AngularVelocityDegrees.Sample(_random) * MathS.DEGREES_TO_RADIANS;

        if (emitterState != null)
        {
            emitterState.ActiveCount++;
        }
        else
        {
            _burstActiveCounts.TryGetValue(effect, out int count);
            _burstActiveCounts[effect] = count + 1;
        }

        return true;
    }

    private void EnsureParticleCapacity(int required, int maxGlobalParticles)
    {
        if (_particles.Length >= required)
        {
            return;
        }

        int target = Math.Min(maxGlobalParticles, Math.Max(required, Math.Max(128, _particles.Length * 2)));
        if (target < required)
        {
            target = required;
        }

        Array.Resize(ref _particles, target);
    }

    private void PublishSnapshot()
    {
        _activeParticleCount = _activeCount;
        _writeSnapshot.Reset();

        if (_activeCount > 0)
        {
            PackSnapshotBatches();
        }

        lock (_snapshotLock)
        {
            (_writeSnapshot, _readySnapshot) = (_readySnapshot, _writeSnapshot);
            _hasReadySnapshot = true;
        }
    }

    private void PackSnapshotBatches()
    {
        float cellSize = SanitizeBatchCellSize(_renderSettings.Particles.BatchCellSize.Get());
        double invCellSize = 1d / cellSize;

        for (var i = 0; i < _activeCount; i++)
        {
            ref readonly LiveParticle particle = ref _particles[i];
            ParticleEffect effect = particle.Effect;
            var key = new BatchKey(
                effect.Texture,
                effect.BlendMode,
                particle.EmitterOwner,
                QuantizeCell(particle.Position.X, invCellSize),
                QuantizeCell(particle.Position.Y, invCellSize),
                QuantizeCell(particle.Position.Z, invCellSize)
            );
            if (!_batchBuckets.TryGetValue(key, out List<int>? bucket))
            {
                bucket = _bucketPool.Count > 0 ? _bucketPool.Pop() : new List<int>(32);
                _batchBuckets[key] = bucket;
                _batchOrder.Add(key);
            }

            bucket.Add(i);
        }

        _writeSnapshot.EnsureInstanceCapacity(_activeCount);
        _writeSnapshot.EnsureBatchCapacity(_batchOrder.Count);

        var instanceOffset = 0;
        var batchIndex = 0;
        for (var b = 0; b < _batchOrder.Count; b++)
        {
            BatchKey key = _batchOrder[b];
            List<int> bucket = _batchBuckets[key];
            int count = bucket.Count;
            if (count == 0)
            {
                continue;
            }

            Vector3 min = new(float.PositiveInfinity);
            Vector3 max = new(float.NegativeInfinity);

            for (var i = 0; i < count; i++)
            {
                ref readonly LiveParticle p = ref _particles[bucket[i]];
                float t = Math.Clamp(p.Age / p.Lifetime, 0f, 1f);
                float size = p.StartSize + (p.EndSize - p.StartSize) * t;
                Vector4 color = Vector4.Lerp(p.StartColor, p.EndColor, t);

                _writeSnapshot.Instances[instanceOffset + i] = new ParticleInstance
                {
                    Position = p.Position,
                    Size = size,
                    Color = color,
                    Rotation = p.Rotation,
                    Emissive = p.Effect.EmissiveIntensity,
                };

                var halfExtent = new Vector3(size * HALF_DIAGONAL_FACTOR);
                min = Vector3.Min(min, p.Position - halfExtent);
                max = Vector3.Max(max, p.Position + halfExtent);
            }

            Vector3 center = (min + max) * 0.5f;
            Vector3 extents = (max - min) * 0.5f;
            var bounds = new BoundingBox(center, extents, extents.Length());

            _writeSnapshot.Batches[batchIndex++] = new ParticleRenderBatch(
                key.Texture,
                key.BlendMode,
                instanceOffset,
                count,
                bounds
            );
            instanceOffset += count;
        }

        _writeSnapshot.InstanceCount = instanceOffset;
        _writeSnapshot.BatchCount = batchIndex;

        foreach (List<int> bucket in _batchBuckets.Values)
        {
            bucket.Clear();
            _bucketPool.Push(bucket);
        }

        _batchBuckets.Clear();
        _batchOrder.Clear();
    }

    private static Vector3 SampleSphereDirection(Random random)
    {
        float cosTheta = 1f - 2f * (float)random.NextDouble();
        float sinTheta = MathF.Sqrt(MathF.Max(0f, 1f - cosTheta * cosTheta));
        float phi = (float)random.NextDouble() * MathF.Tau;
        return new Vector3(
            sinTheta * MathF.Cos(phi),
            cosTheta,
            sinTheta * MathF.Sin(phi)
        );
    }

    private static Vector3 SampleConeDirection(in Vector3 rawAxis, float spreadDegrees, Random random)
    {
        float axisLenSq = rawAxis.LengthSquared();
        Vector3 axis = axisLenSq > 1e-6f ? rawAxis / MathF.Sqrt(axisLenSq) : Vector3.UnitY;

        if (spreadDegrees <= 0.001f)
        {
            return axis;
        }

        if (spreadDegrees >= 179.999f)
        {
            return SampleSphereDirection(random);
        }

        Vector3 reference = MathF.Abs(axis.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        Vector3 tangent = Vector3.Normalize(Vector3.Cross(reference, axis));
        Vector3 bitangent = Vector3.Cross(axis, tangent);

        float thetaMax = spreadDegrees * MathS.DEGREES_TO_RADIANS;
        float cosThetaMax = MathF.Cos(thetaMax);
        float cosTheta = 1f - (float)random.NextDouble() * (1f - cosThetaMax);
        float sinTheta = MathF.Sqrt(MathF.Max(0f, 1f - cosTheta * cosTheta));
        float phi = (float)random.NextDouble() * MathF.Tau;

        return Vector3.Normalize(
            axis * cosTheta
            + (tangent * MathF.Cos(phi) + bitangent * MathF.Sin(phi)) * sinTheta
        );
    }

    private static float ClampDensity(float density)
    {
        return float.IsNaN(density) ? 0f : Math.Clamp(density, 0f, float.MaxValue);
    }

    private static int ScaleBurstCount(int requestedCount, float density, int maxAvailable)
    {
        if (requestedCount <= 0 || density <= 0f || maxAvailable <= 0)
        {
            return 0;
        }

        double scaled = requestedCount * (double)density;
        if (!double.IsFinite(scaled) || scaled >= maxAvailable)
        {
            return maxAvailable;
        }

        int rounded = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
        return Math.Clamp(rounded, 1, maxAvailable);
    }

    private static float SanitizeBatchCellSize(float cellSize)
    {
        if (!float.IsFinite(cellSize) || cellSize <= 0f)
        {
            return ParticleSettings.DEFAULT_BATCH_CELL_SIZE;
        }

        return MathF.Max(cellSize, MIN_BATCH_CELL_SIZE);
    }

    private static int QuantizeCell(float coordinate, double invCellSize)
    {
        double scaled = Math.Floor(coordinate * invCellSize);
        if (double.IsNaN(scaled))
        {
            return 0;
        }

        if (scaled <= int.MinValue)
        {
            return int.MinValue;
        }

        if (scaled >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return (int)scaled;
    }

    private static bool IsFinite(in Vector3 vector)
    {
        return float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);
    }

    private readonly record struct BatchKey(
        Texture? Texture,
        ParticleBlendMode BlendMode,
        Uuid EmitterOwner,
        int CellX,
        int CellY,
        int CellZ
    );

    private readonly record struct QueuedBurst(ParticleBurstRequest Request, long Generation);

    private struct LiveParticle
    {
        public ParticleEffect Effect;
        public Uuid EmitterOwner;
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Gravity;
        public Vector4 StartColor;
        public Vector4 EndColor;
        public float Age;
        public float Lifetime;
        public float StartSize;
        public float EndSize;
        public float Drag;
        public float Rotation;
        public float AngularVelocity;
    }

    private sealed class EmitterRuntimeState
    {
        public float Accumulator;
        public int ActiveCount;

        public void Reset()
        {
            Accumulator = 0f;
            ActiveCount = 0;
        }
    }

    private struct EmitAction : IForEach<TransformComponent, ParticleEmitterComponent>
    {
        public ParticleSystem Owner;
        public float Delta;
        public int MaxGlobalParticles;
        public float Density;

        public void Execute(float delta, DataStore store, int entity, in TransformComponent transform, in ParticleEmitterComponent emitter)
        {
            Uuid owner = store.GetUuid(entity);
            bool muted = store.Has(typeof(ParticleEmitterMutedComponent), entity);
            Owner.EmitForEntity(owner, muted, in transform, in emitter, Delta, MaxGlobalParticles, Density);
        }
    }

    private sealed class SnapshotGate;
}
