/// <summary>
/// The top-level UI screens. The game boots straight into <see cref="Game"/>; the Main menu was
/// removed in SCRUM-33. The numbers are stored in the scene: never reuse 0 (the old MainMenu).
/// </summary>
public enum UIState
{
    Game = 1,
    Settings = 2,
}
