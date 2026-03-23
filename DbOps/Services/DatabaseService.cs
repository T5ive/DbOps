namespace DbOps.Services;

public class DatabaseService(string connectionString)
{
    public async Task<List<string>> GetUserDatabasesAsync()
    {
        var databases = new List<string>();
        // Exclude system databases.
        const string query = """
                             SELECT name 
                             FROM sys.databases 
                             WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')
                             ORDER BY name
                             """;

        // Ensuring we connect to master to query sys.databases
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            databases.Add(reader.GetString(0));
        }

        return databases;
    }
}
