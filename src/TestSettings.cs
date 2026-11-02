using System;
using System.Collections.Generic;
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
        internal const string DefaultValue = "72|Rcto. PQ|true;73|Rcto. Leu|true;74|Neutrófilos|true;75|Linfocitos|true;76|Monocitos|true;77|Eosinófilos|true;78|Basófilos|true;92|Hto|true;93|Hb|false";
        internal static TestDefinition[] Load()
        { return Parse(ConfigurationManager.AppSettings["Hemograma.Pruebas"] ?? DefaultValue); }
        internal static TestDefinition[] Parse(string value)
        {
            var result = new List<TestDefinition>();
            foreach (string entry in value.Split(';'))
            {
                var parts = entry.Split('|'); int id; bool selected;
                if (parts.Length != 3 || !Int32.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out id) || id <= 0 ||
                    String.IsNullOrWhiteSpace(parts[1]) || parts[1].Trim().Length > 80 || !Boolean.TryParse(parts[2], out selected) || result.Any(t => t.Id == id))
                    throw new ConfigurationErrorsException("Hemograma.Pruebas: use ID_TEST|Nombre|true o false, separados por punto y coma, sin IDs repetidos.");
                result.Add(new TestDefinition { Id = id, Name = parts[1].Trim(), Selected = selected });
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
            if (settings.SelectSingleNode("add[@key='Hemograma.Pruebas']") != null) return;
            settings.AppendChild(doc.CreateComment(" ID_TEST|Nombre visible|Seleccionada al iniciar (true/false). Separar pruebas con ;. Reiniciar después de editar. "));
            var node = doc.CreateElement("add"); node.SetAttribute("key", "Hemograma.Pruebas"); node.SetAttribute("value", DefaultValue); settings.AppendChild(node);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            doc.Save(temporary); File.Replace(temporary, path, null);
        }
    }
}
