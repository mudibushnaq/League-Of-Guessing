public interface IModeSelectionService
{
    string SelectedModeId { get; }
    void Set(string modeId);
}