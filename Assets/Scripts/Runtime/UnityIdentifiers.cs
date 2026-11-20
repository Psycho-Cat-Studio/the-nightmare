// Serialized Unity identifiers. Keep values in sync with TagManager and Build Settings.
/// <summary>Names shared Unity tags used to classify gameplay collisions and interactions.</summary>
public static class GameTags
{
    public const string Floor = "Floor";
    public const string Enemy = "Enemy";
    public const string CaptureArea = "CaptureArea";
    public const string EnemyHead = "EnemyHead";
    public const string EnemyBody = "EnemyBody";
    public const string EnemyLegs = "EnemyLegs";
    public const string Barrel = "Barrel";
    public const string Snares = "Snares";
    public const string SlowDownWall = "SlowDownWall";
    public const string Player = "Player";
    public const string Weapon = "Weapon";
    public const string Chest = "Chest";
    public const string Reviving = "reviving";
    public const string RemovableChairs = "removableChairs";
}

/// <summary>Names shared player rendering and collision layers.</summary>
public static class GameLayers
{
    public const string UI = "UI";
    public const string PostFxWorld = "PostFX World";
    public const string LocalPlayer = "LocalPlayer";
    public const string RemotePlayer = "RemotePlayer";
    public const string UnrenderedPlayer = "UnrenderedPlayer";
}

/// <summary>Names the existing Unity input mappings without changing their configured bindings.</summary>
public static class GameInput
{
    public const string Fire = "Fire1";
    public const string Aim = "Fire2";
    public const string Scroll = "Mouse ScrollWheel";
    public const string Horizontal = "Horizontal";
    public const string Vertical = "Vertical";
    public const string MouseX = "Mouse X";
    public const string MouseY = "Mouse Y";
}

/// <summary>Build-scene names shared by menu navigation, session transitions and runtime verification.</summary>
public static class GameScenes
{
    public const string MainMenu = "MainMenu";
    public const string Lobby = "LobbyTests";
    public const string Match = "ExtremeHomeMakeover";
}

/// <summary>Resource sprite keys shared by build-grid cells and candle capture indicators.</summary>
public static class IndicatorSprites
{
    public const string Red = "red";
    public const string Green = "green";
}
