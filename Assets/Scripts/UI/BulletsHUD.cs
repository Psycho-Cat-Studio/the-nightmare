using TMPro;
using UnityEngine;

/// <summary>Displays loaded rounds and fire mode for the currently held local weapon.</summary>
public class BulletsHUD : MonoBehaviour {
    private TextMeshProUGUI _text;
    public PlayerEquipment Equipment;
    public bool playerEnabled = false;

    void Start() {
        _text = gameObject.GetComponent<TextMeshProUGUI>();
    }

    void Update() {
        if (playerEnabled && Equipment != null && Equipment.getActiveWeapon() != null)
            if (Equipment.getActiveWeapon().CurrentAmmo < 1000)
                _text.text = Equipment.getActiveWeapon().CurrentMagAmmo + "/" +
                             Equipment.getActiveWeapon().ReserveAmmo;
            else
                _text.text = Equipment.getActiveWeapon().CurrentMagAmmo + "/inf.";// + "/\u221E"; //znak infinity nieobsługiwany przez czcionkę
    }
}
