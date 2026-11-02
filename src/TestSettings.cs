using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;

namespace Hemograma
{
    internal sealed class TestDefinition
    {
        internal int Id;
        internal string Name;
        internal bool Selected;
    }
    internal static class TestSettings
    {
        internal const string LegacyDefaultValue = "72|Rcto. PQ|true;73|Rcto. Leu|true;74|Neutrófilos|true;75|Linfocitos|true;76|Monocitos|true;77|Eosinófilos|true;78|Basófilos|true;92|Hto|true;93|Hb|false";
        internal const string DefaultValue = "Rcto. PQ|true;Rcto. Leu|true;Neutrófilos|true;Linfocitos|true;Monocitos|true;Eosinófilos|true;Basófilos|true;Hto|true;Hb|false";
        static TestDefinition[] loaded;
        internal static TestDefinition[] Load()
        {
            if (loaded != null) return loaded;
            var definitions = Parse(ConfigurationManager.AppSettings["Hemograma.Pruebas"] ?? DefaultValue);
            if (definitions.All(t => t.Id > 0)) return loaded = definitions;
            string connection = SqlSettings.Connection(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connection.ini"),
                ConfigurationManager.ConnectionStrings["eDelphyn"].ConnectionString, false);
            using (var c = new SqlConnection(connection))
            using (var command = new SqlCommand("SELECT ID_TEST,COD_TEST,DES_TEST,SRT_TEST FROM dbo.TEST", c))
            using (var adapter = new SqlDataAdapter(command))
            {
                var catalog = new DataTable(); command.CommandTimeout = 15; c.Open(); adapter.Fill(catalog);
                return loaded = Resolve(definitions, catalog);
            }
        }
        static bool SameName(string a, string b)
        { return CultureInfo.InvariantCulture.CompareInfo.Compare(a.Trim(), b.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0; }
        internal static TestDefinition[] Resolve(TestDefinition[] definitions, DataTable catalog)
        {
            var result = new List<TestDefinition>();
            foreach (var item in definitions)
            {
                int id = item.Id;
                if (id == 0)
                {
                    var matches = catalog.AsEnumerable().Where(row => new[] { "COD_TEST", "DES_TEST", "SRT_TEST" }.Any(column => SameName(item.Name, Convert.ToString(row[column]))))
                        .Select(row => Convert.ToInt32(row["ID_TEST"])).Distinct().ToArray();
                    if (matches.Length != 1) throw new ConfigurationErrorsException("La prueba '" + item.Name + "' " + (matches.Length == 0 ? "no existe" : "coincide con varias pruebas") + " en TEST. Use un nombre o código único de la base de datos.");
                    id = matches[0];
                }
                if (id <= 0 || result.Any(t => t.Id == id)) throw new ConfigurationErrorsException("Hemograma.Pruebas repite la misma prueba con distintos nombres.");
                result.Add(new TestDefinition { Id = id, Name = item.Name, Selected = item.Selected });
            }
            return result.ToArray();
        }
        internal static TestDefinition[] Parse(string value)
        {
            var result = new List<TestDefinition>();
            foreach (string entry in value.Split(';'))
            {
                var parts = entry.Split('|'); int id = 0; bool selected;
                bool legacy = parts.Length == 3;
                int nameAt = legacy ? 1 : 0;
                if ((parts.Length != 2 && !legacy) || (legacy && (!Int32.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0)) ||
                    String.IsNullOrWhiteSpace(parts[nameAt]) || parts[nameAt].Trim().Length > 80 || !Boolean.TryParse(parts[parts.Length - 1], out selected) ||
                    result.Any(t => SameName(t.Name, parts[nameAt]) || (id > 0 && t.Id == id)))
                    throw new ConfigurationErrorsException("Hemograma.Pruebas: use Nombre|true o false, separados por punto y coma, sin pruebas repetidas.");
                result.Add(new TestDefinition { Id = id, Name = parts[nameAt].Trim(), Selected = selected });
            }
            if (result.Count > 32) throw new ConfigurationErrorsException("Configure como máximo 32 pruebas.");
            return result.ToArray();
        }
        // Add the documented setting to older installations without replacing custom settings.
        internal static void Ensure(string path)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) doc.Load(reader);
            var settings = doc.SelectSingleNode("/configuration/appSettings");
            if (settings == null) { settings = doc.CreateElement("appSettings"); doc.DocumentElement.AppendChild(settings); }
            var existing = settings.SelectSingleNode("add[@key='Hemograma.Pruebas']") as XmlElement;
            if (existing != null && existing.GetAttribute("value") != LegacyDefaultValue) return;
            if (existing != null) settings.RemoveChild(existing);
            foreach (XmlNode comment in settings.ChildNodes.Cast<XmlNode>().Where(n => n is XmlComment && n.Value.Contains("ID_TEST|Nombre")).ToArray()) settings.RemoveChild(comment);
            settings.AppendChild(doc.CreateComment(" Nombre de prueba|Seleccionada al iniciar (true/false). Separar pruebas con ;. Reiniciar después de editar. "));
            var node = doc.CreateElement("add"); node.SetAttribute("key", "Hemograma.Pruebas"); node.SetAttribute("value", DefaultValue); settings.AppendChild(node);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            doc.Save(temporary); File.Replace(temporary, path, null);
        }
    }
}
