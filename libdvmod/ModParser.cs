using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace libdvmod
{
    public static class ModParser
    {
        [NotNull]
        public static Mod DownloadMod([NotNull] Uri uri)
        {
            if (uri is null) throw new ArgumentNullException(nameof(uri));

            var stream = DownloadManager.Download(uri) ?? throw new FileNotFoundException("failed to download mod", uri.ToString());
            stream = DownloadManager.MemoryCache(stream);
            var data = LoadData(stream, out var archive);

            if (data.Origin is null) data.Origin = uri;
            else if (data.Origin != uri) return DownloadMod(data.Origin);

            return new Mod(data) {BundledFiles = archive};
        }

        [NotNull]
        private static ModData LoadData([NotNull] Stream stream, [CanBeNull] out Folder archive)
        {
            archive = null;

            try
            {
                using (var sr = new StreamReader(DownloadManager.MemoryCache(stream)))
                using (var jr = new JsonTextReader(sr))
                    return new JsonSerializer().Deserialize<ModData>(jr) ?? throw new InvalidOperationException("failed to read metadata");
            }
            catch(JsonReaderException)
            {
            }

            var contents = AssetProcessor.Load(ArchiveExtractor.Read(DownloadManager.MemoryCache(stream), null));
            if (contents is File file) return LoadData(file.Data, out archive);
            if (!(contents is Folder folder)) throw new InvalidOperationException();
            
            if (folder.TryGetFile("dvmod.json", out var dvmod))
                using (var sr = new StreamReader(dvmod.Data))
                using (var jr = new JsonTextReader(sr))
                    return new JsonSerializer().Deserialize<ModData>(jr) ?? throw new InvalidOperationException("failed to read metadata");

            return GuessModData(folder);
        }
        
        [NotNull]
        private static ModData GuessModData([NotNull] Folder data)
        {
            // todo: better guesses

            return new ModData()
            {
                Name = "Unknown Mod",
                Author = "Unknown Author",
                Versions =
                {
                    new ModVersion()
                    {
                        Name = "current",
                        Assets = new List<ModAsset>()
                        {
                            new ModAsset()
                            {
                                Origin = new Uri("dvmod://current-archive/")
                            }
                        }
                    }
                }
            };
        }
    }
}