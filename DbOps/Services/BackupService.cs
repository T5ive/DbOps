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

    public async Task ExportBakAsync(string dbName)
    {
        var fileName = $"{dbName}_{DateTime.Now:yyyy-MM-dd}.bak";
        var filePath = Path.Combine(_backupDir, fileName);

        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };

        Console.WriteLine($"\nExporting {dbName} to {Path.GetFullPath(filePath)} as .bak...");

        var query = $"BACKUP DATABASE [{dbName}] TO DISK = @Path WITH INIT, FORMAT";

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Path", Path.GetFullPath(filePath));

        await command.ExecuteNonQueryAsync();

        Console.WriteLine($"Successfully backed up {dbName} to .bak");
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
