/// <summary>A placed prefab supplies its behavior; placement does not switch on item indices.</summary>
public interface IPlacedItem
{
    /// <summary>Associates an authority-created item with its reserved cell before backend spawning.</summary>
    void InitializeCell(string cellId);
}
