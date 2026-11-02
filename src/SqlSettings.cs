using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Hemograma
{
    internal static class SqlSettings
    {
        internal static Dictionary<string, string> Read(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(path)) foreach (string line in File.ReadAllLines(path))
            { int at = line.IndexOf('='); if (at > 0) values[line.Substring(0, at).Trim()] = line.Substring(at + 1); }
            return values;
        }
        static string Get(Dictionary<string, string> values, string key, string fallback)
        { string value; return values.TryGetValue(key, out value) ? value : fallback; }
        static string Protect(string value)
        { return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser)); }
        static string Unprotect(string value)
        { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser)); }
        internal static string Connection(string path, string fallback, bool migrate = true)
        {
            var values = Read(path);
            string protectedConnection = Get(values, "ConnectionProtected", "");
            if (!String.IsNullOrEmpty(protectedConnection)) return new SqlConnectionStringBuilder(Unprotect(protectedConnection)).ConnectionString;
            var cs = new SqlConnectionStringBuilder(fallback.StartsWith("dpapi:", StringComparison.Ordinal) ? Unprotect(fallback.Substring(6)) : fallback);
            cs.DataSource = Get(values, "Server", cs.DataSource); cs.InitialCatalog = Get(values, "Database", cs.InitialCatalog);
            if (Get(values, "Authentication", "Windows") == "Sql")
            {
                cs.IntegratedSecurity = false;
                string protectedUser = Get(values, "UserProtected", "");
                cs.UserID = String.IsNullOrEmpty(protectedUser) ? Get(values, "User", "eDelphyn") : Unprotect(protectedUser);
                string encrypted = Get(values, "PasswordProtected", "");
                cs.Password = String.IsNullOrEmpty(encrypted) ? "" : Unprotect(encrypted);

            }
            if (migrate && File.Exists(path)) Save(cs, path);
            return cs.ConnectionString;
        }
        internal static SqlConnectionStringBuilder FromRequest(string request, string target)
        {
            var values = Read(request);
            var cs = new SqlConnectionStringBuilder { DataSource = Get(values, "Server", "localhost"), InitialCatalog = Get(values, "Database", "eDelphyn"),
                UserID = Get(values, "User", "eDelphyn"), Password = Get(values, "Password", ""), IntegratedSecurity = false, ConnectTimeout = 8, ApplicationName = "eDelphyn Hemograma" };
            if (String.IsNullOrEmpty(cs.Password) && File.Exists(target))
            {
                var previous = new SqlConnectionStringBuilder(Connection(target, "Integrated Security=True", false));
                if (!previous.IntegratedSecurity && previous.UserID == cs.UserID) cs.Password = previous.Password;
            }
            if (String.IsNullOrWhiteSpace(cs.DataSource) || String.IsNullOrWhiteSpace(cs.InitialCatalog) || String.IsNullOrWhiteSpace(cs.UserID) || String.IsNullOrEmpty(cs.Password))
                throw new InvalidOperationException("Ingrese servidor, base, usuario y contraseña. La contraseña vacía solo conserva una credencial ya guardada para ese usuario.");
            return cs;
        }
        internal static void ProtectConfiguration(string path)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) doc.Load(reader);
            var node = doc.SelectSingleNode("/configuration/connectionStrings/add[@name='eDelphyn']") as XmlElement;
            if (node == null || node.GetAttribute("connectionString").StartsWith("dpapi:", StringComparison.Ordinal)) return;
            string connection = new SqlConnectionStringBuilder(node.GetAttribute("connectionString")).ConnectionString;
            node.SetAttribute("connectionString", "dpapi:" + Protect(connection));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            doc.Save(temporary); File.Replace(temporary, path, null);
        }
        internal static void Save(SqlConnectionStringBuilder cs, string path)
        {
            string text = "[SQL]\r\nCredentialProtection=DPAPI-CurrentUser-Base64\r\nConnectionProtected=" + Protect(cs.ConnectionString) + "\r\n";
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, text, Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
    }
}
