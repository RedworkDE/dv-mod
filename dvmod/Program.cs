using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using libdvmod;
using Microsoft.Win32;
using File = System.IO.File;

namespace dvmod
{
    public static class Program
    {
        public static bool IsStandalone;
        private static string[] _args;
        private const string GAME_EXE = "DerailValley.exe";

        public static void Main(string[] args)
        {
            //MessageBox.Show("loading");

            if (args is object && args.Length == 2 && args[1] == "--doorstop-invoke")
            {
                _args = args;
                args = new string[Environment.GetCommandLineArgs().Length - 1];
                Array.Copy(Environment.GetCommandLineArgs(), 1, args, 0, args.Length);
                IsStandalone = false;
            }
            else
            {
                FindBasePath();
                IsStandalone = true;
            }

            //MessageBox.Show(string.Join("\n", args));

            if (!File.Exists(GAME_EXE))
            {
                MessageBox.Show("game is gone?");
                Environment.Exit(-1);
            }

            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                var assmName = new AssemblyName(eventArgs.Name);

                if (assmName.Name == "libdvmod")
                {
                    try
                    {
                        using (var data = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Program), "libdvmod.dll"))
                        {
                            var ms = new MemoryStream();
                            data.CopyTo(ms);
                            return Assembly.Load(ms.ToArray());
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex);
                    }
                }

                if (assmName.Name == "Newtonsoft.Json")
                {
                    try
                    {
                        // this breaks patching of newtonsoft.json via BepInEx preloader
                        Assembly.LoadFile(Path.GetFullPath("DerailValley_Data\\Managed\\Newtonsoft.Json.dll"));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex);
                    }
                }

                return null;
            };
            
            try
            {
                MainInner(args);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex);
            }
        }

        private static void MainInner(string[] args)
        {
            // this can't be in Main as otherwise libdvmod may not be loaded
            Installation.Default = new Installation(Environment.CurrentDirectory);

            if (HandleArgs(args))
            {
                // MessageBox.Show("continue to game");

                if (!IsStandalone) ChainDoorstop();

                return;
            }

            //MessageBox.Show("terminate process");
            Environment.Exit(0);
        }

        private static void ChainDoorstop()
        {
            try
            {
                IniFile ini;
                using (var fs = new FileStream("doorstop_config.ini", FileMode.Open, FileAccess.ReadWrite))
                {
                    using (var sr = new StreamReader(fs, Encoding.UTF8, true, /*StreamReader.DefaultBufferSize*/1024, true))
                        ini = IniFile.Read(sr);

                    try
                    {
                        if (File.Exists("doorstop_config.ini.old"))
                        {
                            IniFile ini2;
                            using (var sr = new StreamReader("doorstop_config.ini.old"))
                                ini2 = IniFile.Read(sr);
                            ini.Merge(ini2, IniMergeOption.None);

                            var target = ini2.GetValue("UnityDoorstop", "targetAssembly");
                            var currentTarget = ini.GetValue("UnityDoorstop", "targetAssembly");

                            if (target is object && target != currentTarget)
                            {
                                var section = ini.GetSection("ChainDoorstop");

                                if (section is null || section.Entries.TrueForAll(e => e.GetValue() != target))
                                    ini.Merge(IniFile.Read($"[ChainDoorstop]\n5000_{target.GetHashCode():X8}={target}"), IniMergeOption.None);
                            }

                            fs.Position = 0;
                            fs.SetLength(0);
                            using (var sw = new StreamWriter(fs, Encoding.UTF8, /*StreamReader.DefaultBufferSize*/1024, true))
                                ini.Write(sw);

                            File.Delete("doorstop_config.ini.old");
                        }
                    }
                    catch { }
                }

                foreach (var target in ini.GetSection("ChainDoorstop")?.Entries.Select(IniExtender.GetValue).OrderBy(s => s) ?? Enumerable.Empty<string>())
                    try
                    {
                        ChainDoorstop(target, _args);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to chain doorstop '{target}': {ex}");
                    }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to chain doorstop: {ex}");
            }
        }

        private static void ChainDoorstop(string targetAssembly, string[] args)
        {
            var assembly = Assembly.LoadFile(Path.GetFullPath(targetAssembly));


            var entryPoint = assembly.EntryPoint ?? assembly.GetExportedTypes().Where(type => !type.IsGenericType).SelectMany(type =>
                                     type.GetMethods().Where(method =>
                                         method.IsStatic && method.Name == "Main" && (method.GetParameters().Length == 0 ||
                                                                                      method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(string[]))))
                                 .FirstOrDefault();

            if (entryPoint == null) return;

            entryPoint.Invoke(null, entryPoint.GetParameters().Length == 0 ? null : new object[] { args });
        }

        private static void FindBasePath()
        {
            if (File.Exists(GAME_EXE)) return;
            Environment.CurrentDirectory = Path.GetDirectoryName(Environment.CurrentDirectory);
            if (File.Exists(GAME_EXE)) return;
            Environment.CurrentDirectory = Path.GetDirectoryName(Environment.CurrentDirectory);
            if (File.Exists(GAME_EXE)) return;
            Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().ManifestModule.FullyQualifiedName);
            if (File.Exists(GAME_EXE)) return;
            Environment.CurrentDirectory = Path.GetDirectoryName(Environment.CurrentDirectory);
            if (File.Exists(GAME_EXE)) return;
            Environment.CurrentDirectory = Path.GetDirectoryName(Environment.CurrentDirectory);
            if (File.Exists(GAME_EXE)) return;

            MessageBox.Show($"{Path.GetFileName(Assembly.GetExecutingAssembly().ManifestModule.FullyQualifiedName)} must be placed at most two directories deep next to {GAME_EXE}.");
            Environment.Exit(-1);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        private static bool HandleArgs(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                RegisterUrlHandler();
                CheckForUpdates();


                if (IsStandalone) ShowGui();

                return true;
            }

            int offset = string.Equals(args[0], "dvmod-handler", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            if (offset == 0 && !IsStandalone) return true;

            RegisterUrlHandler();
            if (string.Equals(args[offset], "init-url", StringComparison.OrdinalIgnoreCase)) return false;

            if (string.Equals(args[offset], "url", StringComparison.OrdinalIgnoreCase)) return HandleUrl(true);
            if (string.Equals(args[offset], "install", StringComparison.OrdinalIgnoreCase)) return HandleUrl(false);

            return false;
        }

        public static void RegisterUrlHandler()
        {
            try
            {
                using (var key = Registry.ClassesRoot.OpenSubKey("dvmod"))
                    if (key is object)
                        return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex);
            }

            try
            {
                using (var dvmod = Registry.ClassesRoot.CreateSubKey("dvmod"))
                {
                    dvmod.SetValue("URL Protocol", "");
                    using (var shell = dvmod.CreateSubKey("shell"))
                    using (var open = shell.CreateSubKey("open"))
                    using (var command = open.CreateSubKey("command"))
                        command.SetValue(null, $"\"{(IsStandalone ? Assembly.GetEntryAssembly().ManifestModule.FullyQualifiedName : Path.GetFullPath(GAME_EXE))}\" dvmod-handler url %1");
                }
            }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    var process = new Process()
                    {
                        StartInfo =
                        {
                            FileName = Environment.GetCommandLineArgs()[0],
                            Arguments = "init-url",
                            Verb = "runas"
                        }
                    };
                    process.Start();
                    process.WaitForExit();
                }
                catch
                {
                    // it works or it doesn't we don't really care here
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex);
            }
        }

        public static bool HandleUrl(bool protocolHandler)
        {
            var idx = Environment.CommandLine.IndexOf(protocolHandler ? " url " : " install ", StringComparison.OrdinalIgnoreCase);
            if (idx == -1) return false;
            var url = Environment.CommandLine.Substring(idx + (protocolHandler ? 5 : 9));

            try
            {
                var mod = ModParser.DownloadMod(new Uri(url));

                if (protocolHandler && MessageBox.Show($"Do you want to install the mod {mod.ModData.Name} by {mod.ModData.Author}?", buttons: MessageBoxButtons.YesNo) != DialogResult.Yes) return false;

                Installation.Default.InstallMod(mod);
                Installation.Default.Apply();

                if (!IsStandalone)
                    return MessageBox.Show($"Successfully installed the mod {mod.ModData.Name} by {mod.ModData.Author}.\n\nDo you want to start the game now?", buttons: MessageBoxButtons.YesNo) == DialogResult.Yes;
                MessageBox.Show($"Successfully installed the mod {mod.ModData.Name} by {mod.ModData.Author}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to install mod: " + ex);
            }

            return false;
        }

        public static void CheckForUpdates()
        {
            // TODO
        }


        public static void InstallOrUpdateLoader()
        {

        }

        public static void ShowGui()
        {

        }
    }
}
