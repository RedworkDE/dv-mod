using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace libdvmod
{
    public static class AssetProcessor
    {
        [NotNull]
        public static FileTree Load([NotNull] FileTree data)
        {
            if (data is File) return data;
            if (!(data is Folder folder)) throw new InvalidOperationException();

            List<ModAsset> assets = null;
            if (folder.TryGetFile("meta.json", out var meta))
            {
                using (var sr = new StreamReader(meta.Data))
                using (var jr = new JsonTextReader(sr))
                    assets = new JsonSerializer().Deserialize<List<ModAsset>>(jr);
            }

            SetProcessors(folder, folder, assets ?? new List<ModAsset>(), "/");

            return folder;
        }


        private static void SetProcessors([NotNull] Folder root, [NotNull] Folder folder, [ItemNotNull, NotNull] List<ModAsset> assets, [NotNull] string archivePath)
        {
            for (var i = 0; i < folder.Children.Count; i++)
            {
                var child = folder.Children[i];
                foreach (var asset in assets)
                {
                    if (IsMatch(asset.Origin.AbsolutePath, archivePath, child.Name, child is File))
                    {
                        if (asset.Processor == ModAssetProcessor.Archive && child is File f)
                        {
                            child = ArchiveExtractor.Read(f.Data, asset.ArchivePath);
                            if (child is Folder fo)
                            {
                                Load(fo);
                                if (string.IsNullOrEmpty(asset.Path)) folder.AddItems(fo.Children);
                            }
                        }

                        if (!string.IsNullOrEmpty(asset.Path))
                        {
                            folder.Children.RemoveAt(i);
                            i--;

                            var split = asset.Path.Split('/');
                            var e = ((IEnumerable<string>)split).GetEnumerator();
                            var croot = folder;
                            if (split[0] == "")
                            {
                                e.MoveNext();
                                croot = root;
                            }

                            // TODO: set Processor to merge on created folders
                            if (child is Folder fo)
                            {
                                var c = croot.PathAddFolder(e);
                                c.AddItems(fo.Children);
                                while (c != null && c.Processor == ModAssetProcessor.None)
                                {
                                    c.Processor = ModAssetProcessor.MergeFolder;
                                    c = c.Parent;
                                }
                            }
                            else if (child is File fi)
                            {
                                child = croot.PathAddFile(e, fi.FileData);
                            }
                        }

                        if (asset.Processor != ModAssetProcessor.Archive) child.Processor = asset.Processor;
                    }
                }

                if (child.Processor == ModAssetProcessor.None &&
                    (archivePath != "/" || !string.Equals(child.Name, "meta.json", StringComparison.OrdinalIgnoreCase) && !string.Equals(child.Name, "dvmod.json", StringComparison.OrdinalIgnoreCase)) &&
                    child is File)
                    child.Processor = ModAssetProcessor.Copy;

                if (child is Folder fol)
                {
                    child.Processor = ModAssetProcessor.MergeFolder;
                    SetProcessors(root, fol, assets, archivePath + child.Name + "/");
                }
            }
        }
        
        private static bool IsMatch([NotNull] string match, [NotNull] string path, [NotNull] string name, bool isFile, int matchPos = 0, int pathPos = 0)
        {
            return string.Equals(match, path + name + (isFile ? "" : "/"), StringComparison.OrdinalIgnoreCase);

            // TODO: actually match something

            //while (matchPos < match.Length && pathPos < path.Length)
            //{

            //}
        }

        public static void LoadAsset([NotNull] Folder root, [NotNull] ModAsset asset, [CanBeNull] Folder bundledFiles)
        {
            FileTree data;
            if (asset.Origin.Scheme == "dvmod" && asset.Origin.Host == "current-archive")
            {
                if (bundledFiles is null) throw new InvalidOperationException("reference ro current-archive from standalone mod info");
                data = bundledFiles.GetPath(asset.Origin.AbsolutePath.TrimStart('/')) ?? throw new FileNotFoundException("referenced file was not found", asset.Origin.ToString());
            }
            else
            {
                var stream = DownloadManager.Download(asset.Origin) ?? throw new FileNotFoundException("unable to download asset", asset.Origin.ToString());
                data = new File("", FileData.FromStream(stream), null);
            }

            if (asset.Processor == ModAssetProcessor.Archive && data is File file)
            {
                data = ArchiveExtractor.Read(file.Data, asset.ArchivePath);
                if (data is Folder fo)
                {
                    Load(fo);
                    if (string.IsNullOrEmpty(asset.Path)) root.AddItems(fo.Children);
                }
            }

            if (!string.IsNullOrEmpty(asset.Path))
            {
                if (data is Folder fo)
                {
                    var c = root.PathAddFolder(asset.Path);
                    c.AddItems(fo.Children);
                    while (c != null && c.Processor == ModAssetProcessor.None)
                    {
                        c.Processor = ModAssetProcessor.MergeFolder;
                        c = c.Parent;
                    }
                }
                else if (data is File fi)
                {
                    data = root.PathAddFile(asset.Path, fi.FileData);
                }
            }
            else if (data is File)
                throw new InvalidOperationException("file assets must specify a folder");

            if (asset.Processor != ModAssetProcessor.Archive) data.Processor = asset.Processor;
            if (data.Processor == ModAssetProcessor.None) data.Processor = data is File ? ModAssetProcessor.Copy : ModAssetProcessor.MergeFolder;
            data = data.Parent;
            while (data != null && data.Processor == ModAssetProcessor.None)
            {
                data.Processor = ModAssetProcessor.MergeFolder;
                data = data.Parent;
            }
        }

        // todo: ensure processors are propagated correctly (in AddItems etc)
        [NotNull]
        public static Folder Merge([NotNull] Folder existing, [NotNull] Folder added)
        {
            for (var i = 0; i < added.Children.Count; i++)
            {
                var child = added.Children[i];
                if (child is Folder folder)
                {
                    Folder target;
                    switch (folder.Processor)
                    {
                        case ModAssetProcessor.MergeFolder:
                            target = existing.GetOrAddFolder(folder.Name);
                            Merge(target, folder);
                            break;
                        case ModAssetProcessor.NewFolder:
                            target = existing.GetFolder(folder.Name);
                            if (target is object) continue;
                            target = existing.GetOrAddFolder(folder.Name);
                            goto insertFiles;
                        case ModAssetProcessor.ReplaceFolder:
                            target = existing.GetOrAddFolder(folder.Name);
                            target.Children.Clear();
                        insertFiles:
                            target.AddItems(folder.Children);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else if (child is File file)
                {
                    
                    switch (file.Processor)
                    {
                        case ModAssetProcessor.Ignore:
                            continue;
                        case ModAssetProcessor.CopyNew:
                            if (existing.GetFile(file.Name) is object) continue;
                            goto case ModAssetProcessor.Copy;
                        case ModAssetProcessor.Copy:
                            existing.ReplaceFile(file.Name, file.FileData);
                            break;
                        case ModAssetProcessor.IniOverwrite:
                        case ModAssetProcessor.IniAdd:
                            var old = existing.GetFile(file.Name);
                            if (old is null) goto case ModAssetProcessor.Copy;

                            using (var oldData = old.Data)
                            using (var sro = new StreamReader(oldData))
                            using (var newData = file.Data)
                            using (var srn = new StreamReader(newData))
                            using (var ms = new MemoryStream())
                            using (var sw = new StreamWriter(ms))
                            {
                                var oldFile = IniFile.Read(sro);
                                var newFile = IniFile.Read(srn);
                                oldFile.Merge(newFile, file.Processor == ModAssetProcessor.IniAdd ? IniMergeOption.None : IniMergeOption.OverwriteValues);
                                oldFile.Write(sw);
                                sw.Flush();
                                ms.Position = 0;
                                existing.ReplaceFile(file.Name, FileData.FromStream(ms));
                            }

                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }

            return existing;
        }
    }
}