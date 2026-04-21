using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Xml;

namespace ElementalTracker
{
    public class ThemeSettings
    {
        public string ThemeName { get; set; } = "Default";

        // Window chrome
        public Color WindowBackground { get; set; } = Color.FromRgb(240, 240, 240);
        public Color WindowForeground { get; set; } = Color.FromRgb(0, 0, 0);

        // Menu & Toolbar
        public Color MenuBackground { get; set; } = Color.FromRgb(240, 240, 240);
        public Color MenuForeground { get; set; } = Color.FromRgb(0, 0, 0);
        public Color ToolbarBackground { get; set; } = Color.FromRgb(240, 240, 240);
        public Color ToolbarForeground { get; set; } = Color.FromRgb(0, 0, 0);
        // Main toolbar (separate from other toolbars)
        public Color MainToolbarBackground { get; set; } = Color.FromRgb(240, 240, 240);
        public Color MainToolbarForeground { get; set; } = Color.FromRgb(0, 0, 0);

        // Status bar
        public Color StatusBarBackground { get; set; } = Color.FromRgb(230, 230, 230);
        public Color StatusBarForeground { get; set; } = Color.FromRgb(0, 0, 0);
		public Color ProgressBarBackground { get; set; } = Color.FromRgb(220, 220, 220);
		public Color ProgressBarForeground { get; set; } = Color.FromRgb(6, 176, 37);

        // Category tree
        public Color TreeBackground { get; set; } = Color.FromRgb(255, 255, 255);
        public Color TreeForeground { get; set; } = Color.FromRgb(0, 0, 0);
        public Color TreeSelectedBackground { get; set; } = Color.FromRgb(51, 153, 255);
        public Color TreeSelectedForeground { get; set; } = Color.FromRgb(255, 255, 255);
		public Color TreeHoverBackground { get; set; } = Color.FromRgb(229, 243, 255);
		public Color TreeHoverForeground { get; set; } = Color.FromRgb(0, 0, 0);

        // ListView
        public Color ListBackground { get; set; } = Color.FromRgb(255, 255, 255);
        public Color ListForeground { get; set; } = Color.FromRgb(0, 0, 0);
        public Color ListHeaderBackground { get; set; } = Color.FromRgb(230, 230, 230);
        public Color ListHeaderForeground { get; set; } = Color.FromRgb(0, 0, 0);
        public Color ListSelectedBackground { get; set; } = Color.FromRgb(51, 153, 255);
        public Color ListSelectedForeground { get; set; } = Color.FromRgb(255, 255, 255);
        public Color ListAlternateRowBackground { get; set; } = Color.FromRgb(245, 245, 250);
        // ListView hover
		public Color ListHoverBackground { get; set; } = Color.FromRgb(229, 243, 255);
		public Color ListHoverForeground { get; set; } = Color.FromRgb(0, 0, 0);
		// List Header Hover
		public Color ListHeaderHoverBackground { get; set; } = Color.FromRgb(229, 243, 255);
		public Color ListHeaderHoverForeground { get; set; } = Color.FromRgb(0, 0, 0);

		// ComboBox
		public Color ComboBoxBackground { get; set; } = Color.FromRgb(255, 255, 255);
		public Color ComboBoxForeground { get; set; } = Color.FromRgb(0, 0, 0);
		public Color ComboBoxBorder { get; set; } = Color.FromRgb(180, 180, 180);
		public Color ComboBoxButtonBackground { get; set; } = Color.FromRgb(230, 230, 230);
		public Color ComboBoxButtonForeground { get; set; } = Color.FromRgb(80, 80, 80);

		// ListView checkboxes
		public Color CheckBoxBackground { get; set; } = Color.FromRgb(255, 255, 255);
		public Color CheckBoxBorder { get; set; } = Color.FromRgb(180, 180, 180);
		public Color CheckBoxCheckMark { get; set; } = Color.FromRgb(0, 0, 0);
		public Color CheckBoxHoverBackground { get; set; } = Color.FromRgb(229, 243, 255);

        // ListView special columns
        public Color ListNameColumnForeground { get; set; } = Color.FromRgb(0, 0, 0);
        public Color ListVersionColumnForeground { get; set; } = Color.FromRgb(0, 0, 0);

        // ListView status indicator colors (the circle icon in St column)
        public Color StatusUnchanged { get; set; } = Color.FromRgb(0, 180, 0);
        public Color StatusChanged { get; set; } = Color.FromRgb(255, 180, 0);
        public Color StatusError { get; set; } = Color.FromRgb(220, 40, 40);
        public Color StatusNew { get; set; } = Color.FromRgb(40, 120, 220);
        public Color StatusUnchecked { get; set; } = Color.FromRgb(160, 160, 160);

        // ListView cell status colors (text coloring for URL, hash, etc.)
		public Color CellStatusOk { get; set; } = Color.FromRgb(50, 200, 0);      // was 0,150,0
		public Color CellStatusChanged { get; set; } = Color.FromRgb(255, 180, 60); // was 200,140,0
		public Color CellStatusError { get; set; } = Color.FromRgb(220, 80, 80);    // was 220,40,40
		public Color CellStatusDefault { get; set; } = Color.FromRgb(0, 0, 0);     // was 0,0,0

        // Track settings panel
        public Color PanelBackground { get; set; } = Color.FromRgb(240, 240, 240);
        public Color PanelForeground { get; set; } = Color.FromRgb(0, 0, 0);
        public Color TextBoxBackground { get; set; } = Color.FromRgb(255, 255, 255);
        public Color TextBoxForeground { get; set; } = Color.FromRgb(0, 0, 0);
        public Color LabelForeground { get; set; } = Color.FromRgb(0, 0, 0);
		public Color VersionMatchColor { get; set; } = Color.FromRgb(0, 160, 0);
		public Color VersionMismatchColor { get; set; } = Color.FromRgb(200, 30, 30);
        public Color TrackToolbarBackground { get; set; } = Color.FromRgb(240, 240, 240);
        public Color TrackToolbarForeground { get; set; } = Color.FromRgb(0, 0, 0);
		public Color ReleaseDateChangedBackground { get; set; } = Color.FromRgb(255, 200, 80);

        // Tabs
        public Color TabBackground { get; set; } = Color.FromRgb(240, 240, 240);
        public Color TabForeground { get; set; } = Color.FromRgb(0, 0, 0);
        public Color TabSelectedBackground { get; set; } = Color.FromRgb(255, 255, 255);
        public Color TabSelectedForeground { get; set; } = Color.FromRgb(0, 0, 0);
        public Color TabContentForeground { get; set; } = Color.FromRgb(0, 0, 0);
        public Color TabHandleBackground { get; set; } = Color.FromRgb(225, 225, 225);
        public Color TabHandleForeground { get; set; } = Color.FromRgb(80, 80, 80);

        // Source view
        public Color SourceBackground { get; set; } = Color.FromRgb(255, 255, 255);
        public Color SourceTagColor { get; set; } = Color.FromRgb(40, 80, 180);
        public Color SourceTextColor { get; set; } = Color.FromRgb(0, 0, 0);
        public Color SourceStartStringColor { get; set; } = Color.FromRgb(0, 160, 0);
        public Color SourceInfoStringColor { get; set; } = Color.FromRgb(200, 140, 0);
        public Color SourceStopStringColor { get; set; } = Color.FromRgb(220, 40, 40);
        public Color SourceBannerBackground { get; set; } = Color.FromRgb(255, 243, 205);
        public Color SourceBannerForeground { get; set; } = Color.FromRgb(100, 80, 0);

        // Buttons
        public Color ButtonBackground { get; set; } = Color.FromRgb(221, 221, 221);
        public Color ButtonForeground { get; set; } = Color.FromRgb(0, 0, 0);

        // Splitters
        public Color SplitterColor { get; set; } = Color.FromRgb(211, 211, 211);

        // Scrollbars
        public Color ScrollBarBackground { get; set; } = Color.FromRgb(240, 240, 240);
        public Color ScrollBarThumb { get; set; } = Color.FromRgb(180, 180, 180);

        // ============================
        // Built-in theme presets
        // ============================

        public static ThemeSettings GetDefaultLight()
        {
            return new ThemeSettings { ThemeName = "Default Light" };
        }

        public static ThemeSettings GetDarkTheme()
        {
            ThemeSettings t = new ThemeSettings();
            t.ThemeName = "Dark";

            t.WindowBackground = Color.FromRgb(30, 30, 30);
            t.WindowForeground = Color.FromRgb(220, 220, 220);

            t.MenuBackground = Color.FromRgb(45, 45, 45);
            t.MenuForeground = Color.FromRgb(220, 220, 220);
            t.ToolbarBackground = Color.FromRgb(45, 45, 45);
            t.ToolbarForeground = Color.FromRgb(220, 220, 220);
            t.MainToolbarBackground = Color.FromRgb(35, 35, 35);
            t.MainToolbarForeground = Color.FromRgb(220, 220, 220);

            t.StatusBarBackground = Color.FromRgb(40, 40, 40);
            t.StatusBarForeground = Color.FromRgb(200, 200, 200);
            t.ProgressBarBackground = Color.FromRgb(60, 60, 60);
			t.ProgressBarForeground = Color.FromRgb(0, 200, 80);

            t.TreeBackground = Color.FromRgb(35, 35, 35);
            t.TreeForeground = Color.FromRgb(210, 210, 210);
            t.TreeSelectedBackground = Color.FromRgb(60, 100, 160);
            t.TreeSelectedForeground = Color.FromRgb(255, 255, 255);
            t.TreeHoverBackground = Color.FromRgb(55, 55, 65);
			t.TreeHoverForeground = Color.FromRgb(255, 255, 255);

            t.ListBackground = Color.FromRgb(35, 35, 35);
            t.ListForeground = Color.FromRgb(210, 210, 210);
            t.ListHeaderBackground = Color.FromRgb(50, 50, 50);
            t.ListHeaderForeground = Color.FromRgb(200, 200, 200);
            t.ListSelectedBackground = Color.FromRgb(60, 100, 160);
            t.ListSelectedForeground = Color.FromRgb(255, 255, 255);
            t.ListAlternateRowBackground = Color.FromRgb(40, 40, 45);
            t.ListHoverBackground = Color.FromRgb(55, 55, 65);
			t.ListHoverForeground = Color.FromRgb(255, 255, 255);
			t.ListHeaderHoverBackground = Color.FromRgb(55, 55, 65);
			t.ListHeaderHoverForeground = Color.FromRgb(220, 220, 220);

			t.ComboBoxBackground = Color.FromRgb(50, 50, 50);
			t.ComboBoxForeground = Color.FromRgb(220, 220, 220);
			t.ComboBoxBorder = Color.FromRgb(80, 80, 80);
			t.ComboBoxButtonBackground = Color.FromRgb(65, 65, 65);
			t.ComboBoxButtonForeground = Color.FromRgb(200, 200, 200);

			t.CheckBoxBackground = Color.FromRgb(50, 50, 50);
			t.CheckBoxBorder = Color.FromRgb(100, 100, 100);
			t.CheckBoxCheckMark = Color.FromRgb(220, 220, 220);
			t.CheckBoxHoverBackground = Color.FromRgb(55, 55, 65);

            t.ListNameColumnForeground = Color.FromRgb(210, 210, 210);
            t.ListVersionColumnForeground = Color.FromRgb(210, 210, 210);

            t.StatusUnchanged = Color.FromRgb(0, 200, 0);
            t.StatusChanged = Color.FromRgb(255, 200, 0);
            t.StatusError = Color.FromRgb(255, 60, 60);
            t.StatusNew = Color.FromRgb(60, 140, 255);
            t.StatusUnchecked = Color.FromRgb(120, 120, 120);

            t.CellStatusOk = Color.FromRgb(0, 180, 0);
            t.CellStatusChanged = Color.FromRgb(220, 160, 0);
            t.CellStatusError = Color.FromRgb(255, 60, 60);
            t.CellStatusDefault = Color.FromRgb(210, 210, 210);

            t.PanelBackground = Color.FromRgb(35, 35, 35);
            t.PanelForeground = Color.FromRgb(210, 210, 210);
            t.TextBoxBackground = Color.FromRgb(50, 50, 50);
            t.TextBoxForeground = Color.FromRgb(220, 220, 220);
            t.LabelForeground = Color.FromRgb(200, 200, 200);
            t.VersionMatchColor = Color.FromRgb(80, 220, 80);
			t.VersionMismatchColor = Color.FromRgb(255, 80, 80);
            t.TrackToolbarBackground = Color.FromRgb(45, 45, 45);
            t.TrackToolbarForeground = Color.FromRgb(220, 220, 220);
            t.ReleaseDateChangedBackground = Color.FromRgb(180, 120, 40);

            t.TabBackground = Color.FromRgb(40, 40, 40);
            t.TabForeground = Color.FromRgb(200, 200, 200);
            t.TabSelectedBackground = Color.FromRgb(55, 55, 55);
            t.TabSelectedForeground = Color.FromRgb(255, 255, 255);
            t.TabContentForeground = Color.FromRgb(210, 210, 210);
            t.TabHandleBackground = Color.FromRgb(50, 50, 50);
            t.TabHandleForeground = Color.FromRgb(160, 160, 160);

            t.SourceBannerBackground = Color.FromRgb(60, 50, 20);
            t.SourceBannerForeground = Color.FromRgb(240, 200, 80);
            t.SourceBackground = Color.FromRgb(30, 30, 30);
            t.SourceTagColor = Color.FromRgb(100, 150, 255);
            t.SourceTextColor = Color.FromRgb(200, 200, 200);
            t.SourceStartStringColor = Color.FromRgb(80, 220, 80);
            t.SourceInfoStringColor = Color.FromRgb(240, 180, 50);
            t.SourceStopStringColor = Color.FromRgb(255, 80, 80);

            t.ButtonBackground = Color.FromRgb(60, 60, 60);
            t.ButtonForeground = Color.FromRgb(220, 220, 220);

            t.SplitterColor = Color.FromRgb(60, 60, 60);

            t.ScrollBarBackground = Color.FromRgb(40, 40, 40);
            t.ScrollBarThumb = Color.FromRgb(80, 80, 80);

            return t;
        }

        public static ThemeSettings GetBlueTheme()
        {
            ThemeSettings t = new ThemeSettings();
            t.ThemeName = "Blue";

            t.WindowBackground = Color.FromRgb(230, 240, 255);
            t.WindowForeground = Color.FromRgb(20, 20, 60);

            t.MenuBackground = Color.FromRgb(220, 235, 255);
            t.MenuForeground = Color.FromRgb(20, 20, 60);
            t.ToolbarBackground = Color.FromRgb(220, 235, 255);
            t.ToolbarForeground = Color.FromRgb(20, 20, 60);
            t.MainToolbarBackground = Color.FromRgb(210, 230, 255);
            t.MainToolbarForeground = Color.FromRgb(20, 20, 60);

            t.StatusBarBackground = Color.FromRgb(200, 220, 250);
            t.StatusBarForeground = Color.FromRgb(20, 20, 60);
            t.ProgressBarBackground = Color.FromRgb(180, 210, 250);
			t.ProgressBarForeground = Color.FromRgb(40, 120, 220);

            t.TreeBackground = Color.FromRgb(240, 245, 255);
            t.TreeForeground = Color.FromRgb(20, 40, 80);
            t.TreeSelectedBackground = Color.FromRgb(70, 130, 210);
            t.TreeSelectedForeground = Color.FromRgb(255, 255, 255);
            t.TreeHoverBackground = Color.FromRgb(210, 230, 255);
			t.TreeHoverForeground = Color.FromRgb(20, 40, 80);

            t.ListBackground = Color.FromRgb(245, 248, 255);
            t.ListForeground = Color.FromRgb(20, 40, 80);
            t.ListHeaderBackground = Color.FromRgb(180, 210, 250);
            t.ListHeaderForeground = Color.FromRgb(20, 30, 60);
            t.ListSelectedBackground = Color.FromRgb(70, 130, 210);
            t.ListSelectedForeground = Color.FromRgb(255, 255, 255);
            t.ListAlternateRowBackground = Color.FromRgb(235, 242, 255);
            t.ListHoverBackground = Color.FromRgb(210, 230, 255);
			t.ListHoverForeground = Color.FromRgb(20, 40, 80);
			t.ListHeaderHoverBackground = Color.FromRgb(180, 210, 255);
			t.ListHeaderHoverForeground = Color.FromRgb(20, 40, 80);

			t.ComboBoxBackground = Color.FromRgb(255, 255, 255);
			t.ComboBoxForeground = Color.FromRgb(20, 40, 80);
			t.ComboBoxBorder = Color.FromRgb(160, 190, 230);
			t.ComboBoxButtonBackground = Color.FromRgb(210, 230, 255);
			t.ComboBoxButtonForeground = Color.FromRgb(40, 60, 120);

			t.CheckBoxBackground = Color.FromRgb(255, 255, 255);
			t.CheckBoxBorder = Color.FromRgb(160, 190, 230);
			t.CheckBoxCheckMark = Color.FromRgb(20, 40, 80);
			t.CheckBoxHoverBackground = Color.FromRgb(210, 230, 255);

            t.ListNameColumnForeground = Color.FromRgb(20, 40, 80);
            t.ListVersionColumnForeground = Color.FromRgb(20, 40, 80);

            t.PanelBackground = Color.FromRgb(235, 242, 255);
            t.PanelForeground = Color.FromRgb(20, 40, 80);
            t.TextBoxBackground = Color.FromRgb(255, 255, 255);
            t.TextBoxForeground = Color.FromRgb(20, 40, 80);
            t.LabelForeground = Color.FromRgb(30, 50, 90);
            t.VersionMatchColor = Color.FromRgb(0, 160, 0);
			t.VersionMismatchColor = Color.FromRgb(200, 40, 40);
            t.TrackToolbarBackground = Color.FromRgb(210, 230, 255);
            t.TrackToolbarForeground = Color.FromRgb(20, 20, 60);
            t.ReleaseDateChangedBackground = Color.FromRgb(200, 140, 50);

            t.TabBackground = Color.FromRgb(220, 235, 255);
            t.TabForeground = Color.FromRgb(20, 40, 80);
            t.TabSelectedBackground = Color.FromRgb(255, 255, 255);
            t.TabSelectedForeground = Color.FromRgb(20, 30, 60);
            t.TabHandleBackground = Color.FromRgb(200, 220, 250);
            t.TabHandleForeground = Color.FromRgb(40, 60, 120);
            t.TabContentForeground = Color.FromRgb(20, 40, 80);

            t.SourceBannerBackground = Color.FromRgb(220, 235, 255);
            t.SourceBannerForeground = Color.FromRgb(40, 60, 120);
            t.SourceBackground = Color.FromRgb(250, 252, 255);
            t.SourceTagColor = Color.FromRgb(40, 80, 180);
            t.SourceTextColor = Color.FromRgb(20, 40, 80);
            t.SourceStartStringColor = Color.FromRgb(0, 150, 0);
            t.SourceInfoStringColor = Color.FromRgb(180, 130, 0);
            t.SourceStopStringColor = Color.FromRgb(200, 40, 40);

            t.ButtonBackground = Color.FromRgb(190, 215, 250);
            t.ButtonForeground = Color.FromRgb(20, 30, 60);

            t.SplitterColor = Color.FromRgb(180, 200, 230);

            t.ScrollBarBackground = Color.FromRgb(220, 235, 255);
            t.ScrollBarThumb = Color.FromRgb(160, 190, 230);

            return t;
        }

        // ============================
        // Save to XML file
        // ============================

        public void Save(string path)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "  ";
            settings.Encoding = Encoding.UTF8;

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (XmlWriter writer = XmlWriter.Create(path, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Theme");

                writer.WriteElementString("ThemeName", ThemeName ?? "Custom");

                // Write every color property using reflection
                foreach (var prop in typeof(ThemeSettings).GetProperties())
                {
                    if (prop.PropertyType == typeof(Color))
                    {
                        Color c = (Color)prop.GetValue(this);
                        writer.WriteElementString(prop.Name, ColorToHex(c));
                    }
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        // ============================
        // Load from XML file
        // ============================

        public static ThemeSettings Load(string path)
        {
            ThemeSettings theme = new ThemeSettings();

            if (!File.Exists(path))
                return theme;

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);

                XmlNode root = doc.SelectSingleNode("//Theme");
                if (root == null)
                    return theme;

                XmlNode nameNode = root.SelectSingleNode("ThemeName");
                if (nameNode != null)
                    theme.ThemeName = nameNode.InnerText;

                foreach (var prop in typeof(ThemeSettings).GetProperties())
                {
                    if (prop.PropertyType == typeof(Color))
                    {
                        XmlNode node = root.SelectSingleNode(prop.Name);
                        if (node != null)
                        {
                            Color? c = HexToColor(node.InnerText);
                            if (c.HasValue)
                                prop.SetValue(theme, c.Value);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Return default theme on any error
            }

            return theme;
        }

        // ============================
        // Color helpers
        // ============================

        public static string ColorToHex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        public static Color? HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return null;

            hex = hex.Trim().TrimStart('#');

            if (hex.Length == 6)
            {
                try
                {
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromRgb(r, g, b);
                }
                catch { return null; }
            }

            return null;
        }

        /// <summary>
        /// Creates a deep copy of this theme.
        /// </summary>
        public ThemeSettings Clone()
        {
            ThemeSettings copy = new ThemeSettings();
            copy.ThemeName = ThemeName;

            foreach (var prop in typeof(ThemeSettings).GetProperties())
            {
                if (prop.CanWrite)
                    prop.SetValue(copy, prop.GetValue(this));
            }

            return copy;
        }
    }
}