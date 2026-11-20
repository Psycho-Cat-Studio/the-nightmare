using UnityEngine;

/// <summary>References authored on the prefab, independent of bone names and child indices.</summary>
public sealed class PlayerRigBindings : MonoBehaviour
{
    public Animator[] Models;
    public Transform RemoteWeaponHand;
    public GameObject GraphicsRoot;
    public GameObject[] ActionRoots;
    public GameObject BuildingRoot;
    public GameObject RemoteArrow;
    public Transform WeaponHolder;
    public Camera WeaponCamera;
    public GameObject[] ColorMarkers;
    public Animator ActiveAnimator
    {
        get
        {
            foreach (var animator in Models) if (animator != null && animator.gameObject.activeSelf) return animator;
            return Models.Length > 0 ? Models[0] : null;
        }
    }
}
