using System;
using System.Data.SqlClient;
using System.IO;
using Hemograma;

internal static class ConnectionSetup
{
    static int Main(string[] args)
    {
        if (args.Length != 4) return 2;
        string request = args[1], target = args[2], report = args[3];
        try
        {
            if (args[0] == "--read-user")
            {
                var existing = new SqlConnectionStringBuilder(SqlSettings.Connection(target, "Integrated Security=True", false));
                File.WriteAllText(report, existing.UserID, new System.Text.UTF8Encoding(false)); return 0;
            }
            var cs = SqlSettings.FromRequest(request, target);
            using (var connection = new SqlConnection(cs.ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.DONATION') IS NOT NULL AND OBJECT_ID('dbo.DONATION_TEST') IS NOT NULL AND OBJECT_ID('dbo.TEST') IS NOT NULL THEN 1 ELSE 0 END", connection))
                    if (Convert.ToInt32(command.ExecuteScalar()) != 1) throw new InvalidOperationException("La base no contiene las tablas esperadas de eDelphyn o el usuario no puede consultarlas.");
            }
            if (args[0] == "--save") { SqlSettings.Save(cs, target); TestSettings.Ensure(Path.Combine(Path.GetDirectoryName(target), "eDelphyn.Hemograma.exe.config")); }
            else if (args[0] != "--test") return 2;
            File.WriteAllText(report, "Conexión correcta con SQL Server y base de datos eDelphyn.");
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(report, "No se pudo conectar: " + ex.Message); return 1; }
        finally { if (File.Exists(request)) File.Delete(request); }
    }
}
