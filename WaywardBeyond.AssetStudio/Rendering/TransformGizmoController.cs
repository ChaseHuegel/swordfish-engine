using System;
using System.Globalization;
using System.Numerics;

namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Performs 3D ray hit-testing and interactive drag math for Translate, Rotate, and Scale gizmos.</summary>
public sealed class TransformGizmoController
{
    private const float TRANSLATE_SNAP_METERS = 0.05f;
    private const float ROTATE_SNAP_DEGREES = 5f;
    private const float SCALE_SNAP_STEP = 0.05f;
    private const float DEGREES_TO_RADIANS = MathF.PI / 180f;
    private const float RADIANS_TO_DEGREES = 180f / MathF.PI;

    private Vector3 _startPosition;
    private Vector3 _dragOrigin;
    private Vector3 _startRotationDeg;
    private Matrix4x4 _startRotationMatrix;
    private Vector3 _startScale;
    private Matrix4x4 _dragBasisRotation;
    private float _dragGizmoScale = 1f;
    private Vector2 _startScreenPos;
    private float _startAxisParam;
    private Vector3 _startPlaneHit;
    private Vector3 _startRingVec;
    private bool _useScreenAngleFallback;

    /// <summary>Currently hovered gizmo handle under the cursor.</summary>
    public TransformGizmoAxis HoveredAxis { get; private set; }

    /// <summary>Gizmo handle currently being dragged, or <see cref="TransformGizmoAxis.None"/>.</summary>
    public TransformGizmoAxis ActiveAxis { get; private set; }

    /// <summary>True while an interactive gizmo drag is in progress.</summary>
    public bool IsDragging => ActiveAxis != TransformGizmoAxis.None;

    /// <summary>Live formatted delta caption during an active drag (e.g. <c>X +0.15 m</c>).</summary>
    public string? DragReadoutLabel { get; private set; }

    /// <summary>Resolves the effective coordinate basis matrix for the requested gizmo mode and space.</summary>
    public static Matrix4x4 ResolveBasisRotation(
        TransformGizmoMode mode,
        TransformGizmoSpace space,
        Vector3 rotationDegrees)
    {
        if (space == TransformGizmoSpace.World && mode != TransformGizmoMode.Scale)
        {
            return Matrix4x4.Identity;
        }

        return ModelTransformMath.CreateRotationMatrix(rotationDegrees);
    }

    /// <summary>Updates <see cref="HoveredAxis"/> from pointer coordinates and returns whether hover changed.</summary>
    public bool UpdateHover(
        Vector2 screenPos,
        float viewportWidth,
        float viewportHeight,
        PreviewCamera camera,
        TransformGizmoMode mode,
        TransformGizmoSpace space,
        Vector3 position,
        Vector3 rotationDegrees,
        bool isReadOnly)
    {
        TransformGizmoAxis next = isReadOnly
            ? TransformGizmoAxis.None
            : HitTest(screenPos, viewportWidth, viewportHeight, camera, mode, space, position, rotationDegrees);
        if (next == HoveredAxis)
        {
            return false;
        }

        HoveredAxis = next;
        return true;
    }

    /// <summary>Tests which gizmo handle lies beneath <paramref name="screenPos"/> in world space.</summary>
    public TransformGizmoAxis HitTest(
        Vector2 screenPos,
        float viewportWidth,
        float viewportHeight,
        PreviewCamera camera,
        TransformGizmoMode mode,
        TransformGizmoSpace space,
        Vector3 position,
        Vector3 rotationDegrees)
    {
        if (viewportWidth <= 2f || viewportHeight <= 2f)
        {
            return TransformGizmoAxis.None;
        }

        (Vector3 rayOrigin, Vector3 rayDir) = camera.ScreenPointToRay(screenPos.X, screenPos.Y, viewportWidth, viewportHeight);
        Matrix4x4 basis = ResolveBasisRotation(mode, space, rotationDegrees);
        (Vector3 axisX, Vector3 axisY, Vector3 axisZ) = TransformGizmoMeshBuilder.ExtractBasisAxes(in basis);
        float s = TransformGizmoMeshBuilder.ComputeScreenConstantScale(position, camera);

        switch (mode)
        {
            case TransformGizmoMode.Translate:
            {
                TransformGizmoAxis planeHit = HitTestPlanarQuads(rayOrigin, rayDir, position, axisX, axisY, axisZ, s);
                if (planeHit != TransformGizmoAxis.None)
                {
                    return planeHit;
                }

                return HitTestAxisSegments(rayOrigin, rayDir, position, axisX, axisY, axisZ, 1.22f * s, 0.13f * s);
            }

            case TransformGizmoMode.Rotate:
                return HitTestRotationRings(rayOrigin, rayDir, position, axisX, axisY, axisZ, 0.92f * s, 0.14f * s);

            case TransformGizmoMode.Scale:
            {
                float centerDist = DistancePointToRay(position, rayOrigin, rayDir);
                if (centerDist <= 0.15f * s)
                {
                    return TransformGizmoAxis.Uniform;
                }

                return HitTestAxisSegments(rayOrigin, rayDir, position, axisX, axisY, axisZ, 1.06f * s, 0.13f * s);
            }

            default:
                return TransformGizmoAxis.None;
        }
    }

    /// <summary>Starts dragging the specified gizmo axis, capturing initial transform and ray anchors.</summary>
    public bool BeginDrag(
        TransformGizmoAxis axis,
        Vector2 screenPos,
        float viewportWidth,
        float viewportHeight,
        PreviewCamera camera,
        TransformGizmoMode mode,
        TransformGizmoSpace space,
        Vector3 position,
        Vector3 rotationDegrees,
        Vector3 scale,
        Vector3? gizmoAnchor = null)
    {
        if (axis == TransformGizmoAxis.None || viewportWidth <= 2f || viewportHeight <= 2f)
        {
            return false;
        }

        ActiveAxis = axis;
        HoveredAxis = axis;
        _startPosition = position;
        _dragOrigin = gizmoAnchor ?? position;
        _startRotationDeg = rotationDegrees;
        _startRotationMatrix = ModelTransformMath.CreateRotationMatrix(rotationDegrees);
        _startScale = scale;
        _dragBasisRotation = ResolveBasisRotation(mode, space, rotationDegrees);
        _dragGizmoScale = TransformGizmoMeshBuilder.ComputeScreenConstantScale(_dragOrigin, camera);
        _startScreenPos = screenPos;

        (Vector3 rayOrigin, Vector3 rayDir) = camera.ScreenPointToRay(screenPos.X, screenPos.Y, viewportWidth, viewportHeight);
        (Vector3 axisX, Vector3 axisY, Vector3 axisZ) = TransformGizmoMeshBuilder.ExtractBasisAxes(in _dragBasisRotation);

        if (mode == TransformGizmoMode.Rotate)
        {
            Vector3 rotAxis = SelectSingleAxisVector(axis, axisX, axisY, axisZ);
            if (MathF.Abs(Vector3.Dot(rayDir, rotAxis)) >= 0.08f
                && TryIntersectRayPlane(rayOrigin, rayDir, _dragOrigin, rotAxis, out Vector3 hit))
            {
                _startRingVec = SafeNormalize(hit - _dragOrigin, PerpendicularVector(rotAxis));
                _useScreenAngleFallback = false;
            }
            else
            {
                _useScreenAngleFallback = true;
            }

            DragReadoutLabel = FormatRotationReadout(axis, 0f);
            return true;
        }

        if (axis is TransformGizmoAxis.XY or TransformGizmoAxis.XZ or TransformGizmoAxis.YZ)
        {
            Vector3 normal = SelectPlaneNormal(axis, axisX, axisY, axisZ);
            if (!TryIntersectRayPlane(rayOrigin, rayDir, _dragOrigin, normal, out _startPlaneHit))
            {
                _startPlaneHit = _dragOrigin;
            }

            DragReadoutLabel = $"{axis} +0.00 m";
            return true;
        }

        if (axis == TransformGizmoAxis.Uniform)
        {
            _startAxisParam = 0f;
            DragReadoutLabel = $"Scale {ModelTransformMath.FormatComponent(scale.X, 2)}x";
            return true;
        }

        Vector3 singleAxis = SelectSingleAxisVector(axis, axisX, axisY, axisZ);
        if (!TryClosestRayParameterOnAxis(rayOrigin, rayDir, _dragOrigin, singleAxis, out _startAxisParam))
        {
            _startAxisParam = 0f;
        }

        DragReadoutLabel = mode == TransformGizmoMode.Scale
            ? FormatScaleReadout(axis, scale)
            : FormatTranslateReadout(axis, 0f);
        return true;
    }

    /// <summary>Computes updated Position, Rotation, and Scale during an active drag and formats a live readout.</summary>
    public bool UpdateDrag(
        Vector2 screenPos,
        float viewportWidth,
        float viewportHeight,
        PreviewCamera camera,
        TransformGizmoMode mode,
        bool snapEnabled,
        bool uniformScale,
        out Vector3 newPosition,
        out Vector3 newRotationDegrees,
        out Vector3 newScale,
        out string? readoutLabel)
    {
        newPosition = _startPosition;
        newRotationDegrees = _startRotationDeg;
        newScale = _startScale;
        readoutLabel = DragReadoutLabel;

        if (!IsDragging || viewportWidth <= 2f || viewportHeight <= 2f)
        {
            return false;
        }

        (Vector3 rayOrigin, Vector3 rayDir) = camera.ScreenPointToRay(screenPos.X, screenPos.Y, viewportWidth, viewportHeight);
        (Vector3 axisX, Vector3 axisY, Vector3 axisZ) = TransformGizmoMeshBuilder.ExtractBasisAxes(in _dragBasisRotation);

        switch (mode)
        {
            case TransformGizmoMode.Translate:
                if (ActiveAxis is TransformGizmoAxis.XY or TransformGizmoAxis.XZ or TransformGizmoAxis.YZ)
                {
                    Vector3 normal = SelectPlaneNormal(ActiveAxis, axisX, axisY, axisZ);
                    if (TryIntersectRayPlane(rayOrigin, rayDir, _dragOrigin, normal, out Vector3 planeHit))
                    {
                        Vector3 rawDelta = planeHit - _startPlaneHit;
                        if (Vector3.Dot(rawDelta, rayDir) < 0f && rawDelta.LengthSquared() > 10000f)
                        {
                            return false;
                        }

                        if (snapEnabled)
                        {
                            rawDelta = SnapPlanarDelta(rawDelta, ActiveAxis, axisX, axisY, axisZ, TRANSLATE_SNAP_METERS);
                        }

                        newPosition = ClampPosition(_startPosition + rawDelta);
                        readoutLabel = string.Create(
                            CultureInfo.InvariantCulture,
                            $"{ActiveAxis} {FormatSignedMeters(rawDelta.Length())}");
                        DragReadoutLabel = readoutLabel;
                        return true;
                    }

                    return false;
                }

                Vector3 moveAxis = SelectSingleAxisVector(ActiveAxis, axisX, axisY, axisZ);
                if (TryClosestRayParameterOnAxis(rayOrigin, rayDir, _dragOrigin, moveAxis, out float axisT))
                {
                    float delta = Math.Clamp(axisT - _startAxisParam, -50f, 50f);
                    if (snapEnabled)
                    {
                        delta = SnapScalar(delta, TRANSLATE_SNAP_METERS);
                    }

                    newPosition = ClampPosition(_startPosition + (moveAxis * delta));
                    readoutLabel = FormatTranslateReadout(ActiveAxis, delta);
                    DragReadoutLabel = readoutLabel;
                    return true;
                }

                return false;

            case TransformGizmoMode.Rotate:
            {
                Vector3 rotAxis = SelectSingleAxisVector(ActiveAxis, axisX, axisY, axisZ);
                float deltaDeg;
                if (!_useScreenAngleFallback
                    && MathF.Abs(Vector3.Dot(rayDir, rotAxis)) >= 0.08f
                    && TryIntersectRayPlane(rayOrigin, rayDir, _dragOrigin, rotAxis, out Vector3 hit))
                {
                    Vector3 currentVec = SafeNormalize(hit - _dragOrigin, _startRingVec);
                    float sin = Vector3.Dot(Vector3.Cross(_startRingVec, currentVec), rotAxis);
                    float cos = Vector3.Dot(_startRingVec, currentVec);
                    deltaDeg = MathF.Atan2(sin, cos) * RADIANS_TO_DEGREES;
                }
                else
                {
                    float dx = screenPos.X - _startScreenPos.X;
                    float dy = _startScreenPos.Y - screenPos.Y;
                    deltaDeg = (dx + dy) * 0.65f;
                }

                if (snapEnabled)
                {
                    deltaDeg = SnapScalar(deltaDeg, ROTATE_SNAP_DEGREES);
                }

                Matrix4x4 deltaRot = Matrix4x4.CreateFromAxisAngle(rotAxis, deltaDeg * DEGREES_TO_RADIANS);
                Matrix4x4 combined = _startRotationMatrix * deltaRot;
                newRotationDegrees = ModelTransformMath.ExtractYawPitchRollDegrees(in combined);
                readoutLabel = FormatRotationReadout(ActiveAxis, deltaDeg);
                DragReadoutLabel = readoutLabel;
                return true;
            }

            case TransformGizmoMode.Scale:
            {
                if (ActiveAxis == TransformGizmoAxis.Uniform)
                {
                    float pixelDelta = ((screenPos.X - _startScreenPos.X) + (_startScreenPos.Y - screenPos.Y)) * 0.005f;
                    float factor = Math.Clamp(1f + pixelDelta, 0.02f, 20f);
                    if (snapEnabled)
                    {
                        float snappedX = Math.Clamp(SnapScalar(_startScale.X * factor, SCALE_SNAP_STEP), 0.01f, 20f);
                        float ratio = _startScale.X > 1e-4f ? snappedX / _startScale.X : 1f;
                        newScale = ClampScale(_startScale * ratio);
                    }
                    else
                    {
                        newScale = ClampScale(_startScale * factor);
                    }

                    readoutLabel = $"Scale {ModelTransformMath.FormatComponent(newScale.X, 2)}";
                    DragReadoutLabel = readoutLabel;
                    return true;
                }

                Vector3 scaleAxis = SelectSingleAxisVector(ActiveAxis, axisX, axisY, axisZ);
                if (!TryClosestRayParameterOnAxis(rayOrigin, rayDir, _dragOrigin, scaleAxis, out float scaleT))
                {
                    return false;
                }

                float worldDelta = scaleT - _startAxisParam;
                float normalizedDelta = worldDelta / MathF.Max(_dragGizmoScale, 0.05f);
                float baseComp = ActiveAxis switch
                {
                    TransformGizmoAxis.X => _startScale.X,
                    TransformGizmoAxis.Y => _startScale.Y,
                    _ => _startScale.Z,
                };
                float updatedComp = Math.Clamp(baseComp * MathF.Max(0.02f, 1f + normalizedDelta), 0.01f, 20f);
                if (snapEnabled)
                {
                    updatedComp = Math.Clamp(SnapScalar(updatedComp, SCALE_SNAP_STEP), 0.01f, 20f);
                }

                if (uniformScale)
                {
                    float factor = baseComp > 1e-4f ? updatedComp / baseComp : 1f;
                    newScale = ClampScale(_startScale * factor);
                }
                else
                {
                    newScale = ActiveAxis switch
                    {
                        TransformGizmoAxis.X => new Vector3(updatedComp, _startScale.Y, _startScale.Z),
                        TransformGizmoAxis.Y => new Vector3(_startScale.X, updatedComp, _startScale.Z),
                        _ => new Vector3(_startScale.X, _startScale.Y, updatedComp),
                    };
                }

                readoutLabel = FormatScaleReadout(ActiveAxis, newScale);
                DragReadoutLabel = readoutLabel;
                return true;
            }

            default:
                return false;
        }
    }

    /// <summary>Ends the active gizmo drag and clears drag state.</summary>
    public void EndDrag()
    {
        ActiveAxis = TransformGizmoAxis.None;
        DragReadoutLabel = null;
    }

    private static TransformGizmoAxis HitTestPlanarQuads(
        Vector3 rayOrigin,
        Vector3 rayDir,
        Vector3 origin,
        Vector3 axisX,
        Vector3 axisY,
        Vector3 axisZ,
        float scale)
    {
        float minBound = 0.21f * scale;
        float maxBound = 0.51f * scale;
        float bestRayT = float.PositiveInfinity;
        TransformGizmoAxis best = TransformGizmoAxis.None;

        CheckQuad(TransformGizmoAxis.XY, axisX, axisY, axisZ);
        CheckQuad(TransformGizmoAxis.XZ, axisX, axisZ, axisY);
        CheckQuad(TransformGizmoAxis.YZ, axisY, axisZ, axisX);
        return best;

        void CheckQuad(TransformGizmoAxis candidate, Vector3 u, Vector3 v, Vector3 n)
        {
            if (!TryIntersectRayPlaneT(rayOrigin, rayDir, origin, n, out float rayT, out Vector3 hit) || rayT <= 0f)
            {
                return;
            }

            Vector3 rel = hit - origin;
            float uCoord = Vector3.Dot(rel, u);
            float vCoord = Vector3.Dot(rel, v);
            if (uCoord >= minBound && uCoord <= maxBound && vCoord >= minBound && vCoord <= maxBound && rayT < bestRayT)
            {
                bestRayT = rayT;
                best = candidate;
            }
        }
    }

    private static TransformGizmoAxis HitTestAxisSegments(
        Vector3 rayOrigin,
        Vector3 rayDir,
        Vector3 origin,
        Vector3 axisX,
        Vector3 axisY,
        Vector3 axisZ,
        float length,
        float threshold)
    {
        float bestDist = threshold;
        TransformGizmoAxis best = TransformGizmoAxis.None;

        CheckAxis(TransformGizmoAxis.X, axisX);
        CheckAxis(TransformGizmoAxis.Y, axisY);
        CheckAxis(TransformGizmoAxis.Z, axisZ);
        return best;

        void CheckAxis(TransformGizmoAxis candidate, Vector3 dir)
        {
            float dist = DistanceRayToSegment(rayOrigin, rayDir, origin + (dir * (0.06f * length)), origin + (dir * length));
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }
    }

    private static TransformGizmoAxis HitTestRotationRings(
        Vector3 rayOrigin,
        Vector3 rayDir,
        Vector3 origin,
        Vector3 axisX,
        Vector3 axisY,
        Vector3 axisZ,
        float radius,
        float tubeTolerance)
    {
        float bestRayT = float.PositiveInfinity;
        float bestRadialErr = float.PositiveInfinity;
        TransformGizmoAxis best = TransformGizmoAxis.None;

        CheckRingPlane(TransformGizmoAxis.X, axisX);
        CheckRingPlane(TransformGizmoAxis.Y, axisY);
        CheckRingPlane(TransformGizmoAxis.Z, axisZ);

        if (best != TransformGizmoAxis.None)
        {
            return best;
        }

        // Grazing-angle fallback via closest segment distance around each ring
        CheckRingSegments(TransformGizmoAxis.X, axisY, axisZ);
        CheckRingSegments(TransformGizmoAxis.Y, axisX, axisZ);
        CheckRingSegments(TransformGizmoAxis.Z, axisX, axisY);
        return best;

        void CheckRingPlane(TransformGizmoAxis candidate, Vector3 normal)
        {
            if (MathF.Abs(Vector3.Dot(rayDir, normal)) < 0.08f)
            {
                return;
            }

            if (!TryIntersectRayPlaneT(rayOrigin, rayDir, origin, normal, out float rayT, out Vector3 hit) || rayT <= 0f)
            {
                return;
            }

            float radialDist = Vector3.Distance(hit, origin);
            float radialErr = MathF.Abs(radialDist - radius);
            if (radialErr <= tubeTolerance && rayT < bestRayT)
            {
                bestRayT = rayT;
                bestRadialErr = radialErr;
                best = candidate;
            }
        }

        void CheckRingSegments(TransformGizmoAxis candidate, Vector3 u, Vector3 v)
        {
            const int segs = 24;
            for (var i = 0; i < segs; i++)
            {
                float a0 = (i / (float)segs) * MathF.Tau;
                float a1 = ((i + 1) / (float)segs) * MathF.Tau;
                Vector3 p0 = origin + (((u * MathF.Cos(a0)) + (v * MathF.Sin(a0))) * radius);
                Vector3 p1 = origin + (((u * MathF.Cos(a1)) + (v * MathF.Sin(a1))) * radius);
                float d = DistanceRayToSegment(rayOrigin, rayDir, p0, p1);
                if (d < tubeTolerance && d < bestRadialErr)
                {
                    bestRadialErr = d;
                    best = candidate;
                }
            }
        }
    }

    private static bool TryClosestRayParameterOnAxis(
        Vector3 rayOrigin,
        Vector3 rayDir,
        Vector3 axisOrigin,
        Vector3 axisDir,
        out float axisT)
    {
        float a = Vector3.Dot(rayDir, rayDir);
        float b = Vector3.Dot(rayDir, axisDir);
        float e = Vector3.Dot(axisDir, axisDir);
        float denom = (a * e) - (b * b);
        if (denom <= 1e-5f)
        {
            axisT = 0f;
            return false;
        }

        Vector3 r = rayOrigin - axisOrigin;
        float c = Vector3.Dot(rayDir, r);
        float f = Vector3.Dot(axisDir, r);
        axisT = ((a * f) - (b * c)) / denom;
        return float.IsFinite(axisT);
    }

    private static bool TryIntersectRayPlane(
        Vector3 rayOrigin,
        Vector3 rayDir,
        Vector3 planeOrigin,
        Vector3 planeNormal,
        out Vector3 hitPoint)
    {
        return TryIntersectRayPlaneT(rayOrigin, rayDir, planeOrigin, planeNormal, out _, out hitPoint);
    }

    private static bool TryIntersectRayPlaneT(
        Vector3 rayOrigin,
        Vector3 rayDir,
        Vector3 planeOrigin,
        Vector3 planeNormal,
        out float rayT,
        out Vector3 hitPoint)
    {
        float denom = Vector3.Dot(rayDir, planeNormal);
        if (MathF.Abs(denom) <= 1e-5f)
        {
            rayT = 0f;
            hitPoint = planeOrigin;
            return false;
        }

        rayT = Vector3.Dot(planeOrigin - rayOrigin, planeNormal) / denom;
        hitPoint = rayOrigin + (rayDir * rayT);
        return float.IsFinite(rayT);
    }

    private static float DistancePointToRay(Vector3 point, Vector3 rayOrigin, Vector3 rayDir)
    {
        Vector3 toPoint = point - rayOrigin;
        float t = MathF.Max(0f, Vector3.Dot(toPoint, rayDir));
        Vector3 closest = rayOrigin + (rayDir * t);
        return Vector3.Distance(point, closest);
    }

    private static float DistanceRayToSegment(Vector3 rayOrigin, Vector3 rayDir, Vector3 segA, Vector3 segB)
    {
        Vector3 segDir = segB - segA;
        float segLenSq = segDir.LengthSquared();
        if (segLenSq < 1e-10f)
        {
            return DistancePointToRay(segA, rayOrigin, rayDir);
        }

        float a = Vector3.Dot(rayDir, rayDir);
        float b = Vector3.Dot(rayDir, segDir);
        float c = segLenSq;
        Vector3 r = rayOrigin - segA;
        float d = Vector3.Dot(rayDir, r);
        float e = Vector3.Dot(segDir, r);
        float denom = (a * c) - (b * b);

        float s = denom > 1e-6f ? MathF.Max(0f, ((b * e) - (c * d)) / denom) : 0f;
        float t = Math.Clamp(((b * s) + e) / c, 0f, 1f);
        s = MathF.Max(0f, ((b * t) - d) / a);

        Vector3 pRay = rayOrigin + (rayDir * s);
        Vector3 pSeg = segA + (segDir * t);
        return Vector3.Distance(pRay, pSeg);
    }

    private static Vector3 SelectSingleAxisVector(TransformGizmoAxis axis, Vector3 x, Vector3 y, Vector3 z) => axis switch
    {
        TransformGizmoAxis.X => x,
        TransformGizmoAxis.Y => y,
        _ => z,
    };

    private static Vector3 SelectPlaneNormal(TransformGizmoAxis axis, Vector3 x, Vector3 y, Vector3 z) => axis switch
    {
        TransformGizmoAxis.XY => z,
        TransformGizmoAxis.XZ => y,
        _ => x,
    };

    private static Vector3 SnapPlanarDelta(
        Vector3 delta,
        TransformGizmoAxis planeAxis,
        Vector3 x,
        Vector3 y,
        Vector3 z,
        float step)
    {
        (Vector3 u, Vector3 v) = planeAxis switch
        {
            TransformGizmoAxis.XY => (x, y),
            TransformGizmoAxis.XZ => (x, z),
            _ => (y, z),
        };
        float du = SnapScalar(Vector3.Dot(delta, u), step);
        float dv = SnapScalar(Vector3.Dot(delta, v), step);
        return (u * du) + (v * dv);
    }

    private static float SnapScalar(float value, float step) => MathF.Round(value / step) * step;

    private static Vector3 ClampPosition(Vector3 p) => new(
        Math.Clamp(p.X, -25f, 25f),
        Math.Clamp(p.Y, -25f, 25f),
        Math.Clamp(p.Z, -25f, 25f));

    private static Vector3 ClampScale(Vector3 s) => new(
        Math.Clamp(s.X, 0.01f, 20f),
        Math.Clamp(s.Y, 0.01f, 20f),
        Math.Clamp(s.Z, 0.01f, 20f));

    private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float lenSq = v.LengthSquared();
        return lenSq > 1e-8f ? v / MathF.Sqrt(lenSq) : fallback;
    }

    private static Vector3 PerpendicularVector(Vector3 axis)
    {
        Vector3 helper = MathF.Abs(Vector3.Dot(axis, Vector3.UnitY)) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
        return Vector3.Normalize(Vector3.Cross(helper, axis));
    }

    private static string FormatSignedMeters(float value)
    {
        string formatted = ModelTransformMath.FormatComponent(MathF.Abs(value), 2);
        return value >= 0f ? $"+{formatted} m" : $"-{formatted} m";
    }

    private static string FormatTranslateReadout(TransformGizmoAxis axis, float delta) =>
        $"{axis} {(delta >= 0f ? "+" : "-")}{ModelTransformMath.FormatComponent(MathF.Abs(delta), 2)} m";

    private static string FormatRotationReadout(TransformGizmoAxis axis, float deltaDeg)
    {
        float abs = MathF.Abs(deltaDeg);
        string sign = deltaDeg >= 0f ? "+" : "-";
        return $"Rot {axis} {sign}{abs.ToString("0.0", CultureInfo.InvariantCulture)}°";
    }

    private static string FormatScaleReadout(TransformGizmoAxis axis, Vector3 scale)
    {
        float comp = axis switch
        {
            TransformGizmoAxis.X => scale.X,
            TransformGizmoAxis.Y => scale.Y,
            _ => scale.Z,
        };
        return $"Scale {axis} {ModelTransformMath.FormatComponent(comp, 2)}";
    }
}
