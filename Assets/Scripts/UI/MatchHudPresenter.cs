using System.Collections;
using Nightmare.Core.Match;
using UnityEngine;

/// <summary>UI/audio observes match state. It never decides transitions or drives the match clock.</summary>
public sealed class MatchHudPresenter : MonoBehaviour
{
    private GameManager _game;
    private MatchSceneBindings _view;
    private Arrow _arrow;

    public void Initialize(GameManager game, MatchSceneBindings view)
    {
        _game = game;
        _view = view;
        _view.CaptureUI?.Initialize(game);
        _game.ProgressChanged += Present;
    }

    public void SetArrow(Arrow arrow)
    {
        _arrow = arrow;
        if (_arrow != null) _arrow.gameObject.SetActive(_game.Phase == MatchPhase.Prepare);
    }

    void Update()
    {
        if (_game == null || _view == null || _view.ReadyPlayers == null) return;
        bool lobby = _game.Phase == MatchPhase.Lobby;
        _view.ReadyPlayers.enabled = lobby;
        if (lobby) _view.ReadyPlayers.text = _game.ReadyPlayersCnt + "/" + _game.Players.Players.Count + " Players ready";
    }

    private void Present(MatchProgress progress)
    {
        StopAllCoroutines();
        if (_arrow != null)
        {
            _arrow.gameObject.SetActive(progress.Phase == MatchPhase.Prepare);
            if (progress.Phase == MatchPhase.Prepare) _arrow.setTarget();
        }
        if (progress.Phase == MatchPhase.Prepare)
        {
            _view.CaptureUI?.setRoom();
            if (progress.StageIndex > 0) _view.Music?.ChangeClip(true);
            StartCoroutine(Instructions(progress.StageIndex == 0));
        }
        else if (progress.Phase == MatchPhase.Defend)
        {
            _view.Music?.ChangeClip(false);
            StartCoroutine(FightInstructions());
        }
        else if (progress.IsOver)
        {
            if (progress.Phase == MatchPhase.Won) _view.Music?.ChangeClip(false);
            if (_view.Instructions != null) _view.Instructions.enabled = false;
            _view.ResultScreens?.ActivateScreen(progress.Phase == MatchPhase.Won);
        }
    }

    private IEnumerator Instructions(bool first)
    {
        if (_view.Instructions == null) yield break;
        _view.Instructions.enabled = true;
        if (!first)
        {
            _view.Instructions.text = "Follow the arrow to the next room!";
            yield return new WaitForSeconds(7f);
        }
        _view.Instructions.text = "Pick up equipment from the chests!";
        yield return new WaitForSeconds(7f);
        _view.Instructions.text = "Fortify the area!";
    }

    private IEnumerator FightInstructions()
    {
        if (_view.Instructions == null) yield break;
        _view.Instructions.text = "Defend the area!";
        yield return new WaitForSeconds(5f);
        _view.Instructions.enabled = false;
    }

    void OnDestroy()
    {
        if (_game != null) _game.ProgressChanged -= Present;
    }
}
