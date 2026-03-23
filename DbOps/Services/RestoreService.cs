using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;

namespace DbOps.Services;

public class RestoreService
{
    private readonly string _connectionString;
    private readonly string _restoreDir;

    public RestoreService(string connectionString, string restoreDir = "./restore")
    {
        _connectionString = connectionString;
        _restoreDir = restoreDir;
        if (!Directory.Exists(_restoreDir))
        {
            Directory.CreateDirectory(_restoreDir);
        }
    }

    public async Task RestoreBakAsync(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string dbName = System.Text.RegularExpressions.Regex.Replace(fileName, @"_\d{4}-\d{2}-\d{2}$", "");
        Console.WriteLine($"\nRestoring {dbName} from {Path.GetFullPath(filePath)} (.bak)...");

        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };

        using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        // 1. Get Logical File Names
        var logicalFiles = new List<(string LogicalName, string Type)>();
        string fileListQuery = "RESTORE FILELISTONLY FROM DISK = @path";
        using (var listCommand = new SqlCommand(fileListQuery, connection))
        {
            listCommand.Parameters.AddWithValue("@path", Path.GetFullPath(filePath));
            using var reader = await listCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                logicalFiles.Add((reader.GetString(0), reader.GetString(2))); // Type is 'D' (Data) or 'L' (Log)
            }
        }

        // 2. Get default data and log paths from SQL Server to place the restored files
        string dataPath = "";
        string logPath = "";
        using (var pathCmd = new SqlCommand("SELECT SERVERPROPERTY('InstanceDefaultDataPath'), SERVERPROPERTY('InstanceDefaultLogPath')", connection))
        {
            using var pathReader = await pathCmd.ExecuteReaderAsync();
            if (await pathReader.ReadAsync())
            {
                dataPath = pathReader.GetString(0);
                logPath = pathReader.GetString(1);
            }
        }

        // 3. Prepare MOVE clauses to logically remap
        var moveClauses = new List<string>();
        foreach (var file in logicalFiles)
        {
            string newPath;
            if (file.Type == "D")
            {
                newPath = Path.Combine(dataPath, $"{dbName}_{file.LogicalName}.mdf");
            }
            else
            {
                newPath = Path.Combine(logPath, $"{dbName}_{file.LogicalName}.ldf");
            }
            moveClauses.Add($"MOVE '{file.LogicalName}' TO '{newPath}'");
        }
        string moveText = string.Join(", ", moveClauses);

        // 4. Force close existing connections
        using (var killCmd = new SqlCommand($"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE", connection))
        {
            try { await killCmd.ExecuteNonQueryAsync(); } catch { /* Ignore if db doesn't exist */ }
        }

        // 5. Restore Database
        string restoreQuery = $"RESTORE DATABASE [{dbName}] FROM DISK = @path WITH REPLACE, {moveText}";
        using (var restoreCmd = new SqlCommand(restoreQuery, connection))
        {
            restoreCmd.Parameters.AddWithValue("@path", Path.GetFullPath(filePath));
            // Setting command timeout to a larger value for DB restore
            restoreCmd.CommandTimeout = 300;
            await restoreCmd.ExecuteNonQueryAsync();
        }

        // 6. Set Multi-User
        using (var multiCmd = new SqlCommand($"ALTER DATABASE [{dbName}] SET MULTI_USER", connection))
        {
            try { await multiCmd.ExecuteNonQueryAsync(); } catch { }
        }

        Console.WriteLine($"Successfully restored {dbName} from .bak");
    }

    public async Task RestoreBacpacAsync(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string dbName = System.Text.RegularExpressions.Regex.Replace(fileName, @"_\d{4}-\d{2}-\d{2}$", "");
        Console.WriteLine($"\nRestoring {dbName} from {Path.GetFullPath(filePath)} (.bacpac)...");

        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };

        // First Drop Database if it exists
        using (var connection = new SqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            using (var killCmd = new SqlCommand($@"
                IF DB_ID('{dbName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{dbName}];
                END", connection))
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
