using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DbOps.Services;

namespace DbOps;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== DbOps Console Application ===");

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        
        var configuration = builder.Build();

        string connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Write("Enter SQL Server Connection String: ");
            connectionString = Console.ReadLine() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("Connection string cannot be empty. Exiting.");
            return;
        }

        string backupPath = configuration.GetSection("DbOps")["BackupPath"] ?? "./backup";
        string restorePath = configuration.GetSection("DbOps")["RestorePath"] ?? "./restore";

        var dbService = new DatabaseService(connectionString);
        var backupService = new BackupService(connectionString, backupPath);
        var restoreService = new RestoreService(connectionString, restorePath);

        while (true)
        {
            try
            {
                Console.WriteLine("\n--- Main Menu ---");
                Console.WriteLine("1. List Databases");
                Console.WriteLine("2. Export Database");
                Console.WriteLine("3. Import Database");
                Console.WriteLine("4. Exit");
                Console.Write("Select an option: ");
                
                string? choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        await ListDatabasesAsync(dbService);
                        break;
                    case "2":
                        await ExportDatabaseAsync(dbService, backupService);
                        break;
                    case "3":
                        await ImportDatabaseAsync(restoreService, restorePath);
                        break;
                    case "4":
                        Console.WriteLine("Exiting...");
                        return;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nAn error occurred: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    static async Task ListDatabasesAsync(DatabaseService dbService)
    {
        Console.WriteLine("\nFetching databases...");
        var databases = await dbService.GetUserDatabasesAsync();
        
        if (!databases.Any())
        {
            Console.WriteLine("No user databases found.");
            return;
        }

        Console.WriteLine("User Databases:");
        foreach (var db in databases)
        {
            Console.WriteLine($"- {db}");
        }
    }

    static async Task ExportDatabaseAsync(DatabaseService dbService, BackupService backupService)
    {
        Console.WriteLine("\nFetching databases for export...");
        var databases = await dbService.GetUserDatabasesAsync();
        
        if (!databases.Any())
        {
            Console.WriteLine("No databases available to export.");
            return;
        }

        Console.WriteLine("Select a database to export:");
        for (int i = 0; i < databases.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {databases[i]}");
        }
        
        Console.Write("Enter number: ");
        if (!int.TryParse(Console.ReadLine(), out int dbIdx) || dbIdx < 1 || dbIdx > databases.Count)
        {
            Console.WriteLine("Invalid selection.");
            return;
        }

        string selectedDb = databases[dbIdx - 1];

        Console.WriteLine("Choose export type:");
        Console.WriteLine("1. bak");
        Console.WriteLine("2. bacpac");
        Console.Write("Enter option: ");
        string? typeChoice = Console.ReadLine();

        if (typeChoice == "1")
        {
            await backupService.ExportBakAsync(selectedDb);
        }
        else if (typeChoice == "2")
        {
            await backupService.ExportBacpacAsync(selectedDb);
        }
        else
        {
            Console.WriteLine("Invalid export type.");
        }
    }

    static async Task ImportDatabaseAsync(RestoreService restoreService, string restoreDir)
    {
        if (!Directory.Exists(restoreDir))
        {
            Directory.CreateDirectory(restoreDir);
        }

        var files = Directory.GetFiles(restoreDir, "*.*")
            .Where(f => f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || 
                        f.EndsWith(".bacpac", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!files.Any())
        {
            Console.WriteLine($"No .bak or .bacpac files found in {Path.GetFullPath(restoreDir)}.");
            return;
        }

        Console.WriteLine("\nSelect a file to import:");
        for (int i = 0; i < files.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {Path.GetFileName(files[i])}");
        }

        Console.Write("Enter number: ");
        if (!int.TryParse(Console.ReadLine(), out int fileIdx) || fileIdx < 1 || fileIdx > files.Count)
        {
            Console.WriteLine("Invalid selection.");
            return;
        }

        string selectedFile = files[fileIdx - 1];
        string extension = Path.GetExtension(selectedFile).ToLowerInvariant();

        if (extension == ".bak")
        {
            await restoreService.RestoreBakAsync(selectedFile);
        }
        else if (extension == ".bacpac")
        {
            await restoreService.RestoreBacpacAsync(selectedFile);
        }
        else
        {
            Console.WriteLine("Unsupported file type.");
        }
    }
}
