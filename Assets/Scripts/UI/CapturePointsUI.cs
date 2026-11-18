using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Image = UnityEngine.UI.Image;

public class CapturePointsUI : MonoBehaviour
{
    private readonly List<TextMeshProUGUI> captures = new List<TextMeshProUGUI>();
    private readonly List<Image> backgrounds = new List<Image>();
    private readonly List<Sprite> normalSprites = new List<Sprite>();
    private List<CaptureArea> areas = new List<CaptureArea>();
    private bool isSet;
    private AudioSource source;
    public Sprite _red;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        foreach (Transform counter in transform)
        {
            var background = counter.GetComponent<Image>();
            backgrounds.Add(background);
            normalSprites.Add(background.sprite);
            captures.Add(counter.GetChild(0).GetComponent<TextMeshProUGUI>());
        }
    }

    void Update()
    {
        if (!isSet) return;
        var game = GameManager.Instance;
        bool fighting = game != null && (game.CurrentMachState == GameManager.MatchState.Room1Fight
            || game.CurrentMachState == GameManager.MatchState.Room2Fight
            || game.CurrentMachState == GameManager.MatchState.Room3Fight);
        bool alarm = false;
        for (int i = 0; i < areas.Count; i++)
        {
            bool critical = fighting && areas[i] != null && areas[i]._progress < 20f;
            backgrounds[i].sprite = critical ? _red : normalSprites[i];
            captures[i].text = areas[i] != null ? ((int)areas[i]._progress).ToString() : "";
            alarm |= critical;
        }
        if (source == null) return;
        if (alarm && !source.isPlaying) source.Play();
        else if (!alarm && source.isPlaying) source.Stop();
    }

    public void setRoom()
    {
        // The HUD and AudioSource persist across rooms, so both warning states must reset.
        if (source != null) source.Stop();
        areas = GameManager.Instance.CurrentRoom.CaptureAreas;
        for (int i = 0; i < captures.Count; i++)
        {
            backgrounds[i].sprite = normalSprites[i];
            backgrounds[i].gameObject.SetActive(i < areas.Count);
        }
        isSet = true;
    }

    void OnDisable()
    {
        if (source != null) source.Stop();
    }
}
