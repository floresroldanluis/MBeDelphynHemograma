using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Hemograma
{
    internal sealed class UpdateRelease
    {
        public Version Version;
        public string Url, Name, Digest;
        public long Size;
    }
    internal static class GitHubUpdates
    {
        internal const string RepositoryUrl = "https://github.com/floresroldanluis/MBeDelphynHemograma";
        internal const string LatestUrl = "https://api.github.com/repos/floresroldanluis/MBeDelphynHemograma/releases/latest";
        const long MaxInstallerSize = 200L * 1024 * 1024;
        static HttpClient Client()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("eDelphyn-Hemograma/" + Assembly.GetExecutingAssembly().GetName().Version);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }
        internal static async Task<UpdateRelease> CheckAsync(CancellationToken token)
        {
            using (var client = Client()) using (var response = await client.GetAsync(LatestUrl, token))
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                    throw new InvalidOperationException("No hay una versión estable publicada en Releases, o el repositorio no está disponible públicamente.");
                if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
                    throw new InvalidOperationException("GitHub limitó temporalmente las consultas. Inténtelo más tarde.");
                response.EnsureSuccessStatusCode();
                return ParseRelease(await response.Content.ReadAsStringAsync());
            }
        }
        internal static UpdateRelease ParseRelease(string json)
        {
            var data = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Deserialize<Dictionary<string, object>>(json);
            if (Convert.ToBoolean(data["draft"]) || Convert.ToBoolean(data["prerelease"])) throw new InvalidOperationException("La versión no es una publicación estable.");
            string tag = Convert.ToString(data["tag_name"]).TrimStart('v', 'V');
            Version parsed;
            if (!Version.TryParse(tag, out parsed) || parsed.Build < 0) throw new InvalidOperationException("La versión publicada debe tener un número como v1.2.0.");
            var version = new Version(parsed.Major, parsed.Minor, parsed.Build, Math.Max(0, parsed.Revision));
            var release = new UpdateRelease { Version = version };
            string expected = "eDelphyn-Hemograma-Setup-" + version.ToString(3) + ".exe";
            var assets = data["assets"] as System.Collections.IList;
            if (assets == null) throw new InvalidOperationException("La publicación no contiene instaladores.");
            foreach (var item in assets)
            {
                var asset = (Dictionary<string, object>)item;
                if (!String.Equals(Convert.ToString(asset["name"]), expected, StringComparison.OrdinalIgnoreCase)) continue;
                string url = Convert.ToString(asset["browser_download_url"]);
                if (!url.StartsWith(RepositoryUrl + "/releases/download/", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("El instalador no pertenece al repositorio configurado.");
                long size = Convert.ToInt64(asset["size"]);
                if (size <= 0 || size > MaxInstallerSize) throw new InvalidOperationException("El tamaño del instalador publicado no es válido.");
                release.Url = url; release.Size = size; release.Name = expected;
                release.Digest = asset.ContainsKey("digest") ? Convert.ToString(asset["digest"]) : null;
                break;
            }
            return release;
        }
        internal static async Task<string> DownloadAsync(UpdateRelease release, IProgress<int> progress, CancellationToken token)
        {
            if (release == null || String.IsNullOrEmpty(release.Url)) throw new InvalidOperationException("La publicación no tiene el instalador esperado.");
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eDelphyn.Hemograma", "Actualizaciones", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "eDelphyn-Setup.exe");
            try
            {
                using (var client = Client()) using (var response = await client.GetAsync(release.Url, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();
                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        byte[] buffer = new byte[81920]; long total = 0; int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                        {
                            total += read;
                            if (total > release.Size || total > MaxInstallerSize) throw new IOException("La descarga supera el tamaño publicado.");
                            await output.WriteAsync(buffer, 0, read, token);
                            if (progress != null) progress.Report((int)(total * 100 / release.Size));
                        }
                        if (total != release.Size) throw new IOException("La descarga está incompleta. Vuelva a intentarlo.");
                    }
                }
                token.ThrowIfCancellationRequested();
                if (!String.IsNullOrWhiteSpace(release.Digest))
                {
                    using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file))
                    {
                        string digest = "sha256:" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                        if (!String.Equals(digest, release.Digest, StringComparison.OrdinalIgnoreCase)) throw new IOException("La descarga no coincide con la huella publicada por GitHub.");
                    }
                }
                Version verified = await Task.Run(() => Deployment.ValidateInstaller(file), token);
                if (verified != release.Version) throw new InvalidOperationException("La versión del instalador no coincide con la publicación.");
                return file;
            }
            catch { if (File.Exists(file)) File.Delete(file); throw; }
        }
    }
}
