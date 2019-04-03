using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using JetBrains.Annotations;

namespace libdvmod
{
    public static class ArchiveExtractor
    {
        [ItemNotNull, NotNull] private static List<Func<Stream, FileTree>> _loadArchive = new List<Func<Stream, FileTree>>();

        static ArchiveExtractor()
        {
            RegisterExtractor(stream =>
            {
                var archive = ZipStorer.Open(stream, FileAccess.Read);

                var root = Folder.Root;

                foreach (var entry in archive.ReadCentralDir())
                    if (!entry.FilenameInZip.EndsWith("/"))
                    {
                        var ms = new MemoryStream();
                        archive.ExtractFile(entry, ms);
                        ms.Position = 0;
                        root.PathAddFile(entry.FilenameInZip, FileData.FromStream(ms));
                    }

                return root;
            });
        }

        public static void RegisterExtractor([NotNull] Func<Stream, FileTree> loader)
        {
            lock(_loadArchive) _loadArchive.Add(loader);
        }

        [NotNull]
        public static FileTree Read([NotNull] Stream data, [CanBeNull] string root)
        {
            var ms = DownloadManager.MemoryCache(data);
            List <Func<Stream, FileTree>> funcs;
            lock (_loadArchive) funcs = _loadArchive.ToList();
            List<Exception> exs = null;
            exs = new List<Exception>();

            foreach (var func in funcs)
            {
                try
                {
                    var result = func(DownloadManager.MemoryCache(ms));
                    if (result is object)
                        if (root is object && result is Folder folder)
                            return folder.GetPath(root);
                        else
                            return result;
                }
                catch(Exception ex)
                {
                    exs.Add(ex);
                    // ignored
                }
            }
            throw new AggregateException("unable to extract archive", exs);
        }
    }
}