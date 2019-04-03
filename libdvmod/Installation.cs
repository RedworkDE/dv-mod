using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace libdvmod
{
    public class Installation
    {
        private static Installation _default;

        [NotNull]
        public static Installation Default
        {
            get => _default ?? throw new InvalidOperationException("Default installation has not been initialized");
            set => _default = _default ?? value ?? throw new ArgumentNullException(nameof(value));
        }

        // TODO: version management
        // TODO: improve/fix dependency resolution
        public Installation([NotNull] string path)
        {
            if (path is null) throw new ArgumentNullException(nameof(path));
            if (!Directory.Exists(path)) throw new ArgumentException("Path must exist", nameof(path));

            Path = path;

            LoadModState();
        }

        [NotNull] public string Path { get; }
        [NotNull] public Folder InstalledFiles { get; } = Folder.Root;
        [NotNull] public Dictionary<Uri, Mod> Mods { get; } = new Dictionary<Uri, Mod>();

        private void LoadModState()
        {

        }

        private void SaveModState()
        {

        }

        public void Install([NotNull] string mod) => Install(new Uri(mod));
        public void Install([NotNull] Uri mod) => InstallMod(ModParser.DownloadMod(mod));
        public void InstallMod([NotNull] Mod mod)
        {
            if (mod is null) throw new ArgumentNullException(nameof(mod));

            mod.UserInstalled = true;

            if (Mods.TryGetValue(mod.ModData.Origin.NotNull(), out var tmp))
            {
                mod = tmp;
                mod.UserInstalled = true;
                if (mod.Installed) return;
            }
            else
            {
                Mods.Add(mod.ModData.Origin.NotNull(), mod);
            }
        }

        public void Apply()
        {
            var targetSet = CollectDependencies(Mods.Values.Where(v => v.UserInstalled));

            DownloadMods(targetSet);

            var targetInstall = targetSet.Reverse().Select(m => m.TargetFiles).Where(f => f is object).Aggregate(GetCurrentFiles(), AssetProcessor.Merge);

            ApplyInstallation(Folder.Root, targetInstall, Path);
        }

        private Folder GetCurrentFiles()
        {
            var folder = Folder.Root;

            var dsc = System.IO.Path.Combine(Path, "doorstop_config.ini");
            if (System.IO.File.Exists(dsc))
                folder.AddFile("doorstop_config.ini", FileData.FromFile(dsc));

            return folder;
        }

        private void ApplyInstallation([NotNull] Folder existing, [NotNull] Folder target, [NotNull] string path)
        {
            foreach (var child in target.Children)
            {
                var dir = System.IO.Path.Combine(path, child.Name);
                if (child is File file)
                    file.FileData.Apply(dir);
                else if (child is Folder folder)
                {
                    Directory.CreateDirectory(dir);
                    ApplyInstallation(/*TODO*/ existing, folder, dir);
                }
            }
        }

        [NotNull, ItemNotNull]
        private IEnumerable<Mod> CollectDependencies([ItemNotNull, NotNull] IEnumerable<Mod> initial)
        {
            var queue = new Queue<Mod>(initial);
            var mods = new Dictionary<Uri, Mod>(queue.ToDictionary(mod => mod.ModData.Origin));

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var version = current.ModData.Versions.Last();

                current.TargetVersion = version.Name;

                if (version.Dependencies == null || version.Dependencies.Count == 0) continue;

                foreach (var dep in version.Dependencies)
                {
                    if (mods.ContainsKey(dep.Origin)) continue;
                    var mod = ModParser.DownloadMod(dep.Origin);
                    if (mods.ContainsKey(mod.ModData.Origin)) continue;

                    mods.Add(mod.ModData.Origin, mod);
                    queue.Enqueue(mod);
                }
            }

            return mods.Values;
        }

        private void DownloadMods([ItemNotNull, NotNull] IEnumerable<Mod> mods)
        {
            foreach (var mod in mods)
                DownloadMod(mod, mod.TargetVersion);
        }

        private void DownloadMod([NotNull] Mod mod, [NotNull] string versionName)
        {
            var version = mod.ModData.Versions.Single(ver => ver.Name == versionName);

            if (version.Assets == null || version.Assets.Count == 0) return;

            var folder = Folder.Root;

            foreach(var asset in version.Assets) AssetProcessor.LoadAsset(folder, asset, mod.BundledFiles);

            mod.TargetFiles = folder;
        }
    }
}


