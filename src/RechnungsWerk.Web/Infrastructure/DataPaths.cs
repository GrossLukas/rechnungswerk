using Microsoft.Data.Sqlite;

namespace RechnungsWerk.Web.Infrastructure;

/// <summary>
/// Datenordner der Anwendung (Einstellung „Data:Path“, im Container /data): SQLite-Datenbank,
/// DataProtection-Schlüssel und der Einrichtungscode liegen gemeinsam hier und gehören zusammen ins Backup.
/// </summary>
public sealed class DataPaths
{
    public const string ConfigKey = "Data:Path";

    public DataPaths(string root)
    {
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Keys);
    }

    public string Root { get; }
    public string Database => Path.Combine(Root, "rechnungswerk.db");
    public string Keys => Path.Combine(Root, "keys");
    public string SetupCode => Path.Combine(Root, "setup-code.txt");

    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = Database,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Private,
    }.ToString();

    public static DataPaths FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[ConfigKey];
        var path = string.IsNullOrWhiteSpace(configured) ? "App_Data" : configured.Trim();
        return new DataPaths(Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path));
    }
}
