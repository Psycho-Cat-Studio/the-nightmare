using System;
using UnityEngine;

/// <summary>Points the local preparation indicator toward the current equipment chest.</summary>
public class Arrow : MonoBehaviour, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    private bool isSet = false;
    private GameObject target;
    void Update()
    {
        if (isSet && target != null)
        {
            Vector3 targetPosition = target.transform.position;
            targetPosition.y = transform.position.y;
            transform.LookAt(targetPosition);
        }
    }

    public void setTarget()
    {
        target = _game.World.CurrentChest;
        isSet = true;
    }
}
