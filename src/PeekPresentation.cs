using System;
using EFT.CameraControl;
using UnityEngine;

namespace LookyLoot;

/// <summary>Angle-driven inventory-only presentation. Never intercepts pointer clicks or drags.</summary>
internal static class PeekPresentation
{
    private static RectTransform? _root, _viewport;
    private static CanvasGroup? _group;
    private static InventoryBlur? _blur;
    private static Vector3 _originalScale, _originalPosition;
    private static float _originalAlpha, _strength;
    private static readonly Vector3[] Corners = new Vector3[4];
    // Where the panel shrinks towards: 0 = its centre, 1 = its trailing edge.
    private const float TrailingPivot = 0.5f;

    internal static void Tick()
    {
        if (!PeekSession.Valid)
        {
            PeekSession.Stop(resetLook: true);
            return;
        }

        // Follow the current head orientation, including vanilla recentering on release.
        // No elapsed-time ramp, movement threshold, speed test or release timer.
        Vector3 head = PeekSession.Player!.HeadRotation;
        float progress = PeekMath.Progress(head.x, head.y, Plugin.FullEffectAngle.Value);
        float strength = PeekMath.Strength(progress);
        if (progress <= 0f)
        {
            Restore();
            return;
        }
        if (_root == null)
            Capture();
        if (_viewport == null || _group == null || _root == null)
            throw new InvalidOperationException("Inventory peek presentation lost its UI root or canvas.");

        Rect view = _viewport.rect;
        var camera = CameraManager.Instance.Camera;
        float fov = camera != null ? Mathf.Clamp(camera.fieldOfView, 10f, 150f) : 60f;
        float focal = view.height / (2f * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad));
        // Movement ramps in linearly with the angle so the panel tracks the head from the
        // start, rather than sitting still and then catching up like scale/fade/blur.
        var target = PeekMath.Parallax(head.x, head.y, focal * Plugin.ParallaxStrength.Value * progress);

        _strength = strength;
        float scale = Mathf.Lerp(1f, Plugin.PeekScale.Value, strength);
        _root.localScale = _originalScale * scale;
        // Shrink towards the edge the panel is being pushed to, so it reads as moved aside
        // rather than shrinking in place. Changes neither pivot, anchors nor child layout.
        Rect rect = _root.rect;
        Vector2 pivot = rect.center + Vector2.Scale(TrailingDirection(target), rect.size * (0.5f * TrailingPivot));
        Vector3 shift = _root.localRotation * Vector3.Scale(pivot, _originalScale);
        _root.anchoredPosition3D = _originalPosition + shift * (1f - scale);
        _group.alpha = _originalAlpha * Mathf.Lerp(1f, Plugin.PeekOpacity.Value, strength);

        _root.GetWorldCorners(Corners);
        Vector3 min = _viewport.InverseTransformPoint(Corners[0]);
        Vector3 max = min;
        for (int i = 1; i < Corners.Length; i++)
        {
            Vector3 point = _viewport.InverseTransformPoint(Corners[i]);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }
        var limited = PeekMath.LimitOffset(min.x, min.y, max.x - min.x, max.y - min.y,
            view.xMin, view.yMin, view.width, view.height, target.x, target.y);
        Vector3 worldOffset = _viewport.TransformVector(new Vector3(limited.x, limited.y, 0f));
        _root.localPosition += _root.parent.InverseTransformVector(worldOffset);
    }

    /// <summary>Unit displacement direction in root-local axes; zero (centre) without parallax.</summary>
    private static Vector2 TrailingDirection((float x, float y) target)
    {
        var direction = new Vector2(target.x, target.y);
        if (direction.sqrMagnitude < 1e-6f)
            return Vector2.zero;
        Vector3 local = _root!.InverseTransformVector(_viewport!.TransformVector(direction));
        return ((Vector2)local).normalized;
    }

    private static void Capture()
    {
        _root = PeekSession.Controller!.Screen.RectTransform;
        var canvas = _root.GetComponentInParent<Canvas>();
        _viewport = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        // Never scale a shared canvas/HUD; the inventory must be a child of its root canvas.
        if (_viewport == null || _viewport == _root || _root.parent == null)
        {
            _root = null;
            throw new InvalidOperationException("Inventory UI is not a child of a supported root canvas.");
        }
        _originalScale = _root.localScale;
        _originalPosition = _root.anchoredPosition3D;
        // A group added here is kept after restore: Destroy is deferred to frame end, so a
        // same-frame recapture would otherwise pick up a group that is about to vanish.
        _group = _root.GetComponent<CanvasGroup>();
        if (_group == null)
            _group = _root.gameObject.AddComponent<CanvasGroup>();
        _originalAlpha = _group.alpha;
        _blur = CameraManager.Instance._blur;
        // No interaction shield, click cancellation, deselection or tooltip closing:
        // a normal MMB examine click with a tiny pan must remain a normal UI operation.
    }

    internal static float BlurMultiplier(InventoryBlur blur)
        => _root != null && blur == _blur && PeekSession.Valid
            ? Mathf.Lerp(1f, Plugin.PeekBlurStrength.Value, _strength)
            : 1f;

    internal static void Restore()
    {
        try
        {
            if (_root != null)
            {
                _root.localScale = _originalScale;
                _root.anchoredPosition3D = _originalPosition;
            }
            if (_group != null)
                _group.alpha = _originalAlpha;
        }
        finally
        {
            _root = _viewport = null;
            _group = null;
            _blur = null;
            _strength = 0f;
        }
    }
}
