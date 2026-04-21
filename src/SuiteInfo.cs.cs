using System;

namespace ElementalTracker
{
    public class SuiteInfo
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsDefault { get; set; } = false;
        public bool IsSelected { get; set; } = false;

        /// <summary>
        /// Default suites (SyMenuSuite, NirSoftSuite) use zip extraction.
        /// User suites read .sps files directly from _Cache.
        /// </summary>
        public bool RequiresExtraction
        {
            get { return IsDefault; }
        }

        public override string ToString()
        {
            return Name + (IsDefault ? " (default)" : " (user)");
        }
    }
}