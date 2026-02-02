using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;

[SingletonClass(
    loadPriority: Priority.CRITICAL,
    context: AppContextType.Project,
    extraBindings: typeof(IModeSelectionService)
)]
public sealed class ModeSelectionService : IModeSelectionService, IProjectInitializable
{
    // default if none chosen in the menu yet
    public string SelectedModeId { get; private set; } = "default";

    public void Set(string modeId)
    {
        SelectedModeId = string.IsNullOrWhiteSpace(modeId) ? "default" : modeId;
    }
    
    int IProjectInitializable.Order => -50;
    UniTask IProjectInitializable.Initialize() => UniTask.CompletedTask;
}