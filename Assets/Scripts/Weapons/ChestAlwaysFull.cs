using System.Collections.Generic;
using Nightmare.Configuration;
using UnityEngine;

/// <summary>Provides repeatable local chest grants and the authored opening presentation.</summary>
public class ChestAlwaysFull : MonoBehaviour
{
    public bool active = true;
    public bool alreadyPicked = false;
    public int grenades;
    // Retained for unmigrated assets; shipped chests author _placeableGrants.
    [HideInInspector] public int snares;
    [HideInInspector] public int teddyBears;
    [HideInInspector] public int barrels;
    [SerializeField] private PlaceableGrant[] _placeableGrants;
    [SerializeField] private Transform _lidClosed;
    [SerializeField] private Transform _lidOpen;
    [SerializeField] private GameObject _lid;
    private bool _isSet = false;

    public IEnumerable<PlaceableGrant> GetPlaceableGrants(ContentCatalog catalog)
    {
        if (_placeableGrants != null && _placeableGrants.Length > 0)
        {
            foreach (var grant in _placeableGrants) yield return grant;
            yield break;
        }
        // Preserve old experimental scenes that have not been migrated to catalog grants.
        int[] amounts = { snares, teddyBears, barrels };
        for (int i = 0; i < amounts.Length && i < catalog.Placeables.Length; i++)
            yield return new PlaceableGrant { Item = catalog.Placeables[i], Amount = amounts[i] };
    }

    private void Start()
    {
        if (_lid == null) _lid = transform.GetChild(0).GetChild(2).gameObject;
        if (_lidClosed == null) _lidClosed = transform.GetChild(3);
        if (_lidOpen == null) _lidOpen = transform.GetChild(2);
        _lid.transform.localPosition = _lidClosed.transform.localPosition;
        _lid.transform.localRotation = _lidClosed.transform.localRotation;
    }

    private void Update() {
        if (active && !_isSet)
        {
            _lid.transform.position = _lidOpen.transform.position;
            _lid.transform.rotation = _lidOpen.transform.rotation;
            _isSet = true;
        }
        alreadyPicked = false;
    }
}
