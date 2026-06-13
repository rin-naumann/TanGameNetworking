using Unity.Netcode;
using UnityEngine;

public class InputManager : NetworkBehaviour
{
    [Header("Movement Keybinds")]
    public KeyCode SprintKey = KeyCode.LeftShift;
    public KeyCode JumpKey = KeyCode.Space;
    public KeyCode DashKey = KeyCode.LeftControl;

    [Header("Action Keybinds")]
    public KeyCode GrappleKey = KeyCode.Q;
    public KeyCode FireKey = KeyCode.Mouse0;
    public KeyCode ScopeKey = KeyCode.Mouse1;
    public KeyCode ReloadKey = KeyCode.R;
}
