using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace libdvmod
{
    public abstract class FileTree
    {
        /// <inheritdoc />
        protected FileTree([NotNull] string name, [CanBeNull] Folder parent)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Parent = parent;
        }

        [NotNull] public string Name { get; }
        [CanBeNull] public Folder Parent { get; }
        public ModAssetProcessor Processor { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Parent + Name;
        }
    }

    public class File : FileTree
    {
        /// <inheritdoc />
        public File([NotNull] string name, [NotNull] FileData fileData, [CanBeNull] Folder parent) : base(name, parent)
        {
            FileData = fileData;
        }

        [NotNull] public FileData FileData { get; }
        [NotNull] public Stream Data => FileData.GetStream() ?? throw new InvalidOperationException("failed to create stream");
    }

    public class FileData
    {
        [CanBeNull] private Stream _data;
        [CanBeNull] private string _file;

        private FileData() { }

        [NotNull]
        public static FileData FromStream([NotNull] Stream data)
        {
            return new FileData()
            {
                _data = DownloadManager.MemoryCache(data)
            };
        }

        [NotNull]
        public static FileData FromFile([NotNull] string file)
        {
            return new FileData(){_file = file};
        }

        [NotNull]
        public Stream GetStream()
        {
            if (_data != null) return DownloadManager.MemoryCache(_data);
            if (_file != null)
            {
                using (var fs = new FileStream(_file, FileMode.Open)) _data = DownloadManager.MemoryCache(fs);
                return DownloadManager.MemoryCache(_data);
            }

            throw new InvalidOperationException();
        }
        
        public void Apply([NotNull] string file)
        {
            if (file == _file) return;

            using (var fs = new FileStream(file, FileMode.Create))
            {
                if (_data is object)
                    DownloadManager.MemoryCache(_data).CopyTo(fs);
                else if (_file is object)
                    System.IO.File.Copy(_file, file, true);
            }
        }
    }

    public class Folder : FileTree
    {
        /// <inheritdoc />
        public Folder([NotNull] string name, [NotNull] Folder parent) : base(name, parent)
        {
        }

        // ReSharper disable once AssignNullToNotNullAttribute -> it is actually allowed, but shouldn't happen except here
        [NotNull] public static Folder Root => new Folder("", null);

        [ItemNotNull, NotNull] public List<FileTree> Children { get; } = new List<FileTree>();

        /// <inheritdoc />
        public override string ToString()
        {
            return base.ToString() + "/";
        }
    }

    public static class FileTreeExtender
    {
        [CanBeNull]
        public static FileTree Get([NotNull] this Folder folder, [NotNull] string name)
        {
            if (name == ".") return folder;
            if (name == "..") return folder.Parent;


            for (var i = 0; i < folder.Children.Count; i++)
            {
                var child = folder.Children[i];
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                    return child;
            }

            return null;
        }

        [CanBeNull]
        public static File GetFile([NotNull] this Folder folder, [NotNull] string name)
        {
            for (var i = 0; i < folder.Children.Count; i++)
            {
                var child = folder.Children[i];
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (child is File file) return file;
                    return null;
                }
            }

            return null;
        }

        public static bool TryGetFile([NotNull] this Folder folder, [NotNull] string name, [CanBeNull] out File file)
        {
            for (var i = 0; i < folder.Children.Count; i++)
            {
                var child = folder.Children[i];
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    file = child as File;
                    return file is object;
                }
            }

            file = null;
            return false;
        }

        [CanBeNull]
        public static Folder GetFolder([NotNull] this Folder folder, [NotNull] string name)
        {
            if (name == ".") return folder;
            if (name == "..") return folder.Parent;


            for (var i = 0; i < folder.Children.Count; i++)
            {
                var child = folder.Children[i];
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (child is Folder file) return file;
                    return null;
                }
            }

            return null;
        }
        
        public static bool TryGetFolder([NotNull] this Folder folder, [NotNull] string name, out Folder file)
        {
            if (name == ".")
            {
                file = folder;
                return file is object;
            }

            if (name == "..")
            {
                file = folder.Parent;
                return file is object;
            }

            for (var i = 0; i < folder.Children.Count; i++)
            {
                var child = folder.Children[i];
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    file = child as Folder;
                    return file is object;
                }
            }

            file = null;
            return false;
        }
        
        [NotNull]
        public static Folder GetOrAddFolder([NotNull] this Folder folder, [NotNull] string name)
        {
            if (name == ".") return folder;
            if (name == "..") return folder.Parent ?? throw new InvalidOperationException("cannot get parent of directory root");

            for (var i = 0; i < folder.Children.Count; i++)
            {
                var child = folder.Children[i];
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (child is Folder f) return f;
                    throw new InvalidOperationException("File with this name already exists");
                }
            }

            var dir = new Folder(name, folder);
            folder.Children.Add(dir);
            return dir;
        }

        [NotNull]
        public static File AddFile([NotNull] this Folder folder, [NotNull] string name, [NotNull] FileData fileData)
        {
            for (var i = 0; i < folder.Children.Count; i++)
                if (string.Equals(folder.Children[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("File with this name already exists");

            var file = new File(name, fileData, folder);
            folder.Children.Add(file);
            return file;
        }

        [NotNull]
        public static File ReplaceFile([NotNull] this Folder folder, [NotNull] string name, [NotNull] FileData fileData)
        {
            File file;
            for (var i = 0; i < folder.Children.Count; i++)
            {
                var child = folder.Children[i];
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (child is Folder) throw new InvalidOperationException("Folder with this name already exists");
                    file = new File(name, fileData, folder);
                    folder.Children[i] = file;
                    return file;
                }
            }

            file = new File(name, fileData, folder);
            folder.Children.Add(file);
            return file;
        }

        [NotNull] public static File PathAddFile([NotNull] this Folder folder, [NotNull] string path, [NotNull] FileData fileData) => folder.PathAddFile(((IEnumerable<string>) path.Split('/')).GetEnumerator(), fileData);

        [NotNull] public static File PathAddFile([NotNull] this Folder folder, [NotNull] IEnumerable<string> path, [NotNull] FileData fileData) => folder.PathAddFile(path.GetEnumerator(), fileData);

        [NotNull]
        public static File PathAddFile([NotNull] this Folder folder, [NotNull] IEnumerator<string> path, [NotNull] FileData fileData)
        {
            string name = null;
            while (path.MoveNext())
            {
                if (name is object)
                    folder = folder.GetOrAddFolder(name);
                name = path.Current ?? throw new InvalidOperationException("name part is null");
            }

            if (name is null) throw new InvalidOperationException("empty path");
            return folder.AddFile(name, fileData);
        }

        [NotNull] public static Folder PathAddFolder([NotNull] this Folder folder, [NotNull] string path) => folder.PathAddFolder(((IEnumerable<string>) path.Split('/')).GetEnumerator());

        [NotNull] public static Folder PathAddFolder([NotNull] this Folder folder, [NotNull] IEnumerable<string> path) => folder.PathAddFolder(path.GetEnumerator());

        [NotNull]
        public static Folder PathAddFolder([NotNull] this Folder folder, [NotNull] IEnumerator<string> path)
        {
            while (path.MoveNext()) folder = folder.GetOrAddFolder(path.Current ?? throw new InvalidOperationException("name part is null"));
            return folder;
        }

        public static void AddItems([NotNull] this Folder root, [ItemNotNull, NotNull] IEnumerable<FileTree> items)
        {
            foreach (var item in items)
            {
                if (item is Folder folder)
                {
                    var f = root.GetOrAddFolder(folder.Name);
                    f.Processor = folder.Processor;
                    f.AddItems(folder.Children);
                }
                else if (item is File file)
                {
                    root.AddFile(file.Name, file.FileData).Processor = file.Processor;
                }
            }
        }

        [CanBeNull] public static FileTree GetPath([NotNull] this Folder folder, [NotNull] string path) => folder.GetPath(((IEnumerable<string>)path.Split('/')).GetEnumerator());

        [CanBeNull] public static FileTree GetPath([NotNull] this Folder folder, [NotNull] IEnumerable<string> path) => folder.GetPath(path.GetEnumerator());

        [CanBeNull]
        public static FileTree GetPath([NotNull] this Folder folder, [NotNull] IEnumerator<string> path)
        {
            FileTree item = folder;
            while (path.MoveNext())
            {
                if (!(item is Folder f)) throw new InvalidOperationException("trying to get item child from file");
                item = f.Get(path.Current ?? throw new InvalidOperationException("path path is null"));
                if (item is null) return null;
            }
            path.Dispose();

            return item;
        }
    }
}