using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace Hemograma
{
    internal static class Deployment
    {
        internal const string PublisherThumbprint = "98D75A8F4EE534F364AA20637458BF471147F6C4";
        internal static string ConnectionString()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "connection.ini");
            return SqlSettings.Connection(path, ConfigurationManager.ConnectionStrings["eDelphyn"].ConnectionString);
        }
        internal static string StageLocal(string path)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eDelphyn.Hemograma", "Actualizaciones", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string destination = Path.Combine(folder, "eDelphyn-Setup.exe");
            File.Copy(path, destination, false);
            return destination;
        }
        internal static void InstallAndRestart(Form owner, string path)
        {
            try
            {
                ValidateInstaller(path);
                string directory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                Process.Start(new ProcessStartInfo(path, "/APPUPDATE=1 /DIR=\"" + directory + "\"") { UseShellExecute = true });
                owner.Close();
            }
            catch (Exception ex) { MessageBox.Show(owner, ex.Message, "No se pudo iniciar la actualización", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        internal static Version ValidateInstaller(string path)
        { return ValidateInstaller(path, Assembly.GetExecutingAssembly().GetName().Version); }
        internal static Version ValidateInstaller(string path, Version installedVersion)
        {
            Signature.Verify(path);
            using (var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path)))
                if (!String.Equals(cert.Thumbprint, PublisherThumbprint, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("El instalador no está firmado por el editor configurado de esta aplicación.");
            var info = FileVersionInfo.GetVersionInfo(path);
            if ((info.ProductName ?? "").Trim() != "eDelphyn Hemograma Installer") throw new InvalidOperationException("El archivo no es un instalador de eDelphyn Hemograma.");
            var version = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
            if (version <= installedVersion)
                throw new InvalidOperationException("El instalador es de la misma versión o de una anterior. Ya tiene la versión " + Application.ProductVersion + ".");
            return version;
        }
    }
    internal static class Signature
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct FileInfo { public uint Size; public string Path; public IntPtr Handle, Subject; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct TrustData
        {
            public uint Size; public IntPtr Policy, Sip; public uint UI, Revocation, Choice;
            public IntPtr File; public uint StateAction; public IntPtr State, Url;
            public uint Flags, Context;
        }
        [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        static extern int WinVerifyTrust(IntPtr window, [In] ref Guid action, ref TrustData data);
        internal static void Verify(string path)
        {
            var file = new FileInfo { Size = (uint)Marshal.SizeOf(typeof(FileInfo)), Path = path };
            IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FileInfo)));
            Marshal.StructureToPtr(file, pointer, false);
            var data = new TrustData { Size = (uint)Marshal.SizeOf(typeof(TrustData)), UI = 2, Choice = 1, File = pointer, StateAction = 1, Flags = 0x80 };
            var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            try
            {
                int result = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
                if (result != 0) throw new InvalidOperationException("Windows no valida la firma del instalador (0x" + result.ToString("X8") + "). Solicite a TI un instalador firmado y de confianza.");
            }
            finally
            {
                data.StateAction = 2; WinVerifyTrust(new IntPtr(-1), ref action, ref data);
                Marshal.DestroyStructure(pointer, typeof(FileInfo)); Marshal.FreeHGlobal(pointer);
            }
        }
    }
}

