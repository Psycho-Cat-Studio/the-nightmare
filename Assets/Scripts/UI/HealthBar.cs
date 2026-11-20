using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presents the local health view and initializes the shared damage overlay.</summary>
public class HealthBar : MonoBehaviour
{
    private TextMeshProUGUI percentage;
    private RectTransform full;
    public Nightmare.Core.Ports.IHealth player;
    public bool playerEnabled;
    private float ratio;

    void Start()
    {
        percentage = transform.GetChild(2).GetComponent<TextMeshProUGUI>();
        full = transform.GetChild(1).GetComponent<RectTransform>();
        PlayerDamageOverlay.Create(this);
    }

    void Update()
    {
        if (playerEnabled)
        {
            ratio = player.CurrentHealth / player.MaximumHealth;
            if (ratio < 0) ratio = 0;
            percentage.text = ((int)(ratio * 100)).ToString();// + "%";
            full.localScale = new Vector3(ratio, full.localScale.y, full.localScale.z);
        }
    }
}
