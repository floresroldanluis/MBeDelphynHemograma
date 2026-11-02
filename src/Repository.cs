using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Hemograma
{
    internal sealed class Filter
    {
        public string FromCode, ToCode;
        public DateTime? FromDate, ToDate;
        public void Validate()
        {
            FromCode = (FromCode ?? "").Trim(); ToCode = (ToCode ?? "").Trim();
            if (FromCode.Length == 0 && ToCode.Length == 0 && !FromDate.HasValue && !ToDate.HasValue)
                throw new InvalidOperationException("Ingrese al menos un límite de donación o active una fecha.");
            if (FromCode.Length > 20 || ToCode.Length > 20) throw new InvalidOperationException("El número de donación admite hasta 20 caracteres.");
            if (FromCode.Length > 0 && ToCode.Length > 0 && String.Compare(FromCode, ToCode, StringComparison.OrdinalIgnoreCase) > 0)
                throw new InvalidOperationException("Donación número: Desde no puede ser mayor que Hasta.");
            if (FromDate.HasValue && ToDate.HasValue && FromDate.Value.Date > ToDate.Value.Date)
                throw new InvalidOperationException("Fecha donación: Desde no puede ser posterior a Hasta.");
        }
    }
    internal sealed class SearchResult
    {
        public DataTable Donations, Tests;
    }
    internal sealed class DeleteResult
    {
        public int Count;
        public string Backup;
    }
    internal sealed partial class Repository
    {
        internal static readonly TestDefinition[] TestDefinitions = TestSettings.Load();
        public static readonly int[] TestIds = TestDefinitions.Select(t => t.Id).ToArray();
        public static readonly string[] TestNames = TestDefinitions.Select(t => t.Name).ToArray();
        static readonly string Allowed = String.Join(",", TestIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        readonly string connectionString;
        readonly bool fixture;
        internal decimal? OperatorId;
        internal string OperatorName;
        string Donations { get { return fixture ? "#DONATION" : "dbo.DONATION"; } }
        string Tests { get { return fixture ? "#DONATION_TEST" : "dbo.DONATION_TEST"; } }
        public Repository(string connectionString) { this.connectionString = connectionString; }
        internal Repository(bool fixture) { this.fixture = fixture; }
        public SqlConnection Open()
        {
            var c = new SqlConnection(connectionString); c.Open(); return c;
        }
        static SqlCommand Command(SqlConnection c, SqlTransaction tx, string sql)
        {
            return new SqlCommand(sql, c, tx) { CommandTimeout = 45 };
        }
        static DataTable Read(SqlCommand cmd, string name)
        {
            using (cmd) using (var adapter = new SqlDataAdapter(cmd))
            { var table = new DataTable(name); adapter.Fill(table); return table; }
        }
        public SearchResult Search(Filter filter)
        { using (var c = Open()) return Search(c, filter); }
        internal SearchResult Search(SqlConnection c, Filter filter)
        {
            filter.Validate();
            using (var tx = c.BeginTransaction(IsolationLevel.Serializable))
            {
                var cmd = Command(c, tx, "SELECT TOP (1001) ID_DONATION, COD_DONATION, DAT_DONATION FROM " + Donations +
                    " WHERE (@fc='' OR COD_DONATION>=@fc) AND (@tc='' OR COD_DONATION<=@tc) AND (@fd IS NULL OR DAT_DONATION>=@fd) AND (@td IS NULL OR DAT_DONATION<@td) ORDER BY ID_DONATION;");
                cmd.Parameters.Add("@fc", SqlDbType.VarChar, 20).Value = filter.FromCode;
                cmd.Parameters.Add("@tc", SqlDbType.VarChar, 20).Value = filter.ToCode;
                cmd.Parameters.Add("@fd", SqlDbType.DateTime).Value = filter.FromDate.HasValue ? (object)filter.FromDate.Value.Date : DBNull.Value;
                cmd.Parameters.Add("@td", SqlDbType.DateTime).Value = filter.ToDate.HasValue ? (object)filter.ToDate.Value.Date.AddDays(1) : DBNull.Value;
                var donations = Read(cmd, "Donaciones");
                if (donations.Rows.Count > 1000) throw new InvalidOperationException("Hay más de 1000 donaciones. Reduzca el rango para revisar los resultados completos.");
                var ids = donations.AsEnumerable().Select(r => Convert.ToDecimal(r["ID_DONATION"])).ToArray();
                var tests = ReadTests(c, tx, ids, false);
                tx.Commit();
                return new SearchResult { Donations = donations, Tests = tests };
            }
        }
        DataTable ReadTests(SqlConnection c, SqlTransaction tx, decimal[] ids, bool locking)
        {
            var cmd = Command(c, tx, "SELECT * FROM " + Tests + (locking ? " WITH (UPDLOCK,HOLDLOCK)" : "") +
                " WHERE ID_TEST IN (" + Allowed + ") AND " + IdClause(ids.Length) + " ORDER BY ID_DONATION,ID_TEST;");
            AddIds(cmd, ids); return Read(cmd, "Pruebas");
        }
        static string IdClause(int count)
        { return count == 0 ? "1=0" : "ID_DONATION IN (" + String.Join(",", Enumerable.Range(0, count).Select(i => "@id" + i)) + ")"; }
        static void AddIds(SqlCommand cmd, decimal[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
            { var p = cmd.Parameters.Add("@id" + i, SqlDbType.Decimal); p.Precision = 15; p.Scale = 0; p.Value = ids[i]; }
        }
        internal static string Fingerprint(DataTable table, decimal[] ids)
        {
            var set = new HashSet<decimal>(ids);
            using (var buffer = new MemoryStream())
            {
                using (var writer = new BinaryWriter(buffer, Encoding.UTF8, true))
                {
                    foreach (DataColumn col in table.Columns) { writer.Write(col.ColumnName); writer.Write(col.DataType.FullName); }
                    foreach (var row in table.AsEnumerable().Where(r => set.Contains(Convert.ToDecimal(r["ID_DONATION"])))
                        .OrderBy(r => Convert.ToDecimal(r["ID_DONATION"])).ThenBy(r => Convert.ToDecimal(r["ID_TEST"])))
                    {
                        foreach (var value in row.ItemArray)
                        {
                            writer.Write(value != DBNull.Value);
                            if (value == DBNull.Value) continue;
                            var bytes = value as byte[];
                            if (bytes != null) writer.Write(Convert.ToBase64String(bytes));
                            else if (value is DateTime) writer.Write(((DateTime)value).ToString("O", CultureInfo.InvariantCulture));
                            else writer.Write(Convert.ToString(value, CultureInfo.InvariantCulture));
                        }
                    }
                }
                using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(buffer.ToArray()));
            }
        }
        public DeleteResult Delete(SearchResult snapshot, decimal[] ids, string backupRoot, int[] selectedTests)
        { BackupRetention.Clean(backupRoot, DateTime.UtcNow); using (var c = Open()) return Delete(c, snapshot, ids, backupRoot, selectedTests); }
        internal DeleteResult Delete(SqlConnection c, SearchResult snapshot, decimal[] ids, string backupRoot, int[] selectedTests = null)
        {
            selectedTests = (selectedTests ?? TestIds).Distinct().ToArray();
            if (selectedTests.Length == 0 || selectedTests.Any(id => !TestIds.Contains(id)))
                throw new InvalidOperationException("Seleccione al menos una prueba válida del hemograma.");
            string selectedTestSql = String.Join(",", selectedTests.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            ids = ids.Distinct().ToArray();
            if (ids.Length == 0 || ids.Length > 1000) throw new InvalidOperationException("Seleccione entre 1 y 1000 donaciones.");
            var known = new HashSet<decimal>(snapshot.Donations.AsEnumerable().Select(r => Convert.ToDecimal(r["ID_DONATION"])));
            if (ids.Any(id => !known.Contains(id))) throw new InvalidOperationException("La selección no pertenece a la búsqueda.");
            using (var tx = c.BeginTransaction(IsolationLevel.Serializable))
            {
                var current = ReadTests(c, tx, ids, true);
                if (Fingerprint(current, ids) != Fingerprint(snapshot.Tests, ids))
                    throw new InvalidOperationException("Las pruebas cambiaron desde la búsqueda. No se eliminó ningún registro. Vuelva a buscar y revise los resultados.");
                foreach (var row in current.AsEnumerable().Where(r => !selectedTests.Contains(Convert.ToInt32(r["ID_TEST"]))).ToArray())
                    current.Rows.Remove(row);
                if (current.Rows.Count == 0) { tx.Rollback(); return new DeleteResult { Count = 0 }; }
                Directory.CreateDirectory(backupRoot);
                string path = Path.Combine(backupRoot, "hemograma-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".xml");
                var backup = new DataSet("RespaldoHemograma");
                backup.ExtendedProperties["Servidor"] = c.DataSource;
                backup.ExtendedProperties["Base"] = c.Database;
                backup.ExtendedProperties["UsuarioWindows"] = Environment.UserDomainName + "\\" + Environment.UserName;
                if (OperatorId.HasValue) { backup.ExtendedProperties["UsuarioeDelphynID"] = OperatorId.Value.ToString(CultureInfo.InvariantCulture); backup.ExtendedProperties["UsuarioeDelphyn"] = OperatorName; }
                backup.ExtendedProperties["FechaUTC"] = DateTime.UtcNow.ToString("O");
                backup.ExtendedProperties["Nota"] = "Copia previa al intento. Su existencia no demuestra que se confirmara la transacción.";
                backup.Tables.Add(current.Copy());
                var selected = snapshot.Donations.Clone();
                foreach (DataRow row in snapshot.Donations.Rows) if (ids.Contains(Convert.ToDecimal(row["ID_DONATION"]))) selected.ImportRow(row);
                backup.Tables.Add(selected);
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { backup.WriteXml(stream, XmlWriteMode.WriteSchema); stream.Flush(true); }
                // The backup must be readable before any DELETE is issued.
                var verify = new DataSet(); verify.ReadXml(path, XmlReadMode.ReadSchema);
                if (Fingerprint(verify.Tables["Pruebas"], ids) != Fingerprint(current, ids)) throw new IOException("No se pudo verificar el respaldo; se canceló la eliminación.");
                using (var cmd = Command(c, tx, "DELETE FROM " + Tests + " WHERE ID_TEST IN (" + selectedTestSql + ") AND " + IdClause(ids.Length) + "; SELECT CAST(@@ROWCOUNT AS int);"))
                {
                    AddIds(cmd, ids); int count = Convert.ToInt32(cmd.ExecuteScalar());
                    if (count != current.Rows.Count) throw new InvalidOperationException("El total eliminado no coincide; se canceló la transacción.");
                    tx.Commit();
                    return new DeleteResult { Count = count, Backup = path };
                }
            }
        }
    }
}
