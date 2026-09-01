using System;
using UnityEngine;

/// <summary>
/// How the PlayerController is driven.
/// - Rewired: normal gameplay input
/// - Scripted: pseudo-player / tutorial bot drives the controller via injected frames
/// - Disabled: ignores all input
/// </summary>
public enum PlayerControlMode
{
    Rewired = 0,
    Scripted = 1,
    Disabled = 2,
}

/// <summary>
/// A single-frame snapshot of player input.
/// This is intentionally small and "arcade": move + sword + shield.
/// </summary>
[Serializable]
public struct PlayerInputFrame
{
    // Stick (x = MoveH, y = MoveV). Maps to world XZ in your controller.
    public Vector2 moveInput;

    // Sword
    public bool attackDown;
    public bool attackHeld;
    public bool attackUp;

    // Shield
    public bool shieldDown;
    public bool shieldHeld;

    // Optional: aim override (useful for tutorial demonstrations while standing still)
    public bool hasAimDirWS;
    public Vector3 aimDirWS;

    public static PlayerInputFrame Neutral => new PlayerInputFrame
    {
        moveInput = Vector2.zero,
        attackDown = false,
        attackHeld = false,
        attackUp = false,
        shieldDown = false,
        shieldHeld = false,
        hasAimDirWS = false,
        aimDirWS = Vector3.right,
    };

    // --- Aliases (helps if you used different names in PlayerController edits) ---
    public Vector2 move { get => moveInput; set => moveInput = value; }
    public bool swordDown { get => attackDown; set => attackDown = value; }
    public bool swordHeld { get => attackHeld; set => attackHeld = value; }
    public bool swordUp   { get => attackUp;   set => attackUp = value; }
    public bool shieldOn  { get => shieldHeld; set => shieldHeld = value; }
}
