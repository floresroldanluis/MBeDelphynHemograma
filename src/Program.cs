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
                    Application.Run(new MainForm(Deployment.ConnectionString()));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "eDelphyn", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
