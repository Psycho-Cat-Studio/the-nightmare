using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays full reserve magazines from the active weapon ammunition state.</summary>
public class ClipsManager : MonoBehaviour {
    private List<Image> _clips = new List<Image>();
    public PlayerEquipment player;
    public bool playerEnabled;
    private int _fullClips = 4;

    void Start() {
        foreach (Transform child in transform) {
            _clips.Add(child.GetComponent<Image>());
        }
    }

    void Update() {
        if (playerEnabled && player != null && player.getActiveWeapon() != null) {
            int ratio = player.getActiveWeapon().FullReserveMagazines;
            ratio = Mathf.Clamp(ratio, 0, Mathf.Min(_fullClips, _clips.Count));

            for (int i = 0; i < ratio; i++) {
                _clips[i].enabled = true;
            }

            for (int i = ratio; i < _fullClips; i++) {
                _clips[i].enabled = false;
            }
        }
    }
}
