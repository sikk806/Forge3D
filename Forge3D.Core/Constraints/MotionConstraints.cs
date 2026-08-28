namespace Forge3D.Core.Constraints;

public readonly record struct MotionConstraints(
    bool LockTranslationX = false,
    bool LockTranslationY = false,
    bool LockTranslationZ = false,
    bool LockRotationX = false,
    bool LockRotationY = false,
    bool LockRotationZ = false)
{
    public static MotionConstraints Free3D => new();

    public static MotionConstraints PlanarXY => new(
        LockTranslationZ: true,
        LockRotationX: true,
        LockRotationY: true);

    public static MotionConstraints PlanarXZ => new(
        LockTranslationY: true,
        LockRotationX: true,
        LockRotationZ: true);

    public static MotionConstraints PlanarYZ => new(
        LockTranslationX: true,
        LockRotationY: true,
        LockRotationZ: true);
}
