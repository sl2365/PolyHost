using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ElementalTracker
{
    public class SpsParser
    {
        /// <summary>
        /// Finds the SPSSuite root path by searching upward from a given starting path
        /// for a folder structure containing "ProgramFiles\SPSSuite".
        /// Returns the full path to the SPSSuite folder, or null if not found.
        /// This is the parent of SyMenuSuite and any future suite folders.
        /// </summary>
        public static string FindSpsSuiteRootPath(string startPath)
        {
            string current = startPath;
            while (!string.IsNullOrEmpty(current))
            {
                string candidate = Path.Combine(current, "ProgramFiles", "SPSSuite");
                if (Directory.Exists(candidate))
                    return candidate;

                string parent = Path.GetDirectoryName(current);
                if (parent == current)
                    break;
                current = parent;
            }
            return null;
        }

        /// <summary>
        /// Extracts SPS zip/sps files from a suite's _Cache folder into a _TmpET folder
        /// within that suite directory.
        /// suitePath should be the full path to a suite folder, e.g.
        /// [SPSSuiteRoot]\SyMenuSuite
        /// Returns the path to _TmpET, or null on failure.
        /// </summary>
        public static string ExtractSpsCache(string suitePath)
        {
            string cachePath = Path.Combine(suitePath, "_Cache");
            string tmpPath = Path.Combine(suitePath, "_TmpET");

            if (!Directory.Exists(cachePath))
                return null;

            // Clean and recreate _TmpET
            if (Directory.Exists(tmpPath))
            {
                try { Directory.Delete(tmpPath, true); }
                catch { }
            }
            Directory.CreateDirectory(tmpPath);

            // Extract all zip files from _Cache
            string[] cacheFiles = Directory.GetFiles(cachePath);
            foreach (string file in cacheFiles)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".zip")
                {
                    try
                    {
                        ZipFile.ExtractToDirectory(file, tmpPath);
                    }
                    catch
                    {
                        // Skip zips that fail to extract
                    }
                }
                else if (ext == ".sps")
                {
                    try
                    {
                        string dest = Path.Combine(tmpPath, Path.GetFileName(file));
                        File.Copy(file, dest, true);
                    }
                    catch { }
                }
            }

            return tmpPath;
        }

        /// <summary>
        /// Scans a folder for .sps files and parses each one into a TrackItem.
        /// Optionally filters by publisher name (empty string = all publishers).
        /// </summary>
        public static List<TrackItem> ParseSpsFiles(string folderPath, string publisherFilter = "")
        {
            List<TrackItem> items = new List<TrackItem>();

            if (!Directory.Exists(folderPath))
                return items;

            string[] spsFiles = Directory.GetFiles(folderPath, "*.sps");

            foreach (string spsFile in spsFiles)
            {
                try
                {
                    string content = File.ReadAllText(spsFile, Encoding.UTF8);
                    FileInfo fi = new FileInfo(spsFile);

                    string publisherName = GetTagValue(content, "SPSPublisherName");

                    // Apply publisher filter
                    if (!string.IsNullOrEmpty(publisherFilter))
                    {
                        if (string.IsNullOrEmpty(publisherName))
                            continue;
                        if (!publisherFilter.Contains(publisherName))
                            continue;
                    }

                    TrackItem item = new TrackItem();
                    item.TrackName = GetTagValue(content, "ProgramName");
                    item.Version = GetTagValue(content, "Version");
                    item.ReleaseDate = GetTagValue(content, "ReleaseDate");
                    item.DownloadURL = GetTagValue(content, "DownloadUrl");
                    item.DownloadSizeKb = GetTagValue(content, "DownloadSizeKb");
                    item.PublisherName = publisherName;
                    item.CreationDate = fi.CreationTime.ToString("yyyy-MM-dd");
                    item.ModificationDate = fi.LastWriteTime.ToString("yyyy-MM-dd");

                    // Use ProgramPublisherWebSite as initial TrackURL if available
                    string website = GetTagValue(content, "ProgramPublisherWebSite");
                    if (!string.IsNullOrEmpty(website))
                        item.TrackURL = website;

                    // Mark as unchecked since these are freshly scanned
                    item.TrackStatus = "unchecked";
                    item.SpsFileName = Path.GetFileName(spsFile);

                    if (!string.IsNullOrEmpty(item.TrackName))
                        items.Add(item);
                }
                catch
                {
                    // Skip files that can't be parsed
                }
            }

            return items;
        }

        /// <summary>
        /// Cleans up the _TmpET folder inside a suite directory.
        /// </summary>
        public static void CleanupTmpFolder(string suitePath)
        {
            string tmpPath = Path.Combine(suitePath, "_TmpET");
            if (Directory.Exists(tmpPath))
            {
                try { Directory.Delete(tmpPath, true); }
                catch { }
            }
        }

        /// <summary>
        /// Scans the SPSSuite root folder and returns a list of all available suites.
        /// Default suites (SyMenuSuite, NirSoftSuite) are marked as requiring extraction.
        /// User suites are any other subfolder that contains a _Cache folder.
        /// </summary>
        public static List<SuiteInfo> DetectSuites(string spsSuiteRoot)
        {
            List<SuiteInfo> suites = new List<SuiteInfo>();

            if (string.IsNullOrEmpty(spsSuiteRoot) || !Directory.Exists(spsSuiteRoot))
                return suites;

            string[] defaultNames = new string[] { "SyMenuSuite", "NirSoftSuite" };

            string[] folders = Directory.GetDirectories(spsSuiteRoot);
            foreach (string folder in folders)
            {
                string folderName = Path.GetFileName(folder);

                // Skip hidden/system folders
                if (folderName.StartsWith("_"))
                    continue;

                // Check if this folder has a _Cache subfolder
                string cachePath = Path.Combine(folder, "_Cache");
                if (!Directory.Exists(cachePath))
                    continue;

                bool isDefault = false;
                foreach (string defName in defaultNames)
                {
                    if (folderName.Equals(defName, StringComparison.OrdinalIgnoreCase))
                    {
                        isDefault = true;
                        break;
                    }
                }

                SuiteInfo suite = new SuiteInfo();
                suite.Name = folderName;
                suite.FullPath = folder;
                suite.IsDefault = isDefault;
                suites.Add(suite);
            }

            return suites;
        }

        /// <summary>
        /// Extracts the text between custom XML-like tags.
        /// e.g. GetTagValue(text, "ProgramName") returns content between
        /// &lt;ProgramName&gt; and &lt;/ProgramName&gt;.
        /// This is a port of the original VB S_GetsNBlockFromText function.
        /// </summary>
        public static string GetTagValue(string text, string tagName)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(tagName))
                return "";

            string startTag = "<" + tagName + ">";
            string endTag = "</" + tagName + ">";

            int startIdx = text.IndexOf(startTag, StringComparison.Ordinal);
            if (startIdx < 0)
                return "";

            startIdx += startTag.Length;

            int endIdx = text.IndexOf(endTag, startIdx, StringComparison.Ordinal);
            if (endIdx < 0)
                return "";

            return text.Substring(startIdx, endIdx - startIdx);
        }
    }
}