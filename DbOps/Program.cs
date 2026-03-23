namespace DbOps;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("=== DbOps Console Application ===");

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        var configuration = builder.Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

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

        var backupPath = configuration.GetSection("DbOps")["BackupPath"] ?? "./backup";
        var restorePath = configuration.GetSection("DbOps")["RestorePath"] ?? "./restore";

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

                var choice = Console.ReadLine();

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

    private static async Task ListDatabasesAsync(DatabaseService dbService)
    {
        Console.WriteLine("\nFetching databases...");
        var databases = await dbService.GetUserDatabasesAsync();

        if (databases.Count == 0)
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

    private static async Task ExportDatabaseAsync(DatabaseService dbService, BackupService backupService)
    {
        Console.WriteLine("\nFetching databases for export...");
        var databases = await dbService.GetUserDatabasesAsync();

        if (databases.Count == 0)
        {
            Console.WriteLine("No databases available to export.");
            return;
        }

        Console.WriteLine("Select a database to export:");
        for (var i = 0; i < databases.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {databases[i]}");
        }

        Console.Write("Enter number: ");
        if (!int.TryParse(Console.ReadLine(), out var dbIdx) || dbIdx < 1 || dbIdx > databases.Count)
        {
            Console.WriteLine("Invalid selection.");
            return;
        }

        var selectedDb = databases[dbIdx - 1];
        await backupService.ExportBacpacAsync(selectedDb);
    }

    private static async Task ImportDatabaseAsync(RestoreService restoreService, string restoreDir)
    {
        if (!Directory.Exists(restoreDir))
        {
            Directory.CreateDirectory(restoreDir);
        }

        var files = Directory.GetFiles(restoreDir, "*.*")
            .Where(f => f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".bacpac", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (files.Count == 0)
        {
            Console.WriteLine($"No .bak or .bacpac files found in {Path.GetFullPath(restoreDir)}.");
            return;
        }

        Console.WriteLine("\nSelect a file to import:");
        for (var i = 0; i < files.Count; i++)
        {
            var filePath = Path.GetFileName(files[i]);

            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var dbName = Regex.Replace(fileName, @"_\d{4}-\d{2}-\d{2}$", "");

            Console.WriteLine($"{i + 1}. {filePath} ({dbName})");
        }

        Console.Write("Enter number: ");
        if (!int.TryParse(Console.ReadLine(), out var fileIdx) || fileIdx < 1 || fileIdx > files.Count)
        {
            Console.WriteLine("Invalid selection.");
            return;
        }

        var selectedFile = files[fileIdx - 1];
        await restoreService.RestoreBacpacAsync(selectedFile);
    }
}