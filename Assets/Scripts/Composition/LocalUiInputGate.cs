using Nightmare.Core.Ports;

/// <summary>Blocks local gameplay input while welcome, pause, settings or result UI owns control.</summary>
public sealed class LocalUiInputGate : IInputGate
{
    public bool CanReadInput => ButtonsControll.screensOver && !WinLoseScreens.winLoseActive
        && !PauseGame.menuActive && !SettingsMenu.IsOpen;
}
