using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Aurora_LINK.Configuration;

/// <summary>
/// Service de gestion de projet Aurora (.aurora).
/// Gère le chargement, l'enregistrement, le suivi de modifications et l'auto-save.
/// </summary>
public sealed class AuroraProjectService
{
    private static readonly Lazy<AuroraProjectService> _instance = new(() => new AuroraProjectService());
    public static AuroraProjectService Instance => _instance.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    public const string FileExtension = ".ora";
    public const string FileFilter = "Projet Aurora|*.ora";
    public const string FloraExtension = ".flora";
    public const string FloraFilter = "Firmware Aurora|*.flora";

    private AuroraProject _project = new();
    private string? _filePath;
    private bool _isDirty;

    /// <summary>Projet en cours d'édition.</summary>
    public AuroraProject Project => _project;

    /// <summary>Chemin du fichier ouvert, ou <c>null</c> si nouveau projet.</summary>
    public string? FilePath => _filePath;

    /// <summary>Indique si le projet a des modifications non enregistrées.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty == value) return;
            _isDirty = value;
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Nom d'affichage du projet (nom du fichier ou "Nouveau projet").</summary>
    public string DisplayName
    {
        get
        {
            if (_filePath is not null)
                return Path.GetFileNameWithoutExtension(_filePath);
            return _project.Name;
        }
    }

    /// <summary>Déclenché quand l'état dirty change.</summary>
    public event EventHandler? DirtyChanged;

    /// <summary>Déclenché quand un projet est chargé ou créé.</summary>
    public event EventHandler? ProjectChanged;

    private AuroraProjectService() { }

    /// <summary>
    /// Crée un nouveau projet vide.
    /// </summary>
    public void NewProject()
    {
        _project = new AuroraProject();
        _filePath = null;
        IsDirty = false;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Charge la configuration Aurora courante dans le projet.
    /// </summary>
    public void LoadConfiguration(AuroraConfiguration config)
    {
        _project.Config = AuroraProjectMapper.ToProjectConfig(config);
        MarkDirty();
    }

    /// <summary>
    /// Exporte la configuration du projet vers un <see cref="AuroraConfiguration"/>.
    /// </summary>
    public AuroraConfiguration GetConfiguration()
    {
        return AuroraProjectMapper.ToConfiguration(_project.Config);
    }

    /// <summary>
    /// Marque le projet comme modifié (déclenche l'auto-save si un chemin est défini).
    /// </summary>
    public void MarkDirty()
    {
        _project.ModifiedUtc = DateTime.UtcNow;
        IsDirty = true;
    }

    /// <summary>
    /// Enregistre le projet dans le fichier courant. Si aucun fichier n'est défini, ne fait rien.
    /// Retourne <c>true</c> si l'enregistrement a réussi.
    /// </summary>
    public async Task<bool> SaveAsync()
    {
        if (_filePath is null)
            return false;

        return await SaveToFileAsync(_filePath);
    }

    /// <summary>
    /// Enregistre le projet dans un fichier spécifié.
    /// </summary>
    public async Task<bool> SaveAsAsync(string path)
    {
        if (await SaveToFileAsync(path))
        {
            _filePath = path;
            ProjectChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Ouvre un projet depuis un fichier .aurora.
    /// </summary>
    public async Task<bool> OpenAsync(string path)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path);
            var project = JsonSerializer.Deserialize<AuroraProject>(json, JsonOptions);
            if (project is null)
                return false;

            _project = project;
            _filePath = path;
            IsDirty = false;
            ProjectChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> SaveToFileAsync(string path)
    {
        try
        {
            _project.ModifiedUtc = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(_project, JsonOptions);
            await File.WriteAllTextAsync(path, json);
            IsDirty = false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ───────────────── Export / Import .flora ─────────────────

    /// <summary>
    /// Valide la configuration courante selon les règles DT-AURORA-MEM-001.
    /// </summary>
    public AuroraValidationResult ValidateConfiguration()
    {
        var config = GetConfiguration();
        return AuroraConfigValidator.Validate(config);
    }

    /// <summary>
    /// Exporte la configuration courante en fichier binaire .flora (page Flash 2048 bytes).
    /// Retourne <c>null</c> en cas de succès, ou un message d'erreur.
    /// </summary>
    public async Task<string?> ExportFloraAsync(string path)
    {
        try
        {
            var config = GetConfiguration();
            var validation = AuroraConfigValidator.Validate(config);
            if (!validation.IsValid)
                return validation.Summary;

            byte[] page = AuroraConfigSerializer.Serialize(config);

            // Vérification de relecture (DT §12.1 étape 7)
            var readBack = AuroraConfigSerializer.Deserialize(page);
            if (readBack is null)
                return "Erreur de vérification : le fichier généré n'est pas lisible (CRC ou structure invalide).";

            await File.WriteAllBytesAsync(path, page);
            return null;
        }
        catch (Exception ex)
        {
            return $"Erreur d'écriture : {ex.Message}";
        }
    }

    /// <summary>
    /// Importe un fichier binaire .flora et charge la configuration dans le projet.
    /// Retourne <c>null</c> en cas de succès, ou un message d'erreur.
    /// </summary>
    public async Task<string?> ImportFloraAsync(string path)
    {
        try
        {
            var data = await File.ReadAllBytesAsync(path);

            if (data.Length != AuroraConfiguration.FlashPageSize)
                return $"Taille du fichier invalide ({data.Length} bytes), attendu {AuroraConfiguration.FlashPageSize} bytes.";

            var config = AuroraConfigSerializer.Deserialize(data);
            if (config is null)
                return "Fichier .flora invalide (magic, version ou CRC32 incorrect).";

            _project.Config = AuroraProjectMapper.ToProjectConfig(config);
            _project.ModifiedUtc = DateTime.UtcNow;
            IsDirty = true;
            ProjectChanged?.Invoke(this, EventArgs.Empty);
            return null;
        }
        catch (Exception ex)
        {
            return $"Erreur de lecture : {ex.Message}";
        }
    }
}
