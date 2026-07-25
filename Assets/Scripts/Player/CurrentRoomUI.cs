using TMPro;
using UnityEngine;

public class CurrentRoomUI : MonoBehaviour
{
    [SerializeField] private TMP_Text roomText;

    private NetworkPlayerController _localController;

    private void OnEnable()
    {
        if (LocalPlayer.Controller != null)
        {
            Bind(LocalPlayer.Controller);
        }
        else
        {
            LocalPlayer.Ready += Bind;
        }
    }

    private void OnDisable()
    {
        LocalPlayer.Ready -= Bind;

        if (_localController != null)
        {
            _localController.RoomChanged -= HandleRoomChanged;
            _localController = null;
        }
    }

    private void Bind(NetworkPlayerController controller)
    {
        LocalPlayer.Ready -= Bind;

        _localController = controller;
        _localController.RoomChanged += HandleRoomChanged;
        HandleRoomChanged(_localController.CurrentRoomIndex);
    }

    private void HandleRoomChanged(int roomIndex)
    {
        if (roomText != null)
        {
            roomText.text = roomIndex >= 0 ? $"Room {roomIndex}" : "—";
        }
    }
}