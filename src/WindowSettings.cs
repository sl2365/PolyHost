using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace ElementalTracker
{
    public class ColumnSetting
    {
        public string Header { get; set; } = "";
        public string Binding { get; set; } = "";
        public double Width { get; set; } = 100;
        public bool Visible { get; set; } = true;
        public int DisplayIndex { get; set; } = -1;
    }

    public class WindowSettings
    {
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public bool IsMaximized { get; set; } = false;
        public bool IsHorizontalLayout { get; set; } = true;

        // Toolbar position
        public string ToolbarPosition { get; set; } = "Top";

        // Vertical layout splitters
        public double VSplitter1 { get; set; } = 450;
        public double VSplitter1Row { get; set; } = double.NaN;
        public double VSplitterCat { get; set; } = 180;

        // Horizontal layout splitters
        public double HRowTop { get; set; } = 250; // height of the top row (list view area)
        public double HTopCol0 { get; set; } = double.NaN; // width of the Track panel in the top row
        public double HBotCol0 { get; set; } = 350; // width of the Track panel in the bottom row

        // Source tab font size
        public double SourceFontSize { get; set; } = 10;

        // Editor path
        public string EditorPath { get; set; } = "";
		public string DefaultPublisherName { get; set; } = "";

		// Search engine
		public string SearchEngine { get; set; } = "DuckDuckGo";
		public string DefaultTrackMode { get; set; } = "html";

        // Column settings
        public List<ColumnSetting> ColumnSettings { get; set; } = new List<ColumnSetting>();

        // Active theme file path (relative to Settings folder)
		public string ActiveThemePath { get; set; } = "";

        // SPS Suite root path (e.g. D:\SyMenu\ProgramFiles\SPSSuite)
        public string SpsSuiteRootPath { get; set; } = "";
        public bool TrackSettingsCollapsed { get; set; } = false;
        public List<string> SelectedSuites { get; set; } = new List<string>();

		// Tab open/closed state
        public bool TabThemeVisible { get; set; } = true;
        public bool TabAppSettingsVisible { get; set; } = true;

        public static List<ColumnSetting> GetDefaultColumns()
        {
            List<ColumnSetting> defaults = new List<ColumnSetting>();
            defaults.Add(new ColumnSetting { Header = "Name", Binding = "TrackName", Width = 225, Visible = true, DisplayIndex = 0 });
            defaults.Add(new ColumnSetting { Header = "Track URL", Binding = "TrackURL", Width = 130, Visible = true, DisplayIndex = 1 });
            defaults.Add(new ColumnSetting { Header = "Start String", Binding = "StartString", Width = 100, Visible = true, DisplayIndex = 2 });
            defaults.Add(new ColumnSetting { Header = "Stop String", Binding = "StopString", Width = 100, Visible = true, DisplayIndex = 3 });
            defaults.Add(new ColumnSetting { Header = "Hash", Binding = "TrackBlockHash", Width = 130, Visible = true, DisplayIndex = 4 });
            defaults.Add(new ColumnSetting { Header = "Version", Binding = "Version", Width = 80, Visible = true, DisplayIndex = 5 });
            defaults.Add(new ColumnSetting { Header = "Latest", Binding = "LatestVersion", Width = 80, Visible = true, DisplayIndex = 6 });
            defaults.Add(new ColumnSetting { Header = "Release Date", Binding = "ReleaseDate", Width = 120, Visible = true, DisplayIndex = 7 });
            defaults.Add(new ColumnSetting { Header = "Download URL", Binding = "DownloadURL", Width = 130, Visible = true, DisplayIndex = 8 });
            defaults.Add(new ColumnSetting { Header = "Size KB", Binding = "DownloadSizeKb", Width = 85, Visible = true, DisplayIndex = 9 });
            defaults.Add(new ColumnSetting { Header = "Created", Binding = "CreationDate", Width = 110, Visible = true, DisplayIndex = 10 });
            defaults.Add(new ColumnSetting { Header = "Modified", Binding = "ModificationDate", Width = 120, Visible = true, DisplayIndex = 11 });
            defaults.Add(new ColumnSetting { Header = "Publisher", Binding = "PublisherName", Width = 120, Visible = true, DisplayIndex = 12 });
            defaults.Add(new ColumnSetting { Header = "Suite", Binding = "SuiteName", Width = 120, Visible = true, DisplayIndex = 13 });
            return defaults;
        }

        public void Save(string path)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "  ";
            settings.Encoding = Encoding.UTF8;

            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (XmlWriter writer = XmlWriter.Create(path, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("WindowSettings");

                writer.WriteElementString("WindowLeft", WindowLeft.ToString());
                writer.WriteElementString("WindowTop", WindowTop.ToString());
                writer.WriteElementString("WindowWidth", WindowWidth.ToString());
                writer.WriteElementString("WindowHeight", WindowHeight.ToString());
                writer.WriteElementString("IsMaximized", IsMaximized.ToString());
                writer.WriteElementString("IsHorizontalLayout", IsHorizontalLayout.ToString());

                writer.WriteElementString("VSplitter1", VSplitter1.ToString());
                writer.WriteElementString("VSplitter1Row", VSplitter1Row.ToString());
                writer.WriteElementString("VSplitterCat", VSplitterCat.ToString());

                writer.WriteElementString("HRowTop", HRowTop.ToString());
                writer.WriteElementString("HTopCol0", HTopCol0.ToString());
                writer.WriteElementString("HBotCol0", HBotCol0.ToString());
                writer.WriteElementString("SourceFontSize", SourceFontSize.ToString());
                writer.WriteElementString("EditorPath", EditorPath ?? "");
                writer.WriteElementString("DefaultPublisherName", DefaultPublisherName ?? "");
                writer.WriteElementString("ActiveThemePath", ActiveThemePath ?? "");
                writer.WriteElementString("SpsSuiteRootPath", SpsSuiteRootPath ?? "");
                writer.WriteElementString("TrackSettingsCollapsed", TrackSettingsCollapsed.ToString());
                // Save selected suites
                writer.WriteStartElement("SelectedSuites");
                foreach (string suite in SelectedSuites)
                {
                    writer.WriteElementString("Suite", suite);
                }
                writer.WriteEndElement();
                writer.WriteElementString("TabThemeVisible", TabThemeVisible.ToString());
                writer.WriteElementString("TabAppSettingsVisible", TabAppSettingsVisible.ToString());
                writer.WriteElementString("ToolbarPosition", ToolbarPosition ?? "Top");
                writer.WriteElementString("SearchEngine", SearchEngine ?? "DuckDuckGo");
                writer.WriteElementString("DefaultTrackMode", DefaultTrackMode ?? "html");

                // Save column settings
                writer.WriteStartElement("Columns");
                foreach (ColumnSetting col in ColumnSettings)
                {
                    writer.WriteStartElement("Column");
                    writer.WriteElementString("Header", col.Header);
                    writer.WriteElementString("Binding", col.Binding);
                    writer.WriteElementString("Width", col.Width.ToString());
                    writer.WriteElementString("Visible", col.Visible.ToString());
                    writer.WriteElementString("DisplayIndex", col.DisplayIndex.ToString());
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        public static WindowSettings Load(string path)
        {
            WindowSettings ws = new WindowSettings();

            if (!File.Exists(path))
                return ws;

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);

                XmlNode root = doc.SelectSingleNode("//WindowSettings");
                if (root == null)
                    return ws;

                ws.WindowLeft = ParseDouble(root, "WindowLeft", ws.WindowLeft);
                ws.WindowTop = ParseDouble(root, "WindowTop", ws.WindowTop);
                ws.WindowWidth = ParseDouble(root, "WindowWidth", ws.WindowWidth);
                ws.WindowHeight = ParseDouble(root, "WindowHeight", ws.WindowHeight);
                ws.IsMaximized = ParseBool(root, "IsMaximized", ws.IsMaximized);
                ws.IsHorizontalLayout = ParseBool(root, "IsHorizontalLayout", ws.IsHorizontalLayout);

                ws.VSplitter1 = ParseDouble(root, "VSplitter1", ws.VSplitter1);
                ws.VSplitter1Row = ParseDouble(root, "VSplitter1Row", ws.VSplitter1Row);
                ws.VSplitterCat = ParseDouble(root, "VSplitterCat", ws.VSplitterCat);

                ws.HRowTop = ParseDouble(root, "HRowTop", ws.HRowTop);
                ws.HTopCol0 = ParseDouble(root, "HTopCol0", ws.HTopCol0);
                ws.HBotCol0 = ParseDouble(root, "HBotCol0", ws.HBotCol0);
                ws.SourceFontSize = ParseDouble(root, "SourceFontSize", ws.SourceFontSize);
                ws.EditorPath = GetNodeText(root, "EditorPath");
                ws.DefaultPublisherName = GetNodeText(root, "DefaultPublisherName");
                ws.ActiveThemePath = GetNodeText(root, "ActiveThemePath");
                ws.SpsSuiteRootPath = GetNodeText(root, "SpsSuiteRootPath");
                ws.TrackSettingsCollapsed = ParseBool(root, "TrackSettingsCollapsed", ws.TrackSettingsCollapsed);
                ws.TabThemeVisible = ParseBool(root, "TabThemeVisible", true);
                ws.TabAppSettingsVisible = ParseBool(root, "TabAppSettingsVisible", true);
                // Load selected suites
                XmlNode suitesNode = root.SelectSingleNode("SelectedSuites");
                if (suitesNode != null)
                {
                    XmlNodeList suiteNodes = suitesNode.SelectNodes("Suite");
                    foreach (XmlNode suiteNode in suiteNodes)
                    {
                        if (!string.IsNullOrEmpty(suiteNode.InnerText))
                            ws.SelectedSuites.Add(suiteNode.InnerText);
                    }
                }
                ws.ToolbarPosition = GetNodeText(root, "ToolbarPosition");
				if (string.IsNullOrEmpty(ws.ToolbarPosition))
				    ws.ToolbarPosition = "Top";

                ws.SearchEngine = GetNodeText(root, "SearchEngine");
                if (string.IsNullOrEmpty(ws.SearchEngine))
                    ws.SearchEngine = "DuckDuckGo";

                ws.DefaultTrackMode = GetNodeText(root, "DefaultTrackMode");
				if (string.IsNullOrEmpty(ws.DefaultTrackMode))
				    ws.DefaultTrackMode = "html";

                // Load column settings
                XmlNode columnsNode = root.SelectSingleNode("Columns");
                if (columnsNode != null)
                {
                    XmlNodeList colNodes = columnsNode.SelectNodes("Column");
                    foreach (XmlNode colNode in colNodes)
                    {
                        ColumnSetting col = new ColumnSetting();
                        col.Header = GetNodeText(colNode, "Header");
                        col.Binding = GetNodeText(colNode, "Binding");
                        col.Width = ParseDouble(colNode, "Width", 100);
                        col.Visible = ParseBool(colNode, "Visible", true);
                        col.DisplayIndex = (int)ParseDouble(colNode, "DisplayIndex", -1);
                        ws.ColumnSettings.Add(col);
                    }
                }
            }
            catch (Exception)
            {
            }

            return ws;
        }

        private static string GetNodeText(XmlNode parent, string name)
        {
            XmlNode node = parent.SelectSingleNode(name);
            if (node != null)
                return node.InnerText ?? "";
            return "";
        }

        private static double ParseDouble(XmlNode parent, string name, double fallback)
        {
            XmlNode node = parent.SelectSingleNode(name);
            if (node != null && double.TryParse(node.InnerText, out double val))
                return val;
            return fallback;
        }

        private static bool ParseBool(XmlNode parent, string name, bool fallback)
        {
            XmlNode node = parent.SelectSingleNode(name);
            if (node != null && bool.TryParse(node.InnerText, out bool val))
                return val;
            return fallback;
        }
    }
}