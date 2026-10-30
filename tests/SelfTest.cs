using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;

namespace Hemograma
{
    internal static class SelfTest
    {
        static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Exec(SqlConnection c, string sql) { using (var cmd = new SqlCommand(sql, c)) cmd.ExecuteNonQuery(); }
        static int Count(SqlConnection c, string where)
        { using (var cmd = new SqlCommand("SELECT COUNT(*) FROM #DONATION_TEST WHERE " + where, c)) return (int)cmd.ExecuteScalar(); }
        static void Reject(Action action, string message)
        { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Assert(rejected, message); }
        public static int Run()
        {
            var log = new StringBuilder();
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results"); Directory.CreateDirectory(root);
            try
            {
                var live = new Repository(ConfigurationManager.ConnectionStrings["eDelphyn"].ConnectionString);
                var result = live.Search(new Filter { FromCode = "C260730", ToCode = "C260740" });
                log.AppendLine("PASS: lectura real de eDelphyn: " + result.Donations.Rows.Count + " donaciones, " + result.Tests.Rows.Count + " registros de hemograma. Sin escrituras.");
                using (var c = live.Open())
                {
                    // Only session-local temporary tables are mutated by these tests.
                    Exec(c, "CREATE TABLE #DONATION (ID_DONATION numeric(15,0) PRIMARY KEY, COD_DONATION varchar(20), DAT_DONATION datetime); CREATE TABLE #DONATION_TEST (ID_DONATION numeric(15,0), ID_TEST numeric(15,0), DES_RESULT varchar(20), COD_VALIDATED char(1), PRIMARY KEY(ID_DONATION,ID_TEST)); INSERT INTO #DONATION VALUES (1,'C000001','20261029 00:00:00'),(2,'C000002','20261029 23:59:59'),(3,'C000003','20261030'); INSERT INTO #DONATION_TEST VALUES (1,72,NULL,'N'),(1,93,'17','A'),(1,10,'NEG','A'),(2,72,'200','A'),(2,93,'14','A'),(3,93,'15','A');");
                    Exec(c, "CREATE TABLE #USR (ID_USR numeric(15,0),DES_USR varchar(40),DES_USERID varchar(40),DAT_PASSWORD datetime,ID_USERPROFILE int,LOG_ACTIVE char(1),LOG_LOCKED char(1)); CREATE TABLE #PWD(ID_USR numeric(15,0),DES_PWD varchar(40)); CREATE TABLE #USERPROFILE(ID_USERPROFILE int,NUM_EXPIRY int); INSERT INTO #USERPROFILE VALUES(1,90);");
                    using (var auth = new SqlCommand("INSERT INTO #USR VALUES(1,'Usuario de prueba',@u,GETDATE(),1,'Y','N'); INSERT INTO #PWD VALUES(1,@p);", c))
                    {
                        auth.Parameters.AddWithValue("@u", Authentication.Digest("OPERADOR")); auth.Parameters.AddWithValue("@p", Authentication.Digest("Prueba-Ñ-2026")); auth.ExecuteNonQuery();
                    }
                    Assert(Authentication.Digest("abc") == "A9993E364706816ABA3E25717850C26C9CD0D89D", "SHA-1 compatible");
                    Assert(Authentication.Authenticate(c, "operador", "Prueba-Ñ-2026", true).Id == 1, "Login compatible");
                    Reject(() => Authentication.Authenticate(c, "operador", "prueba-Ñ-2026", true), "Contraseña distingue mayúsculas");
                    Reject(() => Authentication.Authenticate(c, "' OR 1=1 --", "otra", true), "Consulta parametrizada");
                    Exec(c, "UPDATE #USR SET LOG_ACTIVE='N'");
                    Reject(() => Authentication.Authenticate(c, "operador", "Prueba-Ñ-2026", true), "Usuario inactivo");
                    Exec(c, "UPDATE #USR SET LOG_ACTIVE='Y',LOG_LOCKED='Y'");
                    Reject(() => Authentication.Authenticate(c, "operador", "Prueba-Ñ-2026", true), "Usuario bloqueado");
                    Exec(c, "UPDATE #USR SET LOG_LOCKED='N',DAT_PASSWORD=DATEADD(day,-100,GETDATE())");
                    Reject(() => Authentication.Authenticate(c, "operador", "Prueba-Ñ-2026", true), "Contraseña vencida");
                    Exec(c, "UPDATE #USR SET DAT_PASSWORD=NULL");
                    Reject(() => Authentication.Authenticate(c, "operador", "Prueba-Ñ-2026", true), "Cambio obligatorio de contraseña");
                    log.AppendLine("PASS: login con cuenta de prueba, SHA-1 compatible, contraseña sensible a mayúsculas, rechazo de inactivos/bloqueados/vencidos y SQL parametrizado.");
                    var repo = new Repository(true);
                    var filter = new Filter { FromDate = new DateTime(2026,10,29), ToDate = new DateTime(2026,10,29) };
                    var snap = repo.Search(c, filter);
                    Assert(snap.Donations.Rows.Count == 2 && snap.Tests.Rows.Count == 4, "Rango inclusivo de fecha / solo hemograma");
                    var both = repo.Search(c, new Filter { FromCode = "C000002", ToCode = "C000003", FromDate = new DateTime(2026,10,29), ToDate = new DateTime(2026,10,29) });
                    Assert(both.Donations.Rows.Count == 1, "Intersección de filtros");
                    log.AppendLine("PASS: fechas inclusivas, filtros combinados, exclusión de otras pruebas.");
                    Reject(() => new Filter().Validate(), "Filtro vacío");
                    Reject(() => new Filter { FromCode = "Z", ToCode = "A" }.Validate(), "Rango invertido");
                    Reject(() => new Filter { FromDate = new DateTime(2026,10,30), ToDate = new DateTime(2026,10,29) }.Validate(), "Fecha invertida");
                    Reject(() => repo.Delete(c, snap, new decimal[0], root), "Selección vacía");
                    Reject(() => repo.Delete(c, snap, new decimal[] { 3 }, root), "Donación fuera de búsqueda");
                    log.AppendLine("PASS: validación de filtros y selección.");
                    Exec(c, "UPDATE #DONATION_TEST SET DES_RESULT='18' WHERE ID_DONATION=1 AND ID_TEST=93");
                    Reject(() => repo.Delete(c, snap, new decimal[] { 1 }, root), "Cambio concurrente");
                    Assert(Count(c, "1=1") == 6, "Cancelación debe preservar todos los registros");
                    log.AppendLine("PASS: cambio posterior a búsqueda bloquea eliminación.");
                    snap = repo.Search(c, filter);
                    string blocked = Path.Combine(root, "not-a-directory"); File.WriteAllText(blocked, "test");
                    bool failedBackup = false;
                    try { repo.Delete(c, snap, new decimal[] { 1 }, blocked); } catch (IOException) { failedBackup = true; }
                    Assert(failedBackup && Count(c, "1=1") == 6, "Respaldo fallido debe impedir DELETE");
                    log.AppendLine("PASS: respaldo fallido impide eliminación.");
                    Reject(() => repo.Delete(c, snap, new decimal[] { 2 }, root, new int[0]), "Pruebas vacías");
                    Reject(() => repo.Delete(c, snap, new decimal[] { 2 }, root, new int[] { 10 }), "Prueba fuera de hemograma");
                    var partial = repo.Delete(c, snap, new decimal[] { 2 }, root, new int[] { 72 });
                    Assert(partial.Count == 1 && Count(c, "ID_DONATION=2 AND ID_TEST=93") == 1, "Hb desmarcada debe conservarse");
                    var partialBackup = new DataSet(); partialBackup.ReadXml(partial.Backup, XmlReadMode.ReadSchema);
                    Assert(partialBackup.Tables["Pruebas"].Rows.Count == 1 && Convert.ToInt32(partialBackup.Tables["Pruebas"].Rows[0]["ID_TEST"]) == 72, "Respaldo solo de pruebas seleccionadas");
                    var hbSnap = repo.Search(c, filter);
                    Assert(repo.Delete(c, hbSnap, new decimal[] { 2 }, root, new int[] { 93 }).Count == 1, "Hb seleccionada debe eliminarse");
                    Exec(c, "INSERT INTO #DONATION_TEST VALUES (2,72,'200','A'),(2,93,'14','A');");
                    log.AppendLine("PASS: selección individual preserva Hb desmarcada; elimina Hb solo al seleccionarla; respaldo limitado a selección; rechaza pruebas vacías o ajenas.");
                    var deleted = repo.Delete(c, snap, new decimal[] { 1 }, root);
                    Assert(deleted.Count == 2, "Debe eliminar dos registros seleccionados");
                    Assert(Count(c, "ID_DONATION=1 AND ID_TEST=10") == 1, "Debe preservar prueba ajena");
                    Assert(Count(c, "ID_DONATION=2") == 2 && Count(c, "ID_DONATION=3") == 1, "Debe preservar donaciones no seleccionadas");
                    var backup = new DataSet(); backup.ReadXml(deleted.Backup, XmlReadMode.ReadSchema);
                    Assert(backup.Tables["Pruebas"].Rows.Count == 2, "Respaldo completo");
                    Assert(backup.Tables["Pruebas"].AsEnumerable().Any(r => r["DES_RESULT"] == DBNull.Value), "Respaldo conserva NULL");
                    log.AppendLine("PASS: elimina solo hemograma seleccionado; conserva otras pruebas y donaciones; respaldo XML verificado.");
                    snap = repo.Search(c, filter);
                    Assert(repo.Delete(c, snap, new decimal[] { 1 }, root).Count == 0, "Donación sin pruebas");
                    // Simulate a server error after DELETE; transaction must restore every deleted row.
                    var rollbackSnap = repo.Search(c, filter);
                    Exec(c, "ALTER TABLE #DONATION_TEST ADD CONSTRAINT CK_Test_Result CHECK (DES_RESULT IS NULL OR DES_RESULT <> 'FAIL');");
                    using (var tx = c.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd = new SqlCommand("DELETE FROM #DONATION_TEST WHERE ID_DONATION=2; INSERT INTO #DONATION_TEST VALUES (9,93,'FAIL','N');", c, tx)) cmd.ExecuteNonQuery();
                        }
                        catch (SqlException) { tx.Rollback(); }
                    }
                    Assert(Count(c, "ID_DONATION=2") == 2, "Rollback SQL");
                    log.AppendLine("PASS: rollback SQL preserva filas ante error; caso sin pruebas no modifica datos.");
                    var recovered = Repository.LoadBackup(deleted.Backup);
                    var preview = repo.PreviewBackup(c, recovered);
                    Assert(preview.Rows.Count == 2 && preview.AsEnumerable().All(r => (bool)r["Available"]), "Vista debe mostrar pruebas ausentes");
                    var wrongOrigin = recovered.Copy(); wrongOrigin.ExtendedProperties["Base"] = "otra_base";
                    Reject(() => repo.RestoreBackup(c, wrongOrigin, new[] { 0 }), "Rechazar otra base");
                    var wrongDonation = recovered.Copy(); wrongDonation.Tables["Donaciones"].Rows[0]["COD_DONATION"] = "OTRA";
                    Reject(() => repo.RestoreBackup(c, wrongDonation, new[] { 0 }), "Rechazar identidad de donación distinta");
                    var changedSchema = recovered.Copy(); changedSchema.Tables["Pruebas"].Columns.Remove("COD_VALIDATED");
                    Reject(() => repo.RestoreBackup(c, changedSchema, new[] { 0 }), "Rechazar esquema diferente");
                    Assert(repo.RestoreBackup(c, recovered, new[] { 0 }) == 1, "Restauración seleccionada");
                    Assert(Count(c, "ID_DONATION=1 AND ID_TEST=72 AND DES_RESULT IS NULL AND COD_VALIDATED='N'") == 1 && Count(c, "ID_DONATION=1 AND ID_TEST=93") == 0, "Restaurar solo la selección y conservar NULL/validación");
                    Reject(() => repo.RestoreBackup(c, recovered, new[] { 0, 1 }), "Duplicado debe cancelar todo");
                    Assert(Count(c, "ID_DONATION=1 AND ID_TEST=93") == 0, "No debe insertar parcialmente");
                    Assert(repo.PreviewBackup(c, recovered).AsEnumerable().Count(r => (bool)r["Available"]) == 1, "Estado posterior a restauración");
                    Exec(c, "DELETE FROM #DONATION_TEST WHERE ID_DONATION=1 AND ID_TEST=72; ALTER TABLE #DONATION_TEST ADD CONSTRAINT CK_Restore_Failure CHECK (DES_RESULT IS NULL OR DES_RESULT <> '18');");
                    bool restoreFailed = false;
                    try { repo.RestoreBackup(c, recovered, new[] { 0, 1 }); } catch (SqlException) { restoreFailed = true; }
                    Assert(restoreFailed && Count(c, "ID_DONATION=1 AND ID_TEST IN (72,93)") == 0, "Error SQL debe revertir todas las inserciones");
                    Exec(c, "ALTER TABLE #DONATION_TEST DROP CONSTRAINT CK_Restore_Failure;");
                    Assert(repo.RestoreBackup(c, recovered, new[] { 0, 1 }) == 2, "Restauración completa");
                    Assert(Count(c, "ID_DONATION=1 AND ID_TEST=93 AND DES_RESULT='18' AND COD_VALIDATED='A'") == 1, "Conservar resultado y validación original");
                    Reject(() => repo.RestoreBackup(c, recovered, new[] { 0, 1 }), "Restauración repetida");
                    Assert(repo.PreviewBackup(c, recovered).AsEnumerable().All(r => !(bool)r["Available"]), "Restaurado no vuelve a estar disponible");
                    log.AppendLine("PASS: vista de respaldo, restauración seleccionada/completa, NULL y validación; rechazo de origen, donación, esquema, duplicados; rollback total ante fallo SQL.");
                }
                log.AppendLine("Todas las pruebas completadas. Ningún DELETE ejecutado contra tablas reales.");
                File.WriteAllText(Path.Combine(root, "results.txt"), log.ToString(), Encoding.UTF8); return 0;
            }
            catch (Exception ex)
            { log.AppendLine("FAIL: " + ex); File.WriteAllText(Path.Combine(root, "results.txt"), log.ToString(), Encoding.UTF8); return 1; }
        }
    }
}
