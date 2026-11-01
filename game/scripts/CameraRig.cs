using Godot;

namespace Ashfall.Game;

/// <summary>WASD or arrow keys to pan, mouse wheel to zoom, middle mouse to drag.</summary>
public partial class CameraRig : Camera2D
{
    [Export] public float PanSpeed = 900f;
    [Export] public float MinZoom = 0.3f;
    [Export] public float MaxZoom = 2.5f;

    private bool _dragging;

    public override void _Ready()
    {
        Zoom = new Vector2(1.1f, 1.1f);
        PositionSmoothingEnabled = true;
        PositionSmoothingSpeed = 10f;
    }

    public override void _Process(double delta)
    {
        var dir = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) dir.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) dir.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) dir.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) dir.X += 1;
        if (dir != Vector2.Zero)
            Position += dir.Normalized() * PanSpeed * (float)delta / Zoom.X;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                SetZoomLevel(Zoom.X * 1.12f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                SetZoomLevel(Zoom.X / 1.12f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle } mb:
                _dragging = mb.Pressed;
                break;
            case InputEventMouseMotion motion when _dragging:
                Position -= motion.Relative / Zoom.X;
                ResetSmoothing();
                break;
        }
    }

    private void SetZoomLevel(float z)
    {
        z = Mathf.Clamp(z, MinZoom, MaxZoom);
        Zoom = new Vector2(z, z);
    }
}
