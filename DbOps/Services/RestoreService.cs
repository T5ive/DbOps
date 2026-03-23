namespace DbOps.Services;

public class RestoreService
{
    private readonly string _connectionString;

    public RestoreService(string connectionString, string restoreDir = "./restore")
    {
        _connectionString = connectionString;
        if (!Directory.Exists(restoreDir))
        {
            Directory.CreateDirectory(restoreDir);
        }
    }

    public async Task RestoreBacpacAsync(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var dbName = Regex.Replace(fileName, @"_\d{4}-\d{2}-\d{2}$", "");
        Console.WriteLine($"\nRestoring {dbName} from {Path.GetFullPath(filePath)} (.bacpac)...");

        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };

        // First Drop Database if it exists
        await using (var connection = new SqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            await using (var killCmd = new SqlCommand($"""
                                                       IF DB_ID('{dbName}') IS NOT NULL
                                                       BEGIN
                                                           ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                                                           DROP DATABASE [{dbName}];
                                                       END
                                                       """, connection))
            {
                await killCmd.ExecuteNonQueryAsync();
            }
        }

        var services = new DacServices(builder.ConnectionString);
        services.Message += (s, e) => Console.WriteLine(e.Message.Message);

        await Task.Run(() =>
        {
            using var package = BacPackage.Load(Path.GetFullPath(filePath));
            services.ImportBacpac(package, dbName);
        });

        Console.WriteLine($"Successfully restored {dbName} from .bacpac");
    }
}