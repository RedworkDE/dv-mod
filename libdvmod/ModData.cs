using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace libdvmod
{
    public class ModData
    {
        /// <summary>
        /// Name of the mod
        /// </summary>
        [NotNull] public string Name { get; set; }
        /// <summary>
        /// Details about this mod, purpose, how to use, etc
        /// </summary>
        [CanBeNull] public string Description { get; set; }
        /// <summary>
        /// Author
        /// </summary>
        [CanBeNull] public string Author { get; set; }
        /// <summary>
        /// Canonical uri for metadata about this mod.
        /// If the mod has not been loaded from that uri it will be reloaded from there
        /// </summary>
        [CanBeNull] public Uri Origin { get; set; }
        /// <summary>
        /// Version history of this mod.
        /// Bigger indices are later versions.
        /// There must always be at least one version.
        /// All versions must have a unique name.
        /// </summary>
        [ItemNotNull, NotNull] public List<ModVersion> Versions { get; set; }
    }

    public class ModVersion
    {
        /// <summary>
        /// Name / Number / Identifier for this version
        /// Must be unique for all versions of a mod
        /// No other restrictions are enforced
        /// </summary>
        [CanBeNull] public string Name { get; set; }
        /// <summary>
        /// Description / Changelog
        /// </summary>
        [CanBeNull] public string Description { get; set; }
        /// <summary>
        /// True if this version is fully compatible with its predecessor and can always replace it (unless a dependent mod does some shady shit)
        /// null if this version is mostly compatible with its predecessor except for removed / deprecated api (this version can be used if the version behaviour of the dependent mod is Minimum, but not if it is Compatible)
        /// False if a mod requiring this mods predecessor or a compatible version is never compatible with this version
        /// </summary>
        [CanBeNull] public bool? IsCompatible { get; set; }
        /// <summary>
        /// This versions predecessor
        /// If null this versions predecessor is the prevision version in Versions
        /// </summary>
        [CanBeNull] public string Succeeds { get; set; }
        /// <summary>
        /// A list of all the assets that make up this version
        /// </summary>
        [ItemNotNull, CanBeNull] public List<ModAsset> Assets { get; set; }
        /// <summary>
        /// A list of all the mods that are required to run install this
        /// </summary>
        [ItemNotNull, CanBeNull] public List<ModDependency> Dependencies { get; set; }
    }

    public class ModAsset
    {
        /// <summary>
        /// The path / filename this asset will be copied to
        /// </summary>
        [CanBeNull] public string Path { get; set; }
        /// <summary>
        /// Uri at which this file can be downloaded
        /// </summary>
        [NotNull] public Uri Origin { get; set; }
        /// <summary>
        /// What actions to perform on this asset before saving it
        /// </summary>
        [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
        public ModAssetProcessor Processor { get; set; }
        /// <summary>
        /// Treat the specifies path inside the archive as its root
        /// </summary>
        public string ArchivePath { get; set; }
    }

    public enum ModAssetProcessor
    {
        /// <summary>
        /// Invalid / default value
        /// </summary>
        None,
        /// <summary>
        /// No action
        /// </summary>
        Ignore,
        /// <summary>
        /// Copy to the output
        /// </summary>
        Copy,
        /// <summary>
        /// Copy only if the file doesn't exist
        /// </summary>
        CopyNew,
        /// <summary>
        /// The file is an archive and must be extracted before saving
        /// </summary>
        Archive,
        /// <summary>
        /// The file is an ini-file and should be merged by overwriting the existing values with those in this file
        /// TODO
        /// </summary>
        IniOverwrite,
        /// <summary>
        /// The file is an ini-file and should be merged by adding the non-existing values to the existing file
        /// TODO
        /// </summary>
        IniAdd,
        /// <summary>
        /// This is a folder that will be merged with the existing one
        /// </summary>
        MergeFolder,
        /// <summary>
        /// This folder will replace the existing folder
        /// </summary>
        ReplaceFolder,
        /// <summary>
        /// This folder will only be applied when no folder of this name exists
        /// </summary>
        NewFolder,
    }

    public class ModDependency
    {
        /// <summary>
        /// Mod this is required
        /// </summary>
        [NotNull] public Uri Origin { get; set; }
        /// <summary>
        /// Version that is required
        /// </summary>
        [CanBeNull] public string Version { get; set; }
        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
        public VersionBehaviour VersionBehaviour { get; set; }
    }

    public enum VersionBehaviour
    {
        /// <summary>
        /// There are no requirements on the version of the dependency
        /// </summary>
        Any,
        /// <summary>
        /// Use the latest successor version
        /// </summary>
        Latest,
        /// <summary>
        /// Use the latest successor version that is not not compatible
        /// </summary>
        Minimum,
        /// <summary>
        /// Use the latest successor version that is compatible
        /// </summary>
        Compatible,
        /// <summary>
        /// Use the specified version
        /// </summary>
        Exact,
    }
}