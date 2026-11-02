using System;
using System.Configuration;
using System.Windows.Forms;

namespace Hemograma
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--self-test") { Environment.Exit(SelfTest.Run()); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                using (var mutex = new System.Threading.Mutex(false, "Local\\eDelphyn.Hemograma.Running"))
                    {
                    TestSettings.Ensure(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
                    ConfigurationManager.RefreshSection("appSettings");
                    TestSettings.Parse(ConfigurationManager.AppSettings["Hemograma.Pruebas"] ?? TestSettings.DefaultValue);
                    string connection = Deployment.ConnectionString();
                    SqlSettings.ProtectConfiguration(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
                    ConfigurationManager.RefreshSection("connectionStrings");
                    using (var login = new LoginForm(connection))
                        if (login.ShowDialog() == DialogResult.OK)
                        {
                            TestSettings.Load();
                            Application.Run(new MainForm(connection, login.Identity));
                        }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "eDelphyn", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
