using TMPro;
using UnityEngine;

/// <summary>Connects the host-room UI to the currently composed session service.</summary>
public class HostGame : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    private string _roomName;

    public void SetRoomName(string name)
    {
        _roomName = name;
    }

    public void CreateRoom()
    {
        var manager = SessionServices.Current;
        if (manager == null || manager.IsNetworkActive) return;

        string roomName = string.IsNullOrEmpty(_roomName) ? Nightmare.Core.Ports.SessionDefaults.RoomName : _roomName;
        _panel.SetActive(true);
        _panel.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "Creating room " + roomName + "...";
        if (!manager.Host(roomName, Nightmare.Core.Ports.SessionDefaults.Port))
            _panel.SetActive(false);
    }
}
