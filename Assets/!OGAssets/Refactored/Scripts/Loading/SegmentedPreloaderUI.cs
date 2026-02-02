using System;
using Cysharp.Threading.Tasks;
using OG.Data;
using OG.Initialization;
using OG.Installers.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

[SingletonMonoBehaviour(
    loadPriority: Priority.CRITICAL,
    createNewInstance: false,
    gameObjectName: nameof(SegmentedPreloaderUI),
    context: AppContextType.PreloaderScene)]
public sealed class SegmentedPreloaderUI : MonoBehaviour, IProjectInitializable
{
    int IProjectInitializable.Order => 0;
    
    [Header("UI")]
    [SerializeField] private Image   fillBar;         // Image -> Filled (Horizontal)
    [SerializeField] private TMP_Text progressText;   // one label for title + details
    [SerializeField] private bool    twoLine = false; // if true, title on line 1, details on line 2
    [SerializeField] private string  separator = " — "; // used when twoLine=false
    
    const int PhaseCount = 3; // Download, LoadLevels, LoadScene
    float Segment => 1f / PhaseCount;

    LoadPhase _phase = LoadPhase.None;
    float _phasePct;              // [0..1] within current phase
    string _title = string.Empty; // current phase title
    string _detail = string.Empty;// current phase detail
    
    Action<LoadPhase> _onPhaseChanged;
    Action<long,long,float> _onDownload;
    Action<int,int,float> _onAssetsLoad;
    Action<float> _onGeneric;
    Action<string> _onSceneStarted;
    Action<string,float> _onSceneProgress;
    Action<string> _onSceneCompleted;
    Action<string,Exception> _onSceneFailed;
    
    [Inject] private ILevelsProviderService _levelsProviderService;
    [Inject] private ISceneLoader _sceneLoader;
    
    void OnDestroy() { Unbind(); }
    
    UniTask IProjectInitializable.Initialize()
    {
        Bind();
        return UniTask.CompletedTask;
    }

    void Bind()
    {
        if (_levelsProviderService == null || _sceneLoader == null) return;

        _onPhaseChanged  = OnPhaseChanged;
        _onDownload      = OnDownloadProgress;
        _onAssetsLoad    = OnAssetsLoadProgress;
        _onGeneric       = OnGenericProgress;
        _onSceneStarted  = OnSceneStarted;
        _onSceneProgress = OnSceneProgress;
        _onSceneCompleted= OnSceneCompleted;
        _onSceneFailed   = OnSceneFailed;

        _levelsProviderService.OnPhaseChanged       += _onPhaseChanged;
        _levelsProviderService.OnDownloadProgress   += _onDownload;
        _levelsProviderService.OnAssetsLoadProgress += _onAssetsLoad;
        _levelsProviderService.OnProgress           += _onGeneric;

        _sceneLoader.OnSceneLoadStarted      += _onSceneStarted;
        _sceneLoader.OnSceneLoadProgress     += _onSceneProgress;
        _sceneLoader.OnSceneLoadCompleted    += _onSceneCompleted;
        _sceneLoader.OnSceneLoadFailed       += _onSceneFailed;
    }

    void Unbind()
    {
        if (_levelsProviderService != null)
        {
            if (_onPhaseChanged  != null) _levelsProviderService.OnPhaseChanged       -= _onPhaseChanged;
            if (_onDownload      != null) _levelsProviderService.OnDownloadProgress   -= _onDownload;
            if (_onAssetsLoad    != null) _levelsProviderService.OnAssetsLoadProgress -= _onAssetsLoad;
            if (_onGeneric       != null) _levelsProviderService.OnProgress           -= _onGeneric;
        }

        if (_sceneLoader == null) return;
        if (_onSceneStarted   != null) _sceneLoader.OnSceneLoadStarted   -= _onSceneStarted;
        if (_onSceneProgress  != null) _sceneLoader.OnSceneLoadProgress  -= _onSceneProgress;
        if (_onSceneCompleted != null) _sceneLoader.OnSceneLoadCompleted -= _onSceneCompleted;
        if (_onSceneFailed    != null) _sceneLoader.OnSceneLoadFailed    -= _onSceneFailed;
    }

    // ---------- Phase changes ----------
    void OnPhaseChanged(LoadPhase p)
    {
        _phase = p;
        _phasePct = 0f;

        switch (p)
        {
            case LoadPhase.CheckingAssets:                // 👈 NEW
                SetTitle("Checking Assets");              // status text will fill the detail
                SetDetail("…");
                break;
            case LoadPhase.DownloadAssets:
                SetTitle("Downloading Assets");
                SetDetail("…");
                break;
            case LoadPhase.LoadLevels:
                SetTitle("Loading Levels");
                SetDetail("0 / ?");
                break;
        }
        UpdateFill();
        RenderText();
    }

    // ---------- Per-phase progress ----------
    void OnDownloadProgress(long downloaded, long total, float pct)
    {
        if (_phase != LoadPhase.DownloadAssets) { return; }
        _phasePct = Mathf.Clamp01(pct);
        SetDetail($"{ToMB(downloaded)} / {ToMB(total)}");
        UpdateFill(); 
        RenderText();
    }

    void OnAssetsLoadProgress(int loaded, int total, float pct)
    {
        if (_phase != LoadPhase.LoadLevels) return;
        _phasePct = Mathf.Clamp01(pct);
        SetDetail($"{loaded} / {total}");
        UpdateFill(); 
        RenderText();
    }
    
    // -------- SceneLoader phase (3) --------
    void OnSceneStarted(string sceneAddress)
    {
        _phase = LoadPhase.LoadScene;
        _phasePct = 0f;
        SetTitle("Loading Game");
        SetDetail("0%");
        UpdateFill(); 
        RenderText();
    }

    void OnSceneProgress(string sceneAddress, float pct)
    {
        if (_phase != LoadPhase.LoadScene) return;
        _phasePct = Mathf.Clamp01(pct);
        SetDetail($"{Mathf.RoundToInt(_phasePct * 100f)}%");
        UpdateFill(); 
        RenderText();
    }

    void OnSceneCompleted(string sceneAddress)
    {
        _phasePct = 1f;
        SetDetail("100%");
        UpdateFill(); 
        RenderText();
    }

    void OnSceneFailed(string sceneAddress, System.Exception ex)
    {
        SetDetail("Failed");
        RenderText();
    }
    
    void OnGenericProgress(float pct)
    {
        // Used for minor sub-steps (discovery, etc.)
        _phasePct = Mathf.Clamp01(pct);
        UpdateFill();
        // Do not overwrite detail text here; phases provide better text
    }

    // ---------- UI helpers ----------
    void UpdateFill()
    {
        float baseOffset = _phase switch
        {
            LoadPhase.CheckingAssets => 0f,
            LoadPhase.DownloadAssets => 0f,
            LoadPhase.LoadLevels     => Segment * 1f,
            LoadPhase.LoadScene      => Segment * 2f,
            _                        => 0f
        };
        float total = Mathf.Clamp01(baseOffset + (_phasePct * Segment));
        if (fillBar) fillBar.fillAmount = total;
    }

    void SetTitle(string s)  => _title  = s ?? "";
    void SetDetail(string s) => _detail = s ?? "";

    void RenderText()
    {
        if (!progressText) return;

        if (twoLine)
        {
            // Bold title on first line, detail on second line
            if (!string.IsNullOrEmpty(_title) && !string.IsNullOrEmpty(_detail))
                progressText.text = $"<b>{_title}</b>\n{_detail}";
            else if (!string.IsNullOrEmpty(_title))
                progressText.text = $"<b>{_title}</b>";
            else
                progressText.text = _detail ?? "";
        }
        else
        {
            // Single line with separator
            if (!string.IsNullOrEmpty(_title) && !string.IsNullOrEmpty(_detail))
                progressText.text = $"<b>{_title}</b>{separator}{_detail}";
            else if (!string.IsNullOrEmpty(_title))
                progressText.text = $"<b>{_title}</b>";
            else
                progressText.text = _detail ?? "";
        }
    }

    static string ToMB(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        double mb = bytes / 1048576d;
        return mb >= 100 ? $"{mb:0} MB" : $"{mb:0.#} MB";
    }
}
