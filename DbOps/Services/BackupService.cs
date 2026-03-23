namespace DbOps.Services;

public class BackupService
{
    private readonly string _connectionString;
    private readonly string _backupDir;

    public BackupService(string connectionString, string backupDir = "./backup")
    {
        _connectionString = connectionString;
        _backupDir = backupDir;
        if (!Directory.Exists(_backupDir))
        {
            Directory.CreateDirectory(_backupDir);
        }
    }

    public async Task ExportBacpacAsync(string dbName)
    {
        var fileName = $"{dbName}_{DateTime.Now:yyyy-MM-dd}.bacpac";
        var filePath = Path.Combine(_backupDir, fileName);

        Console.WriteLine($"\nExporting {dbName} to {Path.GetFullPath(filePath)} as .bacpac...");

        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = dbName
        };

        var services = new DacServices(builder.ConnectionString);
        services.Message += (s, e) => Console.WriteLine(e.Message.Message);

        await Task.Run(() => services.ExportBacpac(Path.GetFullPath(filePath), dbName));

        Console.WriteLine($"Successfully exported {dbName} to .bacpac");
    }
}
