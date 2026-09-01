using System.Windows.Input;

namespace Forge3D.Editor.Input;

public sealed class CameraInputState
{
    public bool IsEnabled { get; set; }

    public bool Forward { get; private set; }

    public bool Backward { get; private set; }

    public bool Left { get; private set; }

    public bool Right { get; private set; }

    public bool Up { get; private set; }

    public bool Down { get; private set; }

    public bool HasMovement => Forward || Backward || Left || Right || Up || Down;

    public bool SetKey(Key key, bool isPressed)
    {
        switch (key)
        {
            case Key.W:
                Forward = isPressed;
                return true;
            case Key.S:
                Backward = isPressed;
                return true;
            case Key.A:
                Left = isPressed;
                return true;
            case Key.D:
                Right = isPressed;
                return true;
            case Key.Q:
                Up = isPressed;
                return true;
            case Key.E:
                Down = isPressed;
                return true;
            default:
                return false;
        }
    }

    public void Clear()
    {
        IsEnabled = false;
        Forward = false;
        Backward = false;
        Left = false;
        Right = false;
        Up = false;
        Down = false;
    }
}
