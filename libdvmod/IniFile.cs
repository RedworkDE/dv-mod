using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using JetBrains.Annotations;

namespace libdvmod
{
    public class IniFile
    {
        [NotNull] public static IniFile Read([NotNull] string data) => Read(new StringReader(data));
        [NotNull] public static IniFile Read([NotNull] Stream stream) => Read(new StreamReader(stream));
        [NotNull] public static IniFile Read([NotNull] TextReader reader) => Read(reader.ReadLine);
        [NotNull]
        public static IniFile Read([NotNull] Func<string> readLine)
        {
            var ini = new IniFile();

            List<string> comment = new List<string>();
            IniSection section = new IniSection();
            int start = 0;

            string line;
            while ((line = readLine()) != null)
            {
                int state = 0;

                for (int i = 0; i < line.Length; i++)
                {
                    var c = line[i];

                    switch (state)
                    {
                        case 0:
                            if (c == ';' || c == '#') // comment
                            {
                                comment.Add(line);
                                goto nextLine;
                            }

                            if (c == '[') // start of section line
                            {
                                state = 1;
                                start = i;
                                break;
                            }

                            if (char.IsWhiteSpace(c)) continue; // continue looking for the start of the line

                            state = 2; // probably a entry
                            start = i;
                            goto case 2; // in case the key is a empty string
                        case 1:
                            if (c == ']') // end of section line
                            {
                                if (section.Name is object || section.Entries.Count > 0) ini.Sections.Add(section);
                                section = new IniSection(){Name = line, Comment = comment, Start = start, End = i};
                                comment = new List<string>();
                                goto nextLine;
                            }

                            break;
                        case 2:
                            if (c == '=') // line has values => valid entry
                            {
                                section.Entries.Add(new IniEntry(){Entry = line, Comment = comment, Start = start, Separator = i});
                                comment = new List<string>();
                                goto nextLine;
                            }

                            break;
                    }
                }
                // line did not parse as anything => either empty or invalid => treat as comment

                comment.Add(line);

                nextLine: ;
            }

            if (section.Name is object || section.Entries.Count > 0) ini.Sections.Add(section);
            ini.EofComment = comment;

            return ini;
        }

        public void Write([NotNull] TextWriter writer) => Write(writer.WriteLine);

        public void Write([NotNull] Action<string> writeLine)
        {
            foreach (var section in Sections)
            {
                section?.Comment.ForEach(writeLine);
                if (section.Name is object) writeLine(section.Name);
                foreach (var entry in section.Entries)
                {
                    entry?.Comment.ForEach(writeLine);
                    writeLine(entry.Entry);
                }
            }
            EofComment?.ForEach(writeLine);
        }

        [ItemNotNull, NotNull] public List<IniSection> Sections = new List<IniSection>();
        [ItemNotNull, NotNull] public List<string> EofComment;
    }

    public class IniSection
    {
        [ItemNotNull, NotNull] public List<string> Comment;
        [CanBeNull] public string Name;
        [ItemNotNull, NotNull] public List<IniEntry> Entries = new List<IniEntry>();
        public int Start;
        public int End;
    }

    public class IniEntry
    {
        [ItemNotNull, NotNull] public List<string> Comment;
        [NotNull] public string Entry;
        public int Start;
        public int Separator;
    }

    public static class IniExtender
    {
        [CanBeNull]
        public static string GetValue([NotNull] this IniFile ini, [CanBeNull] string section, [NotNull] string key)
        {
            return ini.GetSection(section)?.GetEntry(key)?.GetValue();
        }

        [CanBeNull]
        public static string GetValue([NotNull] this IniSection section, [NotNull] string key)
        {
            return section.GetEntry(key)?.GetValue();
        }

        [NotNull]
        public static string GetValue([NotNull] this IniEntry entry)
        {
            // todo clean up value

            return entry.Entry.Substring(entry.Separator + 1);
        }

        [NotNull]
        public static string GetName([NotNull] this IniEntry entry)
        {
            return entry.Entry.Substring(entry.Start, entry.Separator - entry.Start);
        }

        [CanBeNull]
        public static string GetName([NotNull] this IniSection section)
        {
            return section.Name?.Substring(section.Start + 1, section.End - section.Start - 1);
        }
        
        [CanBeNull] public static IniSection GetSection([NotNull] this IniFile ini, [CanBeNull] string section) => GetSection(ini, section, out _);

        [CanBeNull]
        public static IniSection GetSection([NotNull] this IniFile ini, [CanBeNull] string section, out int idx)
        {

            if (ini.Sections.Count == 0)
            {
                idx = -1;
                return null;
            }
            if (section is null)
            {
                idx = 0;
                if (ini.Sections[0].Name is null) return ini.Sections[0];
                return null;
            }

            for (var i = 0; i < ini.Sections.Count; i++)
            {
                var sec = ini.Sections[i];
                if (sec.Name is object && Cmp.Compare(section.Whitespace(out var start, out var len), start, len, sec.Name, sec.Start + 1, sec.End - sec.Start - 1, CompareOptions.IgnoreCase) == 0)
                {
                    idx = i;
                    return sec;
                }
            }

            idx = -1;
            return null;
        }

        [CanBeNull] public static IniEntry GetEntry([NotNull] this IniSection section, [NotNull] string key) => GetEntry(section, key, out _);
        [CanBeNull]
        public static IniEntry GetEntry([NotNull] this IniSection section, [NotNull] string key, out int idx)
        {
            for (var i = 0; i < section.Entries.Count; i++)
            {
                var entry = section.Entries[i];
                if (Cmp.Compare(key.Whitespace(out var start, out var len), start, len, entry.Entry, entry.Start, entry.Separator - entry.Start, CompareOptions.IgnoreCase) == 0)
                {
                    idx = i;
                    return entry;
                }
            }

            idx = -1;
            return null;
        }

        public static void Merge([NotNull] this IniFile current, [NotNull] IniFile other, IniMergeOption options)
        {
            foreach (var otherSection in other.Sections)
            {
                var currentSection = current.GetSection(otherSection.GetName());

                if (currentSection is null)
                    current.Sections.Add(otherSection);
                else
                    currentSection.Merge(otherSection, options);
            }

            if ((options & IniMergeOption.OverwriteComments) != 0)
                current.EofComment.AddRange(other.EofComment);
        }

        public static void Merge([NotNull] this IniSection current, [NotNull] IniSection other, IniMergeOption options)
        {
            foreach (var otherEntry in other.Entries)
            {
                var currentEntry = current.GetEntry(otherEntry.GetName());

                if (currentEntry is null)
                    current.Entries.Add(otherEntry);
                else
                    currentEntry.Merge(otherEntry, options);
            }

            if ((options & IniMergeOption.OverwriteComments) != 0)
                current.Comment.AddRange(other.Comment);
        }

        public static void Merge([NotNull] this IniEntry current, [NotNull] IniEntry other, IniMergeOption options)
        {
            if ((options & IniMergeOption.OverwriteValues) != 0)
            {
                current.Entry = other.Entry;
                current.Start = other.Start;
                current.Separator = other.Separator;
            }

            if ((options & IniMergeOption.OverwriteComments) != 0)
            current.Comment.AddRange(other.Comment);
        }


        [NotNull]
        private static string Whitespace([NotNull] this string s, out int start, out int len)
        {
            start = -1;
            len = 0;

            for (var i = 0; i < s.Length; i++)
                if (!char.IsWhiteSpace(s[i]))
                    if (start == -1)
                        start = i;
                    else
                        len = i - start + 1;

            if (start == -1) start = 0;
            return s;
        }

        [NotNull] private static CompareInfo Cmp => CultureInfo.CurrentCulture.CompareInfo;
    }

    [Flags]
    public enum IniMergeOption
    {
        None = 0,
        OverwriteValues = 1,
        OverwriteComments = 2,
        Overwrite = OverwriteValues | OverwriteComments,
    }
}