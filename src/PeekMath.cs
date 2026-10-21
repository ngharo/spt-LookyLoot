using System;

namespace LookyLoot;

internal static class PeekMath
{
    /// <summary>Head deflection as a 0–1 fraction of the full-effect angle. Linear, no dead zone.</summary>
    internal static float Progress(float pitch, float yaw, float fullEffectAngle)
    {
        const double radians = Math.PI / 180.0;
        double dot = Math.Cos(pitch * radians) * Math.Cos(yaw * radians);
        double angle = Math.Acos(Math.Max(-1.0, Math.Min(1.0, dot))) / radians;
        return (float)Math.Min(1.0, angle / Math.Max(0.001f, fullEffectAngle));
    }

    /// <summary>Scale/fade/blur response: gentle near centre, no threshold, smooth saturation.</summary>
    internal static float Strength(float progress) => progress * progress * (3f - 2f * progress);

    internal static (float x, float y) Parallax(float pitch, float yaw, float projectionScale)
    {
        const double radians = Math.PI / 180.0;
        // EFT's observed horizontal look convention requires positive head yaw ->
        // positive panel X for inverse screen motion. Vertical behavior is unchanged.
        return ((float)Math.Tan(yaw * radians) * projectionScale,
            (float)Math.Tan(pitch * radians) * projectionScale);
    }

    /// <summary>
    /// Softly limit the area offscreen, not each axis independently (half off both axes hides 75%).
    /// The offset eases into the limit instead of stopping dead. Bounds/offset share one space.
    /// </summary>
    internal static (float x, float y) LimitOffset(float panelX, float panelY, float panelW, float panelH,
        float viewX, float viewY, float viewW, float viewH, float dx, float dy)
    {
        const float visible = 0.5f;
        float length = (float)Math.Sqrt(dx * dx + dy * dy);
        if (panelW <= 0 || panelH <= 0 || viewW <= 0 || viewH <= 0 || length <= 0f)
            return (0f, 0f);
        if (Fraction(0f, 0f) < visible)
            return (0f, 0f); // Don't make an already-clipped user layout worse.

        // Furthest distance along the offset direction that keeps enough of the panel visible.
        float ux = dx / length, uy = dy / length;
        float low = 0f, high = viewW + viewH + panelW + panelH;
        for (int i = 0; i < 24; i++)
        {
            float mid = (low + high) * 0.5f;
            if (Fraction(ux * mid, uy * mid) >= visible)
                low = mid;
            else
                high = mid;
        }
        if (low <= 0f)
            return (0f, 0f);

        float soft = low * (float)Math.Tanh(length / low);
        return (ux * soft, uy * soft);

        float Fraction(float x, float y)
        {
            float width = Math.Max(0f, Math.Min(panelX + x + panelW, viewX + viewW) - Math.Max(panelX + x, viewX));
            float height = Math.Max(0f, Math.Min(panelY + y + panelH, viewY + viewH) - Math.Max(panelY + y, viewY));
            return width / panelW * (height / panelH);
        }
    }
}
