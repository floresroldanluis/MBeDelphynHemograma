using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Xml;

namespace Hemograma
{
    internal sealed partial class Repository
    {
        internal static DataSet LoadBackup(string path)
        {
            var data = new DataSet();
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32 * 1024 * 1024 }))
                data.ReadXml(reader, XmlReadMode.ReadSchema);
            ValidateBackup(data);
            return data;
        }
        static void ValidateBackup(DataSet data)
        {
            if (!data.Tables.Contains("Pruebas") || !data.Tables.Contains("Donaciones")) throw new InvalidOperationException("El archivo no es un respaldo de hemograma.");
            var tests = data.Tables["Pruebas"]; var donations = data.Tables["Donaciones"];
            if (tests.Rows.Count == 0 || tests.Rows.Count > 9000 || donations.Rows.Count == 0 || donations.Rows.Count > 1000)
                throw new InvalidOperationException("El respaldo está vacío o excede el límite permitido.");
            foreach (string column in new[] { "ID_DONATION", "ID_TEST", "DES_RESULT" })
                if (!tests.Columns.Contains(column)) throw new InvalidOperationException("Faltan campos de pruebas en el respaldo.");
            foreach (string column in new[] { "ID_DONATION", "COD_DONATION", "DAT_DONATION" })
                if (!donations.Columns.Contains(column)) throw new InvalidOperationException("Faltan campos de donación en el respaldo.");
            var ids = donations.AsEnumerable().Select(r => Convert.ToDecimal(r["ID_DONATION"])).ToArray();
            if (ids.Distinct().Count() != ids.Length) throw new InvalidOperationException("El respaldo repite donaciones.");
            var keys = new HashSet<string>();
            foreach (DataRow row in tests.Rows)
                if (!ids.Contains(Convert.ToDecimal(row["ID_DONATION"])) || !TestIds.Contains(Convert.ToInt32(row["ID_TEST"])) || !keys.Add(Key(row)))
                    throw new InvalidOperationException("El respaldo contiene pruebas ajenas o duplicadas.");
        }
        static string Key(DataRow row) { return Convert.ToDecimal(row["ID_DONATION"]) + "/" + Convert.ToInt32(row["ID_TEST"]); }
        static void CheckOrigin(SqlConnection c, DataSet data)
        {
            if (!String.Equals(Convert.ToString(data.ExtendedProperties["Servidor"]), c.DataSource, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(Convert.ToString(data.ExtendedProperties["Base"]), c.Database, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("El respaldo pertenece a otro servidor o base de datos. Conéctese al origen del respaldo.");
        }
        DataTable BackupDonations(SqlConnection c, SqlTransaction tx, DataSet data)
        {
            var ids = data.Tables["Donaciones"].AsEnumerable().Select(r => Convert.ToDecimal(r["ID_DONATION"])).ToArray();
            var cmd = Command(c, tx, "SELECT ID_DONATION,COD_DONATION,DAT_DONATION FROM " + Donations + " WITH (UPDLOCK,HOLDLOCK) WHERE " + IdClause(ids.Length));
            AddIds(cmd, ids); return Read(cmd, "Actuales");
        }
        static bool DonationMatches(DataRow saved, DataTable current)
        {
            var row = current.AsEnumerable().FirstOrDefault(r => Convert.ToDecimal(r["ID_DONATION"]) == Convert.ToDecimal(saved["ID_DONATION"]));
            return row != null && Object.Equals(row["COD_DONATION"], saved["COD_DONATION"]) && Object.Equals(row["DAT_DONATION"], saved["DAT_DONATION"]);
        }
        public DataTable PreviewBackup(DataSet data)
        { using (var c = Open()) return PreviewBackup(c, data); }
        internal DataTable PreviewBackup(SqlConnection c, DataSet data)
        {
            ValidateBackup(data); CheckOrigin(c, data);
            using (var tx = c.BeginTransaction(IsolationLevel.Serializable))
            {
                var donations = BackupDonations(c, tx, data);
                var ids = data.Tables["Donaciones"].AsEnumerable().Select(r => Convert.ToDecimal(r["ID_DONATION"])).ToArray();
                var existing = new HashSet<string>(ReadTests(c, tx, ids, false).AsEnumerable().Select(Key));
                var display = BackupDisplay(data, donations, existing);
                tx.Commit(); return display;
            }
        }
        internal static DataTable BackupDisplay(DataSet data, DataTable donations = null, HashSet<string> existing = null)
        {
            ValidateBackup(data);
                var display = new DataTable();
                display.Columns.Add("Seleccionar", typeof(bool)); display.Columns.Add("Fecha donación", typeof(DateTime));
                display.Columns.Add("Número donación"); display.Columns.Add("Prueba"); display.Columns.Add("Resultado respaldado");
                display.Columns.Add("Validación (código)"); display.Columns.Add("Estado actual");
                display.Columns.Add("Index", typeof(int)); display.Columns.Add("Available", typeof(bool));
                var rows = data.Tables["Pruebas"].Rows;
                for (int i = 0; i < rows.Count; i++)
                {
                    var test = rows[i];
                    var donation = data.Tables["Donaciones"].AsEnumerable().Single(r => Convert.ToDecimal(r["ID_DONATION"]) == Convert.ToDecimal(test["ID_DONATION"]));
                    bool verified = donations != null && existing != null;
                    bool matches = verified && DonationMatches(donation, donations), exists = verified && existing.Contains(Key(test));
                    string state = !verified ? "Sin verificar: no restaurable" : !matches ? "Donación no coincide / no existe" : exists ? "Ya existe: no se sobrescribe" : "Ausente: puede restaurarse";
                    display.Rows.Add(false, donation["DAT_DONATION"], donation["COD_DONATION"], TestNames[Array.IndexOf(TestIds, Convert.ToInt32(test["ID_TEST"]))],
                        String.IsNullOrWhiteSpace(Convert.ToString(test["DES_RESULT"])) ? "Sin resultado" : Convert.ToString(test["DES_RESULT"]),
                        test.Table.Columns.Contains("COD_VALIDATED") ? Convert.ToString(test["COD_VALIDATED"]) : "", state, i, matches && !exists);
                }
            return display;
        }
        public int RestoreBackup(DataSet data, int[] indexes)
        { using (var c = Open()) return RestoreBackup(c, data, indexes); }
        internal int RestoreBackup(SqlConnection c, DataSet data, int[] indexes)
        {
            ValidateBackup(data); CheckOrigin(c, data);
            indexes = indexes.Distinct().ToArray();
            if (indexes.Length == 0 || indexes.Any(i => i < 0 || i >= data.Tables["Pruebas"].Rows.Count)) throw new InvalidOperationException("Seleccione pruebas del respaldo para restaurar.");
            var saved = data.Tables["Pruebas"];
            using (var tx = c.BeginTransaction(IsolationLevel.Serializable))
            {
                var donations = BackupDonations(c, tx, data);
                var ids = indexes.Select(i => Convert.ToDecimal(saved.Rows[i]["ID_DONATION"])).Distinct().ToArray();
                var current = ReadTests(c, tx, ids, true);
                if (current.Columns.Count != saved.Columns.Count || current.Columns.Cast<DataColumn>().Any(col => !saved.Columns.Contains(col.ColumnName) || saved.Columns[col.ColumnName].DataType != col.DataType))
                    throw new InvalidOperationException("La estructura de la tabla cambió. No se puede restaurar este respaldo automáticamente.");
                var existing = new HashSet<string>(current.AsEnumerable().Select(Key));
                foreach (int i in indexes)
                {
                    var row = saved.Rows[i];
                    var donation = data.Tables["Donaciones"].AsEnumerable().Single(r => Convert.ToDecimal(r["ID_DONATION"]) == Convert.ToDecimal(row["ID_DONATION"]));
                    if (!DonationMatches(donation, donations)) throw new InvalidOperationException("Una donación no existe o no coincide con el respaldo. No se restauró ningún registro.");
                    if (existing.Contains(Key(row))) throw new InvalidOperationException("Una prueba seleccionada ya existe. No se sobrescribió ni restauró ningún registro. Actualice la vista.");
                }
                var columns = current.Columns.Cast<DataColumn>().ToArray();
                string sql = "INSERT INTO " + Tests + " (" + String.Join(",", columns.Select(col => "[" + col.ColumnName.Replace("]", "]]") + "]")) + ") VALUES (" + String.Join(",", columns.Select((col, i) => "@v" + i)) + ")";
                foreach (int index in indexes)
                    using (var cmd = Command(c, tx, sql))
                    {
                        for (int i = 0; i < columns.Length; i++) cmd.Parameters.AddWithValue("@v" + i, saved.Rows[index][columns[i].ColumnName]);
                        cmd.ExecuteNonQuery();
                    }
                // Verify all restored fields before committing, including NULLs and validation data.
                var after = ReadTests(c, tx, ids, true);
                foreach (int index in indexes)
                {
                    var row = saved.Rows[index]; var actual = after.AsEnumerable().Single(r => Key(r) == Key(row));
                    var expectedTable = current.Clone(); expectedTable.ImportRow(row);
                    var actualTable = current.Clone(); actualTable.ImportRow(actual);
                    if (Fingerprint(expectedTable, ids) != Fingerprint(actualTable, ids)) throw new InvalidOperationException("La verificación de los datos restaurados falló. Se canceló la operación.");
                }
                tx.Commit(); return indexes.Length;
            }
        }
    }
}
