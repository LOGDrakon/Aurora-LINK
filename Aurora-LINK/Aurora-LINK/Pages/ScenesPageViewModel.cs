using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aurora_LINK.Configuration;

namespace Aurora_LINK.Pages;

/// <summary>
/// ViewModel d'une scène éditable (DT-AURORA-MEM-001 §7).
/// Gère la visibilité conditionnelle des champs selon le mode LED (§7.4).
/// Couleur RGB sur les deux LEDs synchrones.
/// </summary>
public sealed class SceneViewModel : INotifyPropertyChanged
{
    private byte _sceneId;
    private AuroraSceneFlags _flags;
    private AuroraLedMode _mode = AuroraLedMode.Off;
    private byte _red;
    private byte _green;
    private byte _blue;
    private ushort _tOnMs;
    private ushort _tOffMs;
    private byte _repeat;
    private byte _fadeTime;

    public byte SceneId
    {
        get => _sceneId;
        set { if (SetField(ref _sceneId, value)) OnPropertyChanged(nameof(DisplayName)); }
    }

    public string DisplayName => $"Scène {_sceneId}";

    public bool AutoStart
    {
        get => _flags.HasFlag(AuroraSceneFlags.AutoStart);
        set
        {
            var next = value
                ? _flags | AuroraSceneFlags.AutoStart
                : _flags & ~AuroraSceneFlags.AutoStart;
            if (SetField(ref _flags, next, nameof(Flags)))
                OnPropertyChanged();
        }
    }

    public AuroraSceneFlags Flags
    {
        get => _flags;
        set
        {
            if (SetField(ref _flags, value))
                OnPropertyChanged(nameof(AutoStart));
        }
    }

    public AuroraLedMode Mode
    {
        get => _mode;
        set
        {
            if (SetField(ref _mode, value))
            {
                OnPropertyChanged(nameof(ModeDisplayName));
                OnPropertyChanged(nameof(ShowColor));
                OnPropertyChanged(nameof(ShowTOn));
                OnPropertyChanged(nameof(ShowTOff));
                OnPropertyChanged(nameof(ShowRepeat));
                OnPropertyChanged(nameof(ShowFadeTime));
                OnPropertyChanged(nameof(TOnLabel));
                OnPropertyChanged(nameof(TOffLabel));
                OnPropertyChanged(nameof(RepeatLabel));
                OnPropertyChanged(nameof(FadeTimeLabel));
                OnPropertyChanged(nameof(SummaryText));
            }
        }
    }

    public string ModeDisplayName => AuroraDisplayNames.GetLedModeName(_mode);

    public byte Red
    {
        get => _red;
        set { if (SetField(ref _red, value)) { OnPropertyChanged(nameof(SummaryText)); OnPropertyChanged(nameof(PreviewColor)); } }
    }

    public byte Green
    {
        get => _green;
        set { if (SetField(ref _green, value)) { OnPropertyChanged(nameof(SummaryText)); OnPropertyChanged(nameof(PreviewColor)); } }
    }

    public byte Blue
    {
        get => _blue;
        set { if (SetField(ref _blue, value)) { OnPropertyChanged(nameof(SummaryText)); OnPropertyChanged(nameof(PreviewColor)); } }
    }

    public Windows.UI.Color PreviewColor => Windows.UI.Color.FromArgb(255, _red, _green, _blue);

    public ushort TOnMs
    {
        get => _tOnMs;
        set { if (SetField(ref _tOnMs, value)) OnPropertyChanged(nameof(SummaryText)); }
    }

    public ushort TOffMs
    {
        get => _tOffMs;
        set { if (SetField(ref _tOffMs, value)) OnPropertyChanged(nameof(SummaryText)); }
    }

    public byte Repeat
    {
        get => _repeat;
        set { if (SetField(ref _repeat, value)) OnPropertyChanged(nameof(SummaryText)); }
    }

    public byte FadeTime
    {
        get => _fadeTime;
        set { if (SetField(ref _fadeTime, value)) OnPropertyChanged(nameof(SummaryText)); }
    }

    // ── Visibilité conditionnelle (DT §7.4) ──────────────────────────

    public bool ShowColor => _mode is not AuroraLedMode.Off;

    public bool ShowTOn => _mode is AuroraLedMode.Blink or AuroraLedMode.Fade
                                 or AuroraLedMode.Burst or AuroraLedMode.Double;

    public bool ShowTOff => _mode is AuroraLedMode.Blink or AuroraLedMode.Fade
                                  or AuroraLedMode.Burst or AuroraLedMode.Double;

    public bool ShowRepeat => _mode is AuroraLedMode.Blink or AuroraLedMode.Fade
                                    or AuroraLedMode.Burst;

    public bool ShowFadeTime => _mode is AuroraLedMode.Static or AuroraLedMode.Blink
                                      or AuroraLedMode.Double;

    // ── Labels contextuels (DT §7.4) ─────────────────────────────────

    public string TOnLabel => _mode switch
    {
        AuroraLedMode.Blink  => "Durée ON (ms)",
        AuroraLedMode.Fade   => "Durée montée (ms)",
        AuroraLedMode.Burst  => "Durée flash (ms)",
        AuroraLedMode.Double => "Durée flash (ms)",
        _ => "Durée ON (ms)",
    };

    public string TOffLabel => _mode switch
    {
        AuroraLedMode.Blink  => "Durée OFF (ms)",
        AuroraLedMode.Fade   => "Durée descente (ms)",
        AuroraLedMode.Burst  => "Pause entre bursts (ms)",
        AuroraLedMode.Double => "Pause inter-flash (ms)",
        _ => "Durée OFF (ms)",
    };

    public string RepeatLabel => _mode switch
    {
        AuroraLedMode.Burst => "Flashs par burst",
        _ => "Répétitions (0 = infini)",
    };

    public string FadeTimeLabel => _mode switch
    {
        AuroraLedMode.Static => "Rampe allumage (×10 ms)",
        AuroraLedMode.Blink  => "Transition (×10 ms)",
        AuroraLedMode.Double => "Pause post-double (×10 ms)",
        _ => "Fondu (×10 ms)",
    };

    // ── Résumé ────────────────────────────────────────────────────────

    public string SummaryText
    {
        get
        {
            var modeName = AuroraDisplayNames.GetLedModeName(_mode);
            var rgb = $"#{_red:X2}{_green:X2}{_blue:X2}";
            return _mode switch
            {
                AuroraLedMode.Off    => "LEDs éteintes",
                AuroraLedMode.Static => $"{modeName} — {rgb}",
                AuroraLedMode.Blink  => $"{modeName} — {rgb}, {_tOnMs} ms ON / {_tOffMs} ms OFF",
                AuroraLedMode.Fade   => $"{modeName} — {rgb}, montée {_tOnMs} ms, descente {_tOffMs} ms",
                AuroraLedMode.Burst  => $"{modeName} — {rgb}, {_repeat} flash(s), {_tOnMs} ms, pause {_tOffMs} ms",
                AuroraLedMode.Double => $"{modeName} — {rgb}, {_tOnMs} ms, pause {_tOffMs} ms",
                _ => modeName,
            };
        }
    }

    // ── Load / Export ─────────────────────────────────────────────────

    public void LoadFrom(AuroraScene scene)
    {
        SceneId = scene.SceneId;
        Flags = scene.Flags;
        Mode = scene.State.Mode;
        Red = scene.State.Red;
        Green = scene.State.Green;
        Blue = scene.State.Blue;
        TOnMs = scene.State.TOnMs;
        TOffMs = scene.State.TOffMs;
        Repeat = scene.State.Repeat;
        FadeTime = scene.State.FadeTime;
    }

    public AuroraScene ToModel()
    {
        return new AuroraScene
        {
            SceneId = _sceneId,
            Flags = _flags,
            State = new AuroraLedState
            {
                Mode = _mode,
                Red = _red,
                Green = _green,
                Blue = _blue,
                TOnMs = _tOnMs,
                TOffMs = _tOffMs,
                Repeat = _repeat,
                FadeTime = _fadeTime,
            },
        };
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

/// <summary>
/// ViewModel de la page Scènes — gère jusqu'à 16 scènes (DT-AURORA-MEM-001 §7).
/// </summary>
public sealed class ScenesPageViewModel : INotifyPropertyChanged
{
    public ObservableCollection<SceneViewModel> Scenes { get; } = [];

    private SceneViewModel? _selectedScene;
    public SceneViewModel? SelectedScene
    {
        get => _selectedScene;
        set
        {
            if (_selectedScene == value) return;
            _selectedScene = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => _selectedScene is not null;
    public bool CanAdd => Scenes.Count < AuroraConfiguration.MaxScenes;

    public Array LedModeValues => Enum.GetValues(typeof(AuroraLedMode));

    public void AddScene()
    {
        if (!CanAdd) return;

        var vm = new SceneViewModel { SceneId = (byte)Scenes.Count };
        vm.PropertyChanged += OnScenePropertyChanged;
        Scenes.Add(vm);
        SelectedScene = vm;
        OnPropertyChanged(nameof(CanAdd));
    }

    public void RemoveSelected()
    {
        if (_selectedScene is null) return;

        _selectedScene.PropertyChanged -= OnScenePropertyChanged;
        int idx = Scenes.IndexOf(_selectedScene);
        Scenes.Remove(_selectedScene);

        // Renuméroter les IDs séquentiellement
        for (int i = 0; i < Scenes.Count; i++)
            Scenes[i].SceneId = (byte)i;

        SelectedScene = idx < Scenes.Count ? Scenes[idx]
                      : Scenes.Count > 0   ? Scenes[^1]
                      : null;
        OnPropertyChanged(nameof(CanAdd));
    }

    public void LoadFrom(AuroraConfiguration config)
    {
        foreach (var existing in Scenes)
            existing.PropertyChanged -= OnScenePropertyChanged;

        Scenes.Clear();
        foreach (var scene in config.Scenes)
        {
            var vm = new SceneViewModel();
            vm.LoadFrom(scene);
            vm.PropertyChanged += OnScenePropertyChanged;
            Scenes.Add(vm);
        }
        SelectedScene = Scenes.Count > 0 ? Scenes[0] : null;
        OnPropertyChanged(nameof(CanAdd));
    }

    private bool _updatingAutoStart;

    private void OnScenePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updatingAutoStart) return;
        if (e.PropertyName != nameof(SceneViewModel.AutoStart)) return;
        if (sender is not SceneViewModel changed || !changed.AutoStart) return;

        _updatingAutoStart = true;
        try
        {
            foreach (var scene in Scenes)
            {
                if (scene != changed && scene.AutoStart)
                    scene.AutoStart = false;
            }
        }
        finally
        {
            _updatingAutoStart = false;
        }
    }

    public void SaveTo(AuroraConfiguration config)
    {
        config.Scenes.Clear();
        foreach (var vm in Scenes)
            config.Scenes.Add(vm.ToModel());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
