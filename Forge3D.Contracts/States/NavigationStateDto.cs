namespace Forge3D.Contracts.States;

public sealed record NavigationStateDto(
    IReadOnlyList<PathPointDto> Path,
    float PathLength,
    int ExpandedNodes,
    double PlanningMilliseconds,
    string Message,
    bool Succeeded);

public sealed record PathPointDto(float X, float Z, float HeadingDegrees = 0.0f);
