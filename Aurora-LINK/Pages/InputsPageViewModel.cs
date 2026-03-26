using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aurora_LINK.Configuration;

namespace Aurora_LINK.Pages;

/// <summary>
/// Représente une règle d'entrée éditable pour le binding UI (DT-AURORA-MEM-001 §8).
/// </summary>
public sealed class InputRuleViewModel : INotifyPropertyChanged
{
    private AuroraTrigger _trigger;
    private AuroraAction _action;
    private byte _target;
    private ushort _param;
    private ushort _debounceMs = 20;
    private byte _priority;
    private bool _isEnabled;

    public byte InputId { get; }
    public string DisplayName => $"I{InputId}";

    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (SetField(ref _isEnabled, value)) OnPropertyChanged(nameof(StatusText)); }
    }

    public string StatusText => _isEnabled ? "Activée" : "Désactivée";

    public AuroraTrigger Trigger
    {
        get => _trigger;
        set
        {
            if (SetField(ref _trigger, value))
            {
                OnPropertyChanged(nameof(ShowParam));
                OnPropertyChanged(nameof(ParamLabel));
            }
        }
    }

    public AuroraAction Action
    {
        get => _action;
        set
        {
            if (SetField(ref _action, value))
            {
                OnPropertyChanged(nameof(ShowTarget));
                OnPropertyChanged(nameof(ShowParam));
                OnPropertyChanged(nameof(TargetLabel));
                OnPropertyChanged(nameof(ParamLabel));
            }
        }
    }

    public byte Target
    {
        get => _target;
        set => SetField(ref _target, value);
    }

    public ushort Param
    {
        get => _param;
        set => SetField(ref _param, value);
    }

    public ushort DebounceMs
    {
        get => _debounceMs;
        set => SetField(ref _debounceMs, value);
    }

    public byte Priority
    {
        get => _priority;
        set => SetField(ref _priority, value);
    }

    /// <summary>Le champ Target est pertinent pour cette action.</summary>
    public bool ShowTarget => _action is AuroraAction.LoadScene or AuroraAction.SetBright;

    /// <summary>Le champ Param est pertinent pour le trigger ou l'action sélectionné.</summary>
    public bool ShowParam => _action is AuroraAction.DimUp or AuroraAction.DimDown
                                      or AuroraAction.SetBright
                          || _trigger is AuroraTrigger.LongPress or AuroraTrigger.DoubleTap
                                      or AuroraTrigger.Pulse;

    public string TargetLabel => _action switch
    {
        AuroraAction.LoadScene => "Scène cible (0–15)",
        AuroraAction.SetBright => "Luminosité (0–255)",
        _ => "Cible",
    };

    public string ParamLabel
    {
        get
        {
            if (_action is AuroraAction.DimUp or AuroraAction.DimDown)
                return "Pas (0–255)";
            if (_action is AuroraAction.SetBright)
                return "Luminosité (0–255)";
            return _trigger switch
            {
                AuroraTrigger.LongPress => "Seuil (ms)",
                AuroraTrigger.DoubleTap => "Fenêtre (ms)",
                AuroraTrigger.Pulse     => "Durée attendue (ms)",
                _ => "Paramètre",
            };
        }
    }

    public InputRuleViewModel(byte inputId)
    {
        InputId = inputId;
    }

    /// <summary>
    /// Initialise le view model depuis un modèle de configuration.
    /// </summary>
    public void LoadFrom(AuroraInputConfig cfg)
    {
        IsEnabled = true;
        Trigger = cfg.Trigger;
        Action = cfg.Action;
        Target = cfg.Target;
        Param = cfg.Param;
        DebounceMs = cfg.DebounceMs;
        Priority = cfg.Priority;
    }

    /// <summary>
    /// Exporte le view model en modèle de configuration.
    /// </summary>
    public AuroraInputConfig ToModel()
    {
        return new AuroraInputConfig
        {
            InputId = InputId,
            Trigger = Trigger,
            Action = Action,
            Target = Target,
            Param = Param,
            DebounceMs = DebounceMs,
            Priority = Priority,
        };
    }

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
/// ViewModel de la page Entrées — gère les 10 entrées I0–I9.
/// </summary>
public sealed class InputsPageViewModel : INotifyPropertyChanged
{
    public ObservableCollection<InputRuleViewModel> Inputs { get; } = [];

    private InputRuleViewModel? _selectedInput;
    public InputRuleViewModel? SelectedInput
    {
        get => _selectedInput;
        set
        {
            if (_selectedInput == value) return;
            _selectedInput = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => _selectedInput is not null;

    public Array TriggerValues => Enum.GetValues(typeof(AuroraTrigger));
    public Array ActionValues => Enum.GetValues(typeof(AuroraAction));

    public InputsPageViewModel()
    {
        for (byte i = 0; i < AuroraConfiguration.MaxInputs; i++)
            Inputs.Add(new InputRuleViewModel(i));
    }

    /// <summary>
    /// Charge les entrées depuis une configuration Aurora.
    /// </summary>
    public void LoadFrom(AuroraConfiguration config)
    {
        // Réinitialiser tout
        foreach (var vm in Inputs)
        {
            vm.IsEnabled = false;
            vm.Trigger = AuroraTrigger.Rising;
            vm.Action = AuroraAction.LoadScene;
            vm.Target = 0;
            vm.Param = 0;
            vm.DebounceMs = 20;
            vm.Priority = 0;
        }

        foreach (var cfg in config.Inputs)
        {
            if (cfg.InputId < Inputs.Count)
                Inputs[cfg.InputId].LoadFrom(cfg);
        }
    }

    /// <summary>
    /// Exporte les entrées activées dans une configuration Aurora.
    /// </summary>
    public void SaveTo(AuroraConfiguration config)
    {
        config.Inputs.Clear();
        foreach (var vm in Inputs)
        {
            if (vm.IsEnabled)
                config.Inputs.Add(vm.ToModel());
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
