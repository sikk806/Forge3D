using System.Globalization;

namespace Forge3D.Core.Data.Time;

public sealed class TimestampConverter
{
    public bool TryConvert(string value, int frameIndex, double frameRate, out double simulationTime)
    {
        simulationTime = 0.0;

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
        {
            simulationTime = numeric > 10_000_000_000 ? numeric / 1000.0 : numeric;
            return true;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            simulationTime = timestamp.ToUnixTimeMilliseconds() / 1000.0;
            return true;
        }

        if (frameRate > 0.0)
        {
            simulationTime = frameIndex / frameRate;
            return true;
        }

        return false;
    }
}
