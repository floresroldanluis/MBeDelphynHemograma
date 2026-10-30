using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Hemograma
{
    internal sealed class LoginIdentity
    {
        internal decimal Id;
        internal string Name;
    }
    internal static class Authentication
    {
        // Compatibility with this eDelphyn installation: uppercase user name,
        // SHA-1 hex in uppercase; the Java server uses Windows-1252.
        internal static string Digest(string value)
        {
            string encoding = ConfigurationManager.AppSettings["eDelphynAuthEncoding"] ?? "windows-1252";
            var codec = Encoding.GetEncoding(encoding, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            byte[] bytes = codec.GetBytes(value);
            try { using (var sha = SHA1.Create()) return String.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("X2"))); }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }
        internal static LoginIdentity Authenticate(string connectionString, string user, string password)
        {
            using (var connection = new SqlConnection(connectionString))
            { connection.Open(); return Authenticate(connection, user, password, false); }
        }
        internal static LoginIdentity Authenticate(SqlConnection connection, string user, string password, bool fixture)
        {
            if (String.IsNullOrWhiteSpace(user) || String.IsNullOrEmpty(password) || user.Length > 100 || password.Length > 256)
                throw new InvalidOperationException("Ingrese su usuario y contraseña de eDelphyn.");
            string prefix = fixture ? "#" : "dbo.";
            string sql = "SELECT u.ID_USR,u.DES_USR,u.DAT_PASSWORD,p.NUM_EXPIRY,GETDATE() AS ServerNow FROM " + prefix + "USR u JOIN " + prefix +
                "PWD w ON w.ID_USR=u.ID_USR JOIN " + prefix + "USERPROFILE p ON p.ID_USERPROFILE=u.ID_USERPROFILE " +
                "WHERE u.DES_USERID=@user AND w.DES_PWD=@password AND u.LOG_ACTIVE='Y' AND u.LOG_LOCKED='N'";
            using (var cmd = new SqlCommand(sql, connection) { CommandTimeout = 15 })
            {
                cmd.Parameters.Add("@user", SqlDbType.VarChar, 40).Value = Digest(user.Trim().ToUpperInvariant());
                cmd.Parameters.Add("@password", SqlDbType.VarChar, 40).Value = Digest(password);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("Usuario o contraseña incorrectos, o cuenta inactiva/bloqueada.");
                    if (reader.IsDBNull(2) || reader.IsDBNull(3) || reader.GetDateTime(2).AddDays(Convert.ToInt32(reader[3])) < reader.GetDateTime(4))
                        throw new InvalidOperationException("Debe actualizar su contraseña en eDelphyn antes de ingresar.");
                    var result = new LoginIdentity { Id = Convert.ToDecimal(reader[0]), Name = Convert.ToString(reader[1]) };
                    if (reader.Read()) throw new InvalidOperationException("La cuenta presenta registros duplicados. Consulte al administrador.");
                    return result;
                }
            }
        }
    }
}
