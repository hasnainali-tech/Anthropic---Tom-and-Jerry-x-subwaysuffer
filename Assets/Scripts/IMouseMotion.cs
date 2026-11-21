// What the mouse model's animator needs to know about whatever is moving it
// (runner PlayerController or free-roam MouseController).
public interface IMouseMotion
{
    float Speed { get; }            // horizontal speed (units/s)
    bool IsMoving { get; }          // legs should cycle
    bool IsGrounded { get; }
    float VerticalVelocity { get; }
    bool IsSliding { get; }         // belly-slide pose (runner)
    bool IsCrouching { get; }       // sneak pose (free roam)
    bool IsScared { get; }          // caught pose
    bool IsWaving { get; }          // friendly idle wave (menus)
    bool IsHidden { get; }          // inside a hiding spot (model hidden)
}
