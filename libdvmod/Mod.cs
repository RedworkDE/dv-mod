using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace libdvmod
{
    public class Mod
    {
        /// <inheritdoc />
        public Mod([NotNull] ModData modData)
        {
            ModData = modData;
        }

        [NotNull] public ModData ModData { get; }
        [CanBeNull] public Folder BundledFiles { get; set; }
        public bool Installed { get; set; }
        [CanBeNull] public string InstalledVersion { get; set; }
        public bool UserInstalled { get; set; }
        [CanBeNull] public string UserInstalledVersion { get; set; }

        public string TargetVersion { get; set; }
        public Folder TargetFiles { get; set; }
    }
}