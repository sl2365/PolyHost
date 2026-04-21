// using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ElementalTracker
{
    public partial class MainWindow : Window
    {
        private string appDir;
        private string categoriesPath;
        private string settingsPath;
        private string webViewPath;
        private bool isHorizontalLayout = false;
        private System.Collections.ObjectModel.ObservableCollection<TrackItem> currentItems = new System.Collections.ObjectModel.ObservableCollection<TrackItem>();
        private string lastSortColumn = "";
        private bool lastSortAscending = true;
        private string cookieBlockerScriptId;

        // Shared controls - created once in code
        private DockPanel categoryPanel;
        private TreeView categoryTree;
        private ListView itemList;
        private DockPanel trackSettingsPanel;
        private TabControl rightTabs;
        private Microsoft.Web.WebView2.Wpf.WebView2 webView;
		private CheckBox selectAllCheckbox;

		// Settings tab/SPS
        private Button btnSpsSelectSuites;
        private Button btnSpsScanCache;
        private StackPanel spsPublisherSettingsPanel;
        private TextBlock txtSettingsSearchCount;
        private TextBox txtSettingsPublisherFilter;
		private TextBox editDefaultPublisherName;
		private ComboBox defaultTrackModeCombo;

        // Track settings fields
        private TextBox editName;
        private TextBox editTrackURL;
        private TextBox editStartString;
        private TextBox editStopString;
        private TextBox editDownloadURL;
        private TextBox editVersion;
        private TextBox editReleaseDateStartString;
		private TextBox editReleaseDateStopString;
        private TextBlock editLatestVersion;
        private TextBox editReleaseDate;
        private TextBox editPublisherName;
        private TextBox editSuiteName;
        private TextBlock trackSettingsLabel;
        private TrackItem currentTrackItem = null;
        private string currentCategoryPath = null;
        private TextBlock startPositionText;
        private TextBlock stopPositionText;
        private TextBlock dateStartPositionText;
		private TextBlock dateStopPositionText;
        private Button btnUpdateVersion;
        private bool suppressAutoDownload = false;
        private bool blockCookiePopups = true;
        private TextBox editEditorPath;
        private TextBox editSyMenuPath;
        private Style columnHeaderStyle;
        private bool isDirty = false;
        private bool isCategoryDirty = false;
        private bool isLoadingFields = false;
        private bool suppressSelectionChange = false;
        private bool suppressCategoryReload = false;
		private Button btnSaveTrack = null;
		private Button btnSaveTrackAs = null;
        private Button btnTrackMode;
        private bool trackSettingsCollapsed = false;
        private ToolBarTray trackToolBarTray;
        private ScrollViewer trackSettingsScrollArea;
        private double trackSettingsExpandedWidth = Double.NaN;
        private double trackSettingsExpandedHeight = Double.NaN;
        private Button btnToggleTrackSettings;

        // Source tab
        private RichTextBox sourceView;
        private TextBox findString;
        private TextBlock findCount;
        private string extensionsPath;
		private TextBlock sourceInfoBanner;

        // Download + search state
        private static readonly HttpClient httpClient = new HttpClient(
            new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                         System.Net.DecompressionMethods.Deflate |
                                         System.Net.DecompressionMethods.Brotli
            });
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:123.0) Gecko/20100101 Firefox/123.0";
        private string currentSource = "";
        private List<int> searchHitPositions = new List<int>();
        private List<string> ignoredSpsFiles = new List<string>();
        private int currentSearchIndex = -1;
        private TextBox webAddressBar;
        private ComboBox searchEngineCombo;

		// Window settings
        private WindowSettings windowSettings;
        private string windowSettingsPath;
        private StackPanel columnCheckboxPanel;
		private TabItem themeTabItem;
		private TabItem appSettingsTabItem;

        // Theme
		private ThemeSettings currentTheme;
		private string currentThemePath;
		private ThemeSettings previewTheme;

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_LINKOVERLAY = 0x000008000;

        private System.Windows.Media.ImageSource GetFolderIcon(string folderPath, bool isLinked)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_SMALLICON;
            if (isLinked)
                flags |= SHGFI_LINKOVERLAY;

            SHGetFileInfo(folderPath, 0, ref shfi, (uint)System.Runtime.InteropServices.Marshal.SizeOf(shfi), flags);

            if (shfi.hIcon == IntPtr.Zero)
                return null;

            System.Windows.Media.ImageSource imageSource =
                System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    shfi.hIcon,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

            DestroyIcon(shfi.hIcon);

            return imageSource;
        }

        // Window constructor
        public MainWindow()
        {
            InitializeComponent();

            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var attr = (System.Reflection.AssemblyInformationalVersionAttribute)
                Attribute.GetCustomAttribute(asm, 
                    typeof(System.Reflection.AssemblyInformationalVersionAttribute));
            this.Title = AppInfo.AppName + " v" + (attr != null ? attr.InformationalVersion : "7");

            appDir = Path.GetDirectoryName(
                System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);

            categoriesPath = Path.Combine(appDir, "Categories");
            settingsPath = Path.Combine(appDir, "Settings");
            webViewPath = Path.Combine(appDir, "WebView");
            extensionsPath = Path.Combine(appDir, "Extensions");

            if (!Directory.Exists(categoriesPath))
                Directory.CreateDirectory(categoriesPath);
            if (!Directory.Exists(settingsPath))
                Directory.CreateDirectory(settingsPath);
            if (!Directory.Exists(webViewPath))
                Directory.CreateDirectory(webViewPath);
            if (!Directory.Exists(extensionsPath))
                Directory.CreateDirectory(extensionsPath);

            // Load window settings
            windowSettingsPath = Path.Combine(settingsPath, "window.xml");
            windowSettings = WindowSettings.Load(windowSettingsPath);

            // Apply window position and size
            this.Left = windowSettings.WindowLeft;
            this.Top = windowSettings.WindowTop;
            this.Width = windowSettings.WindowWidth;
            this.Height = windowSettings.WindowHeight;
            if (windowSettings.IsMaximized)
                this.WindowState = WindowState.Maximized;

            isHorizontalLayout = windowSettings.IsHorizontalLayout;

            // Apply layout visibility to match saved setting
            if (isHorizontalLayout)
            {
			    layoutVertical.Visibility = Visibility.Collapsed;
			    layoutHorizontal.Visibility = Visibility.Visible;
			    btnToggleLayout.Content = "\xeca5";
			    btnToggleLayout.FontFamily = new FontFamily("Segoe Fluent Icons");
			    btnToggleLayout.FontSize = 16;
			    btnToggleLayout.ToolTip = "Switch to Vertical Layout";
            }

			currentTheme = ThemeSettings.GetDefaultLight();
			currentThemePath = "";
			if (!string.IsNullOrEmpty(windowSettings.ActiveThemePath))
			{
			    string themePath = Path.Combine(settingsPath, windowSettings.ActiveThemePath);
			    if (File.Exists(themePath))
			    {
			        currentTheme = ThemeSettings.Load(themePath);
			        currentThemePath = themePath;
			    }
			}
			previewTheme = currentTheme.Clone();

			BuildSharedControls();
            UpdateSpsVisibility();
            UpdateSpsSettingsEnabled();
            if (!string.IsNullOrEmpty(windowSettings.SpsSuiteRootPath))
                menuOpenSpsBuilder.Visibility = Visibility.Visible;
			// Tab close/restore
            menuViewSettings.Click += (s, ev) =>
            {
                if (!rightTabs.Items.Contains(appSettingsTabItem))
                    rightTabs.Items.Add(appSettingsTabItem);
                rightTabs.SelectedItem = appSettingsTabItem;
                ApplyTheme(currentTheme);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    FixClosableTabForegrounds();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
            menuViewTheme.Click += (s, ev) =>
            {
                if (!rightTabs.Items.Contains(themeTabItem))
                    rightTabs.Items.Add(themeTabItem);
                rightTabs.SelectedItem = themeTabItem;
                ApplyTheme(currentTheme);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    FixClosableTabForegrounds();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };

			rightTabs.SelectionChanged += (s, e) =>
			{
			    foreach (TabItem ti in rightTabs.Items)
			    {
			        if (ti.Header is StackPanel sp)
			        {
			            bool isSelected = ti.IsSelected;
			            SolidColorBrush brush = isSelected
			                ? new SolidColorBrush(currentTheme.TabSelectedForeground)
			                : new SolidColorBrush(currentTheme.TabHandleForeground);

			            foreach (var child in sp.Children)
			            {
			                if (child is TextBlock tb)
			                    tb.Foreground = brush;
			                else if (child is Button btn)
			                    btn.Foreground = brush;
			            }
			        }
			    }
			};

            PlaceControlsInLayout();

            // Restore tab visibility from saved settings
            if (!windowSettings.TabThemeVisible && rightTabs.Items.Contains(themeTabItem))
                rightTabs.Items.Remove(themeTabItem);
            if (!windowSettings.TabAppSettingsVisible && rightTabs.Items.Contains(appSettingsTabItem))
                rightTabs.Items.Remove(appSettingsTabItem);
            if (rightTabs.Items.Count > 0)
                rightTabs.SelectedIndex = 0;

            // Restore toolbar position from saved settings
			if (!string.IsNullOrEmpty(windowSettings.ToolbarPosition) &&
			    windowSettings.ToolbarPosition != "Top")
			{
			    DockToolbarTo(windowSettings.ToolbarPosition);
			}
            ApplyTheme(currentTheme);
            SetupWebViewAutoZoom();
            // Pre-initialise WebView2 with extensions enabled
            InitWebView();
            RefreshCategoryTree();

            // Select first category on startup
            if (categoryTree.Items.Count > 0)
            {
                TreeViewItem firstCat = categoryTree.Items[0] as TreeViewItem;
                if (firstCat != null)
                    firstCat.IsSelected = true;
            }

            // Apply splitter positions after layout is ready
            this.Loaded += (s, ev) =>
            {
                ApplySplitterPositions();

                // Restore collapsed state of Track Settings panel
                if (windowSettings.TrackSettingsCollapsed && !trackSettingsCollapsed)
                {
                    btnToggleTrackSettings.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                }
            };

            // Save settings on close
            this.Closing += (s, ev) =>
            {
                if (isCategoryDirty && currentItems.Count > 0)
                {
                    MessageBoxResult result = MessageBox.Show(
                        "You have unsaved changes.\n\nDo you want to save before closing?",
                        AppInfo.ShortName + " — Unsaved Changes",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        Save_Click(s, new RoutedEventArgs());
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        ev.Cancel = true;
                        return;
                    }
                }
                SaveWindowSettings();
            };

            statusFile.Text = "Ready — " + appDir;
        }
		
        // ============================
        // Build Shared Controls Once
        // ============================

        private void SetupListViewHorizontalScrolling()
        {
            // Normal scroll wheel: horizontal when no vertical scrollbar, or with Shift held
            itemList.PreviewMouseWheel += (s, e) =>
            {
                ScrollViewer sv = FindScrollViewer(itemList);
                if (sv == null) return;

                bool hasVerticalScroll = sv.ScrollableHeight > 0;

                if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) ||
                    System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift))
                {
                    if (e.Delta > 0)
                        sv.LineLeft();
                    else
                        sv.LineRight();

                    e.Handled = true;
                    return;
                }

                if (!hasVerticalScroll && sv.ScrollableWidth > 0)
                {
                    if (e.Delta > 0)
                        sv.LineLeft();
                    else
                        sv.LineRight();

                    e.Handled = true;
                }
            };

            // Tilt wheel: hook Windows message for WM_MOUSEHWHEEL
            itemList.Loaded += (s, e) =>
            {
                var source = PresentationSource.FromVisual(itemList) as System.Windows.Interop.HwndSource;
                if (source != null)
                {
                    source.AddHook(ListViewTiltWheelHook);
                }
            };
        }

        private void SetupColumnResizeStabilizer()
        {
            GridView gv = itemList.View as GridView;
            if (gv == null) return;

            foreach (GridViewColumn col in gv.Columns)
            {
                DependencyPropertyDescriptor dpd = DependencyPropertyDescriptor.FromProperty(
                    GridViewColumn.WidthProperty, typeof(GridViewColumn));

                if (dpd != null)
                {
                    dpd.AddValueChanged(col, (s, e) =>
                    {
                        ScrollViewer sv = FindScrollViewer(itemList);
                        if (sv == null) return;

                        // Calculate total column width
                        double totalWidth = 0;
                        foreach (GridViewColumn c in gv.Columns)
                            totalWidth += c.ActualWidth;

                        // If columns are narrower than the viewport, clamp scroll to prevent jitter
                        if (totalWidth <= sv.ViewportWidth && sv.HorizontalOffset > 0)
                        {
                            sv.ScrollToHorizontalOffset(0);
                        }
                        else if (sv.HorizontalOffset > totalWidth - sv.ViewportWidth)
                        {
                            sv.ScrollToHorizontalOffset(totalWidth - sv.ViewportWidth);
                        }
                    });
                }
            }
        }

        private const int WM_MOUSEHWHEEL = 0x020E;

        private IntPtr ListViewTiltWheelHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL)
            {
                // Check if mouse is over the ListView
                Point mousePos = System.Windows.Input.Mouse.GetPosition(itemList);
                if (mousePos.X >= 0 && mousePos.Y >= 0 &&
                    mousePos.X <= itemList.ActualWidth && mousePos.Y <= itemList.ActualHeight)
                {
                    ScrollViewer sv = FindScrollViewer(itemList);
                    if (sv != null && sv.ScrollableWidth > 0)
                    {
                        int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);

                        if (delta > 0)
                            sv.LineRight();
                        else
                            sv.LineLeft();

                        handled = true;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private ScrollViewer FindScrollViewer(DependencyObject obj)
        {
            if (obj is ScrollViewer sv)
                return sv;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                ScrollViewer result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void BuildSharedControls()
        {
            // --- Category Tree Panel ---
            categoryPanel = new DockPanel();

            categoryTree = new TreeView();
            categoryTree.Margin = new Thickness(4);
            categoryTree.SelectedItemChanged += CategoryTree_Selected;

            // Keep the selected item highlighted even when tree loses focus
            categoryTree.Resources.Add(
                SystemColors.InactiveSelectionHighlightBrushKey,
                SystemColors.HighlightBrush);
            categoryTree.Resources.Add(
                SystemColors.InactiveSelectionHighlightTextBrushKey,
                SystemColors.HighlightTextBrush);
            categoryPanel.Children.Add(categoryTree);

			// Category Tree Context Menu
			ContextMenu categoryContextMenu = new ContextMenu();

			MenuItem menuOpenFolder = new MenuItem();
			menuOpenFolder.Header = "Open Folder";
			menuOpenFolder.Click += OpenCategoryFolder_Click;
			categoryContextMenu.Items.Add(menuOpenFolder);

			MenuItem menuRefreshCategory = new MenuItem();
			menuRefreshCategory.Header = "Refresh";
			menuRefreshCategory.Click += RefreshCategories_Click;
			categoryContextMenu.Items.Add(menuRefreshCategory);

			categoryContextMenu.Items.Add(new Separator());

			MenuItem menuNewCategory = new MenuItem();
			menuNewCategory.Header = "New";
			menuNewCategory.Click += NewCategory_Click;
			categoryContextMenu.Items.Add(menuNewCategory);

			MenuItem menuRenameCategory = new MenuItem();
			menuRenameCategory.Header = "Rename";
			menuRenameCategory.Click += RenameCategory_Click;
			categoryContextMenu.Items.Add(menuRenameCategory);

			categoryContextMenu.Items.Add(new Separator());

			MenuItem menuDeleteCategory = new MenuItem();
			menuDeleteCategory.Header = "Delete";
			menuDeleteCategory.Click += DeleteCategory_Click;
			categoryContextMenu.Items.Add(menuDeleteCategory);

			categoryTree.PreviewMouseRightButtonDown += (s, ev) =>
			{
			    DependencyObject source = ev.OriginalSource as DependencyObject;
			    while (source != null && !(source is TreeViewItem))
			    {
			        source = VisualTreeHelper.GetParent(source);
			    }
			    if (source is TreeViewItem tvi)
			    {
			        tvi.IsSelected = true;
			        ev.Handled = true;
			    }
			};

			categoryTree.ContextMenu = categoryContextMenu;

            // --- Item List ---
            itemList = new ListView();
            itemList.Margin = new Thickness(4);
            itemList.SelectionChanged += ItemList_SelectionChanged;
	        itemList.PreviewMouseLeftButtonDown += ItemList_PreviewMouseDown;
            itemList.KeyDown += (s, ev) =>
            {
                if (ev.Key == System.Windows.Input.Key.Delete)
                {
                    DeleteTrack_Click(s, ev);
                    ev.Handled = true;
                }
            };
            itemList.AddHandler(GridViewColumnHeader.ClickEvent,
                new RoutedEventHandler(ColumnHeader_Click));

            GridView gridView = new GridView();

            GridViewColumn checkCol = new GridViewColumn();
            checkCol.Width = 26;
		    FrameworkElementFactory headerCheckbox = new FrameworkElementFactory(typeof(CheckBox));
		    headerCheckbox.AddHandler(CheckBox.LoadedEvent, new RoutedEventHandler((s, ev) =>
		    {
		        selectAllCheckbox = s as CheckBox;
		    }));
		    headerCheckbox.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler(SelectAll_Click));
            checkCol.Header = new GridViewColumnHeader();
            checkCol.HeaderTemplate = new DataTemplate();
            checkCol.HeaderTemplate.VisualTree = headerCheckbox;
            FrameworkElementFactory cellCheckbox = new FrameworkElementFactory(typeof(CheckBox));
            cellCheckbox.SetBinding(CheckBox.IsCheckedProperty,
                new System.Windows.Data.Binding("IsSelected"));
            cellCheckbox.SetValue(CheckBox.HorizontalAlignmentProperty,
                HorizontalAlignment.Center);
            checkCol.CellTemplate = new DataTemplate();
            checkCol.CellTemplate.VisualTree = cellCheckbox;
            gridView.Columns.Add(checkCol);

            GridViewColumn statusCol = new GridViewColumn();
            statusCol.Header = "St";
            statusCol.Width = 30;
            statusCol.CellTemplate = CreateStatusCellTemplate();
            gridView.Columns.Add(statusCol);

            // Build columns from saved settings or defaults
            List<ColumnSetting> colSettings = windowSettings.ColumnSettings;
            if (colSettings.Count == 0)
                colSettings = WindowSettings.GetDefaultColumns();

            // Sort by DisplayIndex for correct order
            colSettings.Sort((a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));

            foreach (ColumnSetting cs in colSettings)
            {
                if (cs.Visible)
                {
                    string statusBinding = GetStatusBindingForColumn(cs.Binding);
                    if (statusBinding != null)
                        gridView.Columns.Add(MakeColumn(cs.Header, cs.Binding, cs.Width, statusBinding));
                    else
                        gridView.Columns.Add(MakeColumn(cs.Header, cs.Binding, cs.Width));
                }
            }

            itemList.View = gridView;
            ContextMenu listContextMenu = new ContextMenu();

            MenuItem ctxNewTrack = new MenuItem();
            ctxNewTrack.Header = "New Track";
            ctxNewTrack.Click += MenuNewTrack_Click;
            listContextMenu.Items.Add(ctxNewTrack);

            MenuItem ctxSave = new MenuItem();
            ctxSave.Header = "Save";
            ctxSave.Click += SaveTrack_Click;
            listContextMenu.Items.Add(ctxSave);

            MenuItem ctxSaveAs = new MenuItem();
            ctxSaveAs.Header = "Save As ...";
            ctxSaveAs.Click += SaveTrackAs_Click;
            listContextMenu.Items.Add(ctxSaveAs);

            listContextMenu.Items.Add(new Separator());

            MenuItem ctxOpenFile = new MenuItem();
            ctxOpenFile.Header = "Open File in Editor";
            ctxOpenFile.Click += OpenFileInEditor_Click;
            listContextMenu.Items.Add(ctxOpenFile);

            MenuItem ctxOpenSpsBuilder = new MenuItem();
            ctxOpenSpsBuilder.Header = "Open in SPS Builder";
            ctxOpenSpsBuilder.Click += OpenInSpsBuilder_Click;
            listContextMenu.Items.Add(ctxOpenSpsBuilder);

            Separator sep1 = new Separator();
            listContextMenu.Items.Add(sep1);

            MenuItem ctxIgnoreTrack = new MenuItem();
            ctxIgnoreTrack.Header = "Ignore Track (hide from ReBuild)";
            ctxIgnoreTrack.Click += IgnoreTrack_Click;
            listContextMenu.Items.Add(ctxIgnoreTrack);

            MenuItem ctxManageIgnored = new MenuItem();
            ctxManageIgnored.Header = "Manage Ignored Tracks...";
            ctxManageIgnored.Click += ManageIgnoredTracks_Click;
            listContextMenu.Items.Add(ctxManageIgnored);

            listContextMenu.Items.Add(new Separator());

            MenuItem ctxDelete = new MenuItem();
            ctxDelete.Header = "Delete";
            ctxDelete.Click += DeleteTrack_Click;
            listContextMenu.Items.Add(ctxDelete);

			// Only show SPS Builder option for SPS categories
			listContextMenu.Opened += (s, ev) =>
			{
			    bool isSps = false;
			    if (!string.IsNullOrEmpty(currentCategoryPath) && Directory.Exists(currentCategoryPath))
			    {
			        isSps = Directory.GetFiles(currentCategoryPath, "*.xml").Length > 0;
			    }
	            ctxOpenSpsBuilder.Visibility = isSps ? Visibility.Visible : Visibility.Collapsed;
	            sep1.Visibility = isSps ? Visibility.Visible : Visibility.Collapsed;
	            ctxIgnoreTrack.Visibility = isSps ? Visibility.Visible : Visibility.Collapsed;
	            ctxManageIgnored.Visibility = isSps ? Visibility.Visible : Visibility.Collapsed;
			};
			
            itemList.ContextMenu = listContextMenu;
            SetupListViewHorizontalScrolling();
            SetupColumnResizeStabilizer();

            // --- Track Settings Panel (Panel 3) ---
            BuildTrackSettingsPanel();

            // --- Tabbed Pane (Panel 4) ---
            BuildTabbedPane();
        }

        private void BuildTrackSettingsPanel()
        {
            trackSettingsPanel = new DockPanel();

            // ---- Fixed header: toggle button + label, non-scrollable ----
            DockPanel headerRow = new DockPanel();
            headerRow.Height = 30;
            DockPanel.SetDock(headerRow, Dock.Top);

            // Toggle button aligned with toolbar below
            Button btnToggleTrackPanel = new Button();
            btnToggleTrackPanel.Content = "\uE89F";
            btnToggleTrackPanel.FontFamily = new FontFamily("Segoe Fluent Icons");
            btnToggleTrackPanel.FontSize = 14;
            btnToggleTrackPanel.Width = 35;
            btnToggleTrackPanel.Margin = new Thickness(3, 0, 3, 0);
            btnToggleTrackPanel.Padding = new Thickness(0);
            btnToggleTrackPanel.ToolTip = "Collapse Track Settings panel";
            btnToggleTrackPanel.VerticalAlignment = VerticalAlignment.Stretch;
            btnToggleTrackPanel.HorizontalContentAlignment = HorizontalAlignment.Center;
            btnToggleTrackPanel.VerticalContentAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(btnToggleTrackPanel, Dock.Left);
            headerRow.Children.Add(btnToggleTrackPanel);

            trackSettingsLabel = new TextBlock();
            trackSettingsLabel.Text = "Track File";
            trackSettingsLabel.FontWeight = FontWeights.Bold;
            trackSettingsLabel.FontSize = 13;
            trackSettingsLabel.VerticalAlignment = VerticalAlignment.Center;
            trackSettingsLabel.Padding = new Thickness(8, 0, 0, 0);
            headerRow.Children.Add(trackSettingsLabel);

            btnToggleTrackSettings = btnToggleTrackPanel;

            btnToggleTrackPanel.Click += (s, ev) =>
            {
                if (!trackSettingsCollapsed)
                {
                    if (isHorizontalLayout)
                    {
                        Grid bottomGrid = trackSettingsHostH.Parent as Grid;
                        if (bottomGrid != null)
                        {
                            int col = Grid.GetColumn(trackSettingsHostH);
                            trackSettingsExpandedWidth = bottomGrid.ColumnDefinitions[col].ActualWidth;
                            bottomGrid.ColumnDefinitions[col].MinWidth = 0;
                            bottomGrid.ColumnDefinitions[col].Width = new GridLength(38, GridUnitType.Pixel);
                        }
                        trackToolBarTray.Margin = new Thickness(0, 0, 0, 0);
                    }
                    else
                    {
                        Grid innerGrid = trackSettingsHostV.Parent as Grid;
                        if (innerGrid != null)
                        {
                            trackSettingsExpandedHeight = innerGrid.RowDefinitions[2].ActualHeight;
                            innerGrid.RowDefinitions[2].MinHeight = 0;
                            trackSettingsHostV.MinHeight = 0;
                            innerGrid.RowDefinitions[2].Height = new GridLength(30, GridUnitType.Pixel);
                        }
                    }

                    trackSettingsSplitterH.IsEnabled = false;
                    trackSettingsSplitterV.IsEnabled = false;

                    btnToggleTrackPanel.Content = "\uE8A0";
                    btnToggleTrackPanel.ToolTip = "Expand Track Settings panel";
                    trackSettingsCollapsed = true;
                }
                else
                {
                    if (isHorizontalLayout)
                    {
                        trackToolBarTray.Margin = new Thickness(0, 0, 0, 0);

                        Grid bottomGrid = trackSettingsHostH.Parent as Grid;
                        if (bottomGrid != null)
                        {
                            int col = Grid.GetColumn(trackSettingsHostH);
                            bottomGrid.ColumnDefinitions[col].MinWidth = 250;
                            if (!Double.IsNaN(trackSettingsExpandedWidth) && trackSettingsExpandedWidth > 100)
                                bottomGrid.ColumnDefinitions[col].Width = new GridLength(trackSettingsExpandedWidth, GridUnitType.Pixel);
                            else
                                bottomGrid.ColumnDefinitions[col].Width = new GridLength(1, GridUnitType.Star);
                        }
                    }
                    else
                    {
                        Grid innerGrid = trackSettingsHostV.Parent as Grid;
                        if (innerGrid != null)
                        {
                            innerGrid.RowDefinitions[2].MinHeight = 250;
                            trackSettingsHostV.MinHeight = 150;
                            if (!Double.IsNaN(trackSettingsExpandedHeight) && trackSettingsExpandedHeight > 100)
                                innerGrid.RowDefinitions[2].Height = new GridLength(trackSettingsExpandedHeight, GridUnitType.Pixel);
                            else
                                innerGrid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
                        }
                    }

                    trackSettingsSplitterH.IsEnabled = true;
                    trackSettingsSplitterV.IsEnabled = true;

                    btnToggleTrackPanel.Content = "\uE89F";
                    btnToggleTrackPanel.ToolTip = "Collapse Track Settings panel";
                    trackSettingsCollapsed = false;
                }
            };

            trackSettingsPanel.Children.Add(headerRow);

            // ---- Vertical toolbar docked to the left ----
            trackToolBarTray = new ToolBarTray();
            trackToolBarTray.Orientation = Orientation.Vertical;
            DockPanel.SetDock(trackToolBarTray, Dock.Left);

            ToolBar trackToolBar = new ToolBar();
            trackToolBar.Band = 0;
            trackToolBar.BandIndex = 0;

            Button btnNewTrack = CreateToolBarButton("\xecc8", "New Track", MenuNewTrack_Click);
            trackToolBar.Items.Add(btnNewTrack);

            trackToolBar.Items.Add(new Separator());

            Button btnGoStart = CreateToolBarButton("\xe761", "Go to Version Start String", GoToStartString_Click);
            trackToolBar.Items.Add(btnGoStart);

            Button btnGoStop = CreateToolBarButton("\xe760", "Go to Version Stop String", GoToStopString_Click);
            trackToolBar.Items.Add(btnGoStop);

            trackToolBar.Items.Add(new Separator());

			Button btnGoDateStart = CreateToolBarButton("\xe787", "Go to Date Start String", GoToDateStartString_Click);
			trackToolBar.Items.Add(btnGoDateStart);

			Button btnGoDateStop = CreateToolBarButton("\xe786", "Go to Date Stop String", GoToDateStopString_Click);
			trackToolBar.Items.Add(btnGoDateStop);

            trackToolBar.Items.Add(new Separator());

            btnTrackMode = new Button();
            btnTrackMode.Content = "</>";
            btnTrackMode.FontFamily = SystemFonts.MessageFontFamily;
            btnTrackMode.FontSize = 12;
            btnTrackMode.FontWeight = FontWeights.Bold;
            btnTrackMode.Margin = new Thickness(1, 2, 1, 2);
			btnTrackMode.Padding = new Thickness(3, 1, 3, 3);
            btnTrackMode.ToolTip = "Track Mode: HTML (click to switch to Text)";
            btnTrackMode.Click += (s, ev) =>
            {
                if (currentTrackItem == null) return;

                if (currentTrackItem.TrackMode == "text")
                {
                    currentTrackItem.TrackMode = "html";
                    btnTrackMode.Content = "</>";
		            btnTrackMode.Padding = new Thickness(3, 1, 3, 3);
                    btnTrackMode.ToolTip = "Track Mode: HTML (click to switch to Text)";
                    statusFile.Text = "Track mode: HTML source";
                }
                else
                {
                    currentTrackItem.TrackMode = "text";
                    btnTrackMode.Content = "Aa";
		            btnTrackMode.Padding = new Thickness(6, 1, 6, 3);
                    btnTrackMode.ToolTip = "Track Mode: Text (click to switch to HTML)";
                    statusFile.Text = "Track mode: Rendered text";
                }

                MarkDirty();
                ReloadSourceForCurrentMode();
            };
            trackToolBar.Items.Add(btnTrackMode);

            Button btnDownloadToolbar = CreateToolBarButton("\xf000", "Download page source", DownloadPage_Click);
            trackToolBar.Items.Add(btnDownloadToolbar);

            Button btnBrowserToolbar = CreateToolBarButton("\uE774", "Open in browser", OpenBrowser_Click);
            trackToolBar.Items.Add(btnBrowserToolbar);

            trackToolBar.Items.Add(new Separator());

            btnUpdateVersion = CreateToolBarButton("\uE898", "Copy Latest Version to Version field", UpdateVersion_Click);
            btnUpdateVersion.IsEnabled = false;
            btnUpdateVersion.Opacity = 0.4;
            btnUpdateVersion.IsEnabledChanged += (s, e) =>
            {
                btnUpdateVersion.Opacity = btnUpdateVersion.IsEnabled ? 1.0 : 0.4;
            };
            trackToolBar.Items.Add(btnUpdateVersion);

            Button btnDownloadFile = CreateToolBarButton("\uE118", "Download file using Download URL", DownloadFile_Click);
            trackToolBar.Items.Add(btnDownloadFile);

            trackToolBarTray.ToolBars.Add(trackToolBar);

            // Hide the overflow grip so buttons align left
            trackToolBar.Loaded += (s, ev) =>
            {
                // Prevent all items from going into overflow
                foreach (var item in trackToolBar.Items)
                {
                    if (item is FrameworkElement fe)
                        ToolBar.SetOverflowMode(fe, OverflowMode.Never);
                }

                var overflowGrid = trackToolBar.Template.FindName("OverflowGrid", trackToolBar) as FrameworkElement;
                if (overflowGrid != null)
                    overflowGrid.Visibility = Visibility.Collapsed;

                var mainPanel = trackToolBar.Template.FindName("PART_ToolBarPanel", trackToolBar) as FrameworkElement;
                if (mainPanel != null && mainPanel is System.Windows.Controls.Primitives.ToolBarPanel tbp)
                    tbp.HorizontalAlignment = HorizontalAlignment.Left;
            };

            // Force narrow width to match the old horizontal toolbar height
            trackToolBarTray.Width = 38;
            trackToolBarTray.Margin = new Thickness(0, 0, 0, 0);

            trackSettingsPanel.Children.Add(trackToolBarTray);

            // ---- Scrollable fields area (fills remaining space) ----
            ScrollViewer scrollArea = new ScrollViewer();
            scrollArea.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            trackSettingsScrollArea = scrollArea;

			StackPanel fieldsPanel = new StackPanel();
			fieldsPanel.Margin = new Thickness(8);

			// ==============================
			// Group 1: Track Data
			// ==============================
			TextBlock trackDataHeader = new TextBlock();
			trackDataHeader.Text = "Track Data";
			trackDataHeader.FontWeight = FontWeights.Bold;
			trackDataHeader.FontSize = 13;
			trackDataHeader.Foreground = new SolidColorBrush(currentTheme.TabSelectedForeground);
			trackDataHeader.Margin = new Thickness(0, 0, 0, 8);

			Border trackDataBorder = new Border();
			trackDataBorder.BorderBrush = new SolidColorBrush(currentTheme.SplitterColor);
			trackDataBorder.BorderThickness = new Thickness(1);
			trackDataBorder.CornerRadius = new CornerRadius(4);
			trackDataBorder.Padding = new Thickness(8);
			trackDataBorder.Margin = new Thickness(0, 0, 0, 12);
			trackDataBorder.Tag = "TrackDataBorder";

			StackPanel trackDataPanel = new StackPanel();
			trackDataPanel.Children.Add(trackDataHeader);

			editName = AddSettingsField(trackDataPanel, "Track Name:");
			editName.ToolTip = "Set a name for your track. This is not a filename.";

			// Track URL
			TextBlock urlLabel = new TextBlock();
			urlLabel.Text = "Track URL:";
			urlLabel.Margin = new Thickness(0, 0, 0, 4);
			trackDataPanel.Children.Add(urlLabel);

			DockPanel urlDock = new DockPanel();
			urlDock.Margin = new Thickness(0, 0, 0, 8);

			editTrackURL = new TextBox();
			editTrackURL.ToolTip = "The webpage to check for info.";
			urlDock.Children.Add(editTrackURL);
			trackDataPanel.Children.Add(urlDock);

			editDownloadURL = AddSettingsField(trackDataPanel, "Download URL:");
			editDownloadURL.ToolTip =
			    "Use {VERSION} placeholders to auto-insert the current version:\n\n" +
			    "  {VERSION}    inserts version as-is, e.g. 2.18\n" +
			    "  {VERSION_}   replaces dots with underscores, e.g. 2_18\n" +
			    "  {VERSION-}   replaces dots with hyphens, e.g. 2-18\n\n" +
			    "Example:\n" +
			    "https://example.com/downloads/App_{VERSION_}.exe\n" +
			    "resolves to: https://example.com/downloads/App_2_18.exe";

			editVersion = AddSettingsField(trackDataPanel, "Version:", 150);
			editVersion.ToolTip = "After a check, use Update Version button to update this field.";

			// Read-only latest version display
			TextBlock latestLabel = new TextBlock();
			latestLabel.Text = "Latest Version (auto-detected):";
			latestLabel.Margin = new Thickness(0, 0, 0, 4);
			trackDataPanel.Children.Add(latestLabel);

			DockPanel latestDock = new DockPanel();
			latestDock.Margin = new Thickness(0, 0, 0, 8);

			TextBlock latestDisplay = new TextBlock();
			latestDisplay.Text = "";
			latestDisplay.FontWeight = FontWeights.Bold;
			latestDisplay.FontSize = 14;
			latestDisplay.VerticalAlignment = VerticalAlignment.Center;
			latestDisplay.ToolTip = "After a check, this displays last tracked info.";
			latestDock.Children.Add(latestDisplay);
			editLatestVersion = latestDisplay;

			trackDataPanel.Children.Add(latestDock);

			// Metadata fields
			editReleaseDate = AddSettingsField(trackDataPanel, "Release Date:");
			editReleaseDate.ToolTip = "Shows the current version release date.";
			editPublisherName = AddSettingsField(trackDataPanel, "Publisher Name:");
			editPublisherName.ToolTip = "Used to display info from SPS files.";
			editSuiteName = AddSettingsField(trackDataPanel, "Suite Name:");
			editSuiteName.ToolTip = "Used to display info from SPS files.";

			trackDataBorder.Child = trackDataPanel;
			fieldsPanel.Children.Add(trackDataBorder);

			// ==============================
			// Group 2: Track Settings
			// ==============================
			TextBlock trackSettingsHeader = new TextBlock();
			trackSettingsHeader.Text = "Track Settings";
			trackSettingsHeader.FontWeight = FontWeights.Bold;
			trackSettingsHeader.FontSize = 13;
			trackSettingsHeader.Foreground = new SolidColorBrush(currentTheme.TabSelectedForeground);
			trackSettingsHeader.Margin = new Thickness(0, 0, 0, 8);

			Border trackSettingsBorder = new Border();
			trackSettingsBorder.BorderBrush = new SolidColorBrush(currentTheme.SplitterColor);
			trackSettingsBorder.BorderThickness = new Thickness(1);
			trackSettingsBorder.CornerRadius = new CornerRadius(4);
			trackSettingsBorder.Padding = new Thickness(8);
			trackSettingsBorder.Margin = new Thickness(0, 0, 0, 12);
			trackSettingsBorder.Tag = "TrackSettingsBorder";

			StackPanel trackSettingsFieldsPanel = new StackPanel();
			trackSettingsFieldsPanel.Children.Add(trackSettingsHeader);

			// Version Start String with position text
			DockPanel startLabelDock = new DockPanel();
			startLabelDock.Margin = new Thickness(0, 0, 0, 4);

			startPositionText = new TextBlock();
			startPositionText.Text = "";
			startPositionText.Foreground = Brushes.Gray;
			startPositionText.FontSize = 11;
			startPositionText.HorizontalAlignment = HorizontalAlignment.Right;
			DockPanel.SetDock(startPositionText, Dock.Right);
			startLabelDock.Children.Add(startPositionText);

			TextBlock startLabel = new TextBlock();
			startLabel.Text = "Version Start String:";
			startLabelDock.Children.Add(startLabel);

			trackSettingsFieldsPanel.Children.Add(startLabelDock);

			DockPanel startDock = new DockPanel();
			startDock.Margin = new Thickness(0, 0, 0, 8);

			editStartString = new TextBox();
			editStartString.Height = 60;
			editStartString.AcceptsReturn = true;
			editStartString.TextWrapping = TextWrapping.Wrap;
			editStartString.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			editStartString.ToolTip = "Enter unique identifier for the start string.";
			startDock.Children.Add(editStartString);
			trackSettingsFieldsPanel.Children.Add(startDock);
			editStartString.TextChanged += (s, ev) =>
			{
			    if (!isLoadingFields)
			        MarkDirty();
			};

			// Version Stop String with position text
			DockPanel stopLabelDock = new DockPanel();
			stopLabelDock.Margin = new Thickness(0, 0, 0, 4);

			stopPositionText = new TextBlock();
			stopPositionText.Text = "";
			stopPositionText.Foreground = Brushes.Gray;
			stopPositionText.FontSize = 11;
			stopPositionText.HorizontalAlignment = HorizontalAlignment.Right;
			DockPanel.SetDock(stopPositionText, Dock.Right);
			stopLabelDock.Children.Add(stopPositionText);

			TextBlock stopLabel = new TextBlock();
			stopLabel.Text = "Version Stop String:";
			stopLabelDock.Children.Add(stopLabel);

			trackSettingsFieldsPanel.Children.Add(stopLabelDock);

			DockPanel stopDock = new DockPanel();
			stopDock.Margin = new Thickness(0, 0, 0, 8);

			editStopString = new TextBox();
			editStopString.Height = 60;
			editStopString.AcceptsReturn = true;
			editStopString.TextWrapping = TextWrapping.Wrap;
			editStopString.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			editStopString.ToolTip = "Enter a string that follows info to be tracked.";
			stopDock.Children.Add(editStopString);
			trackSettingsFieldsPanel.Children.Add(stopDock);

			editStopString.TextChanged += (s, ev) =>
			{
			    if (!isLoadingFields)
			    {
			        MarkDirty();
			        if (currentTrackItem != null)
			            currentTrackItem.StopString = editStopString.Text;
			    }
			};

			// Release Date Start String with position text
			DockPanel dateStartLabelDock = new DockPanel();
			dateStartLabelDock.Margin = new Thickness(0, 0, 0, 4);

			dateStartPositionText = new TextBlock();
			dateStartPositionText.Text = "";
			dateStartPositionText.Foreground = Brushes.Gray;
			dateStartPositionText.FontSize = 11;
			dateStartPositionText.HorizontalAlignment = HorizontalAlignment.Right;
			DockPanel.SetDock(dateStartPositionText, Dock.Right);
			dateStartLabelDock.Children.Add(dateStartPositionText);

			TextBlock dateStartLabel = new TextBlock();
			dateStartLabel.Text = "Release Date Start String:";
			dateStartLabelDock.Children.Add(dateStartLabel);

			trackSettingsFieldsPanel.Children.Add(dateStartLabelDock);

			DockPanel dateStartDock = new DockPanel();
			dateStartDock.Margin = new Thickness(0, 0, 0, 8);

			editReleaseDateStartString = new TextBox();
			editReleaseDateStartString.Height = 60;
			editReleaseDateStartString.AcceptsReturn = true;
			editReleaseDateStartString.TextWrapping = TextWrapping.Wrap;
			editReleaseDateStartString.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			editReleaseDateStartString.Tag = "ThemeTextBox";
			editReleaseDateStartString.ToolTip = "Enter unique identifier for the release date start string.";
			editReleaseDateStartString.TextChanged += (s, ev) =>
			{
			    if (!isLoadingFields) MarkDirty();
			};
			dateStartDock.Children.Add(editReleaseDateStartString);
			trackSettingsFieldsPanel.Children.Add(dateStartDock);

			// Release Date Stop String with position text
			DockPanel dateStopLabelDock = new DockPanel();
			dateStopLabelDock.Margin = new Thickness(0, 0, 0, 4);

			dateStopPositionText = new TextBlock();
			dateStopPositionText.Text = "";
			dateStopPositionText.Foreground = Brushes.Gray;
			dateStopPositionText.FontSize = 11;
			dateStopPositionText.HorizontalAlignment = HorizontalAlignment.Right;
			DockPanel.SetDock(dateStopPositionText, Dock.Right);
			dateStopLabelDock.Children.Add(dateStopPositionText);

			TextBlock dateStopLabel = new TextBlock();
			dateStopLabel.Text = "Release Date Stop String:";
			dateStopLabelDock.Children.Add(dateStopLabel);

			trackSettingsFieldsPanel.Children.Add(dateStopLabelDock);

			DockPanel dateStopDock = new DockPanel();
			dateStopDock.Margin = new Thickness(0, 0, 0, 8);

			editReleaseDateStopString = new TextBox();
			editReleaseDateStopString.Height = 60;
			editReleaseDateStopString.AcceptsReturn = true;
			editReleaseDateStopString.TextWrapping = TextWrapping.Wrap;
			editReleaseDateStopString.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			editReleaseDateStopString.Tag = "ThemeTextBox";
			editReleaseDateStopString.ToolTip = "Enter a string that follows the release date.";
			editReleaseDateStopString.TextChanged += (s, ev) =>
			{
			    if (!isLoadingFields) MarkDirty();
			};
			dateStopDock.Children.Add(editReleaseDateStopString);
			trackSettingsFieldsPanel.Children.Add(dateStopDock);

			trackSettingsBorder.Child = trackSettingsFieldsPanel;
			fieldsPanel.Children.Add(trackSettingsBorder);

            scrollArea.Content = fieldsPanel;
            trackSettingsPanel.Children.Add(scrollArea);

            // Wire up dirty tracking on all editable fields
            editName.TextChanged += Field_TextChanged;
            editTrackURL.TextChanged += Field_TextChanged;
            editStartString.TextChanged += Field_TextChanged;
            editStopString.TextChanged += Field_TextChanged;
            editDownloadURL.TextChanged += Field_TextChanged;
            editVersion.TextChanged += Field_TextChanged;
            editReleaseDate.TextChanged += Field_TextChanged;
            editPublisherName.TextChanged += Field_TextChanged;
            editSuiteName.TextChanged += Field_TextChanged;

			AddSelectAllOnRightClick(editName);
			AddSelectAllOnRightClick(editTrackURL);
			AddSelectAllOnRightClick(editStartString);
			AddSelectAllOnRightClick(editStopString);
			AddSelectAllOnRightClick(editDownloadURL);
			AddSelectAllOnRightClick(editVersion);
			AddSelectAllOnRightClick(editReleaseDate);
			AddSelectAllOnRightClick(editPublisherName);
			AddSelectAllOnRightClick(editSuiteName);
        }

        private void BuildTabbedPane()
        {
            rightTabs = new TabControl();
            rightTabs.Margin = new Thickness(4);

            // Tab 1: Source
			TabItem sourceTab = new TabItem();
			sourceTab.Header = "📄 Source";
			DockPanel sourcePanel = new DockPanel();

			// Source tab toolbar
			ToolBarTray sourceToolBarTray = new ToolBarTray();
			DockPanel.SetDock(sourceToolBarTray, Dock.Top);

			ToolBar sourceToolBar = new ToolBar();
			sourceToolBar.Band = 0;
			sourceToolBar.BandIndex = 0;

			findString = new TextBox();
			findString.Width = 300;
			findString.Margin = new Thickness(0, 0, 4, 0);
			findString.PreviewKeyDown += (s, ev) =>
			{
			    if (ev.Key == System.Windows.Input.Key.Enter)
			    {
			        SearchDown_Click(findString, new RoutedEventArgs());
			        ev.Handled = true;
			    }
			};
			sourceToolBar.Items.Add(findString);

			Button btnSearchDown = new Button();
			StackPanel spDown = new StackPanel();
			spDown.Orientation = Orientation.Horizontal;

            spDown = new StackPanel();
            spDown.Orientation = Orientation.Horizontal;

            TextBlock iconSearchDown = new TextBlock();
            iconSearchDown.Text = "\uE721";
            iconSearchDown.FontFamily = new FontFamily("Segoe Fluent Icons");
            iconSearchDown.FontSize = 15;
            iconSearchDown.VerticalAlignment = VerticalAlignment.Center;
            spDown.Children.Add(iconSearchDown);

            TextBlock iconArrowDown = new TextBlock();
            iconArrowDown.Text = "\uE760";
            iconArrowDown.FontFamily = new FontFamily("Segoe Fluent Icons");
            iconArrowDown.FontSize = 15;
            iconArrowDown.Margin = new Thickness(2, 0, 0, 0);
            iconArrowDown.VerticalAlignment = VerticalAlignment.Center;
            iconArrowDown.RenderTransformOrigin = new Point(0.5, 0.5);
            iconArrowDown.RenderTransform = new RotateTransform(270);
            spDown.Children.Add(iconArrowDown);

            btnSearchDown.Content = spDown;
            btnSearchDown.Width = 42;
            btnSearchDown.Padding = new Thickness(0, 2, 0, 2);
            btnSearchDown.Margin = new Thickness(2, 0, 2, 0);
            btnSearchDown.ToolTip = "Search next from top";
            btnSearchDown.Click += SearchDown_Click;
            sourceToolBar.Items.Add(btnSearchDown);

            Button btnSearchUp = new Button();
            StackPanel spUp = new StackPanel();
            spUp.Orientation = Orientation.Horizontal;

            TextBlock iconSearchUp = new TextBlock();
            iconSearchUp.Text = "\uE721";
            iconSearchUp.FontFamily = new FontFamily("Segoe Fluent Icons");
            iconSearchUp.FontSize = 15;
            iconSearchUp.VerticalAlignment = VerticalAlignment.Center;
            spUp.Children.Add(iconSearchUp);

            TextBlock iconArrowUp = new TextBlock();
            iconArrowUp.Text = "\uE761";
            iconArrowUp.FontFamily = new FontFamily("Segoe Fluent Icons");
            iconArrowUp.FontSize = 15;
            iconArrowUp.Margin = new Thickness(2, 0, 0, 0);
            iconArrowUp.VerticalAlignment = VerticalAlignment.Center;
            iconArrowUp.RenderTransformOrigin = new Point(0.5, 0.5);
            iconArrowUp.RenderTransform = new RotateTransform(270);
            spUp.Children.Add(iconArrowUp);

            btnSearchUp.Content = spUp;
            btnSearchUp.Width = 42;
            btnSearchUp.Padding = new Thickness(0, 2, 0, 2);
            btnSearchUp.Margin = new Thickness(2, 0, 2, 0);
            btnSearchUp.ToolTip = "Search next from bottom";
            btnSearchUp.Click += SearchUp_Click;
            sourceToolBar.Items.Add(btnSearchUp);

            sourceToolBar.Items.Add(new Separator());

            Button btnFontSmaller = CreateToolBarButton("\uE8E7", "Decrease font size", null);
            btnFontSmaller.Click += (s, ev) =>
            {
                if (sourceView.FontSize > 6)
                {
                    sourceView.FontSize -= 1;
                    if (!string.IsNullOrEmpty(currentSource))
                        DisplaySource(currentSource);
                }
            };
            sourceToolBar.Items.Add(btnFontSmaller);

            Button btnFontLarger = CreateToolBarButton("\uE8E8", "Increase font size", null);
            btnFontLarger.Click += (s, ev) =>
            {
                if (sourceView.FontSize < 30)
                {
                    sourceView.FontSize += 1;
                    if (!string.IsNullOrEmpty(currentSource))
                        DisplaySource(currentSource);
                }
            };
            sourceToolBar.Items.Add(btnFontLarger);

			sourceToolBar.Items.Add(new Separator());

			findCount = new TextBlock();
			findCount.VerticalAlignment = VerticalAlignment.Center;
			findCount.Margin = new Thickness(4, 0, 0, 0);
			sourceToolBar.Items.Add(findCount);

			sourceToolBarTray.ToolBars.Add(sourceToolBar);
			sourcePanel.Children.Add(sourceToolBarTray);

			// Info banner — shown when HTML source looks empty/JS-loaded
			sourceInfoBanner = new TextBlock();
			sourceInfoBanner.Text = "ℹ HTML mode shows raw unexecuted source code. If content appears " +
			    "missing, the site likely loads via JavaScript — use the Track Mode button (Aa) to " +
			    "switch to Text mode for rendered content.";
			sourceInfoBanner.TextWrapping = TextWrapping.Wrap;
			sourceInfoBanner.Padding = new Thickness(8, 5, 8, 5);
			sourceInfoBanner.FontSize = 11;
			sourceInfoBanner.Background = new SolidColorBrush(currentTheme.SourceBannerBackground);
			sourceInfoBanner.Foreground = new SolidColorBrush(currentTheme.SourceBannerForeground);
			sourceInfoBanner.Visibility = Visibility.Collapsed;
			DockPanel.SetDock(sourceInfoBanner, Dock.Top);
			sourcePanel.Children.Add(sourceInfoBanner);

			sourceView = new RichTextBox();
			sourceView.IsReadOnly = true;
			sourceView.FontFamily = new FontFamily("Consolas");
			sourceView.FontSize = windowSettings.SourceFontSize;
			sourceView.Foreground = new SolidColorBrush(Color.FromRgb(40, 80, 180));
			sourceView.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
			sourceView.AutoWordSelection = false;
            // Context menu for adding selected text to Start/Stop strings
            ContextMenu sourceContextMenu = new ContextMenu();

            MenuItem menuCopy = new MenuItem();
            menuCopy.Header = "Copy";
            menuCopy.Click += (s, ev) =>
            {
                if (!sourceView.Selection.IsEmpty)
                    Clipboard.SetText(sourceView.Selection.Text);
            };
            sourceContextMenu.Items.Add(menuCopy);

            sourceContextMenu.Items.Add(new Separator());

            MenuItem menuSetStartString = new MenuItem();
            menuSetStartString.Header = "Set Version Start String";
            menuSetStartString.Click += (s, ev) =>
            {
                string selected = sourceView.Selection.Text.Trim();
                if (string.IsNullOrEmpty(selected)) return;

                editStartString.Text = selected;
                MarkDirty();
                statusFile.Text = "Start String set from selection";

                if (!string.IsNullOrEmpty(currentSource))
                    DisplaySource(currentSource);
            };
            sourceContextMenu.Items.Add(menuSetStartString);

            MenuItem menuSetStopString = new MenuItem();
            menuSetStopString.Header = "Set Version Stop String";
            menuSetStopString.Click += (s, ev) =>
            {
                string selected = sourceView.Selection.Text.Trim();
                if (string.IsNullOrEmpty(selected)) return;

                editStopString.Text = selected;
                MarkDirty();
                statusFile.Text = "Stop String set from selection";

                if (!string.IsNullOrEmpty(currentSource))
                    DisplaySource(currentSource);
            };
            sourceContextMenu.Items.Add(menuSetStopString);

			MenuItem menuSetDateStartString = new MenuItem();
			menuSetDateStartString.Header = "Set Date Start String";
			menuSetDateStartString.Click += (s, ev) =>
			{
			    string selected = sourceView.Selection.Text.Trim();
			    if (string.IsNullOrEmpty(selected)) return;

			    editReleaseDateStartString.Text = selected;
			    MarkDirty();
			    statusFile.Text = "Date Start String set from selection";

			    if (!string.IsNullOrEmpty(currentSource))
			        DisplaySource(currentSource);
			};
			sourceContextMenu.Items.Add(menuSetDateStartString);

			MenuItem menuSetDateStopString = new MenuItem();
			menuSetDateStopString.Header = "Set Date Stop String";
			menuSetDateStopString.Click += (s, ev) =>
			{
			    string selected = sourceView.Selection.Text.Trim();
			    if (string.IsNullOrEmpty(selected)) return;

			    editReleaseDateStopString.Text = selected;
			    MarkDirty();
			    statusFile.Text = "Date Stop String set from selection";

			    if (!string.IsNullOrEmpty(currentSource))
			        DisplaySource(currentSource);
			};
			sourceContextMenu.Items.Add(menuSetDateStopString);

            MenuItem menuAddReleaseDate = new MenuItem();
            menuAddReleaseDate.Header = "Set Release Date";
            menuAddReleaseDate.Click += (s, ev) =>
            {
                string selected = sourceView.Selection.Text.Trim();
                if (string.IsNullOrEmpty(selected)) return;

                editReleaseDate.Text = selected;
                MarkDirty();
                statusFile.Text = "Release Date set from selection: " + selected;
            };
            sourceContextMenu.Items.Add(menuAddReleaseDate);

            MenuItem menuSetDownloadUrl = new MenuItem();
            menuSetDownloadUrl.Header = "Set Download URL";
            menuSetDownloadUrl.Click += (s, ev) =>
            {
                string selected = sourceView.Selection.Text.Trim();
                if (string.IsNullOrEmpty(selected)) return;

                editDownloadURL.Text = selected;
                MarkDirty();
                statusFile.Text = "Download URL set from selection: " + selected;
            };
            sourceContextMenu.Items.Add(menuSetDownloadUrl);

            // Only enable options when text is selected; conditional items only when they match
            sourceContextMenu.Opened += (s, ev) =>
            {
                bool hasSelection = !sourceView.Selection.IsEmpty &&
                                    !string.IsNullOrWhiteSpace(sourceView.Selection.Text);
                string selectedText = hasSelection ? sourceView.Selection.Text.Trim() : "";

                menuCopy.IsEnabled = hasSelection;
                menuSetStartString.IsEnabled = hasSelection;
                menuSetStopString.IsEnabled = hasSelection;
                menuSetDateStartString.IsEnabled = hasSelection;
				menuSetDateStopString.IsEnabled = hasSelection;

                if (hasSelection && LooksLikeDate(selectedText))
                {
                    menuAddReleaseDate.Visibility = Visibility.Visible;
                    menuAddReleaseDate.IsEnabled = true;
                }
                else
                {
                    menuAddReleaseDate.Visibility = Visibility.Collapsed;
                }

                if (hasSelection && LooksLikeDownloadUrl(selectedText))
                {
                    menuSetDownloadUrl.Visibility = Visibility.Visible;
                    menuSetDownloadUrl.IsEnabled = true;
                }
                else
                {
                    menuSetDownloadUrl.Visibility = Visibility.Collapsed;
                }
            };

            sourceView.ContextMenu = sourceContextMenu;
			sourcePanel.Children.Add(sourceView);

			sourceTab.Content = sourcePanel;
			rightTabs.Items.Add(sourceTab);

            // Tab 4: App Settings
            appSettingsTabItem = new TabItem();
            TabItem appSettingsTab = appSettingsTabItem;
            appSettingsTab.Header = CreateClosableTabHeader("⚙ Settings", appSettingsTab);

            DockPanel appSettingsOuter = new DockPanel();

            // App Settings toolbar at the top
            ToolBarTray appSettingsToolBarTray = new ToolBarTray();
            DockPanel.SetDock(appSettingsToolBarTray, Dock.Top);

            ToolBar appSettingsToolBar = new ToolBar();
            appSettingsToolBar.Band = 0;
            appSettingsToolBar.BandIndex = 0;

            TextBlock appSettingsTitle = new TextBlock();
            appSettingsTitle.Text = "Application Settings";
            appSettingsTitle.FontWeight = FontWeights.Bold;
            appSettingsTitle.FontSize = 13;
            appSettingsTitle.VerticalAlignment = VerticalAlignment.Center;
            appSettingsTitle.Margin = new Thickness(2, 0, 8, 0);
            appSettingsToolBar.Items.Add(appSettingsTitle);

            appSettingsToolBar.Items.Add(new Separator());

			Button btnApplyColumns = CreateToolBarButton("\xe930", "Apply column visibility changes to the list", ApplyColumnSettings_Click);
            appSettingsToolBar.Items.Add(btnApplyColumns);

			Button btnSaveSettings = CreateToolBarButton("\uE74E", "Save all application settings now", SaveAppSettings_Click);
            appSettingsToolBar.Items.Add(btnSaveSettings);

			Button btnRestoreDefaults = CreateToolBarButton("\xe845", "Reset all column settings to defaults", RestoreDefaultColumns_Click);
            appSettingsToolBar.Items.Add(btnRestoreDefaults);

			appSettingsToolBar.Items.Add(new Separator());

			ComboBox toolbarCombo = new ComboBox();
			toolbarCombo.Width = 100;
			toolbarCombo.Items.Add("Top");
			toolbarCombo.Items.Add("Bottom");
			toolbarCombo.Items.Add("Left");
			toolbarCombo.Items.Add("Right");
			string savedPos = windowSettings.ToolbarPosition ?? "Top";
			int comboIndex = 0;
			for (int i = 0; i < toolbarCombo.Items.Count; i++)
			{
			    if ((string)toolbarCombo.Items[i] == savedPos)
			    {
			        comboIndex = i;
			        break;
			    }
			}
			toolbarCombo.SelectedIndex = comboIndex;
			toolbarCombo.SelectionChanged += (s, ev) =>
			{
			    string pos = toolbarCombo.SelectedItem as string;
			    if (pos != null)
			    {
			        DockToolbarTo(pos);
			    }
			};
			appSettingsToolBar.Items.Add(toolbarCombo);

			TextBlock toolbarPosLabel = new TextBlock();
			toolbarPosLabel.Text = "Toolbar Position";
			toolbarPosLabel.VerticalAlignment = VerticalAlignment.Center;
			toolbarPosLabel.Margin = new Thickness(4, 0, 0, 0);
			appSettingsToolBar.Items.Add(toolbarPosLabel);

            appSettingsToolBarTray.ToolBars.Add(appSettingsToolBar);
            appSettingsOuter.Children.Add(appSettingsToolBarTray);

            // Scrollable content area below the toolbar
            ScrollViewer appSettingsScroll = new ScrollViewer();
            appSettingsScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            StackPanel appSettingsPanel = new StackPanel();
            appSettingsPanel.Margin = new Thickness(12);

            // Editor path setting (bordered)
            Border editorBorder = new Border();
            editorBorder.BorderBrush = Brushes.Gray;
            editorBorder.BorderThickness = new Thickness(1);
            editorBorder.CornerRadius = new CornerRadius(4);
            editorBorder.Padding = new Thickness(10);
            editorBorder.Margin = new Thickness(0, 0, 0, 16);

            StackPanel editorGroupPanel = new StackPanel();

            TextBlock editorLabel = new TextBlock();
            editorLabel.Text = "Text Editor Path:";
            editorLabel.FontWeight = FontWeights.Bold;
            editorLabel.Margin = new Thickness(0, 0, 0, 4);
            editorLabel.Tag = "ThemeGroupHeader";
            editorGroupPanel.Children.Add(editorLabel);

            TextBlock editorHint = new TextBlock();
            editorHint.Text = "Custom text editor path for .track files. Can be relative to app folder. If blank, uses system default.";
            editorHint.Foreground = Brushes.Gray;
            editorHint.FontSize = 11;
            editorHint.TextWrapping = TextWrapping.Wrap;
            editorHint.Margin = new Thickness(0, 0, 0, 4);
            editorGroupPanel.Children.Add(editorHint);

            DockPanel editorDock = new DockPanel();

            Button btnBrowseEditor = new Button();
            btnBrowseEditor.Content = "\xe838";
            btnBrowseEditor.FontFamily = new FontFamily("Segoe Fluent Icons");
            btnBrowseEditor.FontSize = 16;
            btnBrowseEditor.Padding = new Thickness(8, 4, 8, 4);
            btnBrowseEditor.Margin = new Thickness(4, 0, 0, 0);
            btnBrowseEditor.ToolTip = "Browse for a text editor executable";
            btnBrowseEditor.Click += BrowseEditor_Click;
            DockPanel.SetDock(btnBrowseEditor, Dock.Right);
            editorDock.Children.Add(btnBrowseEditor);

            editEditorPath = new TextBox();
            editEditorPath.Text = windowSettings.EditorPath ?? "";
            editorDock.Children.Add(editEditorPath);

            editorGroupPanel.Children.Add(editorDock);

            editorBorder.Child = editorGroupPanel;
            appSettingsPanel.Children.Add(editorBorder);

			// Default Track Mode setting (bordered)
			Border trackModeBorder = new Border();
			trackModeBorder.BorderBrush = Brushes.Gray;
			trackModeBorder.BorderThickness = new Thickness(1);
			trackModeBorder.CornerRadius = new CornerRadius(4);
			trackModeBorder.Padding = new Thickness(10);
			trackModeBorder.Margin = new Thickness(0, 0, 0, 16);

			StackPanel trackModeGroupPanel = new StackPanel();

			TextBlock trackModeLabel = new TextBlock();
			trackModeLabel.Text = "Default Track Mode:";
			trackModeLabel.FontWeight = FontWeights.Bold;
			trackModeLabel.Margin = new Thickness(0, 0, 0, 4);
			trackModeLabel.Tag = "ThemeGroupHeader";
			trackModeGroupPanel.Children.Add(trackModeLabel);

			TextBlock trackModeHint = new TextBlock();
			trackModeHint.Text = "Sets the default mode for new tracks. HTML checks the raw page source; Text uses the browser-rendered text. Each track remembers its own mode after creation.";
			trackModeHint.Foreground = Brushes.Gray;
			trackModeHint.FontSize = 11;
			trackModeHint.TextWrapping = TextWrapping.Wrap;
			trackModeHint.Margin = new Thickness(0, 0, 0, 8);
			trackModeGroupPanel.Children.Add(trackModeHint);

			defaultTrackModeCombo = new ComboBox();
			defaultTrackModeCombo.Width = 150;
			defaultTrackModeCombo.HorizontalAlignment = HorizontalAlignment.Left;
			defaultTrackModeCombo.Items.Add("HTML (page source)");
			defaultTrackModeCombo.Items.Add("Text (rendered text)");
			defaultTrackModeCombo.SelectedIndex = (windowSettings.DefaultTrackMode == "text") ? 1 : 0;
			defaultTrackModeCombo.SelectionChanged += (s, ev) =>
			{
			    windowSettings.DefaultTrackMode = (defaultTrackModeCombo.SelectedIndex == 1) ? "text" : "html";
			};
			trackModeGroupPanel.Children.Add(defaultTrackModeCombo);

			trackModeBorder.Child = trackModeGroupPanel;
			appSettingsPanel.Children.Add(trackModeBorder);

			// Default Publisher Name setting (bordered)
			Border publisherBorder = new Border();
			publisherBorder.BorderBrush = Brushes.Gray;
			publisherBorder.BorderThickness = new Thickness(1);
			publisherBorder.CornerRadius = new CornerRadius(4);
			publisherBorder.Padding = new Thickness(10);
			publisherBorder.Margin = new Thickness(0, 0, 0, 16);

			StackPanel publisherGroupPanel = new StackPanel();

			TextBlock publisherLabel = new TextBlock();
			publisherLabel.Text = "Default Publisher Name:";
			publisherLabel.FontWeight = FontWeights.Bold;
			publisherLabel.Margin = new Thickness(0, 0, 0, 4);
			publisherLabel.Tag = "ThemeGroupHeader";
			publisherGroupPanel.Children.Add(publisherLabel);

			TextBlock publisherHint = new TextBlock();
			publisherHint.Text = "Used to auto-fill the Publisher Name field when creating new track files.";
			publisherHint.Foreground = Brushes.Gray;
			publisherHint.FontSize = 11;
			publisherHint.TextWrapping = TextWrapping.Wrap;
			publisherHint.Margin = new Thickness(0, 0, 0, 4);
			publisherGroupPanel.Children.Add(publisherHint);

			editDefaultPublisherName = new TextBox();
			editDefaultPublisherName.Text = windowSettings.DefaultPublisherName ?? "";
			publisherGroupPanel.Children.Add(editDefaultPublisherName);

			publisherBorder.Child = publisherGroupPanel;
			appSettingsPanel.Children.Add(publisherBorder);

            // SPS Settings group (bordered)
            Border spsBorder = new Border();
            spsBorder.BorderBrush = Brushes.Gray;
            spsBorder.BorderThickness = new Thickness(1);
            spsBorder.CornerRadius = new CornerRadius(4);
            spsBorder.Padding = new Thickness(10);
            spsBorder.Margin = new Thickness(0, 0, 0, 16);

            StackPanel spsGroupPanel = new StackPanel();

            TextBlock syMenuLabel = new TextBlock();
            syMenuLabel.Text = "SPSSuite Path:";
            syMenuLabel.FontWeight = FontWeights.Bold;
            syMenuLabel.Margin = new Thickness(0, 0, 0, 4);
            syMenuLabel.Tag = "ThemeGroupHeader";
            spsGroupPanel.Children.Add(syMenuLabel);

            TextBlock syMenuHint = new TextBlock();
            syMenuHint.Text = "Path to the SyMenu SPSSuite folder (e.g. SyMenu\\ProgramFiles\\SPSSuite). Used for SPS integration.";
            syMenuHint.Foreground = Brushes.Gray;
            syMenuHint.FontSize = 11;
            syMenuHint.TextWrapping = TextWrapping.Wrap;
            syMenuHint.Margin = new Thickness(0, 0, 0, 4);
            spsGroupPanel.Children.Add(syMenuHint);

            DockPanel syMenuDock = new DockPanel();
            syMenuDock.Margin = new Thickness(0, 0, 0, 8);

            Button btnBrowseSyMenu = new Button();
            btnBrowseSyMenu.Content = "\xe838";
            btnBrowseSyMenu.FontFamily = new FontFamily("Segoe Fluent Icons");
            btnBrowseSyMenu.FontSize = 16;
            btnBrowseSyMenu.Padding = new Thickness(8, 4, 8, 4);
            btnBrowseSyMenu.Margin = new Thickness(4, 0, 0, 0);
            btnBrowseSyMenu.ToolTip = "Browse for the SPSSuite folder";
            btnBrowseSyMenu.Click += (s, ev) =>
            {
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                folderDialog.Description = "Select the SPSSuite folder (contains SyMenuSuite, etc.)";
                folderDialog.ShowNewFolderButton = false;
                if (!string.IsNullOrEmpty(editSyMenuPath.Text) && Directory.Exists(editSyMenuPath.Text))
                    folderDialog.SelectedPath = editSyMenuPath.Text;

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selected = folderDialog.SelectedPath;

                    // If user selected a suite subfolder or _Cache, go up
                    string selectedName = Path.GetFileName(selected);
                    if (selectedName.Equals("_Cache", StringComparison.OrdinalIgnoreCase))
                        selected = Path.GetDirectoryName(Path.GetDirectoryName(selected));
                    else if (selectedName.Equals("SyMenuSuite", StringComparison.OrdinalIgnoreCase) ||
                             selectedName.Equals("NirSoftSuite", StringComparison.OrdinalIgnoreCase))
                        selected = Path.GetDirectoryName(selected);

                    editSyMenuPath.Text = selected;
                    windowSettings.SpsSuiteRootPath = selected;
                    windowSettings.Save(windowSettingsPath);
                    UpdateSpsVisibility();
                    UpdateSpsSettingsEnabled();
                    statusFile.Text = "SPSSuite path set: " + selected;
                }
            };
            DockPanel.SetDock(btnBrowseSyMenu, Dock.Right);
            syMenuDock.Children.Add(btnBrowseSyMenu);

            editSyMenuPath = new TextBox();
            editSyMenuPath.Text = windowSettings.SpsSuiteRootPath ?? "";
            editSyMenuPath.TextChanged += (s, ev) =>
            {
                string path = editSyMenuPath.Text.Trim();
                windowSettings.SpsSuiteRootPath = string.IsNullOrEmpty(path) ? null : path;
                windowSettings.Save(windowSettingsPath);
                UpdateSpsVisibility();
                UpdateSpsSettingsEnabled();
            };
            syMenuDock.Children.Add(editSyMenuPath);

            spsGroupPanel.Children.Add(syMenuDock);

            // SPS action buttons and publisher filter
            WrapPanel spsButtonPanel = new WrapPanel();
            spsButtonPanel.Margin = new Thickness(0, 4, 0, 0);

            btnSpsSelectSuites = new Button();
            btnSpsSelectSuites.Content = "\xE71D";
            btnSpsSelectSuites.FontFamily = new FontFamily("Segoe Fluent Icons");
            btnSpsSelectSuites.FontSize = 17;
            btnSpsSelectSuites.Padding = new Thickness(7, 4, 7, 4);
            btnSpsSelectSuites.Margin = new Thickness(0, 0, 4, 0);
            btnSpsSelectSuites.ToolTip = "Select SPS Suites";
            btnSpsSelectSuites.Click += SelectSuites_Click;
            spsButtonPanel.Children.Add(btnSpsSelectSuites);

            btnSpsScanCache = new Button();
            btnSpsScanCache.Content = "\xE777";
            btnSpsScanCache.FontFamily = new FontFamily("Segoe Fluent Icons");
            btnSpsScanCache.FontSize = 17;
            btnSpsScanCache.Padding = new Thickness(7, 4, 7, 4);
            btnSpsScanCache.Margin = new Thickness(0, 0, 8, 0);
            btnSpsScanCache.ToolTip = "Scan SyMenu SPS cache(s) and rebuild category from SPS data";
            btnSpsScanCache.Click += ReBuild_Click;
            spsButtonPanel.Children.Add(btnSpsScanCache);

            spsPublisherSettingsPanel = new StackPanel();
            spsPublisherSettingsPanel.Orientation = Orientation.Vertical;
            spsPublisherSettingsPanel.VerticalAlignment = VerticalAlignment.Center;
            spsPublisherSettingsPanel.Margin = new Thickness(4, 0, 4, 0);

            TextBlock settingsPublisherLabel = new TextBlock();
            settingsPublisherLabel.Text = "Publisher:";
            spsPublisherSettingsPanel.Children.Add(settingsPublisherLabel);

            txtSettingsSearchCount = new TextBlock();
            txtSettingsSearchCount.Text = "";
            txtSettingsSearchCount.FontSize = 11;
            spsPublisherSettingsPanel.Children.Add(txtSettingsSearchCount);

            spsButtonPanel.Children.Add(spsPublisherSettingsPanel);

            txtSettingsPublisherFilter = new TextBox();
            txtSettingsPublisherFilter.Width = 150;
            txtSettingsPublisherFilter.VerticalAlignment = VerticalAlignment.Center;
            txtSettingsPublisherFilter.Margin = new Thickness(2, 0, 2, 0);
            txtSettingsPublisherFilter.ToolTip = "Filter by SPS publisher name (empty = all)";
            txtSettingsPublisherFilter.KeyDown += txtPublisherFilter_KeyDown;
            spsButtonPanel.Children.Add(txtSettingsPublisherFilter);

            spsGroupPanel.Children.Add(spsButtonPanel);

            spsBorder.Child = spsGroupPanel;
            appSettingsPanel.Children.Add(spsBorder);

            // Column Visibility section (bordered)
            Border columnBorder = new Border();
            columnBorder.BorderBrush = Brushes.Gray;
            columnBorder.BorderThickness = new Thickness(1);
            columnBorder.CornerRadius = new CornerRadius(4);
            columnBorder.Padding = new Thickness(10);
            columnBorder.Margin = new Thickness(0, 0, 0, 16);

            StackPanel columnGroupPanel = new StackPanel();

            TextBlock colTitle = new TextBlock();
            colTitle.Text = "Column Visibility:";
            colTitle.FontWeight = FontWeights.Bold;
            colTitle.Margin = new Thickness(0, 0, 0, 4);
            colTitle.Tag = "ThemeGroupHeader";
            columnGroupPanel.Children.Add(colTitle);

            TextBlock colHint = new TextBlock();
            colHint.Text = "Show/hide columns. Drag column headers to reorder. Drag column edges to resize.";
            colHint.Foreground = Brushes.Gray;
            colHint.FontSize = 11;
            colHint.TextWrapping = TextWrapping.Wrap;
            colHint.Margin = new Thickness(0, 0, 0, 8);
            columnGroupPanel.Children.Add(colHint);

            // Build checkbox list from current or default settings
            List<ColumnSetting> currentColSettings = windowSettings.ColumnSettings;
            if (currentColSettings.Count == 0)
                currentColSettings = WindowSettings.GetDefaultColumns();
            currentColSettings.Sort((a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));

            columnCheckboxPanel = new StackPanel();

            foreach (ColumnSetting cs in currentColSettings)
            {
                CheckBox chk = new CheckBox();
                chk.Content = cs.Header + "  (" + cs.Binding + ")";
                chk.IsChecked = cs.Visible;
                chk.Tag = cs.Binding;
                chk.Margin = new Thickness(0, 2, 0, 2);
                columnCheckboxPanel.Children.Add(chk);
            }

            columnGroupPanel.Children.Add(columnCheckboxPanel);

            columnBorder.Child = columnGroupPanel;
            appSettingsPanel.Children.Add(columnBorder);

            appSettingsScroll.Content = appSettingsPanel;
            appSettingsOuter.Children.Add(appSettingsScroll);
            // Apply initial theme colours to settings group headers
            ApplyGroupHeadersToPanel(appSettingsPanel, new SolidColorBrush(currentTheme.TabSelectedForeground), new SolidColorBrush(currentTheme.SplitterColor));
            appSettingsTab.Content = appSettingsOuter;

            // Tab 2: WebView2
            TabItem webTab = new TabItem();
            webTab.Header = "🌐 WebView";

            DockPanel webPanel = new DockPanel();

            // ---- TOP TOOLBAR: Search engine + address bar + nav buttons ----
            DockPanel webNavBar = new DockPanel();
            webNavBar.Background = new SolidColorBrush(currentTheme.ToolbarBackground);
            webNavBar.Margin = new Thickness(0);
            DockPanel.SetDock(webNavBar, Dock.Top);

            // Left side: search engine selector
            searchEngineCombo = new ComboBox();
            searchEngineCombo.Width = 110;
            searchEngineCombo.Margin = new Thickness(4, 4, 2, 4);
            searchEngineCombo.VerticalAlignment = VerticalAlignment.Center;
            searchEngineCombo.Items.Add("DuckDuckGo");
            searchEngineCombo.Items.Add("Google");
            searchEngineCombo.Items.Add("Bing");
            searchEngineCombo.Items.Add("Startpage");
            searchEngineCombo.Items.Add("Brave");
            searchEngineCombo.Items.Add("Yahoo");
            int engineIndex = searchEngineCombo.Items.IndexOf(windowSettings.SearchEngine);
            searchEngineCombo.SelectedIndex = engineIndex >= 0 ? engineIndex : 0;
            DockPanel.SetDock(searchEngineCombo, Dock.Left);
            webNavBar.Children.Add(searchEngineCombo);

            // Right side: buttons (dock right, in reverse order)
            Button btnSetTrackURL = CreateToolBarButton("\uE71B", "Set Track URL from current page", null);
            btnSetTrackURL.Margin = new Thickness(2, 4, 4, 4);
            btnSetTrackURL.VerticalAlignment = VerticalAlignment.Center;
            btnSetTrackURL.Click += (s, ev) =>
            {
                try
                {
                    if (webView.CoreWebView2 != null)
                    {
                        string currentUrl = webView.CoreWebView2.Source;
                        if (!string.IsNullOrEmpty(currentUrl) && currentUrl != "about:blank")
                        {
                            editTrackURL.Text = currentUrl;
                            MarkDirty();
                            statusFile.Text = "Track URL set to: " + currentUrl;
                        }
                    }
                }
                catch (Exception) { }
            };
            DockPanel.SetDock(btnSetTrackURL, Dock.Right);
            webNavBar.Children.Add(btnSetTrackURL);

            Separator navSep2 = new Separator();
            navSep2.Width = 1;
            navSep2.Margin = new Thickness(4, 4, 4, 4);
            DockPanel.SetDock(navSep2, Dock.Right);
            webNavBar.Children.Add(navSep2);

            Button btnWebHome = CreateToolBarButton("\xe80f", "Return to current track URL", null);
            btnWebHome.Margin = new Thickness(2, 4, 2, 4);
            btnWebHome.VerticalAlignment = VerticalAlignment.Center;
            btnWebHome.Click += (s, ev) =>
            {
                try
                {
                    if (webView.CoreWebView2 != null && currentTrackItem != null &&
                        !string.IsNullOrWhiteSpace(currentTrackItem.TrackURL))
                    {
                        string url = currentTrackItem.TrackURL;
                        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                            url = "https://" + url;
                        webView.CoreWebView2.Navigate(url);
                    }
                }
                catch (Exception) { }
            };
            DockPanel.SetDock(btnWebHome, Dock.Right);
            webNavBar.Children.Add(btnWebHome);

            Button btnWebForward = CreateToolBarButton("\xe761", "Forward", null);
            btnWebForward.Margin = new Thickness(2, 4, 2, 4);
            btnWebForward.VerticalAlignment = VerticalAlignment.Center;
            btnWebForward.Click += (s, ev) =>
            {
                try { if (webView.CoreWebView2 != null && webView.CanGoForward) webView.GoForward(); }
                catch (Exception) { }
            };
            DockPanel.SetDock(btnWebForward, Dock.Right);
            webNavBar.Children.Add(btnWebForward);

            Button btnWebBack = CreateToolBarButton("\xe760", "Back", null);
            btnWebBack.Margin = new Thickness(2, 4, 2, 4);
            btnWebBack.VerticalAlignment = VerticalAlignment.Center;
            btnWebBack.Click += (s, ev) =>
            {
                try { if (webView.CoreWebView2 != null && webView.CanGoBack) webView.GoBack(); }
                catch (Exception) { }
            };
            DockPanel.SetDock(btnWebBack, Dock.Right);
            webNavBar.Children.Add(btnWebBack);

            Separator navSep1 = new Separator();
            navSep1.Width = 1;
            navSep1.Margin = new Thickness(4, 4, 4, 4);
            DockPanel.SetDock(navSep1, Dock.Right);
            webNavBar.Children.Add(navSep1);

            // Go button docked right of the address bar
            Button btnWebGo = CreateToolBarButton("\uE721", "Search or navigate to URL", null);
            btnWebGo.Margin = new Thickness(2, 4, 2, 4);
            btnWebGo.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(btnWebGo, Dock.Right);
            webNavBar.Children.Add(btnWebGo);

            // Address bar fills remaining space
            webAddressBar = new TextBox();
            webAddressBar.Margin = new Thickness(2, 4, 2, 4);
            webAddressBar.VerticalAlignment = VerticalAlignment.Center;
            webAddressBar.VerticalContentAlignment = VerticalAlignment.Center;
            webAddressBar.ToolTip = "Enter a URL or search term, then press Enter or click Go";
            webNavBar.Children.Add(webAddressBar);

            // Search/navigate action
            Action doWebSearch = () =>
            {
                string input = webAddressBar.Text.Trim();
                if (string.IsNullOrEmpty(input)) return;

                string navigateUrl;

                if (input.StartsWith("http://") || input.StartsWith("https://") ||
                    (input.Contains(".") && !input.Contains(" ")))
                {
                    navigateUrl = input;
                    if (!navigateUrl.StartsWith("http://") && !navigateUrl.StartsWith("https://"))
                        navigateUrl = "https://" + navigateUrl;
                }
                else
                {
                    string query = Uri.EscapeDataString(input);
                    string engine = searchEngineCombo.SelectedItem as string ?? "DuckDuckGo";

                    switch (engine)
                    {
                        case "Google":
                            navigateUrl = "https://www.google.com/search?q=" + query;
                            break;
                        case "Bing":
                            navigateUrl = "https://www.bing.com/search?q=" + query;
                            break;
                        case "Startpage":
                            navigateUrl = "https://www.startpage.com/do/search?q=" + query;
                            break;
                        case "Brave":
                            navigateUrl = "https://search.brave.com/search?q=" + query;
                            break;
                        case "Yahoo":
                            navigateUrl = "https://search.yahoo.com/search?p=" + query;
                            break;
                        default:
                            navigateUrl = "https://duckduckgo.com/?q=" + query;
                            break;
                    }
                }

                try
                {
                    if (webView.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.Navigate(navigateUrl);
                        statusFile.Text = "Navigating: " + navigateUrl;
                    }
                }
                catch (Exception ex)
                {
                    statusFile.Text = "Navigation error: " + ex.Message;
                }
            };

            btnWebGo.Click += (s, ev) => doWebSearch();
            webAddressBar.KeyDown += (s, ev) =>
            {
                if (ev.Key == System.Windows.Input.Key.Enter)
                    doWebSearch();
            };

            // ---- BOTTOM TOOLBAR: Zoom, cookies, clear data, checkbox ----
            ToolBarTray webBottomToolBarTray = new ToolBarTray();
            DockPanel.SetDock(webBottomToolBarTray, Dock.Bottom);

            ToolBar webBottomToolBar = new ToolBar();
            webBottomToolBar.Band = 0;
            webBottomToolBar.BandIndex = 0;

            Button btnZoomIn = CreateToolBarButton("\xe8a3", "Zoom in", ZoomIn_Click);
            webBottomToolBar.Items.Add(btnZoomIn);

            Button btnZoomOut = CreateToolBarButton("\xe71f", "Zoom out", ZoomOut_Click);
            webBottomToolBar.Items.Add(btnZoomOut);

            Button btnZoomReset = CreateToolBarButton("100%", "Reset zoom", ZoomReset_Click);
            btnZoomReset.FontFamily = SystemFonts.MessageFontFamily;
            btnZoomReset.FontSize = SystemFonts.MessageFontSize;
            webBottomToolBar.Items.Add(btnZoomReset);

            Button btnZoomFit = CreateToolBarButton("\xe9a6", "Fit to panel width", ZoomFit_Click);
            webBottomToolBar.Items.Add(btnZoomFit);

            webBottomToolBar.Items.Add(new Separator());

            Button btnClearCookies = CreateToolBarButton("🍪", "Delete all cookies", ClearCookies_Click);
            btnClearCookies.FontSize = 15;
		    btnClearCookies.Padding = new Thickness(6, 0, 6, 1);
            btnClearCookies.FontFamily = SystemFonts.MessageFontFamily;
            webBottomToolBar.Items.Add(btnClearCookies);

            Button btnClearAll = CreateToolBarButton("\xecad", "Clear all cookies, cache, and browsing data", ClearAllData_Click);
            webBottomToolBar.Items.Add(btnClearAll);

            webBottomToolBar.Items.Add(new Separator());

            CheckBox chkBlockPopups = new CheckBox();
            chkBlockPopups.Content = "Block Cookie Popups";
            chkBlockPopups.VerticalAlignment = VerticalAlignment.Center;
            chkBlockPopups.IsChecked = true;
            chkBlockPopups.ToolTip = "Automatically hides cookie consent popups.\nUnchecking clears cookies for this site\n(you may be signed out) and reload the page.";
            chkBlockPopups.Margin = new Thickness(2, 0, 2, 0);

            chkBlockPopups.Checked += async (s, ev) =>
            {
                blockCookiePopups = true;
                statusFile.Text = "Cookie popup blocker enabled";

                // Re-add early CSS blocker
                if (string.IsNullOrEmpty(cookieBlockerScriptId))
                {
                    try
                    {
                        cookieBlockerScriptId = await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                            (function() {
                                var style = document.createElement('style');
                                style.id = '__patCookieBlockerEarlyCSS';
                                style.textContent = `
                                    [id*='cookie' i], [class*='cookie' i],
                                    [id*='consent' i], [class*='consent' i],
                                    [id*='gdpr' i], [class*='gdpr' i],
                                    [id*='onetrust' i], [class*='onetrust' i],
                                    [id*='CybotCookiebot' i],
                                    .cookie-banner, .cookie-notice, .cookie-popup,
                                    #cookie-law-info-bar, .cli-modal,
                                    [class*='cc-banner' i], [class*='cc-window' i],
                                    [class*='cookiewall' i], [id*='cookiewall' i] {
                                        display: none !important;
                                        visibility: hidden !important;
                                        opacity: 0 !important;
                                        pointer-events: none !important;
                                    }
                                    body, html {
                                        overflow: auto !important;
                                    }
                                `;
                                if (document.head) {
                                    document.head.appendChild(style);
                                } else {
                                    document.addEventListener('DOMContentLoaded', function() {
                                        document.head.appendChild(style);
                                    });
                                }
                            })();
                        ");
                    }
                    catch (Exception) { }
                }

                try
                {
                    if (webView.CoreWebView2 != null)
                        await InjectCookiePopupBlocker();
                }
                catch (Exception) { }
            };

            chkBlockPopups.Unchecked += async (s, ev) =>
            {
                blockCookiePopups = false;

                // Remove the early injected CSS blocker
                if (!string.IsNullOrEmpty(cookieBlockerScriptId))
                {
                    try
                    {
                        webView.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(cookieBlockerScriptId);
                        cookieBlockerScriptId = null;
                    }
                    catch (Exception) { }
                }

                statusFile.Text = "Cookie popup blocker disabled — clearing site cookies and reloading...";
                try
                {
                    if (webView.CoreWebView2 != null)
                    {
                        await webView.CoreWebView2.ExecuteScriptAsync(@"
                            window.__patCookieBlockerActive = false;
                            var css = document.getElementById('__patCookieBlockerCSS');
                            if (css) css.remove();
                            var earlyCss = document.getElementById('__patCookieBlockerEarlyCSS');
                            if (earlyCss) earlyCss.remove();
                        ");

                        string currentUrl = webView.CoreWebView2.Source;

                        if (!string.IsNullOrEmpty(currentUrl) && currentUrl != "about:blank")
                        {
                            try
                            {
                                var cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(currentUrl);
                                foreach (var cookie in cookies)
                                {
                                    webView.CoreWebView2.CookieManager.DeleteCookie(cookie);
                                }
                            }
                            catch (Exception) { }

                            await webView.CoreWebView2.ExecuteScriptAsync(@"
                                try { localStorage.clear(); } catch(e) {}
                                try { sessionStorage.clear(); } catch(e) {}
                            ");

                            webView.CoreWebView2.Navigate(currentUrl);
                        }
                    }
                }
                catch (Exception) { }
            };
            webBottomToolBar.Items.Add(chkBlockPopups);

            webBottomToolBar.Items.Add(new Separator());

            Button btnInstallUBlock = CreateToolBarButton("\xf140", "Install/Update uBlock Origin", InstallUBlock_Click);
            btnInstallUBlock.Padding = new Thickness(6, 1, 6, 3);
            webBottomToolBar.Items.Add(btnInstallUBlock);

            Button btnListExtensions = CreateToolBarButton("\uE71D", "List Extensions", ListExtensions_Click);
            webBottomToolBar.Items.Add(btnListExtensions);

            webBottomToolBarTray.ToolBars.Add(webBottomToolBar);

            // ---- Add to DockPanel in correct order ----
            // Top toolbar first (docked top)
            webPanel.Children.Add(webNavBar);
            // Bottom toolbar second (docked bottom)
            webPanel.Children.Add(webBottomToolBarTray);

            // WebView2 last — it fills the remaining space
			webView = new Microsoft.Web.WebView2.Wpf.WebView2();
			webView.CreationProperties = new Microsoft.Web.WebView2.Wpf.CoreWebView2CreationProperties
			{
			    UserDataFolder = webViewPath
			};
			webPanel.Children.Add(webView);

            // Update address bar when navigation occurs
            webView.SourceChanged += (s, ev) =>
            {
                try
                {
                    string currentUrl = webView.Source?.ToString() ?? "";
                    if (currentUrl != "about:blank" && !string.IsNullOrEmpty(currentUrl))
                    {
                        webAddressBar.Text = currentUrl;
                    }
                }
                catch (Exception) { }
            };

            // Select all text when address bar gets focus (like a real browser)
            webAddressBar.GotFocus += (s, ev) =>
            {
                webAddressBar.SelectAll();
            };
            webAddressBar.PreviewMouseLeftButtonDown += (s, ev) =>
            {
                if (!webAddressBar.IsKeyboardFocusWithin)
                {
                    webAddressBar.Focus();
                    ev.Handled = true;
                }
            };

            webTab.Content = webPanel;
			rightTabs.Items.Add(webTab);
			rightTabs.Items.Add(BuildThemeTab());
			rightTabs.Items.Add(appSettingsTab);
        }

        // ============================
        // Settings Field Helpers
        // ============================

        private TextBox AddSettingsField(StackPanel parent, string label, double width = 0)
        {
            TextBlock lbl = new TextBlock();
            lbl.Text = label;
            lbl.Margin = new Thickness(0, 0, 0, 4);
            parent.Children.Add(lbl);

            TextBox txt = new TextBox();
            txt.Margin = new Thickness(0, 0, 0, 8);
            if (width > 0)
            {
                txt.Width = width;
                txt.HorizontalAlignment = HorizontalAlignment.Left;
            }
            parent.Children.Add(txt);
            return txt;
        }

        private void ApplyCharacterSelection(TextBox txt)
        {
            int selectionAnchor = -1;

            txt.PreviewMouseLeftButtonDown += (s, ev) =>
            {
                TextBox tb = s as TextBox;
                if (tb == null) return;

                int clickPos = tb.GetCharacterIndexFromPoint(ev.GetPosition(tb), true);
                if (clickPos >= 0)
                {
                    selectionAnchor = clickPos;
                    tb.Focus();
                    tb.CaretIndex = clickPos;
                    tb.Select(clickPos, 0);
                    ev.Handled = true;
                    tb.CaptureMouse();
                }
            };

            txt.PreviewMouseMove += (s, ev) =>
            {
                TextBox tb = s as TextBox;
                if (tb == null) return;

                if (ev.LeftButton == System.Windows.Input.MouseButtonState.Pressed
                    && selectionAnchor >= 0 && tb.IsMouseCaptured)
                {
                    int currentPos = tb.GetCharacterIndexFromPoint(ev.GetPosition(tb), true);
                    if (currentPos >= 0)
                    {
                        int start = Math.Min(selectionAnchor, currentPos);
                        int length = Math.Abs(currentPos - selectionAnchor);
                        tb.Select(start, length);
                    }
                    ev.Handled = true;
                }
            };

            txt.PreviewMouseLeftButtonUp += (s, ev) =>
            {
                TextBox tb = s as TextBox;
                if (tb != null && tb.IsMouseCaptured)
                {
                    tb.ReleaseMouseCapture();
                }
                selectionAnchor = -1;
            };
        }

        private TextBox AddSettingsMultiline(StackPanel parent, string label)
        {
            TextBlock lbl = new TextBlock();
            lbl.Text = label;
            lbl.Margin = new Thickness(0, 0, 0, 4);
            parent.Children.Add(lbl);

            TextBox txt = new TextBox();
            txt.Height = 60;
            txt.AcceptsReturn = true;
            txt.TextWrapping = TextWrapping.Wrap;
            txt.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            txt.Margin = new Thickness(0, 0, 0, 8);
            ApplyCharacterSelection(txt);
            parent.Children.Add(txt);
            return txt;
        }

		private static readonly StatusToColorConverter statusConverter = new StatusToColorConverter();

		private GridViewColumn MakeColumn(string header, string binding, double width)
		{
		    GridViewColumn col = new GridViewColumn();
		    col.Header = header;
		    col.Width = width;

		    DataTemplate template = new DataTemplate();
		    FrameworkElementFactory textBlock = new FrameworkElementFactory(typeof(TextBlock));
		    textBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(binding));
		    textBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
		    textBlock.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
		    textBlock.SetValue(TextBlock.MaxHeightProperty, 20.0);
		    template.VisualTree = textBlock;
		    col.CellTemplate = template;

		    return col;
		}

		private GridViewColumn MakeColumn(string header, string binding, double width, string statusBinding = null)
		{
		    GridViewColumn col = new GridViewColumn();
		    col.Header = header;
		    col.Width = width;

		    DataTemplate template = new DataTemplate();

		    FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
		    border.SetValue(Border.PaddingProperty, new Thickness(2, 0, 2, 0));
		    border.SetValue(Border.MarginProperty, new Thickness(-6, -2, -6, -2));

		    FrameworkElementFactory textBlock = new FrameworkElementFactory(typeof(TextBlock));
		    textBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(binding));
		    textBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
		    textBlock.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
		    textBlock.SetValue(TextBlock.MaxHeightProperty, 20.0);

		    // Only apply status colouring if a statusBinding was provided
		    if (statusBinding != null)
		    {
		        System.Windows.Data.Binding fgBinding = new System.Windows.Data.Binding(statusBinding);
		        fgBinding.Converter = new StatusToColorConverter();
		        textBlock.SetBinding(TextBlock.ForegroundProperty, fgBinding);
		    }

		    border.AppendChild(textBlock);
		    template.VisualTree = border;
		    col.CellTemplate = template;

		    return col;
		}
		
        private string GetStatusBindingForColumn(string binding)
        {
            switch (binding)
            {
                case "TrackURL": return "TrackURLStatus";
                case "StartString": return "StartStringStatus";
                case "StopString": return "StopStringStatus";
                case "TrackBlockHash": return "HashStatus";
                case "LatestVersion": return "LatestVersionStatus";
                case "DownloadURL": return "DownloadURLStatus";
                case "DownloadSizeKb": return "DownloadSizeStatus";
                default: return null;
            }
        }

        // ============================
        // Layout Placement
        // ============================

        private void PlaceControlsInLayout()
        {
            RemoveFromParent(categoryPanel);
            RemoveFromParent(itemList);
            RemoveFromParent(trackSettingsPanel);
            RemoveFromParent(rightTabs);

            if (isHorizontalLayout)
            {
                catHostH.Child = categoryPanel;
                listHostH.Child = itemList;
                trackSettingsHostH.Child = trackSettingsPanel;
                tabsHostH.Child = rightTabs;
            }
            else
            {
                catHostV.Child = categoryPanel;
                listHostV.Child = itemList;
                trackSettingsHostV.Child = trackSettingsPanel;
                tabsHostV.Child = rightTabs;
            }
        }

        private void RemoveFromParent(UIElement element)
        {
            if (catHostV.Child == element) catHostV.Child = null;
            if (listHostV.Child == element) listHostV.Child = null;
            if (trackSettingsHostV.Child == element) trackSettingsHostV.Child = null;
            if (tabsHostV.Child == element) tabsHostV.Child = null;
            if (catHostH.Child == element) catHostH.Child = null;
            if (listHostH.Child == element) listHostH.Child = null;
            if (trackSettingsHostH.Child == element) trackSettingsHostH.Child = null;
            if (tabsHostH.Child == element) tabsHostH.Child = null;
        }

        // ============================
        // Category Tree
        // ============================

        private void RefreshCategoryTree()
        {
            categoryTree.Items.Clear();

            if (!Directory.Exists(categoriesPath))
                return;

            foreach (string dir in Directory.GetDirectories(categoriesPath))
            {
                string folderName = Path.GetFileName(dir);
                int trackCount = Directory.GetFiles(dir, "*.track").Length;
                bool isSpsCategory = Directory.GetFiles(dir, "*.xml").Length > 0;

                // For SPS categories, count entries inside the XML file
                if (isSpsCategory)
                {
                    string[] xmlFiles = Directory.GetFiles(dir, "*.xml");
                    string pf, sp;
                    List<string> ss;
                    List<TrackItem> spsItems = TrackItem.LoadSpsListFromFile(xmlFiles[0], out pf, out sp, out ss, out _);
                    trackCount = spsItems.Count;
                }
                string sourceLinkFile = Path.Combine(dir, "_source.link");
                bool isLinked = File.Exists(sourceLinkFile);

                string labelText = trackCount > 0
                    ? folderName + " (" + trackCount + ")"
                    : folderName;

                if (isLinked)
                    labelText += " [linked]";
                if (isSpsCategory)
                    labelText += " [SPS]";

                // Build a header with real folder icon
                StackPanel headerPanel = new StackPanel();
                headerPanel.Orientation = Orientation.Horizontal;

                System.Windows.Media.ImageSource icon = GetFolderIcon(dir, isLinked);
                if (icon != null)
                {
                    Image iconImage = new Image();
                    iconImage.Source = icon;
                    iconImage.Width = 16;
                    iconImage.Height = 16;
                    iconImage.Margin = new Thickness(0, 0, 4, 0);
                    headerPanel.Children.Add(iconImage);
                }

                TextBlock label = new TextBlock();
                label.Text = labelText;
                label.VerticalAlignment = VerticalAlignment.Center;
                headerPanel.Children.Add(label);

                TreeViewItem item = new TreeViewItem();
                item.Header = headerPanel;
                item.Tag = dir;
                item.IsExpanded = false;
                categoryTree.Items.Add(item);
            }

            // Re-select the previously active category and refresh the list
            if (!string.IsNullOrEmpty(currentCategoryPath))
            {
                foreach (TreeViewItem item in categoryTree.Items)
                {
                    if ((string)item.Tag == currentCategoryPath)
                    {
                        if (suppressCategoryReload)
                            suppressSelectionChange = true;

                        item.IsSelected = true;
                        item.BringIntoView();

                        if (suppressCategoryReload)
                            suppressSelectionChange = false;

                        break;
                    }
                }

                if (!suppressCategoryReload)
                    LoadTrackFiles(currentCategoryPath);
            }
            else
            {
                statusFile.Text = categoryTree.Items.Count + " categories";
            }
        }

        private void RefreshCategories_Click(object sender, RoutedEventArgs e)
        {
            RefreshCategoryTree();
            statusFile.Text = "Category list refreshed.";
        }

        private void NewCategory_Click(object sender, RoutedEventArgs e)
        {
            string newName = ShowInputDialog("New Category", "Enter category name:");

            if (string.IsNullOrWhiteSpace(newName))
                return;

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                newName = newName.Replace(c.ToString(), "");
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Invalid folder name.", AppInfo.ShortName,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string newPath = Path.Combine(categoriesPath, newName);

            if (Directory.Exists(newPath))
            {
                MessageBox.Show("Category '" + newName + "' already exists.",
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Directory.CreateDirectory(newPath);
                RefreshCategoryTree();

                foreach (TreeViewItem item in categoryTree.Items)
                {
                    if ((string)item.Tag == newPath)
                    {
                        item.IsSelected = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error creating category: " + ex.Message,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

		private void RenameCategory_Click(object sender, RoutedEventArgs e)
		{
		    // Get selected item in the categoryTree
		    if (categoryTree.SelectedItem is TreeViewItem selectedCat && selectedCat.Tag is string catPath)
		    {
		        string oldName = System.IO.Path.GetFileName(catPath);
		        string parentDir = System.IO.Path.GetDirectoryName(catPath) ?? "";

		        // Prompt for new name using your standard dialog:
		        string newName = ShowInputDialog("Rename Category", $"Enter new name for \"{oldName}\":");
		        if (string.IsNullOrWhiteSpace(newName) || newName == oldName)
		            return;

		        // Remove invalid filename characters
		        string safeName = newName;
		        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
		            safeName = safeName.Replace(c.ToString(), "");

		        if (string.IsNullOrWhiteSpace(safeName))
		        {
		            MessageBox.Show("Invalid category name.", AppInfo.ShortName,
		                MessageBoxButton.OK, MessageBoxImage.Warning);
		            return;
		        }

		        string newPath = System.IO.Path.Combine(parentDir, safeName);
		        if (Directory.Exists(newPath))
		        {
		            MessageBox.Show("A category with that name already exists.", AppInfo.ShortName,
		                MessageBoxButton.OK, MessageBoxImage.Warning);
		            return;
		        }

		        try
		        {
		            Directory.Move(catPath, newPath);
		            RefreshCategories_Click(null, null); // Update the tree
		        }
		        catch (Exception ex)
		        {
		            MessageBox.Show($"Failed to rename category:\n{ex.Message}",
		                AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
		        }
		    }
		    else
		    {
		        MessageBox.Show("Select a category to rename.", AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Information);
		    }
		}

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem selected = categoryTree.SelectedItem as TreeViewItem;

            if (selected == null)
            {
                MessageBox.Show("No category selected.", AppInfo.ShortName,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string folderPath = (string)selected.Tag;
            string folderName = Path.GetFileName(folderPath);

            int fileCount = 0;
            if (Directory.Exists(folderPath))
            {
                fileCount = Directory.GetFiles(folderPath).Length;
            }

            string message = "Delete category '" + folderName + "'?";
            if (fileCount > 0)
            {
                message += "\n\nThis folder contains " + fileCount +
                    " file(s) which will also be deleted!";
            }

            MessageBoxResult result = MessageBox.Show(message, "Delete Category",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Directory.Delete(folderPath, true);
                    RefreshCategoryTree();
                    currentItems.Clear();
                    itemList.ItemsSource = null;
                    ClearTrackFields();
                    statusFile.Text = "Category deleted: " + folderName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error deleting category: " + ex.Message,
                        AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenCategoryFolder_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem selected = categoryTree.SelectedItem as TreeViewItem;

            if (selected != null)
            {
                string folderPath = (string)selected.Tag;
                if (Directory.Exists(folderPath))
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(folderPath)
                        { UseShellExecute = true });
                    return;
                }
            }

            // No category selected — open the root Categories folder
            if (Directory.Exists(categoriesPath))
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(categoriesPath)
                    { UseShellExecute = true });
            }
        }

        private void CategoryTree_Selected(object sender,
            RoutedPropertyChangedEventArgs<object> e)
        {
            if (suppressSelectionChange) return;
            TreeViewItem selected = e.NewValue as TreeViewItem;

            if (selected == null)
                return;

            string folderPath = (string)selected.Tag;

            // If switching to a different category with unsaved changes, ask to save
            if (isCategoryDirty && currentItems.Count > 0 &&
                !string.IsNullOrEmpty(currentCategoryPath) && folderPath != currentCategoryPath)
            {
                MessageBoxResult result = MessageBox.Show(
                    "You have unsaved changes in '" + Path.GetFileName(currentCategoryPath) + "'.\n\n" +
                    "Do you want to save before switching?",
                    AppInfo.ShortName + " — Unsaved Changes",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveAll_Click(null, null);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    string previousPath = currentCategoryPath;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        suppressSelectionChange = true;
                        foreach (TreeViewItem item in categoryTree.Items)
                        {
                            if ((string)item.Tag == previousPath)
                            {
                                item.IsSelected = true;
                                break;
                            }
                        }
                        suppressSelectionChange = false;
                    }));
                    return;
                }
                ClearAllDirty();
            }

            currentCategoryPath = folderPath;
            LoadTrackFiles(folderPath);
        }

        // ============================
        // Track File Loading
        // ============================

        private void LoadTrackFiles(string folderPath)
        {
            currentItems.Clear();
            string folderName = Path.GetFileName(folderPath);

            if (!Directory.Exists(folderPath))
                return;

            // Check for SPS XML file first
            string[] xmlFiles = Directory.GetFiles(folderPath, "*.xml");
            if (xmlFiles.Length > 0)
            {
                string spsFile = xmlFiles[0];
                string publisherFilter;
                string suitePath;
                List<string> selectedSuites;
                List<TrackItem> spsItems = TrackItem.LoadSpsListFromFile(spsFile, out publisherFilter, out suitePath, out selectedSuites, out _);

                foreach (TrackItem item in spsItems)
                {
                    currentItems.Add(item);
                }

                if (txtSettingsPublisherFilter != null) txtSettingsPublisherFilter.Text = publisherFilter;

                isLoadingFields = true;
                itemList.ItemsSource = null;
                itemList.ItemsSource = currentItems;

                if (currentItems.Count > 0)
                {
                    itemList.SelectedIndex = 0;
                }
                else
                {
                    ClearTrackFields();
                }
                isLoadingFields = false;

                ClearAllDirty();
                statusFile.Text = "SPS Category: " + folderName + " — " + currentItems.Count + " items";
                return;
            }

            // Standard category — load individual .track files
            string[] trackFiles = Directory.GetFiles(folderPath, "*.track");

            foreach (string file in trackFiles)
            {
                TrackItem item = TrackItem.LoadFromFile(file);
                currentItems.Add(item);
            }

            isLoadingFields = true;
            itemList.ItemsSource = null;
            itemList.ItemsSource = currentItems;

            if (currentItems.Count > 0)
            {
                itemList.SelectedIndex = 0;
            }
            else
            {
                ClearTrackFields();
            }
            isLoadingFields = false;

            ClearAllDirty();
            statusFile.Text = "Category: " + folderName + " — " + currentItems.Count + " items";
        }

        // ============================
        // Item List
        // ============================

        private void ItemList_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (suppressAutoDownload || suppressSelectionChange)
                return;

            if (!isDirty || currentTrackItem == null)
                return;

            // Check if the click is on a different item
            DependencyObject dep = (DependencyObject)e.OriginalSource;
            while (dep != null && !(dep is ListViewItem))
                dep = VisualTreeHelper.GetParent(dep);

            if (dep == null)
                return;

            ListViewItem clickedContainer = dep as ListViewItem;
            TrackItem clickedItem = clickedContainer.Content as TrackItem;

            if (clickedItem == null || clickedItem == currentTrackItem)
                return;

            // Stop the click from changing selection — we'll handle it ourselves
            e.Handled = true;

            // Unsaved changes — prompt
            MessageBoxResult result = MessageBox.Show(
                "Track '" + currentTrackItem.TrackName + "' has unsaved changes.\n\n" +
                "Save before switching?",
                AppInfo.ShortName,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

			if (result == MessageBoxResult.Yes)
			{
			    SaveCurrentTrackFields();

			    // Check if this is an SPS category — save as XML instead of individual .track
			    bool isSpsCategory = false;
			    if (!string.IsNullOrEmpty(currentCategoryPath))
			    {
			        if (Directory.GetFiles(currentCategoryPath, "*.xml").Length > 0)
			            isSpsCategory = true;
			        else if (currentItems.Count > 0 && string.IsNullOrEmpty(currentItems[0].FilePath))
			            isSpsCategory = true;
			    }

			    if (isSpsCategory)
			    {
			        string[] xmlFiles = Directory.GetFiles(currentCategoryPath, "*.xml");
			        string xmlPath;
			        string publisherFilter = txtSettingsPublisherFilter != null
			            ? txtSettingsPublisherFilter.Text.Trim()
			            : "";
			        string suitePath = "";
			        List<string> savedSuites = windowSettings.SelectedSuites;

			        if (xmlFiles.Length > 0)
			        {
			            xmlPath = xmlFiles[0];
			            string existingPf, existingSp;
			            List<string> existingSuites;
			            TrackItem.LoadSpsListFromFile(xmlPath, out existingPf, out existingSp, out existingSuites, out _);
			            if (!string.IsNullOrEmpty(existingSp))
			                suitePath = existingSp;
			            if (existingSuites.Count > 0)
			                savedSuites = existingSuites;
			        }
			        else
			        {
			            string folderName = Path.GetFileName(currentCategoryPath);
			            xmlPath = Path.Combine(currentCategoryPath, folderName + ".xml");
			        }

			        TrackItem.SaveSpsListToFile(xmlPath, currentItems.ToList(), publisherFilter, suitePath, savedSuites, ignoredSpsFiles);
			    }
			    else
			    {
			        currentTrackItem.SaveToFile();
			    }

			    ClearDirty();
			    statusFile.Text = "Saved: " + currentTrackItem.TrackName;

				currentTrackItem.ReleaseDateStatus = "";
				currentTrackItem.LatestReleaseDate = "";
				editReleaseDate.Background = new SolidColorBrush(currentTheme.TextBoxBackground);

			    itemList.SelectedItem = clickedItem;
			}
            else if (result == MessageBoxResult.No)
            {
                // Discard changes — reload original values from file
                if (!string.IsNullOrEmpty(currentTrackItem.FilePath) && File.Exists(currentTrackItem.FilePath))
					currentTrackItem.ReloadFromFile();

                isLoadingFields = true;
                editName.Text = currentTrackItem.TrackName;
                editTrackURL.Text = currentTrackItem.TrackURL;
                editStartString.Text = currentTrackItem.StartString;
                editStopString.Text = currentTrackItem.StopString;
                editDownloadURL.Text = currentTrackItem.DownloadURL;
                editVersion.Text = currentTrackItem.Version;
                editReleaseDate.Text = currentTrackItem.ReleaseDate;
                editPublisherName.Text = currentTrackItem.PublisherName;
                editSuiteName.Text = currentTrackItem.SuiteName;
                isLoadingFields = false;

                currentTrackItem.IsDirtyInMemory = false;
                ClearDirty();
                RecalculateCategoryDirty();

                // Now switch to the clicked item
                itemList.SelectedItem = clickedItem;
            }
            // Cancel — do nothing, selection stays
        }

		private void SaveCurrentTrackFields()
		{
		    if (currentTrackItem == null) return;

		    currentTrackItem.TrackName = editName.Text;
		    currentTrackItem.TrackURL = editTrackURL.Text;
		    currentTrackItem.StartString = editStartString.Text;
		    currentTrackItem.StopString = editStopString.Text;
		    currentTrackItem.ReleaseDateStartString = editReleaseDateStartString.Text;
			currentTrackItem.ReleaseDateStopString = editReleaseDateStopString.Text;
		    currentTrackItem.DownloadURL = editDownloadURL.Text;
		    currentTrackItem.Version = editVersion.Text;

		    // If a newer version was detected, apply it
		    if (!string.IsNullOrEmpty(currentTrackItem.LatestVersion) &&
		        currentTrackItem.TrackStatus != "error")
		    {
		        currentTrackItem.Version = currentTrackItem.LatestVersion;
		        editVersion.Text = currentTrackItem.LatestVersion;

		        // Version now matches latest, so status is unchanged
		        if (currentTrackItem.TrackStatus == "changed")
		            currentTrackItem.TrackStatus = "unchanged";
		    }

		    currentTrackItem.ReleaseDate = editReleaseDate.Text;
		    currentTrackItem.PublisherName = editPublisherName.Text;
		    currentTrackItem.SuiteName = editSuiteName.Text;
		}

        private void ItemList_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            TrackItem selected = itemList.SelectedItem as TrackItem;

            if (selected == null)
                return;

            currentTrackItem = selected;

            isLoadingFields = true;

            editName.Text = selected.TrackName;
            editTrackURL.Text = selected.TrackURL;
            editStartString.Text = selected.StartString;
            editStopString.Text = selected.StopString;
            editDownloadURL.Text = selected.DownloadURL;
            editVersion.Text = selected.Version;
            UpdateVersionDisplay();
            editReleaseDate.Text = selected.ReleaseDate;
            editReleaseDateStartString.Text = selected.ReleaseDateStartString ?? "";
			editReleaseDateStopString.Text = selected.ReleaseDateStopString ?? "";
			if (selected.ReleaseDateStatus == "changed" || selected.ReleaseDateStatus == "new")
			{
			    editReleaseDate.Background = new SolidColorBrush(currentTheme.ReleaseDateChangedBackground);
			}
			else
			{
			    editReleaseDate.Background = new SolidColorBrush(currentTheme.TextBoxBackground);
			}
            editPublisherName.Text = selected.PublisherName;
            editSuiteName.Text = selected.SuiteName;

		    // Set Track File label
			string fileName = "(Unsaved)";
			if (!string.IsNullOrEmpty(selected.FilePath))
			    fileName = System.IO.Path.GetFileName(selected.FilePath);

			if (!string.IsNullOrEmpty(selected.FilePath) && selected.FilePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
			    fileName += " [SPS]";
			trackSettingsLabel.Text = "Track File: " + fileName;

            // Update track mode button
            if (btnTrackMode != null)
            {
                if (selected.TrackMode == "text")
                {
                    btnTrackMode.Content = "Aa";
                    btnTrackMode.ToolTip = "Track Mode: Text (click to switch to HTML)";
                }
                else
                {
                    btnTrackMode.Content = "</>";
                    btnTrackMode.ToolTip = "Track Mode: HTML (click to switch to Text)";
                }
            }
			statusFile.Text = "Track: " + selected.TrackName + " | Mode: [" + selected.TrackMode + "]";
            isLoadingFields = false;
            isDirty = false;
            UpdateSaveButtonStates();

            statusFile.Text = "Track: " + selected.TrackName +
                " (" + Path.GetFileName(selected.FilePath) + ")";

            // Auto-download source if track has a URL
            if (!suppressAutoDownload && !string.IsNullOrWhiteSpace(selected.TrackURL))
            {
                AutoDownloadSource(selected.TrackURL);
            }
            else if (!suppressAutoDownload)
            {
                currentSource = "";
                sourceView.Document.Blocks.Clear();
            }
        }
        
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            bool isChecked = cb?.IsChecked ?? false;

            foreach (TrackItem item in currentItems)
            {
                item.IsSelected = isChecked;
            }

            itemList.ItemsSource = null;
            itemList.ItemsSource = currentItems;
        }

        private void ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader header = e.OriginalSource as GridViewColumnHeader;

            if (header == null || header.Column == null)
                return;

            string sortBy = "";

            // Try DisplayMemberBinding first
            if (header.Column.DisplayMemberBinding is System.Windows.Data.Binding binding)
            {
                sortBy = binding.Path.Path;
            }

            // If no DisplayMemberBinding, extract from CellTemplate
            if (string.IsNullOrEmpty(sortBy) && header.Column.CellTemplate != null)
            {
                // Match column header text to the property binding
                string headerText = header.Column.Header as string;
                if (!string.IsNullOrEmpty(headerText))
                {
                    // Look up the binding from column settings
                    List<ColumnSetting> colSettings = windowSettings.ColumnSettings;
                    if (colSettings.Count == 0)
                        colSettings = WindowSettings.GetDefaultColumns();

                    foreach (ColumnSetting cs in colSettings)
                    {
                        if (cs.Header == headerText)
                        {
                            sortBy = cs.Binding;
                            break;
                        }
                    }
                }
            }

            // Skip non-sortable columns (checkbox, status icon)
            if (string.IsNullOrEmpty(sortBy))
                return;

            // Toggle sort direction
            if (sortBy == lastSortColumn)
            {
                lastSortAscending = !lastSortAscending;
            }
            else
            {
                lastSortColumn = sortBy;
                lastSortAscending = true;
            }

            // Remember selection
            int savedIndex = itemList.SelectedIndex;
            TrackItem savedItem = currentTrackItem;

            var sorted = currentItems.OrderBy(x => GetPropertyValue(x, sortBy),
                StringComparer.OrdinalIgnoreCase).ToList();
            if (!lastSortAscending)
                sorted.Reverse();

            currentItems.Clear();
            foreach (var item in sorted)
                currentItems.Add(item);

            suppressAutoDownload = true;
            itemList.ItemsSource = null;
            itemList.ItemsSource = currentItems;

            // Restore selection to the same item (not same index)
            if (savedItem != null)
            {
                int newIndex = currentItems.IndexOf(savedItem);
                if (newIndex >= 0)
                    itemList.SelectedIndex = newIndex;
            }
            suppressAutoDownload = false;

            statusFile.Text = "Sorted by " + sortBy + (lastSortAscending ? " ▲" : " ▼");
        }

        private string GetPropertyValue(TrackItem item, string propertyName)
        {
            var prop = typeof(TrackItem).GetProperty(propertyName);
            if (prop != null)
            {
                object val = prop.GetValue(item);
                return val?.ToString() ?? "";
            }
            return "";
        }

        private async void CheckSelected_Click(object sender, RoutedEventArgs e)
        {
            List<TrackItem> toCheck = new List<TrackItem>();

            foreach (TrackItem item in currentItems)
            {
                if (item.IsSelected)
                {
                    toCheck.Add(item);
                }
            }

            if (toCheck.Count == 0)
            {
                MessageBox.Show("No tracks selected. Use the checkboxes to select tracks.",
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await BatchCheck(toCheck);
        }

        private async void CheckAll_Click(object sender, RoutedEventArgs e)
        {
            if (currentItems.Count == 0)
            {
                MessageBox.Show("No tracks in current category.",
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Tick all checkboxes by simulating the Select All header checkbox
            CheckBox tempCb = new CheckBox();
            tempCb.IsChecked = true;
            SelectAll_Click(tempCb, new RoutedEventArgs());

            await BatchCheck(currentItems.ToList());
        }

        private async System.Threading.Tasks.Task BatchCheck(List<TrackItem> items)
        {
            int total = items.Count;
            int current = 0;
            int changed = 0;
            int unchanged = 0;
            int errors = 0;
            int newChecks = 0;

            // Remember selection before we start
            int savedIndex = itemList.SelectedIndex;
            TrackItem savedItem = currentTrackItem;

            statusProgress.IsIndeterminate = false;
            statusProgress.Minimum = 0;
            statusProgress.Maximum = total;
            statusProgress.Value = 0;

            btnCheckSelected.IsEnabled = false;
            btnCheckAll.IsEnabled = false;

            // Determine if this is an SPS category (save once at end, not per-item)
            bool isSpsCategory = false;
            if (!string.IsNullOrEmpty(currentCategoryPath))
            {
                if (Directory.GetFiles(currentCategoryPath, "*.xml").Length > 0)
                    isSpsCategory = true;
                else if (currentItems.Count > 0 && string.IsNullOrEmpty(currentItems[0].FilePath))
                    isSpsCategory = true;
            }

            try
            {
                foreach (TrackItem item in items)
                {
                    current++;
                    statusFile.Text = "Checking " + current + "/" + total +
                        ": " + item.TrackName;
                    statusProgress.Value = current;

                    if (string.IsNullOrWhiteSpace(item.TrackURL))
                    {
                        item.TrackStatus = "error";
                        item.LastChecked = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                        errors++;
                        continue;
                    }

                    string url = item.TrackURL;
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                    {
                        url = "https://" + url;
                    }

                    try
                    {
                        if (item.TrackMode == "text")
                        {
                            // Text mode — use WebView2 to get rendered text
                            try
                            {
                                await webView.EnsureCoreWebView2Async();
                                var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
                                EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs> navHandler = null;
                                navHandler = (ns, ne) =>
                                {
                                    webView.CoreWebView2.NavigationCompleted -= navHandler;
                                    tcs.TrySetResult(ne.IsSuccess);
                                };
                                webView.CoreWebView2.NavigationCompleted += navHandler;
                                webView.CoreWebView2.Navigate(url);

                                bool navSuccess = await tcs.Task;

                                if (navSuccess)
                                {
                                    // Wait for JS to render
                                    await System.Threading.Tasks.Task.Delay(2000);

                                    string text = await webView.CoreWebView2.ExecuteScriptAsync(
                                        "document.body.innerText");

                                    if (text != null && text.StartsWith("\"") && text.EndsWith("\""))
                                    {
                                        text = System.Text.Json.JsonSerializer.Deserialize<string>(text);
                                    }

                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        long downloadBytes = System.Text.Encoding.UTF8.GetByteCount(text);
                                        CheckResult result = item.ApplyCheck(text, downloadBytes);

                                        switch (result.Status)
                                        {
                                            case "changed": changed++; break;
                                            case "unchanged": unchanged++; break;
                                            case "new": newChecks++; break;
                                            case "error": errors++; break;
                                        }
                                    }
                                    else
                                    {
                                        item.TrackStatus = "error";
                                        item.LastChecked = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                                        errors++;
                                    }
                                }
                                else
                                {
                                    item.TrackStatus = "error";
                                    item.TrackURLStatus = "error";
                                    item.LastChecked = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                                    errors++;
                                }
                            }
                            catch (Exception)
                            {
                                item.TrackStatus = "error";
                                item.TrackURLStatus = "error";
                                item.LastChecked = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                                errors++;
                            }
                        }
                        else
                        {
                            // HTML mode (original)
                            SetupHttpHeaders(url);
                            string source = await httpClient.GetStringAsync(url);

                            // Detect bot challenge — fall back to WebView
                            if (IsChallengePage(source))
                            {
                                statusFile.Text = "Checking " + current + "/" + total +
                                    ": " + item.TrackName + " (bot challenge, using WebView2)";
                                string wvSource = await DownloadViaWebView(url);
                                if (!string.IsNullOrEmpty(wvSource) && !IsChallengePage(wvSource))
                                    source = wvSource;
                            }

                            long downloadBytes = System.Text.Encoding.UTF8.GetByteCount(source);

                            CheckResult result = item.ApplyCheck(source, downloadBytes);

                            switch (result.Status)
                            {
                                case "changed": changed++; break;
                                case "unchanged": unchanged++; break;
                                case "new": newChecks++; break;
                                case "error": errors++; break;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // HttpClient failed — try WebView2 fallback (HTML mode only)
                        try
                        {
                            statusFile.Text = "Checking " + current + "/" + total +
                                ": " + item.TrackName + " (WebView2 fallback)";

                            string source = await DownloadViaWebView(url);
                            if (!string.IsNullOrEmpty(source))
                            {
                                long downloadBytes = System.Text.Encoding.UTF8.GetByteCount(source);
                                CheckResult result = item.ApplyCheck(source, downloadBytes);

                                switch (result.Status)
                                {
                                    case "changed": changed++; break;
                                    case "unchanged": unchanged++; break;
                                    case "new": newChecks++; break;
                                    case "error": errors++; break;
                                }
                            }
                            else
                            {
                                item.TrackStatus = "error";
                                item.TrackURLStatus = "error";
                                item.StartStringStatus = "";
                                item.StopStringStatus = "";
                                item.HashStatus = "";
                                item.LatestVersionStatus = "";
                                item.LastChecked = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                                errors++;
                            }
                        }
                        catch (Exception)
                        {
                            item.TrackStatus = "error";
                            item.TrackURLStatus = "error";
                            item.StartStringStatus = "";
                            item.StopStringStatus = "";
                            item.HashStatus = "";
                            item.LatestVersionStatus = "";
                            item.LastChecked = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                            errors++;
                        }
                    }

					// Auto-update release date if changed
					if (!string.IsNullOrEmpty(item.LatestReleaseDate))
					{
					    if (item.ReleaseDateStatus == "new" || item.ReleaseDateStatus == "changed")
					    {
					        item.ReleaseDate = item.LatestReleaseDate;
					    }
					}

                    // Small delay to avoid hammering servers
                    await System.Threading.Tasks.Task.Delay(500);
                }
            }
            finally
            {
                btnCheckSelected.IsEnabled = true;
                btnCheckAll.IsEnabled = true;
                statusProgress.IsIndeterminate = false;
                statusProgress.Value = 0;
                statusProgress.Maximum = 100;
            }

			// Mark dirty since check results have changed item data
			if (!isSpsCategory && (changed > 0 || newChecks > 0 || errors > 0))
			{
			    isCategoryDirty = true;
			    UpdateSaveButtonStates();
			}

            // For SPS categories, save all results once at the end
            if (isSpsCategory && !string.IsNullOrEmpty(currentCategoryPath))
            {
                try
                {
                    string[] xmlFiles = Directory.GetFiles(currentCategoryPath, "*.xml");
                    string xmlPath;
                    string publisherFilter = txtSettingsPublisherFilter != null
                        ? txtSettingsPublisherFilter.Text.Trim()
                        : "";
                    string suitePath = "";
                    List<string> savedSuites = windowSettings.SelectedSuites;

                    if (xmlFiles.Length > 0)
                    {
                        xmlPath = xmlFiles[0];
                        string existingPf, existingSp;
                        List<string> existingSuites;
                        TrackItem.LoadSpsListFromFile(xmlPath, out existingPf, out existingSp, out existingSuites, out _);
                        if (!string.IsNullOrEmpty(existingSp))
                            suitePath = existingSp;
                        if (existingSuites.Count > 0)
                            savedSuites = existingSuites;
                    }
                    else
                    {
                        string folderName = Path.GetFileName(currentCategoryPath);
                        xmlPath = Path.Combine(currentCategoryPath, folderName + ".xml");
                    }

                    TrackItem.SaveSpsListToFile(xmlPath, currentItems.ToList(), publisherFilter, suitePath, savedSuites, ignoredSpsFiles);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving SPS check results: " + ex.Message,
                        AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // Refresh the list and preserve selection
            isLoadingFields = true;
            suppressAutoDownload = true;
            itemList.ItemsSource = null;
            itemList.ItemsSource = currentItems;
            if (savedIndex >= 0 && savedIndex < currentItems.Count)
                itemList.SelectedIndex = savedIndex;
            suppressAutoDownload = false;
            isLoadingFields = false;

            // Update fields if a track is loaded
            if (currentTrackItem != null)
            {
                isLoadingFields = true;
                editVersion.Text = currentTrackItem.Version;
                UpdateVersionDisplay();
                editReleaseDate.Text = currentTrackItem.ReleaseDate;
				if (currentTrackItem.ReleaseDateStatus == "changed" || currentTrackItem.ReleaseDateStatus == "new")
				{
				    editReleaseDate.Background = new SolidColorBrush(currentTheme.ReleaseDateChangedBackground);
				}
				else
				{
				    editReleaseDate.Background = new SolidColorBrush(currentTheme.TextBoxBackground);
				}
                isLoadingFields = false;
            }

            // Re-download source for the currently selected track
            if (currentTrackItem != null && !string.IsNullOrWhiteSpace(currentTrackItem.TrackURL))
            {
                AutoDownloadSource(currentTrackItem.TrackURL);
            }

            // Set dirty flags based on check results
            if (changed > 0 || newChecks > 0)
            {
                isCategoryDirty = true;
                // If the currently selected track was in the checked set and was modified, mark isDirty too
                if (currentTrackItem != null && currentTrackItem.IsDirtyInMemory)
                    isDirty = true;
                UpdateSaveButtonStates();
            }

            statusFile.Text = "Done: " + changed + " changed, " + unchanged +
                " unchanged, " + newChecks + " new, " + errors + " errors";
        }

        private DataTemplate CreateStatusCellTemplate()
        {
            DataTemplate template = new DataTemplate();

            FrameworkElementFactory textBlock = new FrameworkElementFactory(typeof(TextBlock));
            textBlock.SetValue(TextBlock.TextProperty, "\uF136"); // Filled circle
            textBlock.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe Fluent Icons"));
            textBlock.SetValue(TextBlock.FontSizeProperty, 10.0);
            textBlock.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textBlock.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textBlock.AddHandler(TextBlock.LoadedEvent, new RoutedEventHandler(StatusIcon_Loaded));
            textBlock.SetBinding(TextBlock.TagProperty, new System.Windows.Data.Binding("TrackStatus"));

            template.VisualTree = textBlock;
            return template;
        }

        private void StatusIcon_Loaded(object sender, RoutedEventArgs e)
        {
            TextBlock tb = sender as TextBlock;
            if (tb == null) return;

            string status = tb.Tag as string;
            ApplyStatusColor(tb, status);

            // Also update when the data context changes
            tb.DataContextChanged += (s, ev) =>
            {
                TrackItem item = tb.DataContext as TrackItem;
                if (item != null)
                {
                    tb.Tag = item.TrackStatus;
                    ApplyStatusColor(tb, item.TrackStatus);
                }
            };
        }

		private void ApplyStatusColor(TextBlock tb, string status)
		{
		    switch (status)
		    {
		        case "unchanged":
		            tb.Foreground = new SolidColorBrush(currentTheme.StatusUnchanged);
		            tb.ToolTip = "Unchanged — no changes detected";
		            break;
		        case "changed":
		            tb.Foreground = new SolidColorBrush(currentTheme.StatusChanged);
		            tb.ToolTip = "Changed — update detected!";
		            break;
		        case "error":
		            tb.Foreground = new SolidColorBrush(currentTheme.StatusError);
		            tb.ToolTip = "Error — check failed";
		            break;
		        case "new":
		            tb.Foreground = new SolidColorBrush(currentTheme.StatusNew);
		            tb.ToolTip = "New — first check recorded";
		            break;
		        default:
		            tb.Foreground = new SolidColorBrush(currentTheme.StatusUnchecked);
		            tb.ToolTip = "Unchecked";
		            break;
		    }
		}

		private void SetBatchStatusText(int total, int changed, int unchanged, int newChecks, int errors)
		{
		    statusFile.Inlines.Clear();
		    statusFile.Inlines.Add(new Run("Batch complete: " + total + " checked — "));

		    Run changedIcon = new Run("\uF136 ");
		    changedIcon.FontFamily = new FontFamily("Segoe Fluent Icons");
		    changedIcon.Foreground = new SolidColorBrush(currentTheme.StatusChanged);
		    statusFile.Inlines.Add(changedIcon);
		    statusFile.Inlines.Add(new Run(changed + " changed  "));

		    Run unchangedIcon = new Run("\uF136 ");
		    unchangedIcon.FontFamily = new FontFamily("Segoe Fluent Icons");
		    unchangedIcon.Foreground = new SolidColorBrush(currentTheme.StatusUnchanged);
		    statusFile.Inlines.Add(unchangedIcon);
		    statusFile.Inlines.Add(new Run(unchanged + " unchanged  "));

		    Run newIcon = new Run("\uF136 ");
		    newIcon.FontFamily = new FontFamily("Segoe Fluent Icons");
		    newIcon.Foreground = new SolidColorBrush(currentTheme.StatusNew);
		    statusFile.Inlines.Add(newIcon);
		    statusFile.Inlines.Add(new Run(newChecks + " new  "));

		    Run errorIcon = new Run("\uF136 ");
		    errorIcon.FontFamily = new FontFamily("Segoe Fluent Icons");
		    errorIcon.Foreground = new SolidColorBrush(currentTheme.StatusError);
		    statusFile.Inlines.Add(errorIcon);
		    statusFile.Inlines.Add(new Run(errors + " errors"));
		}
		
		private async void AutoDownloadSource(string url)
		{
		    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
		        url = "https://" + url;

		    statusProgress.IsIndeterminate = true;

		    try
		    {
		        // Always navigate WebView (needed for both modes)
		        try
		        {
		            await webView.EnsureCoreWebView2Async();
		            webView.CoreWebView2.Navigate(url);
		        }
		        catch (Exception) { }

		        // If text mode, wait for WebView to load then extract rendered text
		        if (currentTrackItem != null && currentTrackItem.TrackMode == "text")
		        {
		            try
		            {
		                var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
		                EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs> handler = null;
		                handler = (s, e) =>
		                {
		                    webView.CoreWebView2.NavigationCompleted -= handler;
		                    tcs.TrySetResult(e.IsSuccess);
		                };
		                webView.CoreWebView2.NavigationCompleted += handler;

		                bool success = await tcs.Task;

		                if (success)
		                {
		                    if (blockCookiePopups)
		                        await InjectCookiePopupBlocker();

		                    await System.Threading.Tasks.Task.Delay(1500);

		                    if (blockCookiePopups)
		                        await InjectCookiePopupBlocker();

		                    string text = await webView.CoreWebView2.ExecuteScriptAsync(
		                        "document.body.innerText");

		                    if (text != null && text.StartsWith("\"") && text.EndsWith("\""))
		                    {
		                        text = System.Text.Json.JsonSerializer.Deserialize<string>(text);
		                    }

		                    if (!string.IsNullOrEmpty(text))
		                    {
		                        currentSource = text;
		                        DisplaySource(currentSource);

		                        // TEXT MODE: banner never needed — content is already rendered
		                        if (sourceInfoBanner != null)
		                            sourceInfoBanner.Visibility = Visibility.Collapsed;

		                        statusFile.Text = "Source: rendered text — " + url;
		                    }
		                }
		            }
		            catch (Exception)
		            {
		                statusFile.Text = "Failed to get rendered text";
		            }
		            return;
		        }

		        // HTML mode (original behaviour)
		        try
		        {
		            SetupHttpHeaders(url);
		            string source = await httpClient.GetStringAsync(url);

		            if (IsChallengePage(source))
		            {
		                statusFile.Text = "Bot challenge detected, using WebView2...";
		                string webViewSource = await DownloadViaWebView(url);
		                if (!string.IsNullOrEmpty(webViewSource) && !IsChallengePage(webViewSource))
		                {
		                    source = webViewSource;
		                    statusFile.Text = "Loaded via WebView2: " + url;
		                }
		                else
		                {
		                    statusFile.Text = "Challenge page — WebView2 fallback failed: " + url;
		                }
		            }

		            currentSource = source;
		            DisplaySource(currentSource);

		            // HTML MODE: show banner if source looks like a JS-only page
					if (sourceInfoBanner != null)
					    sourceInfoBanner.Visibility = LooksLikeJsOnlyPage(currentSource)
					        ? Visibility.Visible : Visibility.Collapsed;
		        }
		        catch (Exception)
		        {
		            try
		            {
		                statusFile.Text = "HttpClient blocked, trying WebView2 fallback...";
		                string source = await DownloadViaWebView(url);
		                if (!string.IsNullOrEmpty(source))
		                {
		                    currentSource = source;
		                    DisplaySource(currentSource);

		                    // HTML MODE FALLBACK: same check
							if (sourceInfoBanner != null)
							    sourceInfoBanner.Visibility = LooksLikeJsOnlyPage(currentSource)
							        ? Visibility.Visible : Visibility.Collapsed;
									
		                    statusFile.Text = "Loaded via WebView2: " + url;
		                }
		                else
		                {
		                    currentSource = "";
		                    sourceView.Document.Blocks.Clear();
		                    statusFile.Text = "Failed to load: " + url;
		                }
		            }
		            catch (Exception)
		            {
		                currentSource = "";
		                sourceView.Document.Blocks.Clear();
		            }
		        }
		    }
		    finally
		    {
		        statusProgress.IsIndeterminate = false;
		        statusProgress.Value = 0;
		    }
		}

        // ============================
        // Track Settings Buttons
        // ============================

		private void MarkDirty()
		{
		    if (isLoadingFields) return;
		    isDirty = true;
		    isCategoryDirty = true;
		    if (currentTrackItem != null)
		        currentTrackItem.IsDirtyInMemory = true;
		    UpdateSaveButtonStates();
		}

        private void MarkCategoryDirty()
        {
            isCategoryDirty = true;
            UpdateSaveButtonStates();
        }

		private void RecalculateCategoryDirty()
		{
		    isCategoryDirty = false;
		    foreach (TrackItem item in currentItems)
		    {
		        if (item.IsDirtyInMemory)
		        {
		            isCategoryDirty = true;
		            break;
		        }
		    }
		}

        private void ClearDirty()
        {
            isDirty = false;
            UpdateSaveButtonStates();
        }

		private void ClearAllDirty()
		{
		    isDirty = false;
		    isCategoryDirty = false;
		    foreach (TrackItem item in currentItems)
		    {
		        item.IsDirtyInMemory = false;
		    }
		    UpdateSaveButtonStates();
		}

		private void UpdateSaveButtonStates()
		{
		    double enabledOpacity = 1.0;
		    double disabledOpacity = 0.4;

		    // Save and Save As reflect the CURRENT track only
		    if (btnSaveTrack != null)
		    {
		        btnSaveTrack.IsEnabled = isDirty;
		        btnSaveTrack.Opacity = isDirty ? enabledOpacity : disabledOpacity;
		    }
		    if (btnSaveTrackAs != null)
		    {
		        btnSaveTrackAs.IsEnabled = isDirty;
		        btnSaveTrackAs.Opacity = isDirty ? enabledOpacity : disabledOpacity;
		    }
			if (btnSave != null)
			{
			    btnSave.IsEnabled = isDirty;
			    btnSave.Opacity = isDirty ? enabledOpacity : disabledOpacity;
			}
			if (btnSaveAs != null)
			{
			    btnSaveAs.IsEnabled = isDirty;
			    btnSaveAs.Opacity = isDirty ? enabledOpacity : disabledOpacity;
			}

		    // Save All reflects the WHOLE CATEGORY
		    if (btnSaveAll != null)
		    {
		        btnSaveAll.IsEnabled = isCategoryDirty;
		        btnSaveAll.Opacity = isCategoryDirty ? enabledOpacity : disabledOpacity;
		    }
		}

        private void Field_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isLoadingFields)
                MarkDirty();
        }

        private void OpenFileInEditor_Click(object sender, RoutedEventArgs e)
        {
            TrackItem selected = itemList.SelectedItem as TrackItem;

            if (selected == null)
            {
                MessageBox.Show("No track selected.", AppInfo.ShortName,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!File.Exists(selected.FilePath))
            {
                MessageBox.Show("Track file not found:\n" + selected.FilePath,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string editorPath = editEditorPath.Text.Trim();

            if (string.IsNullOrEmpty(editorPath))
            {
                // Fall back to default system editor
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(selected.FilePath)
                        { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error opening file:\n" + ex.Message,
                        AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            // Resolve relative path against app directory
            string resolvedPath = editorPath;
            if (!Path.IsPathRooted(editorPath))
            {
                resolvedPath = Path.Combine(appDir, editorPath);
            }

            if (!File.Exists(resolvedPath))
            {
                MessageBox.Show("Editor not found:\n" + resolvedPath +
                    "\n\nSet the editor path in App Settings, or leave blank to use the system default.",
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = resolvedPath,
                        Arguments = "\"" + selected.FilePath + "\"",
                        UseShellExecute = false
                    });
                statusFile.Text = "Opened in editor: " + Path.GetFileName(selected.FilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error launching editor:\n" + ex.Message,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

		private void OpenInSpsBuilder_Click(object sender, RoutedEventArgs e)
		{
		    if (currentTrackItem == null)
		    {
		        MessageBox.Show("No track selected.", AppInfo.ShortName,
		            MessageBoxButton.OK, MessageBoxImage.Warning);
		        return;
		    }

		    string spsRoot = windowSettings.SpsSuiteRootPath;
		    if (string.IsNullOrEmpty(spsRoot) || !Directory.Exists(spsRoot))
		    {
		        MessageBox.Show("SPSSuite path is not set.\n\nSet it in App Settings first.",
		            AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
		        return;
		    }

		    string builderPath = Path.Combine(spsRoot, "SyMenuSuite", "SPS_Builder_sps", "SPSBuilder.exe");
		    if (!File.Exists(builderPath))
		    {
		        MessageBox.Show("SPSBuilder.exe not found at:\n" + builderPath,
		            AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
		        return;
		    }

		    try
		    {
		        string suiteName = currentTrackItem.SuiteName;
		        string spsFileName = currentTrackItem.SpsFileName;

		        if (string.IsNullOrEmpty(suiteName) || string.IsNullOrEmpty(spsFileName))
		        {
		            MessageBox.Show("No SPS file information for this track.\n\nTry rebuilding the category first.",
		                AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
		            return;
		        }

		        // Try direct path first (works for user suites where .sps files are in _Cache)
		        string spsFile = Path.Combine(spsRoot, suiteName, "_Cache", spsFileName);

		        if (!File.Exists(spsFile))
		        {
		            // Not found directly — try extracting from zip files in _Cache (default suites)
		            string cachePath = Path.Combine(spsRoot, suiteName, "_Cache");
		            if (Directory.Exists(cachePath))
		            {
		                string tmpPath = Path.Combine(spsRoot, suiteName, "_TmpET_Open");

		                // Clean up any previous temp folder
		                if (Directory.Exists(tmpPath))
		                {
		                    try { Directory.Delete(tmpPath, true); } catch { }
		                }
		                Directory.CreateDirectory(tmpPath);

		                bool found = false;
		                string[] zipFiles = Directory.GetFiles(cachePath, "*.zip");
		                foreach (string zipFile in zipFiles)
		                {
		                    try
		                    {
		                        using (ZipArchive archive = ZipFile.OpenRead(zipFile))
		                        {
		                            foreach (ZipArchiveEntry entry in archive.Entries)
		                            {
		                                if (entry.Name.Equals(spsFileName, StringComparison.OrdinalIgnoreCase))
		                                {
		                                    string extractedPath = Path.Combine(tmpPath, entry.Name);
		                                    entry.ExtractToFile(extractedPath, true);
		                                    spsFile = extractedPath;
		                                    found = true;
		                                    break;
		                                }
		                            }
		                        }
		                        if (found) break;
		                    }
		                    catch { }
		                }

		                if (!found)
		                {
		                    // Clean up temp folder if nothing found
		                    try { Directory.Delete(tmpPath, true); } catch { }

		                    MessageBox.Show("SPS file not found:\n" + spsFileName +
		                        "\n\nSearched _Cache folder and zip files in:\n" +
		                        cachePath, AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
		                    return;
		                }
		            }
		            else
		            {
		                MessageBox.Show("_Cache folder not found:\n" +
		                    Path.Combine(spsRoot, suiteName, "_Cache"),
		                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
		                return;
		            }
		        }

		        System.Diagnostics.Process.Start(builderPath, "\"" + spsFile + "\"");
		        statusFile.Text = "Opened SPS Builder: " + spsFileName;
		    }
		    catch (Exception ex)
		    {
		        MessageBox.Show("Error launching SPS Builder: " + ex.Message,
		            AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
		    }
		}

        private void BrowseEditor_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Title = "Select Text Editor";
            dlg.Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*";
            dlg.InitialDirectory = appDir;

            if (dlg.ShowDialog() == true)
            {
                string selectedPath = dlg.FileName;

                // Try to make it relative to app directory
                try
                {
                    string appDirFull = Path.GetFullPath(appDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    string selectedFull = Path.GetFullPath(selectedPath);

                    if (selectedFull.StartsWith(appDirFull, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedPath = selectedFull.Substring(appDirFull.Length);
                    }
                }
                catch (Exception) { }

                editEditorPath.Text = selectedPath;
                windowSettings.EditorPath = selectedPath;
                statusFile.Text = "Editor set: " + selectedPath;
            }
        }

        private void SaveTrack_Click(object sender, RoutedEventArgs e)
        {
            // Check if this is an SPS category — save as single XML
            bool isSpsCategory = false;
            if (!string.IsNullOrEmpty(currentCategoryPath))
            {
                if (Directory.GetFiles(currentCategoryPath, "*.xml").Length > 0)
                    isSpsCategory = true;
                else if (currentItems.Count > 0 && string.IsNullOrEmpty(currentItems[0].FilePath))
                    isSpsCategory = true;
            }

            if (isSpsCategory)
            {
                SaveCurrentTrackFields();
                string[] xmlFiles = Directory.GetFiles(currentCategoryPath, "*.xml");
                string xmlPath;
                string publisherFilter = txtSettingsPublisherFilter != null
                	? txtSettingsPublisherFilter.Text.Trim()
                	: "";
                string suitePath = "";
                List<string> savedSuites = windowSettings.SelectedSuites;

                if (xmlFiles.Length > 0)
                {
                    xmlPath = xmlFiles[0];
                    string existingPf, existingSp;
                    List<string> existingSuites;
                    TrackItem.LoadSpsListFromFile(xmlPath, out existingPf, out existingSp, out existingSuites, out _);
                    if (!string.IsNullOrEmpty(existingSp))
                        suitePath = existingSp;
                    if (existingSuites.Count > 0)
                        savedSuites = existingSuites;
                }
                else
                {
                    string folderName = Path.GetFileName(currentCategoryPath);
                    xmlPath = Path.Combine(currentCategoryPath, folderName + ".xml");
                }

                TrackItem.SaveSpsListToFile(xmlPath, currentItems.ToList(), publisherFilter, suitePath, savedSuites, ignoredSpsFiles);
				// Clear dirty on the saved track, then recalculate
				if (currentTrackItem != null)
				    currentTrackItem.IsDirtyInMemory = false;
				isDirty = false;
				RecalculateCategoryDirty();
				UpdateSaveButtonStates();

				isLoadingFields = true;
				suppressAutoDownload = true;
				int selectedIndex = itemList.SelectedIndex;
				itemList.ItemsSource = null;
				itemList.ItemsSource = currentItems;
				if (selectedIndex >= 0)
				{
				    itemList.SelectedIndex = selectedIndex;
				    itemList.ScrollIntoView(itemList.SelectedItem);
				}
				suppressAutoDownload = false;
				isLoadingFields = false;

				suppressCategoryReload = true;
				RefreshCategoryTree();
				suppressCategoryReload = false;
				statusFile.Text = "SPS category saved: " + Path.GetFileName(currentCategoryPath);
				return;
            }

            // Use stored reference instead of relying on list selection
            TrackItem selected = currentTrackItem;

            if (selected == null)
            {
                CreateNewTrack();
                return;
            }

            string newName = editName.Text.Trim();

            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Track Name cannot be empty.", AppInfo.ShortName,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if name changed - need to rename the file
            string oldName = Path.GetFileNameWithoutExtension(selected.FilePath);

            // Update all fields from UI
            selected.TrackName = newName;
            selected.TrackURL = editTrackURL.Text;
            selected.StartString = editStartString.Text;
            selected.StopString = editStopString.Text;
            selected.DownloadURL = editDownloadURL.Text;
            selected.Version = editVersion.Text;
			if (!string.IsNullOrEmpty(selected.LatestVersion) && selected.TrackStatus != "error")
			{
			    selected.Version = selected.LatestVersion;
			    editVersion.Text = selected.LatestVersion;
			}
            selected.ReleaseDate = editReleaseDate.Text;
            selected.ReleaseDateStartString = editReleaseDateStartString.Text;
			selected.ReleaseDateStopString = editReleaseDateStopString.Text;
            selected.PublisherName = editPublisherName.Text;
            selected.SuiteName = editSuiteName.Text;

            try
            {
                // If the name changed, rename the file
                if (oldName != newName)
                {
                    string safeName = newName;
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        safeName = safeName.Replace(c.ToString(), "");
                    }

                    string dir = Path.GetDirectoryName(selected.FilePath);
                    string newPath = Path.Combine(dir, safeName + ".track");

                    if (File.Exists(newPath) && newPath != selected.FilePath)
                    {
                        MessageBox.Show("A track file with that name already exists.",
                            AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (File.Exists(selected.FilePath))
                    {
                        File.Delete(selected.FilePath);
                    }

                    selected.FilePath = newPath;
                }

                // If status was "changed" and version is now up to date, mark as unchanged
                if (selected.TrackStatus == "changed" &&
                    !string.IsNullOrEmpty(selected.LatestVersion) &&
                    selected.Version == selected.LatestVersion)
                {
                    selected.TrackStatus = "unchanged";
                }

                selected.SaveToFile();

                // Clear dirty on this track only, recalculate category
                selected.IsDirtyInMemory = false;
                isDirty = false;
                RecalculateCategoryDirty();
                UpdateSaveButtonStates();

                isLoadingFields = true;
                suppressAutoDownload = true;
                int selectedIndex = currentItems.IndexOf(selected);
                itemList.ItemsSource = null;
                itemList.ItemsSource = currentItems;
				if (selectedIndex >= 0)
				{
				    itemList.SelectedIndex = selectedIndex;
				    itemList.ScrollIntoView(itemList.SelectedItem);
				}
                suppressAutoDownload = false;
                isLoadingFields = false;

                UpdateVersionDisplay();
                suppressCategoryReload = true;
                RefreshCategoryTree();
                suppressCategoryReload = false;
                if (currentTrackItem != null && !string.IsNullOrWhiteSpace(currentTrackItem.TrackURL))
                {
                    AutoDownloadSource(currentTrackItem.TrackURL);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving track: " + ex.Message,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

		private void DeleteTrack_Click(object sender, RoutedEventArgs e)
		{
		    TrackItem selected = itemList.SelectedItem as TrackItem;

		    if (selected == null)
		    {
		        MessageBox.Show("No track selected.", AppInfo.ShortName,
		            MessageBoxButton.OK, MessageBoxImage.Information);
		        return;
		    }

		    // Determine if this is an SPS category
		    bool isSpsCategory = false;
		    if (!string.IsNullOrEmpty(currentCategoryPath))
		    {
		        if (Directory.GetFiles(currentCategoryPath, "*.xml").Length > 0)
		            isSpsCategory = true;
		        else if (currentItems.Count > 0 && string.IsNullOrEmpty(currentItems[0].FilePath))
		            isSpsCategory = true;
		    }

		    if (isSpsCategory)
		    {
		        // --- SPS category: remove entry from list + XML, optionally delete .sps file ---
		        string spsFileInfo = "";
		        string spsFilePath = null;

		        if (!string.IsNullOrEmpty(selected.SpsFileName) && !string.IsNullOrEmpty(selected.SuiteName)
		            && !string.IsNullOrEmpty(windowSettings.SpsSuiteRootPath))
		        {
		            spsFilePath = Path.Combine(windowSettings.SpsSuiteRootPath,
		                selected.SuiteName, "_Cache", selected.SpsFileName);
		            if (File.Exists(spsFilePath))
		                spsFileInfo = "\n\nSPS file on disk:\n" + spsFilePath +
		                    "\n\nAlso delete the .sps file from the cache?";
		        }

		        // First confirm removing from the list
		        MessageBoxResult result = MessageBox.Show(
		            "Remove track '" + selected.TrackName + "' from this SPS category?" + spsFileInfo,
		            "Delete SPS Track",
		            MessageBoxButton.YesNo, MessageBoxImage.Warning);

		        if (result != MessageBoxResult.Yes)
		            return;

		        try
		        {
		            int deletedIndex = currentItems.IndexOf(selected);

		            // Remove from in-memory list
		            currentItems.Remove(selected);

		            // Save updated XML
		            string[] xmlFiles = Directory.GetFiles(currentCategoryPath, "*.xml");
		            if (xmlFiles.Length > 0)
		            {
		                string xmlPath = xmlFiles[0];
		                string publisherFilter = txtSettingsPublisherFilter != null
		                    ? txtSettingsPublisherFilter.Text.Trim() : "";
		                string suitePath = "";
		                List<string> savedSuites = windowSettings.SelectedSuites;

		                string existingPf, existingSp;
		                List<string> existingSuites;
		                TrackItem.LoadSpsListFromFile(xmlPath, out existingPf, out existingSp, out existingSuites, out _);
		                if (!string.IsNullOrEmpty(existingSp)) suitePath = existingSp;
		                if (existingSuites.Count > 0) savedSuites = existingSuites;

		                TrackItem.SaveSpsListToFile(xmlPath, currentItems.ToList(),
		                    publisherFilter, suitePath, savedSuites, ignoredSpsFiles);
		            }

		            // Delete the .sps file from disk if user confirmed and it exists
		            if (!string.IsNullOrEmpty(spsFilePath) && File.Exists(spsFilePath))
		            {
		                try { File.Delete(spsFilePath); }
		                catch (Exception ex2)
		                {
		                    MessageBox.Show("Could not delete SPS file:\n" + ex2.Message,
		                        AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
		                }
		            }

		            // Refresh UI
		            isLoadingFields = true;
		            suppressAutoDownload = true;
		            itemList.ItemsSource = null;
		            itemList.ItemsSource = currentItems;

		            if (currentItems.Count > 0)
		            {
		                int newIndex = deletedIndex;
		                if (newIndex >= currentItems.Count)
		                    newIndex = currentItems.Count - 1;
		                suppressAutoDownload = false;
		                isLoadingFields = false;
		                itemList.SelectedIndex = newIndex;
		            }
		            else
		            {
		                ClearTrackFields();
		                currentTrackItem = null;
		                suppressAutoDownload = false;
		                isLoadingFields = false;
		            }

		            suppressCategoryReload = true;
		            RefreshCategoryTree();
		            suppressCategoryReload = false;

		            RecalculateCategoryDirty();
		            isDirty = false;
		            UpdateSaveButtonStates();

		            statusFile.Text = "Deleted SPS track: " + selected.TrackName;
		        }
		        catch (Exception ex)
		        {
		            suppressAutoDownload = false;
		            isLoadingFields = false;
		            MessageBox.Show("Error deleting SPS track: " + ex.Message,
		                AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
		        }

		        return;
		    }

		    // --- Standard .track file category (original logic) ---
		    MessageBoxResult trackResult = MessageBox.Show(
		        "Delete track '" + selected.TrackName + "'?\n\n" +
		        "File: " + Path.GetFileName(selected.FilePath),
		        "Delete Track",
		        MessageBoxButton.YesNo, MessageBoxImage.Warning);

		    if (trackResult != MessageBoxResult.Yes)
		        return;

				try
				{
				    int deletedIndex = currentItems.IndexOf(selected);

				    // Check if this is an SPS category
				    isSpsCategory = false;
				    if (!string.IsNullOrEmpty(currentCategoryPath))
				    {
				        if (Directory.GetFiles(currentCategoryPath, "*.xml").Length > 0)
				            isSpsCategory = true;
				        else if (currentItems.Count > 0 && string.IsNullOrEmpty(currentItems[0].FilePath))
				            isSpsCategory = true;
				    }

				    if (isSpsCategory)
				    {
				        // SPS category — remove from in-memory list (don't reload from disk)
				        isLoadingFields = true;
				        suppressAutoDownload = true;

				        currentItems.Remove(selected);

				        suppressAutoDownload = true;
				        itemList.ItemsSource = null;
				        itemList.ItemsSource = currentItems;
				    }
				    else
				    {
				        // Standard category — delete the .track file and reload
				        if (File.Exists(selected.FilePath))
				        {
				            File.Delete(selected.FilePath);
				        }

				        isLoadingFields = true;
				        suppressAutoDownload = true;

				        if (!string.IsNullOrEmpty(currentCategoryPath))
				        {
				            LoadTrackFiles(currentCategoryPath);
				        }
				    }

		            suppressCategoryReload = true;
		            RefreshCategoryTree();
		            suppressCategoryReload = false;

		            if (currentItems.Count > 0)
		            {
		                int newIndex = deletedIndex;
		                if (newIndex >= currentItems.Count)
		                    newIndex = currentItems.Count - 1;
		                suppressAutoDownload = false;
		                isLoadingFields = false;
		                itemList.SelectedIndex = newIndex;
		            }
		            else
		            {
		                ClearTrackFields();
		                currentTrackItem = null;
		                suppressAutoDownload = false;
		                isLoadingFields = false;
		            }

	                if (isSpsCategory)
	                {
	                    // Mark category dirty so Save All is enabled
	                    isCategoryDirty = true;
	                    isDirty = false;
	                    UpdateSaveButtonStates();
	                }
	                else
	                {
	                    RecalculateCategoryDirty();
	                    isDirty = false;
	                    UpdateSaveButtonStates();
	                }

		            statusFile.Text = "Deleted: " + selected.TrackName;
		        }
		        catch (Exception ex)
		        {
		            suppressAutoDownload = false;
		            isLoadingFields = false;
		            MessageBox.Show("Error deleting track: " + ex.Message,
		                AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
		        }
		}

        private void CreateNewTrack()
        {
            // Use stored category path instead of relying on tree selection
            TreeViewItem selectedCat = categoryTree.SelectedItem as TreeViewItem;
            string folderPath = selectedCat != null
                ? (string)selectedCat.Tag
                : currentCategoryPath;

            if (string.IsNullOrEmpty(folderPath))
            {
                MessageBox.Show("Please select a category first.", AppInfo.ShortName,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string name = editName.Text;

            if (string.IsNullOrWhiteSpace(name))
            {
                name = ShowInputDialog("New Track", "Enter Track Name:");
                if (string.IsNullOrWhiteSpace(name))
                    return;
            }

            string safeName = name;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(c.ToString(), "");
            }

            string filePath = Path.Combine(folderPath, safeName + ".track");

            if (File.Exists(filePath))
            {
                MessageBoxResult overwrite = MessageBox.Show(
                    "Track file '" + safeName + ".track' already exists.\n\n" +
                    "Do you want to overwrite it?",
                    AppInfo.ShortName, MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (overwrite != MessageBoxResult.Yes)
                    return;
            }

            TrackItem newItem = new TrackItem();
            newItem.TrackName = name;
            newItem.FilePath = filePath;
            newItem.TrackURL = editTrackURL.Text;
            newItem.StartString = editStartString.Text;
            newItem.StopString = editStopString.Text;
            newItem.DownloadURL = editDownloadURL.Text;
            newItem.Version = editVersion.Text;
            newItem.ReleaseDate = editReleaseDate.Text;
            newItem.PublisherName = string.IsNullOrWhiteSpace(editPublisherName.Text)
			    ? (windowSettings.DefaultPublisherName ?? "")
			    : editPublisherName.Text;
            newItem.SuiteName = editSuiteName.Text;
            newItem.CreationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            newItem.TrackMode = windowSettings.DefaultTrackMode ?? "html";

            try
            {
                newItem.SaveToFile(filePath);
                LoadTrackFiles(folderPath);
                RefreshCategoryTree();
                statusFile.Text = "Created: " + name;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error creating track: " + ex.Message,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

		private void ClearTrackFields()
		{
		    isLoadingFields = true;
		    currentTrackItem = null;
		    editName.Text = "";
		    editTrackURL.Text = "";
		    editStartString.Text = "";
		    editStopString.Text = "";
		    editDownloadURL.Text = "";
		    editVersion.Text = "";
		    editLatestVersion.Text = "";
		    editLatestVersion.Foreground = Brushes.Gray;
		    btnUpdateVersion.IsEnabled = false;
		    editReleaseDate.Text = "";
		    editReleaseDateStartString.Text = "";
			editReleaseDateStopString.Text = "";
		    editPublisherName.Text = "";
		    editSuiteName.Text = "";
		    isLoadingFields = false;
		}

        private void UpdateVersion_Click(object sender, RoutedEventArgs e)
        {
            if (currentTrackItem != null && !string.IsNullOrEmpty(currentTrackItem.LatestVersion))
            {
                // Push current UI values into the model before saving
                currentTrackItem.TrackName = editName.Text;
                currentTrackItem.TrackURL = editTrackURL.Text;
                currentTrackItem.StartString = editStartString.Text;
                currentTrackItem.StopString = editStopString.Text;
                currentTrackItem.DownloadURL = editDownloadURL.Text;
                currentTrackItem.ReleaseDate = editReleaseDate.Text;
                currentTrackItem.PublisherName = editPublisherName.Text;
                currentTrackItem.SuiteName = editSuiteName.Text;

                // Now update the version
                editVersion.Text = currentTrackItem.LatestVersion;
                currentTrackItem.Version = currentTrackItem.LatestVersion;

                // Check if this is an SPS category — save as XML instead of .track
                bool isSpsCategory = false;
                if (!string.IsNullOrEmpty(currentCategoryPath))
                {
                    if (Directory.GetFiles(currentCategoryPath, "*.xml").Length > 0)
                        isSpsCategory = true;
                    else if (currentItems.Count > 0 && string.IsNullOrEmpty(currentItems[0].FilePath))
                        isSpsCategory = true;
                }

                if (isSpsCategory)
                {
                    // Save the entire SPS category XML
                    string[] xmlFiles = Directory.GetFiles(currentCategoryPath, "*.xml");
                    string xmlPath;
                    string publisherFilter = txtSettingsPublisherFilter != null
                        ? txtSettingsPublisherFilter.Text.Trim()
                        : "";
                    string suitePath = "";
                    List<string> savedSuites = windowSettings.SelectedSuites;

                    if (xmlFiles.Length > 0)
                    {
                        xmlPath = xmlFiles[0];
                        string existingPf, existingSp;
                        List<string> existingSuites;
                        TrackItem.LoadSpsListFromFile(xmlPath, out existingPf, out existingSp, out existingSuites, out _);
                        if (!string.IsNullOrEmpty(existingSp))
                            suitePath = existingSp;
                        if (existingSuites.Count > 0)
                            savedSuites = existingSuites;
                    }
                    else
                    {
                        string folderName = Path.GetFileName(currentCategoryPath);
                        xmlPath = Path.Combine(currentCategoryPath, folderName + ".xml");
                    }

                    TrackItem.SaveSpsListToFile(xmlPath, currentItems.ToList(), publisherFilter, suitePath, savedSuites, ignoredSpsFiles);
                }
				else
				{
				    if (!string.IsNullOrEmpty(currentTrackItem.FilePath))
				        currentTrackItem.SaveToFile();
				}

                // Refresh list
                int selectedIndex = currentItems.IndexOf(currentTrackItem);
                suppressAutoDownload = true;
                itemList.ItemsSource = null;
                itemList.ItemsSource = currentItems;
				if (selectedIndex >= 0)
				{
				    itemList.SelectedIndex = selectedIndex;
				    itemList.ScrollIntoView(itemList.SelectedItem);
				}
                suppressAutoDownload = false;

                UpdateVersionDisplay();
                ClearDirty();
                statusFile.Text = "Version updated to " + currentTrackItem.Version;
            }
        }

		private void UpdateVersionDisplay()
		{
		    string current = editVersion.Text.Trim();
		    string latest = currentTrackItem?.LatestVersion ?? "";

		    if (string.IsNullOrEmpty(latest) || latest == "(not yet checked)")
		    {
		        editLatestVersion.Text = "(not yet checked)";
		        editLatestVersion.Foreground = Brushes.Gray;
		        btnUpdateVersion.IsEnabled = false;
		        return;
		    }

		    editLatestVersion.Text = latest;

		    if (current == latest)
		    {
		        // Same version — green, button disabled
		        editLatestVersion.Foreground = new SolidColorBrush(currentTheme.VersionMatchColor);
		        btnUpdateVersion.IsEnabled = false;
		    }
		    else
		    {
		        // Different version — red, button enabled
		        editLatestVersion.Foreground = new SolidColorBrush(currentTheme.VersionMismatchColor);
		        btnUpdateVersion.IsEnabled = true;
		    }
		}

        private string ResolveDownloadURL(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            string version = editVersion.Text.Trim();

            bool hasPlaceholder = url.Contains("{VERSION}") ||
                                  url.Contains("{VERSION_}") ||
                                  url.Contains("{VERSION-}");

            if (hasPlaceholder)
            {
                if (string.IsNullOrWhiteSpace(version))
                {
                    MessageBox.Show(
                        "Download URL contains a {VERSION} placeholder but the Version field is empty.\n\n" +
                        "Please update the version first.",
                        AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                url = url.Replace("{VERSION_}", version.Replace(".", "_"));
                url = url.Replace("{VERSION-}", version.Replace(".", "-"));
                url = url.Replace("{VERSION}", version);
            }

            return url;
        }

        private async void DownloadFile_Click(object sender, RoutedEventArgs e)
        {
            string rawUrl = editDownloadURL.Text.Trim();

            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                MessageBox.Show("No Download URL specified.", AppInfo.ShortName,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string resolvedUrl = ResolveDownloadURL(rawUrl);
            if (resolvedUrl == null)
                return;

            // Extract filename from URL
            string fileName;
            try
            {
                Uri uri = new Uri(resolvedUrl);
                fileName = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrWhiteSpace(fileName))
                    fileName = "download";
            }
            catch
            {
                fileName = "download";
            }

            // Ask user where to save
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = fileName;
            dlg.Filter = "All files (*.*)|*.*";
            dlg.Title = "Save Downloaded File";

            if (dlg.ShowDialog() != true)
                return;

            string savePath = dlg.FileName;

            try
            {
                statusFile.Text = "Downloading: " + resolvedUrl;

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(10);

                    using (HttpResponseMessage response = await client.GetAsync(resolvedUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        long? totalBytes = response.Content.Headers.ContentLength;

                        using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                        using (FileStream fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[81920];
                            long totalRead = 0;
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalRead += bytesRead;

                                if (totalBytes.HasValue && totalBytes.Value > 0)
                                {
                                    int percent = (int)((totalRead * 100) / totalBytes.Value);
                                    statusFile.Text = "Downloading: " + percent + "% (" +
                                        (totalRead / 1024) + " KB / " + (totalBytes.Value / 1024) + " KB)";
                                }
                                else
                                {
                                    statusFile.Text = "Downloading: " + (totalRead / 1024) + " KB...";
                                }
                            }
                        }
                    }
                }

                statusFile.Text = "Downloaded: " + Path.GetFileName(savePath);
            }
            catch (Exception ex)
            {
                statusFile.Text = "Download failed.";
                MessageBox.Show("Download failed:\n\n" + ex.Message, AppInfo.ShortName,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================
        // Layout Toggle
        // ============================

        private void ToggleLayout_Click(object sender, RoutedEventArgs e)
        {
            // Save current layout's splitter positions before switching
            SaveCurrentSplitterPositions();

            // If panel is collapsed, reset the state before switching
            // so we get a clean starting position in the new layout
            bool wasCollapsed = trackSettingsCollapsed;
            if (trackSettingsCollapsed)
            {
                // Reset state without expanding — just clear the flag
                trackSettingsCollapsed = false;
            }

            isHorizontalLayout = !isHorizontalLayout;

			if (isHorizontalLayout)
			{
			    layoutVertical.Visibility = Visibility.Collapsed;
			    layoutHorizontal.Visibility = Visibility.Visible;
			    btnToggleLayout.Content = "\xeca5";
			    btnToggleLayout.FontFamily = new FontFamily("Segoe Fluent Icons");
			    btnToggleLayout.FontSize = 16;
			    btnToggleLayout.ToolTip = "Switch to Vertical Layout";
			}
			else
			{
			    layoutHorizontal.Visibility = Visibility.Collapsed;
			    layoutVertical.Visibility = Visibility.Visible;
			    btnToggleLayout.Content = "\xeca5";
			    btnToggleLayout.FontFamily = new FontFamily("Segoe Fluent Icons");
			    btnToggleLayout.FontSize = 16;
			    btnToggleLayout.ToolTip = "Switch to Horizontal Layout";
			}

            PlaceControlsInLayout();

            // Restore the target layout's splitter positions after a brief delay
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplySplitterPositions();

                // Re-apply collapsed state in the new layout
                if (wasCollapsed)
                {
                    btnToggleTrackSettings.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            statusFile.Text = isHorizontalLayout ? "Layout: Horizontal" : "Layout: Vertical";
        }

        private void SaveCurrentSplitterPositions()
        {
            try
            {
                if (!isHorizontalLayout && layoutVertical.IsVisible)
                {
                    var cols = layoutVertical.ColumnDefinitions;
                    if (cols.Count >= 3 && cols[0].ActualWidth > 0)
                        windowSettings.VSplitter1 = cols[0].ActualWidth;

                    foreach (var child in layoutVertical.Children)
                    {
                        if (child is Grid leftPanel && Grid.GetColumn((System.Windows.UIElement)child) == 0)
                        {
                            if (leftPanel.RowDefinitions.Count >= 3 && leftPanel.RowDefinitions[0].ActualHeight > 0)
                                windowSettings.VSplitter1Row = leftPanel.RowDefinitions[0].ActualHeight;

                            foreach (var sub in leftPanel.Children)
                            {
                                if (sub is Grid topGrid && Grid.GetRow((System.Windows.UIElement)sub) == 0)
                                {
                                    if (topGrid.ColumnDefinitions.Count >= 3 && topGrid.ColumnDefinitions[0].ActualWidth > 0)
                                        windowSettings.VSplitterCat = topGrid.ColumnDefinitions[0].ActualWidth;
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }
                else if (isHorizontalLayout && layoutHorizontal.IsVisible)
                {
                    var rows = layoutHorizontal.RowDefinitions;
                    if (rows.Count >= 3 && rows[0].ActualHeight > 0)
                        windowSettings.HRowTop = rows[0].ActualHeight;

                    foreach (var child in layoutHorizontal.Children)
                    {
                        if (child is Grid g)
                        {
                            int row = Grid.GetRow((System.Windows.UIElement)child);
                            if (row == 0 && g.ColumnDefinitions.Count >= 3 && g.ColumnDefinitions[0].ActualWidth > 0)
                                windowSettings.HTopCol0 = g.ColumnDefinitions[0].ActualWidth;
                            else if (row == 2 && g.ColumnDefinitions.Count >= 3 && g.ColumnDefinitions[0].ActualWidth > 0)
                                windowSettings.HBotCol0 = g.ColumnDefinitions[0].ActualWidth;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void ApplySplitterPositions()
        {
            try
            {
                if (!isHorizontalLayout && layoutVertical.IsVisible)
                {
                    var cols = layoutVertical.ColumnDefinitions;

                    // Main left/right split (column 0)
                    if (!double.IsNaN(windowSettings.VSplitter1) && windowSettings.VSplitter1 > 0)
                        cols[0].Width = new GridLength(windowSettings.VSplitter1);

                    // Inside the left panel: top row has Cat+List
                    Grid leftPanel = layoutVertical.Children[0] as Grid;
                    if (leftPanel == null)
                    {
                        // Find the Grid in column 0
                        foreach (var child in layoutVertical.Children)
                        {
                            if (child is Grid g && Grid.GetColumn((System.Windows.UIElement)child) == 0)
                            {
                                leftPanel = g;
                                break;
                            }
                        }
                    }

                    if (leftPanel != null)
                    {
                        if (!double.IsNaN(windowSettings.VSplitter1Row) && windowSettings.VSplitter1Row >= 150)
                        {
                            // Use star ratios instead of fixed pixel heights
                            // This lets the grid enforce MinHeight properly
                            double totalHeight = leftPanel.ActualHeight;
                            if (totalHeight <= 0) totalHeight = this.ActualHeight - 100;
                            double trackSettingsHeight = totalHeight - windowSettings.VSplitter1Row - 5;

                            // Ensure minimum height for track settings
                            if (trackSettingsHeight < 250) trackSettingsHeight = 250;
                            double topHeight = totalHeight - trackSettingsHeight - 5;
                            if (topHeight < 150) topHeight = 150;

                            // Use star sizing so grid can enforce mins on resize
                            leftPanel.RowDefinitions[0].Height = new GridLength(topHeight, GridUnitType.Star);
                            leftPanel.RowDefinitions[2].Height = new GridLength(trackSettingsHeight, GridUnitType.Star);
                        }

                        // Cat/List splitter
                        foreach (var child in leftPanel.Children)
                        {
                            if (child is Grid topGrid && Grid.GetRow((System.Windows.UIElement)child) == 0)
                            {
                                if (topGrid.ColumnDefinitions.Count >= 3 &&
                                    !double.IsNaN(windowSettings.VSplitterCat) &&
                                    windowSettings.VSplitterCat > 0)
                                {
                                    topGrid.ColumnDefinitions[0].Width = new GridLength(windowSettings.VSplitterCat);
                                }
                                break;
                            }
                        }
                    }
                }
                else if (isHorizontalLayout && layoutHorizontal.IsVisible)
                {
                    // Horizontal layout
                    var rows = layoutHorizontal.RowDefinitions;

                    if (!double.IsNaN(windowSettings.HRowTop) && windowSettings.HRowTop > 0)
                    {
                        double totalHeight = this.ActualHeight - 100;
                        if (totalHeight <= 0) totalHeight = 600;
                        double topHeight = windowSettings.HRowTop;
                        double bottomHeight = totalHeight - topHeight - 5;

                        if (bottomHeight < 250) bottomHeight = 250;
                        topHeight = totalHeight - bottomHeight - 5;
                        if (topHeight < 150) topHeight = 150;

                        // Use star sizing so grid enforces MinHeight on resize
                        rows[0].Height = new GridLength(topHeight, GridUnitType.Star);
                        rows[2].Height = new GridLength(bottomHeight, GridUnitType.Star);
                    }

                    // Top row columns
                    Grid topRow = null;
                    foreach (var child in layoutHorizontal.Children)
                    {
                        if (child is Grid g && Grid.GetRow((System.Windows.UIElement)child) == 0)
                        {
                            topRow = g;
                            break;
                        }
                    }

                    if (topRow != null && topRow.ColumnDefinitions.Count >= 3 &&
                        !double.IsNaN(windowSettings.HTopCol0) && windowSettings.HTopCol0 > 0)
                    {
                        topRow.ColumnDefinitions[0].Width = new GridLength(windowSettings.HTopCol0);
                    }

                    // Bottom row columns
                    Grid botRow = null;
                    foreach (var child in layoutHorizontal.Children)
                    {
                        if (child is Grid g && Grid.GetRow((System.Windows.UIElement)child) == 2)
                        {
                            botRow = g;
                            break;
                        }
                    }

                    if (botRow != null && botRow.ColumnDefinitions.Count >= 3 &&
                        !double.IsNaN(windowSettings.HBotCol0) && windowSettings.HBotCol0 > 0)
                    {
                        botRow.ColumnDefinitions[0].Width = new GridLength(windowSettings.HBotCol0);
                    }
                }
            }
            catch (Exception)
            {
                // Silently ignore layout restore errors
            }
        }

        private void SaveWindowSettings()
        {
            try
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    windowSettings.IsMaximized = true;
                }
                else
                {
                    windowSettings.IsMaximized = false;
                    windowSettings.WindowLeft = this.Left;
                    windowSettings.WindowTop = this.Top;
                    windowSettings.WindowWidth = this.Width;
                    windowSettings.WindowHeight = this.Height;
                }

                windowSettings.IsHorizontalLayout = isHorizontalLayout;
                windowSettings.TrackSettingsCollapsed = trackSettingsCollapsed;

                SaveCurrentSplitterPositions();
                windowSettings.SearchEngine = searchEngineCombo.SelectedItem as string ?? "DuckDuckGo";
                windowSettings.SourceFontSize = sourceView.FontSize;
                SaveColumnSettings();
                windowSettings.EditorPath = editEditorPath.Text.Trim();
                windowSettings.DefaultPublisherName = editDefaultPublisherName.Text.Trim();
                if (editSyMenuPath != null)
                    windowSettings.SpsSuiteRootPath = editSyMenuPath.Text.Trim();
                windowSettings.TabThemeVisible = rightTabs.Items.Contains(themeTabItem);
                windowSettings.TabAppSettingsVisible = rightTabs.Items.Contains(appSettingsTabItem);
                windowSettings.Save(windowSettingsPath);
            }
            catch (Exception)
            {
            }
        }

		// ============================
		// Theme Application
		// ============================

		private TabItem BuildThemeTab()
		{
		    themeTabItem = new TabItem();
		    TabItem themeTab = themeTabItem;
		    themeTab.Header = CreateClosableTabHeader("🎨 Theme", themeTab);

		    DockPanel themeOuterPanel = new DockPanel();

		    // ---- Declare themePanel EARLY so lambda closures can reference it ----
		    StackPanel themePanel = new StackPanel();
		    themePanel.Margin = new Thickness(12);

		    // ---- Toolbar row ----
		    ToolBarTray themeToolBarTray = new ToolBarTray();
		    DockPanel.SetDock(themeToolBarTray, Dock.Top);

		    ToolBar themeToolBar = new ToolBar();
		    themeToolBar.Band = 0;
		    themeToolBar.BandIndex = 0;

		    TextBlock themeToolBarTitle = new TextBlock();
		    themeToolBarTitle.Text = "Theme Settings";
		    themeToolBarTitle.FontWeight = FontWeights.Bold;
		    themeToolBarTitle.FontSize = 13;
		    themeToolBarTitle.VerticalAlignment = VerticalAlignment.Center;
		    themeToolBarTitle.Margin = new Thickness(2, 0, 8, 0);
		    themeToolBar.Items.Add(themeToolBarTitle);

		    themeToolBar.Items.Add(new Separator());

		    ComboBox presetCombo = new ComboBox();
		    presetCombo.Width = 180;
		    // Add built-in presets
		    presetCombo.Items.Add("Default Light");
		    presetCombo.Items.Add("Dark");
		    presetCombo.Items.Add("Blue");
		    // Add custom .thm files from Settings folder
		    PopulateThemeCombo(presetCombo);
		    // Select the combo item matching the actually loaded theme
			string activeThemeName = currentTheme.ThemeName ?? "";
			bool found = false;
			for (int i = 0; i < presetCombo.Items.Count; i++)
			{
			    string itemText = presetCombo.Items[i] as string;
			    if (itemText == null) continue; // skip Separators

			    // Match by theme name for built-ins, or by filename for custom themes
			    if (itemText == activeThemeName)
			    {
			        presetCombo.SelectedIndex = i;
			        found = true;
			        break;
			    }

			    // Also match custom themes by filename (without .thm extension)
			    if (!string.IsNullOrEmpty(windowSettings.ActiveThemePath))
			    {
			        string fileBaseName = Path.GetFileNameWithoutExtension(windowSettings.ActiveThemePath);
			        if (itemText == fileBaseName)
			        {
			            presetCombo.SelectedIndex = i;
			            found = true;
			            break;
			        }
			    }
			}
			if (!found)
			{
			    presetCombo.SelectedIndex = 0;
			}

		    // Instant-apply on selection change (no separate "Apply" button needed)
			// Hook DropDownOpened once to style separator items
			presetCombo.DropDownOpened += (ds, de) =>
			{
			    for (int i = 0; i < presetCombo.Items.Count; i++)
			    {
			        if (presetCombo.Items[i] is Separator)
			        {
			            ComboBoxItem container = presetCombo.ItemContainerGenerator
			                .ContainerFromIndex(i) as ComboBoxItem;
			            if (container != null)
			            {
			                container.IsEnabled = false;
			                container.Padding = new Thickness(0);
			                container.MinHeight = 0;
			                container.Height = 2;
			            }
			        }
			    }
			};

			presetCombo.SelectionChanged += (s, ev) =>
			{
			    // If user clicked a Separator, revert to previous selection
			    if (presetCombo.SelectedItem is Separator || presetCombo.SelectedItem == null)
			    {
			        if (ev.RemovedItems.Count > 0 && ev.RemovedItems[0] is string prev)
			            presetCombo.SelectedItem = prev;
			        else
			            presetCombo.SelectedIndex = 0;
			        return;
			    }

			    string selected = presetCombo.SelectedItem as string;
			    if (string.IsNullOrEmpty(selected)) return;

			    switch (selected)
			    {
			        case "Default Light":
			            previewTheme = ThemeSettings.GetDefaultLight();
			            break;
			        case "Dark":
			            previewTheme = ThemeSettings.GetDarkTheme();
			            break;
			        case "Blue":
			            previewTheme = ThemeSettings.GetBlueTheme();
			            break;
			        default:
			            string themePath = Path.Combine(settingsPath, selected + ".thm");
			            if (File.Exists(themePath))
			            {
			                previewTheme = ThemeSettings.Load(themePath);
			                currentThemePath = themePath;
			                windowSettings.ActiveThemePath = selected + ".thm";
			            }
			            else
			            {
			                previewTheme = ThemeSettings.GetDefaultLight();
			            }
			            break;
			    }
			    ApplyTheme(previewTheme);
			    RefreshThemeSwatches(themePanel);
			    FixClosableTabForegrounds();
			    statusFile.Text = "Theme applied: " + selected;
			};
			
		    themeToolBar.Items.Add(presetCombo);

			Button btnApplyPreset = CreateToolBarButton("\xe930", "Apply preset theme", null);
			themeToolBar.Items.Add(btnApplyPreset);

			themeToolBar.Items.Add(new Separator());

			Button btnLoadTheme = CreateToolBarButton("\xe838", "Browse for theme file", null);
			themeToolBar.Items.Add(btnLoadTheme);

			Button btnSaveTheme = CreateToolBarButton("\uE74E", "Save theme to file", null);
			themeToolBar.Items.Add(btnSaveTheme);

			Button btnSaveThemeAs = CreateToolBarButton("\uE792", "Save theme as new file", null);
			themeToolBar.Items.Add(btnSaveThemeAs);

			themeToolBar.Items.Add(new Separator());

			Button btnResetTheme = CreateToolBarButton("\xe845", "Revert to last saved/loaded theme", null);
			themeToolBar.Items.Add(btnResetTheme);

			Button btnDeleteTheme = CreateToolBarButton("\uE74D", "Delete selected custom theme", null);
			themeToolBar.Items.Add(btnDeleteTheme);

		    themeToolBarTray.ToolBars.Add(themeToolBar);		    themeOuterPanel.Children.Add(themeToolBarTray);

		    // ---- Scrollable colour swatch area ----
		    ScrollViewer themeScroll = new ScrollViewer();
		    themeScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

		    // themePanel was declared at the top of the method

		    // ---- Build colour swatch rows ----
		    AddThemeGroup(themePanel, "Window & General", panel =>
		    {
		        AddColorSwatch(panel, "Window Background", () => previewTheme.WindowBackground, c => previewTheme.WindowBackground = c);
		        AddColorSwatch(panel, "Window Foreground", () => previewTheme.WindowForeground, c => previewTheme.WindowForeground = c);
		        AddColorSwatch(panel, "Button Background", () => previewTheme.ButtonBackground, c => previewTheme.ButtonBackground = c);
		        AddColorSwatch(panel, "Button Foreground", () => previewTheme.ButtonForeground, c => previewTheme.ButtonForeground = c);
		        AddColorSwatch(panel, "Splitter Colour", () => previewTheme.SplitterColor, c => previewTheme.SplitterColor = c);
		    });

		    AddThemeGroup(themePanel, "Menu & Toolbar", panel =>
		    {
		        AddColorSwatch(panel, "Menu Background", () => previewTheme.MenuBackground, c => previewTheme.MenuBackground = c);
		        AddColorSwatch(panel, "Menu Foreground", () => previewTheme.MenuForeground, c => previewTheme.MenuForeground = c);
		        AddColorSwatch(panel, "Main Toolbar Background", () => previewTheme.MainToolbarBackground, c => previewTheme.MainToolbarBackground = c);
		        AddColorSwatch(panel, "Main Toolbar Foreground", () => previewTheme.MainToolbarForeground, c => previewTheme.MainToolbarForeground = c);
		        AddColorSwatch(panel, "Toolbar Background", () => previewTheme.ToolbarBackground, c => previewTheme.ToolbarBackground = c);
		        AddColorSwatch(panel, "Toolbar Foreground", () => previewTheme.ToolbarForeground, c => previewTheme.ToolbarForeground = c);
		    });

		    AddThemeGroup(themePanel, "Status Bar", panel =>
		    {
		        AddColorSwatch(panel, "StatusBar Background", () => previewTheme.StatusBarBackground, c => previewTheme.StatusBarBackground = c);
		        AddColorSwatch(panel, "StatusBar Foreground", () => previewTheme.StatusBarForeground, c => previewTheme.StatusBarForeground = c);
		        AddColorSwatch(panel, "ProgressBar Background", () => previewTheme.ProgressBarBackground, c => previewTheme.ProgressBarBackground = c);
		        AddColorSwatch(panel, "ProgressBar Foreground", () => previewTheme.ProgressBarForeground, c => previewTheme.ProgressBarForeground = c);
		    });

		    AddThemeGroup(themePanel, "Category Tree", panel =>
		    {
		        AddColorSwatch(panel, "Tree Background", () => previewTheme.TreeBackground, c => previewTheme.TreeBackground = c);
		        AddColorSwatch(panel, "Tree Foreground", () => previewTheme.TreeForeground, c => previewTheme.TreeForeground = c);
		        AddColorSwatch(panel, "Tree Selected Background", () => previewTheme.TreeSelectedBackground, c => previewTheme.TreeSelectedBackground = c);
		        AddColorSwatch(panel, "Tree Selected Foreground", () => previewTheme.TreeSelectedForeground, c => previewTheme.TreeSelectedForeground = c);
				AddColorSwatch(panel, "Tree Hover Background", () => previewTheme.TreeHoverBackground, c => previewTheme.TreeHoverBackground = c);
				AddColorSwatch(panel, "Tree Hover Foreground", () => previewTheme.TreeHoverForeground, c => previewTheme.TreeHoverForeground = c);
		    });

		    AddThemeGroup(themePanel, "ListView", panel =>
		    {
		        AddColorSwatch(panel, "List Background", () => previewTheme.ListBackground, c => previewTheme.ListBackground = c);
		        AddColorSwatch(panel, "List: Name/Version Columns", () => previewTheme.ListForeground, c => previewTheme.ListForeground = c);
		        AddColorSwatch(panel, "List Header Background", () => previewTheme.ListHeaderBackground, c => previewTheme.ListHeaderBackground = c);
		        AddColorSwatch(panel, "List Header Foreground", () => previewTheme.ListHeaderForeground, c => previewTheme.ListHeaderForeground = c);
		        AddColorSwatch(panel, "List Selected Background", () => previewTheme.ListSelectedBackground, c => previewTheme.ListSelectedBackground = c);
		        AddColorSwatch(panel, "List Selected Foreground", () => previewTheme.ListSelectedForeground, c => previewTheme.ListSelectedForeground = c);
		        AddColorSwatch(panel, "List Hover Background", () => previewTheme.ListHoverBackground, c => previewTheme.ListHoverBackground = c);
		        AddColorSwatch(panel, "List Hover Foreground", () => previewTheme.ListHoverForeground, c => previewTheme.ListHoverForeground = c);
		        AddColorSwatch(panel, "Header Hover Background", () => previewTheme.ListHeaderHoverBackground, c => previewTheme.ListHeaderHoverBackground = c);
		        AddColorSwatch(panel, "Header Hover Foreground", () => previewTheme.ListHeaderHoverForeground, c => previewTheme.ListHeaderHoverForeground = c);
		    });

		    AddThemeGroup(themePanel, "ComboBox", panel =>
		    {
		        AddColorSwatch(panel, "Background", () => previewTheme.ComboBoxBackground, c => previewTheme.ComboBoxBackground = c);
		        AddColorSwatch(panel, "Foreground", () => previewTheme.ComboBoxForeground, c => previewTheme.ComboBoxForeground = c);
		        AddColorSwatch(panel, "Border", () => previewTheme.ComboBoxBorder, c => previewTheme.ComboBoxBorder = c);
		        AddColorSwatch(panel, "Button Background", () => previewTheme.ComboBoxButtonBackground, c => previewTheme.ComboBoxButtonBackground = c);
		        AddColorSwatch(panel, "Button Arrow", () => previewTheme.ComboBoxButtonForeground, c => previewTheme.ComboBoxButtonForeground = c);
		    });

		    AddThemeGroup(themePanel, "ListView Checkboxes", panel =>
		    {
		        AddColorSwatch(panel, "Box Background", () => previewTheme.CheckBoxBackground, c => previewTheme.CheckBoxBackground = c);
		        AddColorSwatch(panel, "Box Border", () => previewTheme.CheckBoxBorder, c => previewTheme.CheckBoxBorder = c);
		        AddColorSwatch(panel, "Check Mark", () => previewTheme.CheckBoxCheckMark, c => previewTheme.CheckBoxCheckMark = c);
		        AddColorSwatch(panel, "Hover Background", () => previewTheme.CheckBoxHoverBackground, c => previewTheme.CheckBoxHoverBackground = c);
		    });

		    AddThemeGroup(themePanel, "List Status Indicators (● icons)", panel =>
		    {
		        AddColorSwatch(panel, "Unchanged", () => previewTheme.StatusUnchanged, c => previewTheme.StatusUnchanged = c);
		        AddColorSwatch(panel, "Changed", () => previewTheme.StatusChanged, c => previewTheme.StatusChanged = c);
		        AddColorSwatch(panel, "Error", () => previewTheme.StatusError, c => previewTheme.StatusError = c);
		        AddColorSwatch(panel, "New", () => previewTheme.StatusNew, c => previewTheme.StatusNew = c);
		        AddColorSwatch(panel, "Unchecked", () => previewTheme.StatusUnchecked, c => previewTheme.StatusUnchecked = c);
		    });

		    AddThemeGroup(themePanel, "List Cell Status Colours (text colour for URL, Hash, etc.)", panel =>
		    {
		        AddColorSwatch(panel, "OK", () => previewTheme.CellStatusOk, c => previewTheme.CellStatusOk = c);
		        AddColorSwatch(panel, "Changed", () => previewTheme.CellStatusChanged, c => previewTheme.CellStatusChanged = c);
		        AddColorSwatch(panel, "Error", () => previewTheme.CellStatusError, c => previewTheme.CellStatusError = c);
		        AddColorSwatch(panel, "Default", () => previewTheme.CellStatusDefault, c => previewTheme.CellStatusDefault = c);
		    });

		    AddThemeGroup(themePanel, "Track Settings Panel", panel =>
		    {
		        AddColorSwatch(panel, "Panel Background", () => previewTheme.PanelBackground, c => previewTheme.PanelBackground = c);
		        AddColorSwatch(panel, "Panel Foreground", () => previewTheme.PanelForeground, c => previewTheme.PanelForeground = c);
		        AddColorSwatch(panel, "TextBox Background", () => previewTheme.TextBoxBackground, c => previewTheme.TextBoxBackground = c);
		        AddColorSwatch(panel, "TextBox Foreground", () => previewTheme.TextBoxForeground, c => previewTheme.TextBoxForeground = c);
		        AddColorSwatch(panel, "Version Match / Found", () => previewTheme.VersionMatchColor, c => previewTheme.VersionMatchColor = c);
		        AddColorSwatch(panel, "Version Mismatch / Not Found", () => previewTheme.VersionMismatchColor, c => previewTheme.VersionMismatchColor = c);
		        AddColorSwatch(panel, "Track Toolbar Background", () => previewTheme.TrackToolbarBackground, c => previewTheme.TrackToolbarBackground = c);
		        AddColorSwatch(panel, "Track Toolbar Foreground", () => previewTheme.TrackToolbarForeground, c => previewTheme.TrackToolbarForeground = c);
		    });

		    AddThemeGroup(themePanel, "Tabs", panel =>
		    {
		        AddColorSwatch(panel, "Tab Bar Background", () => previewTheme.TabBackground, c => previewTheme.TabBackground = c);
		        AddColorSwatch(panel, "Tab Handle Background", () => previewTheme.TabHandleBackground, c => previewTheme.TabHandleBackground = c);
		        AddColorSwatch(panel, "Tab Handle Foreground", () => previewTheme.TabHandleForeground, c => previewTheme.TabHandleForeground = c);
		        AddColorSwatch(panel, "Tab Selected Background", () => previewTheme.TabSelectedBackground, c => previewTheme.TabSelectedBackground = c);
		        AddColorSwatch(panel, "Tab Selected Foreground", () => previewTheme.TabSelectedForeground, c => previewTheme.TabSelectedForeground = c);
		        AddColorSwatch(panel, "Tab Content Foreground", () => previewTheme.TabContentForeground, c => previewTheme.TabContentForeground = c);
		    });

		    AddThemeGroup(themePanel, "Source View", panel =>
		    {
	            AddColorSwatch(panel, "JS Banner Background", () => previewTheme.SourceBannerBackground, c => previewTheme.SourceBannerBackground = c);
	            AddColorSwatch(panel, "JS Banner Foreground", () => previewTheme.SourceBannerForeground, c => previewTheme.SourceBannerForeground = c);
		        AddColorSwatch(panel, "Source Background", () => previewTheme.SourceBackground, c => previewTheme.SourceBackground = c);
		        AddColorSwatch(panel, "Start String", () => previewTheme.SourceStartStringColor, c => previewTheme.SourceStartStringColor = c);
		        AddColorSwatch(panel, "Info String", () => previewTheme.SourceInfoStringColor, c => previewTheme.SourceInfoStringColor = c);
		        AddColorSwatch(panel, "Stop String", () => previewTheme.SourceStopStringColor, c => previewTheme.SourceStopStringColor = c);
		        AddColorSwatch(panel, "HTML Tags", () => previewTheme.SourceTagColor, c => previewTheme.SourceTagColor = c);
		        AddColorSwatch(panel, "Text Content", () => previewTheme.SourceTextColor, c => previewTheme.SourceTextColor = c);
		    });

		    AddThemeGroup(themePanel, "Scrollbars", panel =>
		    {
		        AddColorSwatch(panel, "Scrollbar Background", () => previewTheme.ScrollBarBackground, c => previewTheme.ScrollBarBackground = c);
		        AddColorSwatch(panel, "Scrollbar Thumb", () => previewTheme.ScrollBarThumb, c => previewTheme.ScrollBarThumb = c);
		    });

		    // ---- Button handlers ----

			btnApplyPreset.Click += (s, ev) =>
			{
			    // Skip if a Separator is somehow selected
			    if (presetCombo.SelectedItem is Separator || presetCombo.SelectedItem == null)
			        return;

			    string selected = presetCombo.SelectedItem as string;
			    if (string.IsNullOrEmpty(selected))
			        return;

			    switch (selected)
			    {
			        case "Default Light":
			            previewTheme = ThemeSettings.GetDefaultLight();
			            break;
			        case "Dark":
			            previewTheme = ThemeSettings.GetDarkTheme();
			            break;
			        case "Blue":
			            previewTheme = ThemeSettings.GetBlueTheme();
			            break;
			        default:
			            // User theme from Settings folder
			            string themePath = Path.Combine(settingsPath, selected + ".thm");
			            if (File.Exists(themePath))
			            {
			                previewTheme = ThemeSettings.Load(themePath);
			                currentThemePath = themePath;
			                windowSettings.ActiveThemePath = selected + ".thm";
			            }
			            else
			            {
			                previewTheme = ThemeSettings.GetDefaultLight();
			            }
			            break;
			    }
			    ApplyTheme(previewTheme);
			    RefreshThemeSwatches(themePanel);
			    FixClosableTabForegrounds();
			    statusFile.Text = "Theme applied: " + selected;
			};

		    btnLoadTheme.Click += (s, ev) =>
		    {
		        Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
		        dlg.Title = "Load Theme";
		        dlg.Filter = "Theme Files (*.thm)|*.thm|XML Files (*.xml)|*.xml|All Files (*.*)|*.*";
		        dlg.InitialDirectory = settingsPath;

		        if (dlg.ShowDialog() == true)
		        {
		            ThemeSettings loaded = ThemeSettings.Load(dlg.FileName);
		            previewTheme = loaded;
		            currentThemePath = dlg.FileName;
		            ApplyTheme(previewTheme);
		            RefreshThemeSwatches(themePanel);
		            FixClosableTabForegrounds();

		            string relativePath = Path.GetFileName(dlg.FileName);
		            windowSettings.ActiveThemePath = relativePath;

		            // Refresh the combo to include the newly loaded file if it's in Settings
		            int savedTabIndex = rightTabs.SelectedIndex;
					PopulateThemeCombo(presetCombo);
					rightTabs.SelectedIndex = savedTabIndex;
					
		            statusFile.Text = "Theme loaded: " + loaded.ThemeName;
		        }
		    };

			btnSaveTheme.Click += (s, ev) =>
			{
			    // If we have a current theme file path, save silently
			    if (!string.IsNullOrEmpty(currentThemePath) && File.Exists(currentThemePath))
			    {
			        previewTheme.Save(currentThemePath);
			        currentTheme = previewTheme.Clone();
			        FixClosableTabForegrounds();
			        statusFile.Text = "Theme saved: " + Path.GetFileNameWithoutExtension(currentThemePath);
			        return;
			    }

			    // No current file — check if a custom theme is selected in the combo
			    string selected = presetCombo.SelectedItem as string;
			    if (!string.IsNullOrEmpty(selected) &&
			        selected != "Default Light" && selected != "Dark" && selected != "Blue")
			    {
			        string savePath = Path.Combine(settingsPath, selected + ".thm");
			        previewTheme.ThemeName = selected;
			        previewTheme.Save(savePath);
			        currentThemePath = savePath;
			        currentTheme = previewTheme.Clone();
			        FixClosableTabForegrounds();
			        windowSettings.ActiveThemePath = selected + ".thm";
			        statusFile.Text = "Theme saved: " + selected;
			        return;
			    }

			    // Built-in theme selected or no file — fall back to Save As behavior
			    // (Can't overwrite built-in presets, so prompt for a name)
			    string themeName = ShowInputDialog("Save Theme As", "Enter a name for this theme:");
			    if (string.IsNullOrWhiteSpace(themeName)) return;

			    string safeName = themeName;
			    foreach (char c in Path.GetInvalidFileNameChars())
			        safeName = safeName.Replace(c.ToString(), "");

			    if (string.IsNullOrWhiteSpace(safeName))
			    {
			        MessageBox.Show("Invalid theme name.", AppInfo.ShortName,
			            MessageBoxButton.OK, MessageBoxImage.Warning);
			        return;
			    }

			    previewTheme.ThemeName = themeName;
			    string newPath = Path.Combine(settingsPath, safeName + ".thm");
			    previewTheme.Save(newPath);
			    currentThemePath = newPath;
			    currentTheme = previewTheme.Clone();
			    FixClosableTabForegrounds();
			    windowSettings.ActiveThemePath = safeName + ".thm";
			    int savedTabIndex = rightTabs.SelectedIndex;
				PopulateThemeCombo(presetCombo);
				rightTabs.SelectedIndex = savedTabIndex;
			    statusFile.Text = "Theme saved: " + themeName;
			};

			btnSaveThemeAs.Click += (s, ev) =>
			{
			    string themeName = ShowInputDialog("Save Theme As", "Enter a name for this theme:");
			    if (string.IsNullOrWhiteSpace(themeName))
			        return;

			    string safeName = themeName;
			    foreach (char c in Path.GetInvalidFileNameChars())
			        safeName = safeName.Replace(c.ToString(), "");

			    if (string.IsNullOrWhiteSpace(safeName))
			    {
			        MessageBox.Show("Invalid theme name.", AppInfo.ShortName,
			            MessageBoxButton.OK, MessageBoxImage.Warning);
			        return;
			    }

			    previewTheme.ThemeName = themeName;
			    string savePath = Path.Combine(settingsPath, safeName + ".thm");

			    if (File.Exists(savePath))
			    {
			        MessageBoxResult overwrite = MessageBox.Show(
			            "Theme file '" + safeName + ".thm' already exists.\nOverwrite?",
			            AppInfo.ShortName, MessageBoxButton.YesNo, MessageBoxImage.Question);
			        if (overwrite != MessageBoxResult.Yes)
			            return;
			    }

			    previewTheme.Save(savePath);
			    currentThemePath = savePath;
			    currentTheme = previewTheme.Clone();
			    FixClosableTabForegrounds();
			    windowSettings.ActiveThemePath = safeName + ".thm";
			    int savedTabIndex = rightTabs.SelectedIndex;
				PopulateThemeCombo(presetCombo);
				rightTabs.SelectedIndex = savedTabIndex;
			    statusFile.Text = "Theme saved as: " + themeName;
			};

		    btnResetTheme.Click += (s, ev) =>
		    {
		        if (!string.IsNullOrEmpty(currentThemePath) && File.Exists(currentThemePath))
		        {
		            previewTheme = ThemeSettings.Load(currentThemePath);
		        }
		        else
		        {
		            previewTheme = currentTheme.Clone();
		        }
		        ApplyTheme(previewTheme);
		        RefreshThemeSwatches(themePanel);
		        FixClosableTabForegrounds();
		        statusFile.Text = "Theme reset to saved state";
		    };

		    btnDeleteTheme.Click += (s, ev) =>
		    {
		        string selected = presetCombo.SelectedItem as string;
		        if (string.IsNullOrEmpty(selected))
		            return;

		        // Can't delete built-in themes
		        if (selected == "Default Light" || selected == "Dark" || selected == "Blue")
		        {
		            MessageBox.Show("Built-in themes cannot be deleted.",
		                AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Information);
		            return;
		        }

		        string filePath = Path.Combine(settingsPath, selected + ".thm");
		        if (!File.Exists(filePath))
		        {
		            MessageBox.Show("Theme file not found:\n" + filePath,
		                AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
		            return;
		        }

		        MessageBoxResult confirm = MessageBox.Show(
		            "Delete theme '" + selected + "'?\n\nThis will permanently delete the file:\n" + filePath,
		            AppInfo.ShortName, MessageBoxButton.YesNo, MessageBoxImage.Question);
		        if (confirm != MessageBoxResult.Yes)
		            return;

		        try
		        {
		            File.Delete(filePath);

		            // If the deleted theme was the active one, revert to Default Light
		            if (currentThemePath == filePath)
		            {
		                currentThemePath = "";
		                windowSettings.ActiveThemePath = "";
		                previewTheme = ThemeSettings.GetDefaultLight();
		                currentTheme = previewTheme.Clone();
		                ApplyTheme(previewTheme);
		                RefreshThemeSwatches(themePanel);
		                FixClosableTabForegrounds();
		            }

		            // Refresh combo and select Default Light
		            int savedTabIndex = rightTabs.SelectedIndex;
		            PopulateThemeCombo(presetCombo);
		            rightTabs.SelectedIndex = savedTabIndex;

		            statusFile.Text = "Theme deleted: " + selected;
		        }
		        catch (Exception ex)
		        {
		            MessageBox.Show("Failed to delete theme:\n" + ex.Message,
		                AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
		        }
		    };

		    themeScroll.Content = themePanel;
		    themeOuterPanel.Children.Add(themeScroll);
		    themeTab.Content = themeOuterPanel;

		    // Initialize preview theme
		    previewTheme = currentTheme.Clone();

		    ApplyThemeToGroupHeaders(currentTheme);
		    return themeTab;
		}

		private void PopulateThemeCombo(ComboBox combo)
		{
		    string currentSelection = combo.SelectedItem as string;

		    combo.Items.Clear();
		    combo.Items.Add("Default Light");
		    combo.Items.Add("Dark");
		    combo.Items.Add("Blue");

		    // Add custom .thm files from the Settings folder
		    if (Directory.Exists(settingsPath))
		    {
		        string[] themeFiles = Directory.GetFiles(settingsPath, "*.thm");
		        bool separatorAdded = false;

		        foreach (string file in themeFiles)
		        {
		            string name = Path.GetFileNameWithoutExtension(file);
		            if (name != "Default Light" && name != "Dark" && name != "Blue")
		            {
		                // Add separator before the first custom theme
		                if (!separatorAdded)
		                {
		                    combo.Items.Add(new Separator());
		                    separatorAdded = true;
		                }
		                combo.Items.Add(name);
		            }
		        }
		    }

		    if (!string.IsNullOrEmpty(currentSelection) && combo.Items.Contains(currentSelection))
		    {
		        combo.SelectedItem = currentSelection;
		    }
		    else
		    {
		        combo.SelectedIndex = 0;
		    }
		}

		private void ThemeAllComboBoxArrows(DependencyObject parent, SolidColorBrush arrowBrush)
		{
		    int count = VisualTreeHelper.GetChildrenCount(parent);
		    for (int i = 0; i < count; i++)
		    {
		        DependencyObject child = VisualTreeHelper.GetChild(parent, i);

		        if (child is System.Windows.Shapes.Path path &&
		            path.Name == "Arrow")
		        {
		            path.Fill = arrowBrush;
		        }

		        ThemeAllComboBoxArrows(child, arrowBrush);
		    }
		}

		private void AddThemeGroup(StackPanel parent, string title, Action<StackPanel> buildSwatches)
		{
		    Border border = new Border();
		    border.BorderBrush = Brushes.Gray;
		    border.BorderThickness = new Thickness(1);
		    border.CornerRadius = new CornerRadius(4);
		    border.Padding = new Thickness(10);
		    border.Margin = new Thickness(0, 0, 0, 8);
		    border.Tag = "ThemeGroup";

		    StackPanel groupPanel = new StackPanel();

		    TextBlock header = new TextBlock();
		    header.Text = title;
		    header.FontWeight = FontWeights.Bold;
		    header.FontSize = 12;
		    header.Margin = new Thickness(0, 0, 0, 6);
		    header.Tag = "ThemeGroupHeader";
		    header.Foreground = new SolidColorBrush(currentTheme.TabSelectedForeground);
		    groupPanel.Children.Add(header);

		    buildSwatches(groupPanel);

		    border.Child = groupPanel;
		    parent.Children.Add(border);
		}

		private void ApplyThemeToGroupHeaders(ThemeSettings theme)
		{
		    SolidColorBrush foreground = new SolidColorBrush(theme.TabSelectedForeground);
		    SolidColorBrush borderBrush = new SolidColorBrush(theme.SplitterColor);

		    // Scan all tabs that have a ScrollViewer with bordered groups
		    foreach (TabItem ti in rightTabs.Items)
		    {
		        DockPanel dp = ti.Content as DockPanel;
		        if (dp == null) continue;

		        foreach (object child in dp.Children)
		        {
		            ScrollViewer sv = child as ScrollViewer;
		            if (sv == null) continue;

		            StackPanel sp = sv.Content as StackPanel;
		            if (sp == null) continue;

		            ApplyGroupHeadersToPanel(sp, foreground, borderBrush);
		        }
		    }
		}

		private void ApplyGroupHeadersToPanel(Panel parent, SolidColorBrush foreground, SolidColorBrush borderBrush)
		{
		    foreach (object child in parent.Children)
		    {
		        Border border = child as Border;
		        if (border == null) continue;

		        border.BorderBrush = borderBrush;

		        StackPanel inner = border.Child as StackPanel;
		        if (inner == null) continue;

		        foreach (object item in inner.Children)
		        {
		            TextBlock tb = item as TextBlock;
		            if (tb != null && tb.Tag as string == "ThemeGroupHeader")
		            {
		                tb.Foreground = foreground;
		            }
		        }
		    }
		}

		private void AddColorSwatch(StackPanel parent, string label,
		    Func<Color> getter, Action<Color> setter)
		{
		    DockPanel row = new DockPanel();
		    row.Margin = new Thickness(0, 2, 0, 2);

		    // Clickable color swatch (acts as the pick button)
		    Border swatch = new Border();
		    swatch.Width = 28;
		    swatch.Height = 20;
		    swatch.BorderBrush = Brushes.Gray;
		    swatch.BorderThickness = new Thickness(1);
		    swatch.CornerRadius = new CornerRadius(3);
		    swatch.Background = new SolidColorBrush(getter());
		    swatch.Cursor = System.Windows.Input.Cursors.Hand;
		    swatch.ToolTip = "Click to pick a colour — Right-click to copy/paste";
		    swatch.Margin = new Thickness(0, 0, 8, 0);
		    DockPanel.SetDock(swatch, Dock.Left);

		    // Hex text display
		    TextBlock hexText = new TextBlock();
		    hexText.Text = ThemeSettings.ColorToHex(getter());
		    hexText.VerticalAlignment = VerticalAlignment.Center;
		    hexText.FontFamily = new FontFamily("Consolas");
		    hexText.FontSize = 11;
		    hexText.Foreground = Brushes.Gray;
		    hexText.Width = 60;
		    hexText.Margin = new Thickness(0, 0, 8, 0);
		    DockPanel.SetDock(hexText, Dock.Right);

		    // Label
		    TextBlock lbl = new TextBlock();
		    lbl.Text = label;
		    lbl.VerticalAlignment = VerticalAlignment.Center;

		    // Click handler — opens Windows colour dialog
		    swatch.MouseLeftButtonDown += (s, ev) =>
		    {
		        Color currentColor = getter();

		        System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog();
		        colorDialog.Color = System.Drawing.Color.FromArgb(currentColor.R, currentColor.G, currentColor.B);
		        colorDialog.FullOpen = true;
		        colorDialog.AnyColor = true;

		        if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		        {
		            Color newColor = Color.FromRgb(
		                colorDialog.Color.R,
		                colorDialog.Color.G,
		                colorDialog.Color.B);

		            setter(newColor);
		            swatch.Background = new SolidColorBrush(newColor);
		            hexText.Text = ThemeSettings.ColorToHex(newColor);

		            // Live preview
		            ApplyTheme(previewTheme);
		        }
		    };

		    // Right-click context menu — Copy / Paste colour
		    ContextMenu swatchMenu = new ContextMenu();

		    MenuItem menuCopyColor = new MenuItem();
		    menuCopyColor.Header = "Copy Colour";
		    menuCopyColor.Click += (s, ev) =>
		    {
		        Clipboard.SetText(ThemeSettings.ColorToHex(getter()));
		    };
		    swatchMenu.Items.Add(menuCopyColor);

		    MenuItem menuPasteColor = new MenuItem();
		    menuPasteColor.Header = "Paste Colour";
		    menuPasteColor.Click += (s, ev) =>
		    {
		        string clipText = Clipboard.GetText().Trim();
		        Color? parsed = ThemeSettings.HexToColor(clipText);
		        if (parsed.HasValue)
		        {
		            setter(parsed.Value);
		            swatch.Background = new SolidColorBrush(parsed.Value);
		            hexText.Text = ThemeSettings.ColorToHex(parsed.Value);
		            ApplyTheme(previewTheme);
		        }
		        else
		        {
		            MessageBox.Show("Clipboard does not contain a valid hex colour.\n\n" +
		                "Expected format: #RRGGBB (e.g. #FF8000)",
		                AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Information);
		        }
		    };
		    swatchMenu.Items.Add(menuPasteColor);

		    swatch.ContextMenu = swatchMenu;

		    row.Children.Add(swatch);
		    row.Children.Add(hexText);
		    row.Children.Add(lbl);
		    parent.Children.Add(row);

		    // Tag the swatch with getter/setter for refresh
		    swatch.Tag = new Tuple<Func<Color>, Action<Color>>(getter, setter);
		}

		private void RefreshThemeSwatches(StackPanel themePanel)
		{
		    RefreshSwatchesRecursive(themePanel);
		}

		private void RefreshSwatchesRecursive(object element)
		{
		    if (element is DockPanel dp)
		    {
		        foreach (var child in dp.Children)
		        {
		            if (child is Border swatch && swatch.Tag is Tuple<Func<Color>, Action<Color>> tag)
		            {
		                Color c = tag.Item1();
		                swatch.Background = new SolidColorBrush(c);

		                // Find the hex text in the same DockPanel
		                foreach (var sibling in dp.Children)
		                {
		                    if (sibling is TextBlock tb && tb.FontFamily != null &&
		                        tb.FontFamily.Source == "Consolas")
		                    {
		                        tb.Text = ThemeSettings.ColorToHex(c);
		                        break;
		                    }
		                }
		            }
		        }
		    }
		    else if (element is Border border && border.Child != null)
		    {
		        RefreshSwatchesRecursive(border.Child);
		    }
		    else if (element is StackPanel sp)
		    {
		        foreach (var child in sp.Children)
		            RefreshSwatchesRecursive(child);
		    }
		}

		// ============================
		// Theme Application
		// ============================

		private void ApplyTheme(ThemeSettings theme)
		{
			this.Resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(theme.MenuBackground);
			this.Resources[SystemColors.ActiveBorderBrushKey] = new SolidColorBrush(theme.MenuBackground);
		    currentTheme = theme;
		    StatusToColorConverter.UpdateFromTheme(theme);

		    SolidColorBrush winBg = new SolidColorBrush(theme.WindowBackground);
		    SolidColorBrush winFg = new SolidColorBrush(theme.WindowForeground);

		    // Window background
		    this.Background = winBg;
		    mainContent.Background = winBg;

		    // Layout grids and border hosts
		    layoutVertical.Background = winBg;
		    layoutHorizontal.Background = winBg;
		    catHostV.Background = winBg;
		    catHostH.Background = winBg;
		    listHostV.Background = winBg;
		    listHostH.Background = winBg;
		    trackSettingsHostV.Background = winBg;
		    trackSettingsHostH.Background = winBg;
		    tabsHostV.Background = winBg;
		    tabsHostH.Background = winBg;

			// ---- Menu (bar + dropdowns) ----
			SolidColorBrush menuBg = new SolidColorBrush(theme.MenuBackground);
			SolidColorBrush menuFg = new SolidColorBrush(theme.MenuForeground);
			foreach (var child in ((DockPanel)this.Content).Children)
			{
			    if (child is Menu menu)
			    {
			        menu.Background = menuBg;
			        menu.Foreground = menuFg;

			        foreach (var item in menu.Items)
			        {
			            if (item is MenuItem mi)
			                ApplyThemeToMenuItem(mi, menuBg, menuFg, true);
			        }
			    }
			}
			
			// ---- Main Toolbar (separate theme) ----
			SolidColorBrush mainTbBg = new SolidColorBrush(theme.MainToolbarBackground);
			SolidColorBrush mainTbFg = new SolidColorBrush(theme.MainToolbarForeground);
			ApplyThemeToToolBarTray(toolBarTray, mainTbBg, mainTbFg);

		    // ---- Status Bar ----
		    SolidColorBrush sbBg = new SolidColorBrush(theme.StatusBarBackground);
		    SolidColorBrush sbFg = new SolidColorBrush(theme.StatusBarForeground);
		    foreach (var child in ((DockPanel)this.Content).Children)
		    {
		        if (child is StatusBar sb)
		        {
		            sb.Background = sbBg;
		            sb.Foreground = sbFg;
		            statusFile.Foreground = sbFg;
		            ApplyStatusBarLegendColors(sb, theme);
		        }
		    }

			// ---- Progress Bar ----
			statusProgress.Background = new SolidColorBrush(theme.ProgressBarBackground);
			statusProgress.Foreground = new SolidColorBrush(theme.ProgressBarForeground);
			statusProgress.BorderBrush = new SolidColorBrush(theme.ProgressBarBackground);

			// Reset to default template so Foreground/Background are respected
			statusProgress.ClearValue(ProgressBar.StyleProperty);

		    // ---- Splitters ----
		    SolidColorBrush splitterBrush = new SolidColorBrush(theme.SplitterColor);
		    ApplyThemeToElement<GridSplitter>(layoutVertical, gs => gs.Background = splitterBrush);
		    ApplyThemeToElement<GridSplitter>(layoutHorizontal, gs => gs.Background = splitterBrush);
		    // Also get nested grids
		    foreach (var child in layoutVertical.Children)
		    {
		        if (child is Grid g)
		        {
		            ApplyThemeToElement<GridSplitter>(g, gs => gs.Background = splitterBrush);
		            foreach (var sub in g.Children)
		            {
		                if (sub is Grid sg)
		                    ApplyThemeToElement<GridSplitter>(sg, gs => gs.Background = splitterBrush);
		            }
		        }
		    }
		    foreach (var child in layoutHorizontal.Children)
		    {
		        if (child is Grid g)
		        {
		            ApplyThemeToElement<GridSplitter>(g, gs => gs.Background = splitterBrush);
		        }
		    }

			// ---- Category Tree ----
			categoryTree.Background = new SolidColorBrush(theme.TreeBackground);
			categoryTree.Foreground = new SolidColorBrush(theme.TreeForeground);
			categoryPanel.Background = new SolidColorBrush(theme.WindowBackground);

			// Also theme the category buttons at the bottom
			foreach (var child in categoryPanel.Children)
			{
			    if (child is StackPanel sp)
			    {
			        foreach (var btn in sp.Children)
			        {
			            if (btn is Button b)
			            {
			                // Buttons will pick up the global button style
			            }
			        }
			    }
			}

			// Override system colour resources so the default template uses our colours
			this.Resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(theme.TreeSelectedBackground);
			this.Resources[SystemColors.HighlightTextBrushKey] = new SolidColorBrush(theme.TreeSelectedForeground);
			this.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = new SolidColorBrush(theme.TreeSelectedBackground);
			this.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = new SolidColorBrush(theme.TreeSelectedForeground);

			Style treeItemStyle = new Style(typeof(TreeViewItem));
			treeItemStyle.Setters.Add(new Setter(TreeViewItem.ForegroundProperty, new SolidColorBrush(theme.TreeForeground)));
			treeItemStyle.Setters.Add(new Setter(TreeViewItem.BackgroundProperty, Brushes.Transparent));

			// Custom template so selection colours actually work
			ControlTemplate treeItemTemplate = new ControlTemplate(typeof(TreeViewItem));

			// Outer StackPanel: header row + items host
			FrameworkElementFactory outerStack = new FrameworkElementFactory(typeof(StackPanel));

			// The header row: border that binds to the TreeViewItem's Background
			FrameworkElementFactory headerBorder = new FrameworkElementFactory(typeof(Border));
			headerBorder.SetBinding(Border.BackgroundProperty,
			    new System.Windows.Data.Binding("Background")
			    {
			        RelativeSource = new System.Windows.Data.RelativeSource(
			            System.Windows.Data.RelativeSourceMode.TemplatedParent)
			    });
			headerBorder.SetValue(Border.PaddingProperty, new Thickness(2, 1, 2, 1));

			FrameworkElementFactory headerPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
			headerPresenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
			headerPresenter.SetBinding(ContentPresenter.ContentProperty,
			    new System.Windows.Data.Binding("Header")
			    {
			        RelativeSource = new System.Windows.Data.RelativeSource(
			            System.Windows.Data.RelativeSourceMode.TemplatedParent)
			    });
			headerBorder.AppendChild(headerPresenter);
			outerStack.AppendChild(headerBorder);

			// Items host for child items — bind Visibility to IsExpanded
			FrameworkElementFactory itemsHost = new FrameworkElementFactory(typeof(ItemsPresenter));
			itemsHost.SetValue(FrameworkElement.MarginProperty, new Thickness(16, 0, 0, 0));
			itemsHost.SetBinding(UIElement.VisibilityProperty,
			    new System.Windows.Data.Binding("IsExpanded")
			    {
			        RelativeSource = new System.Windows.Data.RelativeSource(
			            System.Windows.Data.RelativeSourceMode.TemplatedParent),
			        Converter = new System.Windows.Controls.BooleanToVisibilityConverter()
			    });
			outerStack.AppendChild(itemsHost);

			treeItemTemplate.VisualTree = outerStack;

			// Trigger: IsSelected → set Background + Foreground on the TreeViewItem itself
			// The Border binds to Background via TemplatedParent, so it picks it up automatically
			Trigger treeSelTrigger = new Trigger();
			treeSelTrigger.Property = TreeViewItem.IsSelectedProperty;
			treeSelTrigger.Value = true;
			treeSelTrigger.Setters.Add(new Setter(TreeViewItem.BackgroundProperty,
			    new SolidColorBrush(theme.TreeSelectedBackground)));
			treeSelTrigger.Setters.Add(new Setter(TreeViewItem.ForegroundProperty,
			    new SolidColorBrush(theme.TreeSelectedForeground)));
			treeItemTemplate.Triggers.Add(treeSelTrigger);

			// Hover trigger (only when not selected)
			MultiTrigger treeHoverTrigger = new MultiTrigger();
			treeHoverTrigger.Conditions.Add(new Condition(TreeViewItem.IsMouseOverProperty, true));
			treeHoverTrigger.Conditions.Add(new Condition(TreeViewItem.IsSelectedProperty, false));
			treeHoverTrigger.Setters.Add(new Setter(TreeViewItem.BackgroundProperty,
			    new SolidColorBrush(theme.TreeHoverBackground)));
			treeHoverTrigger.Setters.Add(new Setter(TreeViewItem.ForegroundProperty,
			    new SolidColorBrush(theme.TreeHoverForeground)));
			treeItemTemplate.Triggers.Add(treeHoverTrigger);

			treeItemStyle.Setters.Add(new Setter(TreeViewItem.TemplateProperty, treeItemTemplate));
			categoryTree.ItemContainerStyle = treeItemStyle;

			// ---- ListView ----
			itemList.Background = new SolidColorBrush(theme.ListBackground);
			itemList.Foreground = new SolidColorBrush(theme.ListForeground);

			// ListView item style with alternate row colours and hover
			Style listItemStyle = new Style(typeof(ListViewItem));
			listItemStyle.Setters.Add(new Setter(ListViewItem.ForegroundProperty, new SolidColorBrush(theme.ListForeground)));
			listItemStyle.Setters.Add(new Setter(ListViewItem.BackgroundProperty, Brushes.Transparent));
			listItemStyle.Setters.Add(new Setter(ListViewItem.BorderBrushProperty, Brushes.Transparent));
			listItemStyle.Setters.Add(new Setter(ListViewItem.BorderThicknessProperty, new Thickness(0)));

			// Custom template so we control Background rendering fully
			ControlTemplate listItemTemplate = new ControlTemplate(typeof(ListViewItem));
			FrameworkElementFactory listBorder = new FrameworkElementFactory(typeof(Border));
			listBorder.SetValue(FrameworkElement.NameProperty, "ItemBorder");
			listBorder.SetBinding(Border.BackgroundProperty,
			    new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			listBorder.SetValue(Border.PaddingProperty, new Thickness(2, 0, 2, 0));

			FrameworkElementFactory gridRowPresenter = new FrameworkElementFactory(typeof(GridViewRowPresenter));
			gridRowPresenter.SetBinding(GridViewRowPresenter.ContentProperty,
			    new System.Windows.Data.Binding("."));
			gridRowPresenter.SetBinding(GridViewRowPresenter.ColumnsProperty,
			    new System.Windows.Data.Binding("View.Columns") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(ListView), 1) });
			listBorder.AppendChild(gridRowPresenter);

			listItemTemplate.VisualTree = listBorder;

			// Selected trigger
			Trigger listSelTrigger = new Trigger();
			listSelTrigger.Property = ListViewItem.IsSelectedProperty;
			listSelTrigger.Value = true;
			listSelTrigger.Setters.Add(new Setter(ListViewItem.BackgroundProperty, new SolidColorBrush(theme.ListSelectedBackground)));
			listSelTrigger.Setters.Add(new Setter(ListViewItem.ForegroundProperty, new SolidColorBrush(theme.ListSelectedForeground)));
			listItemTemplate.Triggers.Add(listSelTrigger);

			// Hover trigger (only when not selected)
			MultiTrigger hoverTrigger = new MultiTrigger();
			hoverTrigger.Conditions.Add(new Condition(ListViewItem.IsMouseOverProperty, true));
			hoverTrigger.Conditions.Add(new Condition(ListViewItem.IsSelectedProperty, false));
			hoverTrigger.Setters.Add(new Setter(ListViewItem.BackgroundProperty, new SolidColorBrush(theme.ListHoverBackground)));
			hoverTrigger.Setters.Add(new Setter(ListViewItem.ForegroundProperty, new SolidColorBrush(theme.ListHoverForeground)));
			listItemTemplate.Triggers.Add(hoverTrigger);

			listItemStyle.Setters.Add(new Setter(ListViewItem.TemplateProperty, listItemTemplate));

			itemList.ItemContainerStyle = listItemStyle;

			// ---- ListView CheckBox styling ----
			string chkBgHex = ThemeSettings.ColorToHex(theme.CheckBoxBackground);
			string chkBorderHex = ThemeSettings.ColorToHex(theme.CheckBoxBorder);
			string chkTickHex = ThemeSettings.ColorToHex(theme.CheckBoxCheckMark);
			string chkHoverBgHex = ThemeSettings.ColorToHex(theme.CheckBoxHoverBackground);

			string checkBoxXaml = @"
			<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
			       xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
			       TargetType=""CheckBox"">
			    <Setter Property=""Template"">
			        <Setter.Value>
			            <ControlTemplate TargetType=""CheckBox"">
			                <Grid>
			                    <Border x:Name=""CheckBorder""
			                            Width=""14"" Height=""14""
			                            Background=""" + chkBgHex + @"""
			                            BorderBrush=""" + chkBorderHex + @"""
			                            BorderThickness=""1""
			                            CornerRadius=""2"">
			                        <TextBlock x:Name=""CheckMark""
			                                   Text=""&#xE73E;""
			                                   FontFamily=""Segoe Fluent Icons""
			                                   FontSize=""12""
			                                   Foreground=""" + chkTickHex + @"""
			                                   HorizontalAlignment=""Center""
			                                   VerticalAlignment=""Center""
			                                   Visibility=""Collapsed"" />
			                    </Border>
			                </Grid>
			                <ControlTemplate.Triggers>
			                    <Trigger Property=""IsChecked"" Value=""True"">
			                        <Setter TargetName=""CheckMark"" Property=""Visibility"" Value=""Visible"" />
			                    </Trigger>
			                    <Trigger Property=""IsMouseOver"" Value=""True"">
			                        <Setter TargetName=""CheckBorder"" Property=""Background"" Value=""" + chkHoverBgHex + @""" />
			                    </Trigger>
			                </ControlTemplate.Triggers>
			            </ControlTemplate>
			        </Setter.Value>
			    </Setter>
			</Style>";

			try
			{
			    Style listCheckBoxStyle = (Style)System.Windows.Markup.XamlReader.Parse(checkBoxXaml);
			    itemList.Resources[typeof(CheckBox)] = listCheckBoxStyle;
			}
			catch (Exception)
			{
			}
			// ---- App Settings tab CheckBox styling ----
			string settingsCheckBoxXaml = @"
			<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
			       xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
			       TargetType=""CheckBox"">
			    <Setter Property=""Template"">
			        <Setter.Value>
			            <ControlTemplate TargetType=""CheckBox"">
			                <StackPanel Orientation=""Horizontal"">
			                    <Border x:Name=""CheckBorder""
			                            Width=""14"" Height=""14""
			                            Background=""" + chkBgHex + @"""
			                            BorderBrush=""" + chkBorderHex + @"""
			                            BorderThickness=""1""
			                            CornerRadius=""2""
			                            VerticalAlignment=""Center"">
			                        <TextBlock x:Name=""CheckMark""
			                                   Text=""&#xE73E;""
			                                   FontFamily=""Segoe Fluent Icons""
			                                   FontSize=""12""
			                                   Foreground=""" + chkTickHex + @"""
			                                   HorizontalAlignment=""Center""
			                                   VerticalAlignment=""Center""
			                                   Visibility=""Collapsed"" />
			                    </Border>
			                    <ContentPresenter Margin=""6,0,0,0""
			                                      VerticalAlignment=""Center"" />
			                </StackPanel>
			                <ControlTemplate.Triggers>
			                    <Trigger Property=""IsChecked"" Value=""True"">
			                        <Setter TargetName=""CheckMark"" Property=""Visibility"" Value=""Visible"" />
			                    </Trigger>
			                    <Trigger Property=""IsMouseOver"" Value=""True"">
			                        <Setter TargetName=""CheckBorder"" Property=""Background"" Value=""" + chkHoverBgHex + @""" />
			                    </Trigger>
			                </ControlTemplate.Triggers>
			            </ControlTemplate>
			        </Setter.Value>
			    </Setter>
			</Style>";

			try
			{
			    Style settingsCheckBoxStyle = (Style)System.Windows.Markup.XamlReader.Parse(settingsCheckBoxXaml);
			    columnCheckboxPanel.Resources[typeof(CheckBox)] = settingsCheckBoxStyle;
			}
			catch (Exception)
			{
			}

			// ListView column headers via custom template (with hover support)
			string hdrBgHex = ThemeSettings.ColorToHex(theme.ListHeaderBackground);
			string hdrFgHex = ThemeSettings.ColorToHex(theme.ListHeaderForeground);
			string hdrHoverBgHex = ThemeSettings.ColorToHex(theme.ListHeaderHoverBackground);
			string hdrHoverFgHex = ThemeSettings.ColorToHex(theme.ListHeaderHoverForeground);

			string headerXaml = @"
			<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
			       xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
			       TargetType=""GridViewColumnHeader"">
			    <Setter Property=""Foreground"" Value=""" + hdrFgHex + @""" />
			    <Setter Property=""Template"">
			        <Setter.Value>
			            <ControlTemplate TargetType=""GridViewColumnHeader"">
			                <Grid>
			                    <Border x:Name=""HeaderBorder""
			                            Background=""" + hdrBgHex + @"""
			                            BorderBrush=""" + hdrBgHex + @"""
			                            BorderThickness=""0,0,1,1""
			                            Padding=""4,2""
			                            TextElement.Foreground=""" + hdrFgHex + @""">
			                        <ContentPresenter VerticalAlignment=""Center""
			                                          RecognizesAccessKey=""True"" />
			                    </Border>
			                    <Thumb x:Name=""PART_HeaderGripper""
			                           HorizontalAlignment=""Right""
			                           Width=""4""
			                           Margin=""0,0,-2,0""
			                           Cursor=""SizeWE"">
			                        <Thumb.Style>
			                            <Style TargetType=""Thumb"">
			                                <Setter Property=""Template"">
			                                    <Setter.Value>
			                                        <ControlTemplate TargetType=""Thumb"">
			                                            <Border Background=""Transparent""
			                                                    Width=""4"" />
			                                        </ControlTemplate>
			                                    </Setter.Value>
			                                </Setter>
			                            </Style>
			                        </Thumb.Style>
			                    </Thumb>
			                </Grid>
			                <ControlTemplate.Triggers>
			                    <Trigger Property=""IsMouseOver"" Value=""True"">
			                        <Setter TargetName=""HeaderBorder"" Property=""Background"" Value=""" + hdrHoverBgHex + @""" />
			                        <Setter TargetName=""HeaderBorder"" Property=""TextElement.Foreground"" Value=""" + hdrHoverFgHex + @""" />
			                    </Trigger>
			                    <Trigger Property=""Role"" Value=""Padding"">
			                        <Setter TargetName=""HeaderBorder"" Property=""Background"" Value=""" + hdrBgHex + @""" />
			                        <Setter TargetName=""HeaderBorder"" Property=""BorderThickness"" Value=""0"" />
			                    </Trigger>
			                </ControlTemplate.Triggers>
			            </ControlTemplate>
			        </Setter.Value>
			    </Setter>
			</Style>";

			try
			{
			    columnHeaderStyle = (Style)System.Windows.Markup.XamlReader.Parse(headerXaml);
			    itemList.Resources[typeof(GridViewColumnHeader)] = columnHeaderStyle;
			    GridView gv = itemList.View as GridView;
			    if (gv != null)
			        gv.ColumnHeaderContainerStyle = columnHeaderStyle;
			}
			catch (Exception)
			{
			}

		    // ---- Track Settings Panel ----
		    ApplyThemeToPanel(trackSettingsPanel, theme);

			// Theme the Track Data / Track Settings group borders and headers
			if (trackSettingsScrollArea != null && trackSettingsScrollArea.Content is StackPanel fp)
			{
			    foreach (var child in fp.Children)
			    {
			        if (child is Border bdr && (bdr.Tag as string == "TrackDataBorder" || bdr.Tag as string == "TrackSettingsBorder"))
			        {
			            bdr.BorderBrush = new SolidColorBrush(theme.SplitterColor);

			            if (bdr.Child is StackPanel sp)
			            {
			                foreach (var inner in sp.Children)
			                {
			                    if (inner is TextBlock tb && (tb.Text == "Track Data" || tb.Text == "Track Settings"))
			                    {
			                        tb.Foreground = new SolidColorBrush(theme.TabSelectedForeground);
			                    }
			                }
			            }
			        }
			    }
			}

			// Theme the Track Settings label and header
			if (trackSettingsLabel != null)
			{
			    trackSettingsLabel.Foreground = new SolidColorBrush(theme.TrackToolbarForeground);

			    // Theme the header row background
			    if (trackSettingsLabel.Parent is DockPanel headerRow)
			    {
			        headerRow.Background = new SolidColorBrush(theme.TrackToolbarBackground);
			    }
			}

			// Theme the toggle button with hover
			if (btnToggleTrackSettings != null)
			{
			    SolidColorBrush trkBg = new SolidColorBrush(theme.TrackToolbarBackground);
			    SolidColorBrush trkFg = new SolidColorBrush(theme.TrackToolbarForeground);

			    // Compute hover colour
			    byte trkR = theme.TrackToolbarBackground.R;
			    byte trkG = theme.TrackToolbarBackground.G;
			    byte trkB = theme.TrackToolbarBackground.B;
			    Color trkHoverColor;
			    if ((trkR + trkG + trkB) / 3 < 128)
			        trkHoverColor = Color.FromRgb(
			            (byte)Math.Min(255, trkR + 40),
			            (byte)Math.Min(255, trkG + 40),
			            (byte)Math.Min(255, trkB + 40));
			    else
			        trkHoverColor = Color.FromRgb(
			            (byte)Math.Max(0, trkR - 40),
			            (byte)Math.Max(0, trkG - 40),
			            (byte)Math.Max(0, trkB - 40));
			    SolidColorBrush trkHoverBrush = new SolidColorBrush(trkHoverColor);

			    // Build a style with hover trigger
			    Style toggleStyle = new Style(typeof(Button));
			    toggleStyle.Setters.Add(new Setter(Button.BackgroundProperty, trkBg));
			    toggleStyle.Setters.Add(new Setter(Button.ForegroundProperty, trkFg));
			    toggleStyle.Setters.Add(new Setter(Button.BorderBrushProperty, trkBg));

			    Trigger toggleHoverTrigger = new Trigger();
			    toggleHoverTrigger.Property = UIElement.IsMouseOverProperty;
			    toggleHoverTrigger.Value = true;
			    toggleHoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, trkHoverBrush));
			    toggleHoverTrigger.Setters.Add(new Setter(Button.BorderBrushProperty, trkHoverBrush));
			    toggleStyle.Triggers.Add(toggleHoverTrigger);

			    // Need a ControlTemplate to allow style triggers to override
			    ControlTemplate toggleTemplate = new ControlTemplate(typeof(Button));
			    var border = new FrameworkElementFactory(typeof(Border));
			    border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			    border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			    border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
			    var cp = new FrameworkElementFactory(typeof(ContentPresenter));
			    cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
			    cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
			    border.AppendChild(cp);
			    toggleTemplate.VisualTree = border;
			    toggleStyle.Setters.Add(new Setter(Button.TemplateProperty, toggleTemplate));

			    btnToggleTrackSettings.Style = toggleStyle;
			}

			// Theme the Track Settings toolbar
			SolidColorBrush tsTbBg = new SolidColorBrush(theme.TrackToolbarBackground);
			SolidColorBrush tsTbFg = new SolidColorBrush(theme.TrackToolbarForeground);
			foreach (var child in trackSettingsPanel.Children)
			{
			    if (child is ToolBarTray tbt)
			        ApplyThemeToToolBarTray(tbt, tsTbBg, tsTbFg);
			}

			// ---- Tabs ----
			SolidColorBrush tabBg = new SolidColorBrush(theme.TabBackground);
			SolidColorBrush tabFg = new SolidColorBrush(theme.TabForeground);
			SolidColorBrush tabSelBg = new SolidColorBrush(theme.TabSelectedBackground);
			SolidColorBrush tabSelFg = new SolidColorBrush(theme.TabSelectedForeground);
			SolidColorBrush tabContentFg = new SolidColorBrush(theme.TabContentForeground);
			rightTabs.Background = tabBg;

			// Compute hover colour
			byte tabR = theme.TabHandleBackground.R;
			byte tabG = theme.TabHandleBackground.G;
			byte tabB = theme.TabHandleBackground.B;
			Color tabHoverColor;
			if ((tabR + tabG + tabB) / 3 < 128)
			    tabHoverColor = Color.FromRgb(
			        (byte)Math.Min(255, tabR + 25),
			        (byte)Math.Min(255, tabG + 25),
			        (byte)Math.Min(255, tabB + 25));
			else
			    tabHoverColor = Color.FromRgb(
			        (byte)Math.Max(0, tabR - 25),
			        (byte)Math.Max(0, tabG - 25),
			        (byte)Math.Max(0, tabB - 25));

			string tabBgHex = ThemeSettings.ColorToHex(theme.TabHandleBackground);
			string tabFgHex = ThemeSettings.ColorToHex(theme.TabHandleForeground);
			string tabSelBgHex = ThemeSettings.ColorToHex(theme.TabSelectedBackground);
			string tabSelFgHex = ThemeSettings.ColorToHex(theme.TabSelectedForeground);
			string tabHoverHex = ThemeSettings.ColorToHex(tabHoverColor);
			string tabSplitterHex = ThemeSettings.ColorToHex(theme.SplitterColor);

			string tabXaml = @"
			<ResourceDictionary
			    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
			    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">

			    <Style TargetType=""TabItem"">
			        <Setter Property=""Background"" Value=""" + tabBgHex + @""" />
			        <Setter Property=""Template"">
			            <Setter.Value>
			                <ControlTemplate TargetType=""TabItem"">
			                    <Border x:Name=""TabOuter"" Background=""Transparent"" Padding=""0"" BorderThickness=""0"">
			                        <Border x:Name=""TabBorder""
			                                Background=""{TemplateBinding Background}""
			                                BorderBrush=""" + tabSplitterHex + @"""

			                                BorderThickness=""1,1,1,0""
			                                Padding=""8,4,8,4""
			                                Margin=""0,0,2,0""
			                                CornerRadius=""4,4,0,0"">
			                            <ContentPresenter x:Name=""TabText""
			                                              ContentSource=""Header""
			                                              VerticalAlignment=""Center""
			                                              TextElement.Foreground=""" + tabFgHex + @""" />
			                        </Border>
			                    </Border>
			                    <ControlTemplate.Triggers>
			                        <Trigger Property=""IsSelected"" Value=""True"">
			                            <Setter Property=""Background"" Value=""" + tabSelBgHex + @""" />
			                            <Setter Property=""Margin"" Value=""0,-2,2,0"" />
			                            <Setter TargetName=""TabText"" Property=""TextElement.Foreground"" Value=""" + tabSelFgHex + @""" />
			                        </Trigger>
			                        <MultiTrigger>
			                            <MultiTrigger.Conditions>
			                                <Condition Property=""IsMouseOver"" Value=""True"" />
			                                <Condition Property=""IsSelected"" Value=""False"" />
			                            </MultiTrigger.Conditions>
			                            <Setter Property=""Background"" Value=""" + tabHoverHex + @""" />
			                        </MultiTrigger>
			                    </ControlTemplate.Triggers>
			                </ControlTemplate>
			            </Setter.Value>
			        </Setter>
			    </Style>

			</ResourceDictionary>";

			try
			{
			    ResourceDictionary tabResources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(tabXaml);
			    foreach (object key in tabResources.Keys)
			    {
			        this.Resources[key] = tabResources[key];
			    }
			}
			catch (Exception)
			{
			    // Fallback
			    rightTabs.Foreground = tabFg;
			}

			// Fix foreground for closable tab headers
			FixClosableTabForegrounds();

			// Apply content foreground to each tab's content panel,
			// and explicitly theme any toolbars inside tabs
			SolidColorBrush tabTbBg = new SolidColorBrush(theme.ToolbarBackground);
			SolidColorBrush tabTbFg = new SolidColorBrush(theme.ToolbarForeground);
			foreach (TabItem ti in rightTabs.Items)
			{
			    if (ti.Content is DockPanel dp)
			    {
			        ApplyTabContentForeground(dp, tabContentFg);

			        // Theme toolbars inside this tab's content
			        foreach (var child in dp.Children)
			        {
			            if (child is ToolBarTray tbt)
			                ApplyThemeToToolBarTray(tbt, tabTbBg, tabTbFg);
			        }
			    }
			    else if (ti.Content is Panel p)
			    {
			        ApplyTabContentForeground(p, tabContentFg);
			    }
			}

			// Theme group headers on the Theme tab (if it exists yet)
			ApplyThemeToGroupHeaders(theme);

			// ---- App Settings editor path field ----
			editEditorPath.Background = new SolidColorBrush(theme.TextBoxBackground);
			editEditorPath.Foreground = new SolidColorBrush(theme.TextBoxForeground);
			editEditorPath.CaretBrush = new SolidColorBrush(theme.TextBoxForeground);
			editEditorPath.BorderBrush = new SolidColorBrush(theme.TabBackground);

			// ---- App Settings default publisher name field ----
			if (editDefaultPublisherName != null)
			{
			    editDefaultPublisherName.Background = new SolidColorBrush(theme.TextBoxBackground);
			    editDefaultPublisherName.Foreground = new SolidColorBrush(theme.TextBoxForeground);
			    editDefaultPublisherName.CaretBrush = new SolidColorBrush(theme.TextBoxForeground);
			    editDefaultPublisherName.BorderBrush = new SolidColorBrush(theme.TabBackground);
			}

			// ---- App Settings SyMenu path field ----
			if (editSyMenuPath != null)
			{
				editSyMenuPath.Background = new SolidColorBrush(theme.TextBoxBackground);
				editSyMenuPath.Foreground = new SolidColorBrush(theme.TextBoxForeground);
				editSyMenuPath.CaretBrush = new SolidColorBrush(theme.TextBoxForeground);
				editSyMenuPath.BorderBrush = new SolidColorBrush(theme.TabBackground);
			}

			// ---- App Settings SPS publisher filter field ----
			if (txtSettingsPublisherFilter != null)
			{
				txtSettingsPublisherFilter.Background = new SolidColorBrush(theme.TextBoxBackground);
				txtSettingsPublisherFilter.Foreground = new SolidColorBrush(theme.TextBoxForeground);
				txtSettingsPublisherFilter.CaretBrush = new SolidColorBrush(theme.TextBoxForeground);
				txtSettingsPublisherFilter.BorderBrush = new SolidColorBrush(theme.TabBackground);
			}

			if (defaultTrackModeCombo != null)
			{
			    Style comboStyle = CreateThemedComboBoxStyle(theme);
			    if (comboStyle != null)
			        defaultTrackModeCombo.Style = comboStyle;
			}

		    // ---- Source View ----
		    sourceView.Background = new SolidColorBrush(theme.SourceBackground);
		    sourceView.Foreground = new SolidColorBrush(theme.SourceTagColor);
			if (sourceInfoBanner != null)
			{
			    sourceInfoBanner.Background = new SolidColorBrush(theme.SourceBannerBackground);
			    sourceInfoBanner.Foreground = new SolidColorBrush(theme.SourceBannerForeground);
			}

			// ---- Buttons (app-wide) ----
			SolidColorBrush btnBg = new SolidColorBrush(theme.ButtonBackground);
			SolidColorBrush btnFg = new SolidColorBrush(theme.ButtonForeground);

			// Compute button hover color
			byte btnR = theme.ButtonBackground.R;
			byte btnG = theme.ButtonBackground.G;
			byte btnB = theme.ButtonBackground.B;
			Color btnHoverColor;
			if ((btnR + btnG + btnB) / 3 < 128)
			    btnHoverColor = Color.FromRgb(
			        (byte)Math.Min(255, btnR + 40),
			        (byte)Math.Min(255, btnG + 40),
			        (byte)Math.Min(255, btnB + 40));
			else
			    btnHoverColor = Color.FromRgb(
			        (byte)Math.Max(0, btnR - 40),
			        (byte)Math.Max(0, btnG - 40),
			        (byte)Math.Max(0, btnB - 40));
			SolidColorBrush btnHoverBrush = new SolidColorBrush(btnHoverColor);

			Style buttonStyle = new Style(typeof(Button));
			buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, btnBg));
			buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, btnFg));
			buttonStyle.Setters.Add(new Setter(Button.BorderBrushProperty, btnBg));

			// Custom template so Background actually renders
			FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
			borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			borderFactory.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
			borderFactory.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
			borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));

			FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
			contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
			contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
			borderFactory.AppendChild(contentPresenter);

			ControlTemplate buttonTemplate = new ControlTemplate(typeof(Button));
			buttonTemplate.VisualTree = borderFactory;

			// Hover trigger for global buttons
			Trigger globalBtnHover = new Trigger();
			globalBtnHover.Property = UIElement.IsMouseOverProperty;
			globalBtnHover.Value = true;
			globalBtnHover.Setters.Add(new Setter(Button.BackgroundProperty, btnHoverBrush));
			globalBtnHover.Setters.Add(new Setter(Button.BorderBrushProperty, btnHoverBrush));
			buttonTemplate.Triggers.Add(globalBtnHover);

			buttonStyle.Setters.Add(new Setter(Button.TemplateProperty, buttonTemplate));

			this.Resources[typeof(Button)] = buttonStyle;

		    // Re-display source with new theme colours
		    if (!string.IsNullOrEmpty(currentSource))
		    {
		        DisplaySource(currentSource);
		    }

			// ---- Scrollbars ----
			SolidColorBrush scrollBg = new SolidColorBrush(theme.ScrollBarBackground);
			SolidColorBrush scrollThumb = new SolidColorBrush(theme.ScrollBarThumb);

			// Compute hover colour for thumb
			byte sR = theme.ScrollBarThumb.R;
			byte sG = theme.ScrollBarThumb.G;
			byte sB = theme.ScrollBarThumb.B;
			Color scrollThumbHover;
			if ((sR + sG + sB) / 3 < 128)
			    scrollThumbHover = Color.FromRgb(
			        (byte)Math.Min(255, sR + 40),
			        (byte)Math.Min(255, sG + 40),
			        (byte)Math.Min(255, sB + 40));
			else
			    scrollThumbHover = Color.FromRgb(
			        (byte)Math.Max(0, sR - 30),
			        (byte)Math.Max(0, sG - 30),
			        (byte)Math.Max(0, sB - 30));

			string bgHex = ThemeSettings.ColorToHex(theme.ScrollBarBackground);
			string thumbHex = ThemeSettings.ColorToHex(theme.ScrollBarThumb);
			string thumbHoverHex = ThemeSettings.ColorToHex(scrollThumbHover);

			string scrollBarXaml = @"
			<ResourceDictionary
			    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
			    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">

			    <Style x:Key=""ScrollBarThumbStyle"" TargetType=""Thumb"">
			        <Setter Property=""Template"">
			            <Setter.Value>
			                <ControlTemplate TargetType=""Thumb"">
			                    <Border x:Name=""ThumbBorder""
			                            Background=""" + thumbHex + @"""
			                            CornerRadius=""3""
			                            Margin=""2"" />
			                    <ControlTemplate.Triggers>
			                        <Trigger Property=""IsMouseOver"" Value=""True"">
			                            <Setter TargetName=""ThumbBorder"" Property=""Background"" Value=""" + thumbHoverHex + @""" />
			                        </Trigger>
			                    </ControlTemplate.Triggers>
			                </ControlTemplate>
			            </Setter.Value>
			        </Setter>
			    </Style>

			    <Style x:Key=""ScrollBarPageButton"" TargetType=""RepeatButton"">
			        <Setter Property=""Template"">
			            <Setter.Value>
			                <ControlTemplate TargetType=""RepeatButton"">
			                    <Border Background=""Transparent"" />
			                </ControlTemplate>
			            </Setter.Value>
			        </Setter>
			        <Setter Property=""Focusable"" Value=""False"" />
			        <Setter Property=""IsTabStop"" Value=""False"" />
			    </Style>

			    <ControlTemplate x:Key=""VerticalScrollBar"" TargetType=""ScrollBar"">
			        <Border Background=""" + bgHex + @""" CornerRadius=""3"">
			            <Track x:Name=""PART_Track"" IsDirectionReversed=""True"">
			                <Track.DecreaseRepeatButton>
			                    <RepeatButton Style=""{StaticResource ScrollBarPageButton}"" Command=""ScrollBar.PageUpCommand"" />
			                </Track.DecreaseRepeatButton>
			                <Track.Thumb>
			                    <Thumb Style=""{StaticResource ScrollBarThumbStyle}"" />
			                </Track.Thumb>
			                <Track.IncreaseRepeatButton>
			                    <RepeatButton Style=""{StaticResource ScrollBarPageButton}"" Command=""ScrollBar.PageDownCommand"" />
			                </Track.IncreaseRepeatButton>
			            </Track>
			        </Border>
			    </ControlTemplate>

			    <ControlTemplate x:Key=""HorizontalScrollBar"" TargetType=""ScrollBar"">
			        <Border Background=""" + bgHex + @""" CornerRadius=""3"">
			            <Track x:Name=""PART_Track"">
			                <Track.DecreaseRepeatButton>
			                    <RepeatButton Style=""{StaticResource ScrollBarPageButton}"" Command=""ScrollBar.PageLeftCommand"" />
			                </Track.DecreaseRepeatButton>
			                <Track.Thumb>
			                    <Thumb Style=""{StaticResource ScrollBarThumbStyle}"" />
			                </Track.Thumb>
			                <Track.IncreaseRepeatButton>
			                    <RepeatButton Style=""{StaticResource ScrollBarPageButton}"" Command=""ScrollBar.PageRightCommand"" />
			                </Track.IncreaseRepeatButton>
			            </Track>
			        </Border>
			    </ControlTemplate>

			    <Style TargetType=""ScrollBar"">
			        <Setter Property=""Background"" Value=""" + bgHex + @""" />
			        <Style.Triggers>
			            <Trigger Property=""Orientation"" Value=""Vertical"">
			                <Setter Property=""Template"" Value=""{StaticResource VerticalScrollBar}"" />
			                <Setter Property=""Width"" Value=""14"" />
			            </Trigger>
			            <Trigger Property=""Orientation"" Value=""Horizontal"">
			                <Setter Property=""Template"" Value=""{StaticResource HorizontalScrollBar}"" />
			                <Setter Property=""Height"" Value=""14"" />
			            </Trigger>
			        </Style.Triggers>
			    </Style>

			</ResourceDictionary>";

			try
			{
			    ResourceDictionary scrollBarResources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(scrollBarXaml);
			    foreach (object key in scrollBarResources.Keys)
			    {
			        this.Resources[key] = scrollBarResources[key];
			    }
			}
			catch (Exception)
			{
			    // Fallback: at least colour the background
			    this.Resources[SystemColors.ScrollBarBrushKey] = scrollBg;
			}

            // Theme the WebView nav bar
            foreach (TabItem ti in rightTabs.Items)
            {
                if (ti.Content is DockPanel dp)
                {
                    foreach (var child in dp.Children)
                    {
                        if (child is DockPanel navBar && !(child is ToolBarTray))
                        {
                            navBar.Background = new SolidColorBrush(theme.ToolbarBackground);
                            foreach (var navChild in navBar.Children)
                            {
                                if (navChild is TextBox tb)
                                {
                                    tb.Background = new SolidColorBrush(theme.TextBoxBackground);
                                    tb.Foreground = new SolidColorBrush(theme.TextBoxForeground);
                                    tb.CaretBrush = new SolidColorBrush(theme.TextBoxForeground);
                                }
                                else if (navChild is TextBlock txt)
                                    txt.Foreground = new SolidColorBrush(theme.ToolbarForeground);
                                else if (navChild is ComboBox cmb)
                                {
                                    Style comboStyle = CreateThemedComboBoxStyle(theme);
                                    if (comboStyle != null)
                                        cmb.Style = comboStyle;
                                }
                            }
                        }
                    }
                }
            }

		    // Rebuild list columns to pick up new StatusToColourConverter brushes
		    RebuildColumns();
		    // Force status icons to refresh with new theme colours
			RefreshStatusIcons(itemList);

			// ---- Theme ComboBox dropdown arrows ----
			// Walk all ComboBoxes and style their toggle buttons after rendering
			this.Dispatcher.BeginInvoke(new Action(() =>
			{
			    ThemeAllComboBoxArrows(this, new SolidColorBrush(theme.ToolbarForeground));
			}), System.Windows.Threading.DispatcherPriority.Loaded);
			if (columnHeaderStyle != null)
			{
			    GridView gv2 = itemList.View as GridView;
			    if (gv2 != null)
			        gv2.ColumnHeaderContainerStyle = columnHeaderStyle;
			}
		}

		private void ApplyThemeToMenuItem(MenuItem mi, SolidColorBrush bg, SolidColorBrush fg, bool isTopLevel = true)
		{
			Style miStyle = new Style(typeof(MenuItem));
			miStyle.Setters.Add(new Setter(MenuItem.BackgroundProperty, bg));
			miStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, fg));
			mi.Style = miStyle;
		    mi.Foreground = fg;

		    // Compute hover colour
		    byte r = bg.Color.R;
		    byte g = bg.Color.G;
		    byte b = bg.Color.B;
		    Color hoverColor;
		    if ((r + g + b) / 3 < 128)
		        hoverColor = Color.FromRgb(
		            (byte)Math.Min(255, r + 30),
		            (byte)Math.Min(255, g + 30),
		            (byte)Math.Min(255, b + 30));
		    else
		        hoverColor = Color.FromRgb(
		            (byte)Math.Max(0, r - 30),
		            (byte)Math.Max(0, g - 30),
		            (byte)Math.Max(0, b - 30));
		    SolidColorBrush hoverBrush = new SolidColorBrush(hoverColor);

		    if (isTopLevel)
		        mi.Template = BuildMenuItemTemplate(bg, fg, hoverBrush);
		    else
		        mi.Template = BuildSubmenuItemTemplate(bg, fg, hoverBrush);

		    if (mi.HasItems)
		    {
		        mi.SubmenuOpened -= MenuItem_SubmenuOpened;
		        mi.SubmenuOpened += MenuItem_SubmenuOpened;
		        mi.Tag = new SolidColorBrush[] { bg, fg };
		    }

		    foreach (var sub in mi.Items)
		    {
		        if (sub is MenuItem subMi)
		            ApplyThemeToMenuItem(subMi, bg, fg, false);
				else if (sub is Separator sep)
				{
				    Style sepStyle = new Style(typeof(Separator));
				    ControlTemplate sepTemplate = new ControlTemplate(typeof(Separator));

				    FrameworkElementFactory sepBorder = new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
				    sepBorder.SetValue(System.Windows.Controls.Border.HeightProperty, 1.0);
				    sepBorder.SetValue(System.Windows.Controls.Border.BackgroundProperty, fg);
				    sepBorder.SetValue(System.Windows.Controls.Border.MarginProperty, new Thickness(4, 4, 4, 4));
				    sepBorder.SetValue(System.Windows.Controls.Border.SnapsToDevicePixelsProperty, true);

				    sepTemplate.VisualTree = sepBorder;
				    sepStyle.Setters.Add(new Setter(Separator.TemplateProperty, sepTemplate));
				    sep.Style = sepStyle;
				}
		    }
		}

		private void ApplyTabContentForeground(DependencyObject parent, SolidColorBrush fg)
		{
		    if (parent == null) return;

		    // Skip toolbars — they have their own theming via ApplyThemeToToolBarTray
		    if (parent is ToolBarTray || parent is ToolBar)
		        return;

		    if (parent is TextBlock tb)
		    {
		        // Don't override special-purpose TextBlocks
		        if (tb != startPositionText && tb != stopPositionText &&
		            tb != editLatestVersion && tb != statusFile && tb != findCount)
		        {
		            tb.Foreground = fg;
		        }
		    }
		    else if (parent is TextBox txt)
		    {
		        // TextBoxes are handled by TextBoxForeground, skip
		    }
		    else if (parent is CheckBox chk)
		    {
		        chk.Foreground = fg;
		    }
		    else if (parent is ComboBox cmb)
		    {
		        cmb.Foreground = fg;
		    }

		    // Recurse through children
		    if (parent is Panel p)
		    {
		        foreach (object child in p.Children)
		        {
		            if (child is DependencyObject d)
		                ApplyTabContentForeground(d, fg);
		        }
		    }
		    else if (parent is Decorator dec && dec.Child != null)
		    {
		        ApplyTabContentForeground(dec.Child, fg);
		    }
		    else if (parent is ContentControl cc && cc.Content is DependencyObject cd)
		    {
		        ApplyTabContentForeground(cd, fg);
		    }
		    else if (parent is ItemsControl ic)
		    {
		        foreach (object item in ic.Items)
		        {
		            if (item is DependencyObject d)
		                ApplyTabContentForeground(d, fg);
		        }
		    }
		}

		private void ApplyThemeToToolBarTray(ToolBarTray tray, SolidColorBrush bg, SolidColorBrush fg)
		{
		    tray.Background = bg;
		    foreach (ToolBar tb in tray.ToolBars)
		    {
		        tb.Background = bg;
		        tb.Foreground = fg;

		        // Compute hover colour
		        byte r = bg.Color.R;
		        byte g = bg.Color.G;
		        byte b = bg.Color.B;
		        Color hoverColor;
		        if ((r + g + b) / 3 < 128)
		            hoverColor = Color.FromRgb(
		                (byte)Math.Min(255, r + 40),
		                (byte)Math.Min(255, g + 40),
		                (byte)Math.Min(255, b + 40));
		        else
		            hoverColor = Color.FromRgb(
		                (byte)Math.Max(0, r - 40),
		                (byte)Math.Max(0, g - 40),
		                (byte)Math.Max(0, b - 40));
		        SolidColorBrush hoverBrush = new SolidColorBrush(hoverColor);

		        // Build a toolbar-specific button style with hover effect
		        Style tbBtnStyle = new Style(typeof(Button));
		        tbBtnStyle.Setters.Add(new Setter(Button.ForegroundProperty, fg));
		        tbBtnStyle.Setters.Add(new Setter(Button.BackgroundProperty, bg));
		        tbBtnStyle.Setters.Add(new Setter(Button.BorderBrushProperty, bg));

		        FrameworkElementFactory btnBorder = new FrameworkElementFactory(typeof(Border));
		        btnBorder.SetValue(FrameworkElement.NameProperty, "TbBtnBorder");
		        btnBorder.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
		        btnBorder.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
		        btnBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
		        btnBorder.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
		        btnBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));

		        FrameworkElementFactory btnContent = new FrameworkElementFactory(typeof(ContentPresenter));
		        btnContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		        btnContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		        btnContent.SetValue(TextElement.ForegroundProperty, fg);
		        btnBorder.AppendChild(btnContent);

		        ControlTemplate btnTemplate = new ControlTemplate(typeof(Button));
		        btnTemplate.VisualTree = btnBorder;

		        // Hover trigger
		        Trigger btnHoverTrigger = new Trigger();
		        btnHoverTrigger.Property = UIElement.IsMouseOverProperty;
		        btnHoverTrigger.Value = true;
		        btnHoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, hoverBrush));
		        btnHoverTrigger.Setters.Add(new Setter(Button.BorderBrushProperty, hoverBrush));
		        btnTemplate.Triggers.Add(btnHoverTrigger);

		        tbBtnStyle.Setters.Add(new Setter(Button.TemplateProperty, btnTemplate));

				// Apply to all items
				foreach (var item in tb.Items)
				{
				    if (item is Button btn)
				        btn.Style = tbBtnStyle;
				    else if (item is TextBlock txt)
				        txt.Foreground = fg;
				    else if (item is CheckBox chk)
				    {
				        chk.Foreground = fg;
				        chk.Background = bg;
				    }
				    else if (item is TextBox tbx)
				    {
				        tbx.Background = new SolidColorBrush(currentTheme.TextBoxBackground);
				        tbx.Foreground = new SolidColorBrush(currentTheme.TextBoxForeground);
				        tbx.CaretBrush = new SolidColorBrush(currentTheme.TextBoxForeground);
				        tbx.BorderBrush = bg;
				    }
                    else if (item is ComboBox cmb)
                    {
                        Style comboStyle = CreateThemedComboBoxStyle(currentTheme);
                        if (comboStyle != null)
                            cmb.Style = comboStyle;
                    }
				}

		        // Override the ToolBar template — preserve overflow button
		        string tbBgHex = ThemeSettings.ColorToHex(bg.Color);
		        string tbFgHex = ThemeSettings.ColorToHex(fg.Color);
		        string hoverHex = ThemeSettings.ColorToHex(hoverColor);

		        string toolBarXaml = @"
		        <ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
		                         xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
		                         TargetType=""ToolBar"">
		            <Border Background=""{TemplateBinding Background}"" Padding=""2"">
		                <DockPanel>
		                    <ToggleButton x:Name=""OverflowButton""
		                                  DockPanel.Dock=""Right""
		                                  IsChecked=""{Binding IsOverflowOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}""
		                                  ClickMode=""Press""
		                                  Visibility=""Collapsed""
		                                  Cursor=""Hand"">
		                        <ToggleButton.Template>
		                            <ControlTemplate TargetType=""ToggleButton"">
		                                <Border x:Name=""OverflowBorder""
		                                        Background=""Transparent""
		                                        Padding=""4,2""
		                                        CornerRadius=""2"">
		                                    <TextBlock Text=""&#xE712;""
		                                               FontFamily=""Segoe Fluent Icons""
		                                               FontSize=""12""
		                                               Foreground=""" + tbFgHex + @"""
		                                               VerticalAlignment=""Center"" />
		                                </Border>
		                                <ControlTemplate.Triggers>
		                                    <Trigger Property=""IsMouseOver"" Value=""True"">
		                                        <Setter TargetName=""OverflowBorder"" Property=""Background"" Value=""" + hoverHex + @""" />
		                                    </Trigger>
		                                    <Trigger Property=""IsChecked"" Value=""True"">
		                                        <Setter TargetName=""OverflowBorder"" Property=""Background"" Value=""" + hoverHex + @""" />
		                                    </Trigger>
		                                </ControlTemplate.Triggers>
		                            </ControlTemplate>
		                        </ToggleButton.Template>
		                    </ToggleButton>
		                    <Popup x:Name=""OverflowPopup""
		                           IsOpen=""{Binding IsOverflowOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}""
		                           Placement=""Bottom""
		                           PlacementTarget=""{Binding ElementName=OverflowButton}""
		                           StaysOpen=""False""
		                           AllowsTransparency=""True""
		                           Focusable=""False""
		                           PopupAnimation=""Fade"">
		                        <Border Background=""" + tbBgHex + @"""
		                                BorderBrush=""" + hoverHex + @"""
		                                BorderThickness=""1""
		                                CornerRadius=""2""
		                                Padding=""2"">
		                            <ToolBarOverflowPanel x:Name=""PART_ToolBarOverflowPanel""
		                                                   WrapWidth=""200""
		                                                   Focusable=""True""
		                                                   FocusVisualStyle=""{x:Null}""
		                                                   KeyboardNavigation.TabNavigation=""Cycle""
		                                                   KeyboardNavigation.DirectionalNavigation=""Cycle"" />
		                        </Border>
		                    </Popup>
		                    <ToolBarPanel x:Name=""PART_ToolBarPanel""
		                                  IsItemsHost=""True"" />
		                </DockPanel>
		            </Border>
		            <ControlTemplate.Triggers>
		                <Trigger Property=""HasOverflowItems"" Value=""True"">
		                    <Setter TargetName=""OverflowButton"" Property=""Visibility"" Value=""Visible"" />
		                </Trigger>
		            </ControlTemplate.Triggers>
		        </ControlTemplate>";

		        try
		        {
		            ControlTemplate parsedTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(toolBarXaml);
		            tb.Template = parsedTemplate;
		        }
		        catch (Exception)
		        {
		            tb.Background = bg;
		            tb.Foreground = fg;
		        }
		    }
		}

        private Style CreateThemedComboBoxStyle(ThemeSettings theme)
        {
            string cmbBgHex = ThemeSettings.ColorToHex(theme.ComboBoxBackground);
            string cmbFgHex = ThemeSettings.ColorToHex(theme.ComboBoxForeground);
            string cmbBorderHex = ThemeSettings.ColorToHex(theme.ComboBoxBorder);
            string cmbBtnBgHex = ThemeSettings.ColorToHex(theme.ComboBoxButtonBackground);
            string cmbBtnFgHex = ThemeSettings.ColorToHex(theme.ComboBoxButtonForeground);

            string comboXaml = @"
            <Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                   xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                   TargetType=""ComboBox"">
                <Setter Property=""Foreground"" Value=""" + cmbFgHex + @""" />
                <Setter Property=""Background"" Value=""" + cmbBgHex + @""" />
                <Setter Property=""BorderBrush"" Value=""" + cmbBorderHex + @""" />
                <Setter Property=""ItemContainerStyle"">
                    <Setter.Value>
                        <Style TargetType=""ComboBoxItem"">
                            <Setter Property=""Foreground"" Value=""" + cmbFgHex + @""" />
                            <Setter Property=""Background"" Value=""" + cmbBgHex + @""" />
                            <Style.Triggers>
                                <Trigger Property=""IsHighlighted"" Value=""True"">
                                    <Setter Property=""Background"" Value=""" + cmbBtnBgHex + @""" />
                                    <Setter Property=""Foreground"" Value=""" + cmbBtnFgHex + @""" />
                                </Trigger>
                            </Style.Triggers>
                        </Style>
                    </Setter.Value>
                </Setter>
                <Setter Property=""Template"">
                    <Setter.Value>
                        <ControlTemplate TargetType=""ComboBox"">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width=""*"" />
                                    <ColumnDefinition Width=""20"" />
                                </Grid.ColumnDefinitions>
                                <Border x:Name=""Border""
                                        Grid.ColumnSpan=""2""
                                        Background=""{TemplateBinding Background}""
                                        BorderBrush=""{TemplateBinding BorderBrush}""
                                        BorderThickness=""1""
                                        CornerRadius=""2"" />
                                <ContentPresenter Grid.Column=""0""
                                                  Margin=""4,2,0,2""
                                                  VerticalAlignment=""Center""
                                                  HorizontalAlignment=""Left""
                                                  Content=""{TemplateBinding SelectionBoxItem}""
                                                  ContentTemplate=""{TemplateBinding SelectionBoxItemTemplate}"" />
                                <Border Grid.Column=""1""
                                        Background=""" + cmbBtnBgHex + @"""
                                        CornerRadius=""0,2,2,0""
                                        BorderThickness=""0"">
                                    <TextBlock Text=""&#xE70D;""
                                               FontFamily=""Segoe Fluent Icons""
                                               FontSize=""10""
                                               Foreground=""" + cmbBtnFgHex + @"""
                                               HorizontalAlignment=""Center""
                                               VerticalAlignment=""Center"" />
                                </Border>
                                <ToggleButton Grid.ColumnSpan=""2""
                                              IsChecked=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}""
                                              Background=""Transparent""
                                              BorderThickness=""0""
                                              Focusable=""False""
                                              ClickMode=""Press"">
                                    <ToggleButton.Template>
                                        <ControlTemplate TargetType=""ToggleButton"">
                                            <Border Background=""Transparent"" />
                                        </ControlTemplate>
                                    </ToggleButton.Template>
                                </ToggleButton>
                                <Popup x:Name=""PART_Popup""
                                       IsOpen=""{TemplateBinding IsDropDownOpen}""
                                       Placement=""Bottom""
                                       AllowsTransparency=""True""
                                       Focusable=""False""
                                       PopupAnimation=""Slide"">
                                    <Border MinWidth=""{TemplateBinding ActualWidth}""
                                            MaxHeight=""{TemplateBinding MaxDropDownHeight}""
                                            Background=""" + cmbBgHex + @"""
                                            BorderBrush=""" + cmbBorderHex + @"""
                                            BorderThickness=""1""
                                            CornerRadius=""2"">
                                        <ScrollViewer>
                                            <StackPanel IsItemsHost=""True"" />
                                        </ScrollViewer>
                                    </Border>
                                </Popup>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>";

            try
            {
                return (Style)System.Windows.Markup.XamlReader.Parse(comboXaml);
            }
            catch (Exception)
            {
                return null;
            }
        }

		private ControlTemplate BuildMenuItemTemplate(SolidColorBrush bg, SolidColorBrush fg, SolidColorBrush hoverBg)
		{
		    ControlTemplate template = new ControlTemplate(typeof(MenuItem));

		    FrameworkElementFactory outerGrid = new FrameworkElementFactory(typeof(Grid));

		    // Root border — binds background to MenuItem.Background
		    FrameworkElementFactory rootBorder = new FrameworkElementFactory(typeof(Border));
		    rootBorder.SetBinding(Border.BackgroundProperty,
		        new System.Windows.Data.Binding("Background")
		        {
		            RelativeSource = new System.Windows.Data.RelativeSource(
		                System.Windows.Data.RelativeSourceMode.TemplatedParent)
		        });
		    rootBorder.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
		    rootBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0));
		    rootBorder.SetValue(Border.PaddingProperty, new Thickness(6, 4, 6, 4));

		    FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
		    contentPresenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
		    contentPresenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
		    contentPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

		    rootBorder.AppendChild(contentPresenter);
		    outerGrid.AppendChild(rootBorder);

		    // Popup for submenu items
		    FrameworkElementFactory popup = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Popup));
		    popup.SetValue(System.Windows.Controls.Primitives.Popup.PlacementProperty,
		        System.Windows.Controls.Primitives.PlacementMode.Bottom);
		    popup.SetBinding(System.Windows.Controls.Primitives.Popup.IsOpenProperty,
		        new System.Windows.Data.Binding("IsSubmenuOpen")
		        {
		            RelativeSource = new System.Windows.Data.RelativeSource(
		                System.Windows.Data.RelativeSourceMode.TemplatedParent)
		        });
		    popup.SetValue(System.Windows.Controls.Primitives.Popup.AllowsTransparencyProperty, true);

		    FrameworkElementFactory popupBorder = new FrameworkElementFactory(typeof(Border));
		    popupBorder.SetValue(Border.BackgroundProperty, bg);
		    popupBorder.SetValue(Border.BorderBrushProperty, fg);
		    popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
		    popupBorder.SetValue(Border.PaddingProperty, new Thickness(0, 2, 0, 2));

		    FrameworkElementFactory itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
		    popupBorder.AppendChild(itemsPresenter);
		    popup.AppendChild(popupBorder);

		    outerGrid.AppendChild(popup);

		    template.VisualTree = outerGrid;

		    // Hover trigger — sets MenuItem.Background, border picks it up via binding
		    Trigger hoverTrigger = new Trigger();
		    hoverTrigger.Property = MenuItem.IsHighlightedProperty;
		    hoverTrigger.Value = true;
		    hoverTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, hoverBg));
		    template.Triggers.Add(hoverTrigger);

		    return template;
		}

		private ControlTemplate BuildSubmenuItemTemplate(SolidColorBrush bg, SolidColorBrush fg, SolidColorBrush hoverBg)
		{
		    ControlTemplate template = new ControlTemplate(typeof(MenuItem));

		    FrameworkElementFactory rootBorder = new FrameworkElementFactory(typeof(Border));
		    rootBorder.SetBinding(Border.BackgroundProperty,
		        new System.Windows.Data.Binding("Background")
		        {
		            RelativeSource = new System.Windows.Data.RelativeSource(
		                System.Windows.Data.RelativeSourceMode.TemplatedParent)
		        });
		    rootBorder.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
		    rootBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0));
		    rootBorder.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));

		    FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
		    contentPresenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
		    contentPresenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
		    contentPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

		    rootBorder.AppendChild(contentPresenter);
		    template.VisualTree = rootBorder;

		    // Hover trigger
		    Trigger hoverTrigger = new Trigger();
		    hoverTrigger.Property = MenuItem.IsHighlightedProperty;
		    hoverTrigger.Value = true;
		    hoverTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, hoverBg));
		    template.Triggers.Add(hoverTrigger);

		    return template;
		}

		private void MenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
		{
		    MenuItem mi = sender as MenuItem;
		    if (mi == null) return;

		    SolidColorBrush[] brushes = mi.Tag as SolidColorBrush[];
		    if (brushes == null || brushes.Length < 2) return;

		    // Re-apply templates to child items when submenu opens
		    foreach (var sub in mi.Items)
		    {
		        if (sub is MenuItem subMi)
		            ApplyThemeToMenuItem(subMi, brushes[0], brushes[1], false);
		    }
		}

		private void ApplyStatusBarLegendColors(StatusBar sb, ThemeSettings theme)
		{
		    foreach (var item in sb.Items)
		    {
		        if (item is StatusBarItem sbi)
		        {
		            if (sbi.Content is StackPanel legendPanel && legendPanel.Orientation == Orientation.Horizontal)
		            {
		                foreach (var child in legendPanel.Children)
		                {
		                    if (child is TextBlock tb && tb.FontFamily != null &&
		                        tb.FontFamily.Source == "Segoe Fluent Icons")
		                    {
		                        int idx = legendPanel.Children.IndexOf(tb);
		                        if (idx + 1 < legendPanel.Children.Count &&
		                            legendPanel.Children[idx + 1] is TextBlock labelTb)
		                        {
		                            string label = labelTb.Text.Trim().ToLower();
		                            switch (label)
		                            {
		                                case "unchanged":
		                                    tb.Foreground = new SolidColorBrush(theme.StatusUnchanged);
		                                    break;
		                                case "changed":
		                                    tb.Foreground = new SolidColorBrush(theme.StatusChanged);
		                                    break;
		                                case "error":
		                                    tb.Foreground = new SolidColorBrush(theme.StatusError);
		                                    break;
		                                case "new":
		                                    tb.Foreground = new SolidColorBrush(theme.StatusNew);
		                                    break;
		                                case "unchecked":
		                                    tb.Foreground = new SolidColorBrush(theme.StatusUnchecked);
		                                    break;
		                            }
		                            labelTb.Foreground = new SolidColorBrush(theme.StatusBarForeground);
		                        }
		                    }
		                }
		            }
		        }
		    }
		}

		private void ApplyThemeToElement<T>(Panel parent, Action<T> applyAction) where T : class
		{
		    foreach (var child in parent.Children)
		    {
		        if (child is T target)
		            applyAction(target);
		    }
		}

		private void ApplyThemeToPanel(DockPanel panel, ThemeSettings theme)
		{
		    panel.Background = new SolidColorBrush(theme.PanelBackground);
		    SolidColorBrush labelBrush = new SolidColorBrush(theme.PanelForeground);
		    SolidColorBrush tbBg = new SolidColorBrush(theme.TextBoxBackground);
		    SolidColorBrush tbFg = new SolidColorBrush(theme.TextBoxForeground);

		    ApplyThemeToPanelRecursive(panel, labelBrush, tbBg, tbFg);
		}
		
		private void ApplyThemeToPanelRecursive(DependencyObject parent, SolidColorBrush labelBrush, SolidColorBrush tbBg, SolidColorBrush tbFg)
		{
		    if (parent == null) return;

		    if (parent is TextBlock tb)
		    {
		        if (tb != startPositionText && tb != stopPositionText && tb != editLatestVersion &&
		            tb != statusFile && tb != findCount)
		        {
		            tb.Foreground = labelBrush;
		        }
		    }
		    else if (parent is TextBox txt)
		    {
		        txt.Background = tbBg;
		        txt.Foreground = tbFg;
		    }

		    // Walk logical children (works even before visual tree is ready)
		    if (parent is Panel p)
		    {
		        foreach (object child in p.Children)
		        {
		            if (child is DependencyObject d)
		                ApplyThemeToPanelRecursive(d, labelBrush, tbBg, tbFg);
		        }
		    }
		    else if (parent is Decorator dec && dec.Child != null)
		    {
		        ApplyThemeToPanelRecursive(dec.Child, labelBrush, tbBg, tbFg);
		    }
		    else if (parent is ContentControl cc && cc.Content is DependencyObject cd)
		    {
		        ApplyThemeToPanelRecursive(cd, labelBrush, tbBg, tbFg);
		    }
		    else if (parent is ItemsControl ic)
		    {
		        foreach (object item in ic.Items)
		        {
		            if (item is DependencyObject d)
		                ApplyThemeToPanelRecursive(d, labelBrush, tbBg, tbFg);
		        }
		    }

		    // Also try visual tree if loaded
		    int count = VisualTreeHelper.GetChildrenCount(parent);
		    for (int i = 0; i < count; i++)
		    {
		        DependencyObject child = VisualTreeHelper.GetChild(parent, i);
		        ApplyThemeToPanelRecursive(child, labelBrush, tbBg, tbFg);
		    }
		}

		private void RebuildColumns()
		{
		    if (itemList == null) return;

		    GridView gridView = itemList.View as GridView;
		    if (gridView == null) return;

		    // Remember current items and selection
		    int selectedIdx = itemList.SelectedIndex;

		    // Clear and rebuild all columns
		    gridView.Columns.Clear();

		    // Checkbox column
		    GridViewColumn checkCol = new GridViewColumn();
		    checkCol.Width = 26;
		    FrameworkElementFactory headerCheckbox = new FrameworkElementFactory(typeof(CheckBox));
		    headerCheckbox.AddHandler(CheckBox.ClickEvent, new RoutedEventHandler(SelectAll_Click));
		    checkCol.Header = new GridViewColumnHeader();
		    checkCol.HeaderTemplate = new DataTemplate();
		    checkCol.HeaderTemplate.VisualTree = headerCheckbox;
		    FrameworkElementFactory cellCheckbox = new FrameworkElementFactory(typeof(CheckBox));
		    cellCheckbox.SetBinding(CheckBox.IsCheckedProperty,
		        new System.Windows.Data.Binding("IsSelected"));
		    cellCheckbox.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		    checkCol.CellTemplate = new DataTemplate();
		    checkCol.CellTemplate.VisualTree = cellCheckbox;
		    gridView.Columns.Add(checkCol);

		    // Status column
		    GridViewColumn statusCol = new GridViewColumn();
		    statusCol.Header = "St";
		    statusCol.Width = 30;
		    statusCol.CellTemplate = CreateStatusCellTemplate();
		    gridView.Columns.Add(statusCol);

		    // Data columns
		    List<ColumnSetting> colSettings = windowSettings.ColumnSettings;
		    if (colSettings.Count == 0)
		        colSettings = WindowSettings.GetDefaultColumns();
		    colSettings.Sort((a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));

		    foreach (ColumnSetting cs in colSettings)
		    {
		        if (cs.Visible)
		        {
		            string statusBinding = GetStatusBindingForColumn(cs.Binding);
		            if (statusBinding != null)
		                gridView.Columns.Add(MakeColumn(cs.Header, cs.Binding, cs.Width, statusBinding));
		            else
		                gridView.Columns.Add(MakeColumn(cs.Header, cs.Binding, cs.Width));
		        }
		    }

		    // Re-apply header style (with hover support)
		    if (columnHeaderStyle != null)
		    {
		        gridView.ColumnHeaderContainerStyle = columnHeaderStyle;
		    }

		    // Restore selection
		    suppressAutoDownload = true;
		    itemList.ItemsSource = null;
		    itemList.ItemsSource = currentItems;
		    if (selectedIdx >= 0 && selectedIdx < currentItems.Count)
		        itemList.SelectedIndex = selectedIdx;
		    suppressAutoDownload = false;
		}

		private void ApplyThemeToSplitters(Grid grid, SolidColorBrush brush)
		{
		    foreach (var child in grid.Children)
		    {
		        if (child is GridSplitter gs)
		        {
		            gs.Background = brush;
		        }
		        else if (child is Grid subGrid)
		        {
		            ApplyThemeToSplitters(subGrid, brush);
		        }
		    }
		}

		private void ApplyThemeToPanelContent(DependencyObject obj, ThemeSettings theme)
		{
		    if (obj == null) return;

		    SolidColorBrush panelFg = new SolidColorBrush(theme.PanelForeground);
		    SolidColorBrush tbBg = new SolidColorBrush(theme.TextBoxBackground);
		    SolidColorBrush tbFg = new SolidColorBrush(theme.TextBoxForeground);

		    if (obj is TextBlock tb)
		    {
		        // Don't override special coloured TextBlocks (like startPositionText, stopPositionText)
		        if (tb != startPositionText && tb != stopPositionText && tb != editLatestVersion)
		        {
		            tb.Foreground = panelFg;
		        }
		    }
		    else if (obj is TextBox txt)
		    {
		        txt.Background = tbBg;
		        txt.Foreground = tbFg;
		    }
		    else if (obj is Panel p)
		    {
		        foreach (var child in p.Children)
		        {
		            ApplyThemeToPanelContent(child as DependencyObject, theme);
		        }
		    }
		    else if (obj is Decorator d && d.Child != null)
		    {
		        ApplyThemeToPanelContent(d.Child, theme);
		    }
		    else if (obj is ContentControl cc && cc.Content is DependencyObject content)
		    {
		        ApplyThemeToPanelContent(content, theme);
		    }
		}

		private void ApplyForegroundRecursive(DependencyObject parent, SolidColorBrush panelFg, SolidColorBrush labelFg, ThemeSettings theme)
		{
		    int count = VisualTreeHelper.GetChildrenCount(parent);
		    // If not yet loaded, walk logical tree instead
		    if (count == 0 && parent is Panel p)
		    {
		        foreach (var child in p.Children)
		        {
		            if (child is TextBlock tb)
		            {
		                // Don't override status position text or latest version display
		                if (tb != startPositionText && tb != stopPositionText && tb != editLatestVersion && tb != statusFile && tb != findCount)
		                    tb.Foreground = labelFg;
		            }
		            if (child is DockPanel dp)
		                ApplyForegroundRecursive(dp, panelFg, labelFg, theme);
		            if (child is StackPanel sp)
		                ApplyForegroundRecursive(sp, panelFg, labelFg, theme);
		            if (child is ScrollViewer sv && sv.Content is DependencyObject svContent)
		                ApplyForegroundRecursive(svContent, panelFg, labelFg, theme);
		        }
		    }
		}

		private void ThemeMenuItemRecursive(MenuItem menuItem, ThemeSettings theme)
		{
		    menuItem.Background = new SolidColorBrush(theme.MenuBackground);
		    menuItem.Foreground = new SolidColorBrush(theme.MenuForeground);

		    foreach (var sub in menuItem.Items)
		    {
		        if (sub is MenuItem subItem)
		        {
		            ThemeMenuItemRecursive(subItem, theme);
		        }
		    }
		}

		private void ThemeStatusBarLegend(StatusBar statusBar, ThemeSettings theme)
		{
		    // Walk through the status bar to find the legend icons and update their colours
		    // The legend is in a StackPanel inside a StatusBarItem (HorizontalAlignment=Right)
		    foreach (var item in statusBar.Items)
		    {
		        if (item is StatusBarItem sbi)
		        {
		            if (sbi.Content is StackPanel sp)
		            {
		                foreach (var child in sp.Children)
		                {
		                    if (child is TextBlock tb)
		                    {
		                        // Check if this is an icon (Segoe Fluent Icons) or a label
		                        if (tb.FontFamily != null && tb.FontFamily.Source != null &&
		                            tb.FontFamily.Source.Contains("Segoe Fluent Icons"))
		                        {
		                            // It's a status icon — match by current colour to figure out which one
		                            string text = tb.Text;
		                            // Find the next sibling TextBlock to identify which status this is
		                        }
		                        else
		                        {
		                            // It's a label text — update foreground
		                            string label = tb.Text.Trim();
		                            tb.Foreground = new SolidColorBrush(theme.StatusBarForeground);
		                        }
		                    }
		                }

		                // Now do a second pass to set icon colours by pairing icon+label
		                TextBlock pendingIcon = null;
		                foreach (var child in sp.Children)
		                {
		                    if (child is TextBlock tb)
		                    {
		                        if (tb.FontFamily != null && tb.FontFamily.Source != null &&
		                            tb.FontFamily.Source.Contains("Segoe Fluent Icons"))
		                        {
		                            pendingIcon = tb;
		                        }
		                        else if (pendingIcon != null)
		                        {
		                            string label = tb.Text.Trim();
		                            switch (label)
		                            {
		                                case "Unchanged":
		                                    pendingIcon.Foreground = new SolidColorBrush(theme.StatusUnchanged);
		                                    break;
		                                case "Changed":
		                                    pendingIcon.Foreground = new SolidColorBrush(theme.StatusChanged);
		                                    break;
		                                case "Error":
		                                    pendingIcon.Foreground = new SolidColorBrush(theme.StatusError);
		                                    break;
		                                case "New":
		                                    pendingIcon.Foreground = new SolidColorBrush(theme.StatusNew);
		                                    break;
		                                case "Unchecked":
		                                    pendingIcon.Foreground = new SolidColorBrush(theme.StatusUnchecked);
		                                    break;
		                            }
		                            pendingIcon = null;
		                        }
		                    }
		                }
		            }
		        }
		    }
		}

		private void RefreshStatusIcons(ListView list)
		{
		    for (int i = 0; i < list.Items.Count; i++)
		    {
		        ListViewItem container = list.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
		        if (container == null) continue;

		        // Find TextBlocks using Segoe Fluent Icons (the status icon)
		        FindAndRefreshStatusIcons(container);
		    }
		}

		private void FindAndRefreshStatusIcons(DependencyObject parent)
		{
		    int count = VisualTreeHelper.GetChildrenCount(parent);
		    for (int i = 0; i < count; i++)
		    {
		        DependencyObject child = VisualTreeHelper.GetChild(parent, i);
		        if (child is TextBlock tb && tb.FontFamily != null &&
		            tb.FontFamily.Source == "Segoe Fluent Icons")
		        {
		            TrackItem item = tb.DataContext as TrackItem;
		            if (item != null)
		            {
		                ApplyStatusColor(tb, item.TrackStatus);
		            }
		        }
		        FindAndRefreshStatusIcons(child);
		    }
		}

        private void MainWindow_Closing(object sender,
            System.ComponentModel.CancelEventArgs e)
        {
            if (isCategoryDirty)
            {
                MessageBoxResult result = MessageBox.Show(
                    "You have unsaved changes.\n\nDo you want to save before closing?",
                    AppInfo.ShortName + " — Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    SaveAll_Click(sender, new RoutedEventArgs());
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
        }

        // ============================
        // Toolbar Docking
        // ============================

        private void DockToolbar_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null) return;

            string position = menuItem.Tag as string;
            if (position != null)
            {
                DockToolbarTo(position);
            }
        }

		private void DockToolbarTo(string position)
		{
		    DockPanel parent = toolBarTray.Parent as DockPanel;
		    if (parent == null) return;

		    parent.Children.Remove(toolBarTray);

		    switch (position)
		    {
		        case "Top":
		            DockPanel.SetDock(toolBarTray, Dock.Top);
		            toolBarTray.Orientation = Orientation.Horizontal;
		            break;
		        case "Bottom":
		            DockPanel.SetDock(toolBarTray, Dock.Bottom);
		            toolBarTray.Orientation = Orientation.Horizontal;
		            break;
		        case "Left":
		            DockPanel.SetDock(toolBarTray, Dock.Left);
		            toolBarTray.Orientation = Orientation.Vertical;
		            break;
		        case "Right":
		            DockPanel.SetDock(toolBarTray, Dock.Right);
		            toolBarTray.Orientation = Orientation.Vertical;
		            break;
		    }

		    parent.Children.Insert(1, toolBarTray);

		    // Save the position to settings
		    windowSettings.ToolbarPosition = position;

		    statusFile.Text = "Toolbar docked: " + position;
		}

        // ============================
        // Input Dialog
        // ============================

		private string ShowInputDialog(string title, string prompt)
		{
		    Window dialog = new Window();
		    dialog.Title = title;
		    dialog.Width = 350;
		    dialog.Height = 160;
		    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		    dialog.Owner = this;
		    dialog.ResizeMode = ResizeMode.NoResize;

		    // Apply theme to window
		    dialog.Background = new SolidColorBrush(currentTheme.WindowBackground);

		    StackPanel panel = new StackPanel();
		    panel.Margin = new Thickness(12);

		    TextBlock label = new TextBlock();
		    label.Text = prompt;
		    label.Foreground = new SolidColorBrush(currentTheme.WindowForeground);
		    label.Margin = new Thickness(0, 0, 0, 8);
		    panel.Children.Add(label);

		    TextBox input = new TextBox();
		    input.Background = new SolidColorBrush(currentTheme.TextBoxBackground);
		    input.Foreground = new SolidColorBrush(currentTheme.TextBoxForeground);
		    input.CaretBrush = new SolidColorBrush(currentTheme.TextBoxForeground);
		    input.BorderBrush = new SolidColorBrush(currentTheme.CheckBoxBorder);
		    input.Margin = new Thickness(0, 0, 0, 12);
		    panel.Children.Add(input);

		    // Compute button hover colour (same logic as MainWindow)
		    byte btnR = currentTheme.ButtonBackground.R;
		    byte btnG = currentTheme.ButtonBackground.G;
		    byte btnB = currentTheme.ButtonBackground.B;
		    Color btnHoverColor;
		    if ((btnR + btnG + btnB) / 3 < 128)
		        btnHoverColor = Color.FromRgb(
		            (byte)Math.Min(255, btnR + 40),
		            (byte)Math.Min(255, btnG + 40),
		            (byte)Math.Min(255, btnB + 40));
		    else
		        btnHoverColor = Color.FromRgb(
		            (byte)Math.Max(0, btnR - 40),
		            (byte)Math.Max(0, btnG - 40),
		            (byte)Math.Max(0, btnB - 40));

		    SolidColorBrush btnBg = new SolidColorBrush(currentTheme.ButtonBackground);
		    SolidColorBrush btnFg = new SolidColorBrush(currentTheme.ButtonForeground);
		    SolidColorBrush btnHoverBrush = new SolidColorBrush(btnHoverColor);

		    // Build themed button style with hover
		    Style buttonStyle = new Style(typeof(Button));
		    buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, btnBg));
		    buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, btnFg));
		    buttonStyle.Setters.Add(new Setter(Button.BorderBrushProperty, btnBg));
		    buttonStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(8, 4, 8, 4)));

		    ControlTemplate btnTemplate = new ControlTemplate(typeof(Button));
		    FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
		    border.Name = "border";
		    border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
		    border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
		    border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
		    border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
		    border.SetValue(Border.PaddingProperty, new Thickness(8, 4, 8, 4));

		    FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
		    cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		    cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
		    border.AppendChild(cp);
		    btnTemplate.VisualTree = border;

		    Trigger hoverTrigger = new Trigger();
		    hoverTrigger.Property = UIElement.IsMouseOverProperty;
		    hoverTrigger.Value = true;
		    hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, btnHoverBrush));
		    hoverTrigger.Setters.Add(new Setter(Button.BorderBrushProperty, btnHoverBrush));
		    btnTemplate.Triggers.Add(hoverTrigger);

		    buttonStyle.Setters.Add(new Setter(Button.TemplateProperty, btnTemplate));

		    StackPanel buttons = new StackPanel();
		    buttons.Orientation = Orientation.Horizontal;
		    buttons.HorizontalAlignment = HorizontalAlignment.Right;

		    Button btnOK = new Button();
		    btnOK.Content = "OK";
		    btnOK.Width = 70;
		    btnOK.IsDefault = true;
		    btnOK.Style = buttonStyle;
		    btnOK.Margin = new Thickness(0, 0, 8, 0);
		    btnOK.Click += (s, ev) => { dialog.DialogResult = true; };
		    buttons.Children.Add(btnOK);

		    Button btnCancel = new Button();
		    btnCancel.Content = "Cancel";
		    btnCancel.Width = 70;
		    btnCancel.IsCancel = true;
		    btnCancel.Style = buttonStyle;
		    buttons.Children.Add(btnCancel);

		    panel.Children.Add(buttons);
		    dialog.Content = panel;

		    dialog.Loaded += (s, ev) => { input.Focus(); };

		    if (dialog.ShowDialog() == true)
		    {
		        return input.Text.Trim();
		    }

		    return null;
		}

        // ============================
        // Menu Handlers
        // ============================

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private FrameworkElement CreateClosableTabHeader(string text, TabItem tab)
        {
            StackPanel header = new StackPanel();
            header.Orientation = Orientation.Horizontal;

            TextBlock label = new TextBlock();
            label.Text = text;
            label.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(label);

            Button closeBtn = new Button();
            closeBtn.Content = "✕";
            closeBtn.FontSize = 9;
            closeBtn.Width = 16;
            closeBtn.Height = 16;
            closeBtn.Padding = new Thickness(0);
            closeBtn.Margin = new Thickness(6, 0, 0, 0);
            closeBtn.VerticalAlignment = VerticalAlignment.Center;
            closeBtn.VerticalContentAlignment = VerticalAlignment.Center;
            closeBtn.HorizontalContentAlignment = HorizontalAlignment.Center;
            closeBtn.ToolTip = "Close tab";
            closeBtn.Cursor = System.Windows.Input.Cursors.Hand;

            // Remove default button chrome so it blends with the tab header
            closeBtn.Template = CreateCloseButtonTemplate();

            closeBtn.Click += (s, ev) =>
            {
                rightTabs.Items.Remove(tab);
                if (rightTabs.Items.Count > 0)
                    rightTabs.SelectedIndex = 0;
            };
            header.Children.Add(closeBtn);

            return header;
        }

		private void FixClosableTabForegrounds()
		{
		    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
		    {
		        foreach (TabItem ti in rightTabs.Items)
		        {
		            if (ti.Header is StackPanel sp)
		            {
		                bool isSelected = ti.IsSelected;
		                SolidColorBrush brush = isSelected
		                    ? new SolidColorBrush(currentTheme.TabSelectedForeground)
		                    : new SolidColorBrush(currentTheme.TabHandleForeground);

		                foreach (var child in sp.Children)
		                {
		                    if (child is TextBlock tb)
		                        tb.Foreground = brush;
		                    else if (child is Button btn)
		                        btn.Foreground = brush;
		                }
		            }
		        }
		    }));
		}

        private ControlTemplate CreateCloseButtonTemplate()
        {
            ControlTemplate template = new ControlTemplate(typeof(Button));

            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            border.SetValue(Border.PaddingProperty, new Thickness(0));
            border.Name = "border";

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;

            // Hover: light semi-transparent background
            Trigger mouseOver = new Trigger();
            mouseOver.Property = UIElement.IsMouseOverProperty;
            mouseOver.Value = true;
            mouseOver.Setters.Add(new Setter(Border.BackgroundProperty,
                new SolidColorBrush(Color.FromArgb(60, 128, 128, 128)), "border"));
            template.Triggers.Add(mouseOver);

            // Pressed: slightly darker
            Trigger pressed = new Trigger();
            pressed.Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty;
            pressed.Value = true;
            pressed.Setters.Add(new Setter(Border.BackgroundProperty,
                new SolidColorBrush(Color.FromArgb(100, 128, 128, 128)), "border"));
            template.Triggers.Add(pressed);

            return template;
        }

        private bool IsSpsAvailable()
        {
            return !string.IsNullOrEmpty(windowSettings.SpsSuiteRootPath);
        }

        private void UpdateSpsVisibility()
        {
            Visibility vis = IsSpsAvailable()
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Menu items
            menuOpenSpsBuilder.Visibility = vis;
            menuSelectSuites.Visibility = vis;
            menuToolsSpsSeparator.Visibility = vis;

            // Toolbar button
            if (btnToolbarReBuild != null)
                btnToolbarReBuild.Visibility = vis;
        }

        private void OpenNewSpsBuilder_Click(object sender, RoutedEventArgs e)
        {
            if (!IsSpsAvailable())
            {
                MessageBox.Show("SPSSuite path is not set. Please set it in Application Settings.",
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string builderPath = Path.Combine(windowSettings.SpsSuiteRootPath,
                "SyMenuSuite", "SPS_Builder_sps", "SPSBuilder.exe");

            if (!File.Exists(builderPath))
            {
                MessageBox.Show("SPSBuilder not found at:\n" + builderPath,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = builderPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open SPSBuilder:\n" + ex.Message,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSpsSettingsEnabled()
        {
            bool enabled = IsSpsAvailable();
            if (btnSpsSelectSuites != null)
            {
                btnSpsSelectSuites.IsEnabled = enabled;
                btnSpsSelectSuites.Opacity = enabled ? 1.0 : 0.4;
            }
            if (btnSpsScanCache != null)
            {
                btnSpsScanCache.IsEnabled = enabled;
                btnSpsScanCache.Opacity = enabled ? 1.0 : 0.4;
            }
            if (spsPublisherSettingsPanel != null)
            {
                spsPublisherSettingsPanel.IsEnabled = enabled;
                spsPublisherSettingsPanel.Opacity = enabled ? 1.0 : 0.4;
            }
            if (txtSettingsPublisherFilter != null)
            {
                txtSettingsPublisherFilter.IsEnabled = enabled;
                txtSettingsPublisherFilter.Opacity = enabled ? 1.0 : 0.4;
            }
        }

        // ============================
        // Toolbar Handlers
        // ============================

		// Generic button handler - Set parameters for all toolbar buttons here
		private Button CreateToolBarButton(string icon, string tooltip, RoutedEventHandler clickHandler)
		{
		    Button btn = new Button();
		    btn.Content = icon;
		    btn.FontFamily = new FontFamily("Segoe Fluent Icons");
		    btn.FontSize = 16;
		    btn.Padding = new Thickness(6, 2, 6, 2);
		    btn.Margin = new Thickness(2, 2, 2, 2);
		    btn.ToolTip = tooltip;
		    if (clickHandler != null)
		        btn.Click += clickHandler;
		    return btn;
		}

		private void IgnoreTrack_Click(object sender, RoutedEventArgs e)
		{
		    if (currentTrackItem == null) return;

		    string spsFile = currentTrackItem.SpsFileName;
		    string trackName = currentTrackItem.TrackName;

		    if (string.IsNullOrEmpty(spsFile) && string.IsNullOrEmpty(trackName))
		    {
		        MessageBox.Show("No SPS file info for this track.", AppInfo.ShortName,
		            MessageBoxButton.OK, MessageBoxImage.Warning);
		        return;
		    }

		    // Build a key: use SpsFileName if available, otherwise TrackName|SuiteName
		    string ignoreKey = !string.IsNullOrEmpty(spsFile)
		        ? spsFile
		        : trackName + "|" + (currentTrackItem.SuiteName ?? "");

		    if (ignoredSpsFiles.Contains(ignoreKey))
		    {
		        MessageBox.Show("This track is already ignored.", AppInfo.ShortName,
		            MessageBoxButton.OK, MessageBoxImage.Information);
		        return;
		    }

		    MessageBoxResult result = MessageBox.Show(
		        "Ignore '" + trackName + "' from future ReBuild scans?\n\n" +
		        "This track will be removed from the list and won't reappear.\n" +
		        "You can undo this via 'Manage Ignored Tracks'.",
		        AppInfo.ShortName, MessageBoxButton.YesNo, MessageBoxImage.Question);

		    if (result != MessageBoxResult.Yes)
		        return;

		    ignoredSpsFiles.Add(ignoreKey);

		    // Remove from current list
		    currentItems.Remove(currentTrackItem);

		    suppressAutoDownload = true;
		    itemList.ItemsSource = null;
		    itemList.ItemsSource = currentItems;
		    if (currentItems.Count > 0)
		        itemList.SelectedIndex = 0;
		    suppressAutoDownload = false;

		    isDirty = true;
		    isCategoryDirty = true;
		    UpdateSaveButtonStates();

		    statusFile.Text = "Ignored: " + trackName + " — will be skipped in future rebuilds";
		}

		private void ManageIgnoredTracks_Click(object sender, RoutedEventArgs e)
		{
		    if (ignoredSpsFiles.Count == 0)
		    {
		        MessageBox.Show("No tracks are currently ignored.", AppInfo.ShortName,
		            MessageBoxButton.OK, MessageBoxImage.Information);
		        return;
		    }

		    // Build a simple list for display
		    StringBuilder sb = new StringBuilder();
		    sb.AppendLine("Currently ignored tracks:\n");
		    for (int i = 0; i < ignoredSpsFiles.Count; i++)
		    {
		        sb.AppendLine((i + 1) + ". " + ignoredSpsFiles[i]);
		    }
		    sb.AppendLine("\nWould you like to clear the ignore list?\n(All tracks will reappear on next ReBuild)");

		    MessageBoxResult result = MessageBox.Show(sb.ToString(), AppInfo.ShortName + " — Ignored Tracks",
		        MessageBoxButton.YesNo, MessageBoxImage.Question);

		    if (result == MessageBoxResult.Yes)
		    {
		        ignoredSpsFiles.Clear();
		        isDirty = true;
		        isCategoryDirty = true;
		        UpdateSaveButtonStates();
		        statusFile.Text = "Ignore list cleared — all tracks will reappear on next ReBuild";
		    }
		}

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveTrack_Click(sender, e);
        }

        private void MenuNewTrack_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentCategoryPath))
            {
                MessageBox.Show("Please select a category first.", AppInfo.ShortName,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Clear fields so user starts fresh
            ClearTrackFields();

            // Prompt for name and create
            string name = ShowInputDialog("New Track", "Enter Track Name:");
            if (string.IsNullOrWhiteSpace(name))
                return;

            editName.Text = name;
            CreateNewTrack();
        }

        private void SaveTrackAs_Click(object sender, RoutedEventArgs e)
        {
            TrackItem selected = currentTrackItem;

            if (selected == null)
            {
                MessageBox.Show("No track selected.", AppInfo.ShortName,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string newName = ShowInputDialog("Save Track As",
                "Enter new name for the track copy:");

            if (string.IsNullOrWhiteSpace(newName))
                return;

            string safeName = newName;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(c.ToString(), "");
            }

            if (string.IsNullOrWhiteSpace(safeName))
            {
                MessageBox.Show("Invalid file name.", AppInfo.ShortName,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string dir = Path.GetDirectoryName(selected.FilePath);
            string newPath = Path.Combine(dir, safeName + ".track");

            if (File.Exists(newPath))
            {
                MessageBoxResult overwrite = MessageBox.Show(
                    "Track file '" + safeName + ".track' already exists.\n\n" +
                    "Do you want to overwrite it?",
                    AppInfo.ShortName, MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (overwrite != MessageBoxResult.Yes)
                    return;
            }

            try
            {
                // Create a copy with current field values
                TrackItem copy = new TrackItem();
                copy.TrackName = newName;
                copy.FilePath = newPath;
                copy.TrackURL = editTrackURL.Text;
                copy.StartString = editStartString.Text;
                copy.StopString = editStopString.Text;
                copy.DownloadURL = editDownloadURL.Text;
                copy.Version = editVersion.Text;
                copy.ReleaseDate = editReleaseDate.Text;
                copy.PublisherName = editPublisherName.Text;
                copy.SuiteName = editSuiteName.Text;
                copy.ReleaseDateStartString = editReleaseDateStartString.Text;
				copy.ReleaseDateStopString = editReleaseDateStopString.Text;
                copy.CreationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

                copy.SaveToFile(newPath);
                ClearDirty();

                // Reload the category and select the new track
                string folderPath = currentCategoryPath;
                if (string.IsNullOrEmpty(folderPath))
                {
                    TreeViewItem selectedCat = categoryTree.SelectedItem as TreeViewItem;
                    if (selectedCat != null)
                        folderPath = (string)selectedCat.Tag;
                }

                if (!string.IsNullOrEmpty(folderPath))
                {
                    LoadTrackFiles(folderPath);
                }
                RefreshCategoryTree();

                // Select the new copy
                for (int i = 0; i < currentItems.Count; i++)
                {
                    if (currentItems[i].FilePath == newPath)
                    {
                        itemList.SelectedIndex = i;
                        break;
                    }
                }

                statusFile.Text = "Saved as: " + newName;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving track copy:\n" + ex.Message,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            if (currentItems == null || currentItems.Count == 0)
            {
                statusFile.Text = "No tracks to save";
                return;
            }

            // First, save the currently selected track's fields if dirty
            if (isDirty && currentTrackItem != null)
            {
                SaveCurrentTrackFields();
            }

            int savedCount = 0;
            int updatedCount = 0;

            foreach (TrackItem item in currentItems)
            {
                // Update version from latest version if available and different
                if (!string.IsNullOrEmpty(item.LatestVersion) &&
                    item.LatestVersion != item.Version &&
                    item.TrackStatus != "error")
                {
                    item.Version = item.LatestVersion;
                    if (item.TrackStatus == "changed")
                        item.TrackStatus = "unchanged";
                    updatedCount++;
                }

                savedCount++;
            }

            // Check if this is an SPS category — save as XML
            bool isSpsCategory = false;
            if (!string.IsNullOrEmpty(currentCategoryPath))
            {
                if (Directory.GetFiles(currentCategoryPath, "*.xml").Length > 0)
                    isSpsCategory = true;
                else if (currentItems.Count > 0 && string.IsNullOrEmpty(currentItems[0].FilePath))
                    isSpsCategory = true;
            }

            if (isSpsCategory)
            {
                try
                {
                    string[] xmlFiles = Directory.GetFiles(currentCategoryPath, "*.xml");
                    string xmlPath;
                    string publisherFilter = txtSettingsPublisherFilter != null
                        ? txtSettingsPublisherFilter.Text.Trim()
                        : "";
                    string suitePath = "";
                    List<string> savedSuites = windowSettings.SelectedSuites;

                    if (xmlFiles.Length > 0)
                    {
                        xmlPath = xmlFiles[0];
                        string existingPf, existingSp;
                        List<string> existingSuites;
                        TrackItem.LoadSpsListFromFile(xmlPath, out existingPf, out existingSp, out existingSuites, out _);
                        if (!string.IsNullOrEmpty(existingSp))
                            suitePath = existingSp;
                        if (existingSuites.Count > 0)
                            savedSuites = existingSuites;
                    }
                    else
                    {
                        string folderName = Path.GetFileName(currentCategoryPath);
                        xmlPath = Path.Combine(currentCategoryPath, folderName + ".xml");
                    }

                    TrackItem.SaveSpsListToFile(xmlPath, currentItems.ToList(), publisherFilter, suitePath, savedSuites, ignoredSpsFiles);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error saving SPS category: " + ex.Message,
                        AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                foreach (TrackItem item in currentItems)
                {
                    item.SaveToFile();
                }
            }

            ClearAllDirty();

            // Refresh the UI fields if the current item was updated
            if (currentTrackItem != null)
            {
                isLoadingFields = true;
                editVersion.Text = currentTrackItem.Version;
                UpdateVersionDisplay();
                isLoadingFields = false;
            }

            // Refresh the list to show updated versions
            suppressAutoDownload = true;
            int selectedIndex = itemList.SelectedIndex;
            itemList.ItemsSource = null;
            itemList.ItemsSource = currentItems;
			if (selectedIndex >= 0)
			{
				itemList.SelectedIndex = selectedIndex;
				itemList.ScrollIntoView(itemList.SelectedItem);
			}
            suppressAutoDownload = false;

            statusFile.Text = "Save All: " + savedCount + " tracks saved, " +
                updatedCount + " versions updated";
        }

        private void SelectSuites_Click(object sender, RoutedEventArgs e)
        {
            string spsSuiteRoot = windowSettings.SpsSuiteRootPath;

            if (string.IsNullOrEmpty(spsSuiteRoot) || !Directory.Exists(spsSuiteRoot))
            {
                MessageBox.Show(
                    "Please set the SPSSuite path first by clicking ReBuild.",
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            List<SuiteInfo> availableSuites = SpsParser.DetectSuites(spsSuiteRoot);

            if (availableSuites.Count == 0)
            {
                MessageBox.Show(
                    "No suites found in:\n" + spsSuiteRoot +
                    "\n\nMake sure suite folders contain a _Cache subfolder.",
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SuiteSelectionWindow selWin = new SuiteSelectionWindow(availableSuites, windowSettings.SelectedSuites, currentTheme);
            selWin.Owner = this;

            if (selWin.ShowDialog() == true)
            {
                windowSettings.SelectedSuites = selWin.SelectedSuiteNames;
                windowSettings.Save(windowSettingsPath);
                statusFile.Text = "Selected suites: " + string.Join(", ", windowSettings.SelectedSuites);
            }
        }

        private void ReBuild_Click(object sender, RoutedEventArgs e)
        {
        	if (txtSettingsSearchCount != null) txtSettingsSearchCount.Text = "";
            // Step 1: Get the SPSSuite root path (saved in settings or ask user)
            string spsSuiteRoot = windowSettings.SpsSuiteRootPath;

            if (string.IsNullOrEmpty(spsSuiteRoot) || !Directory.Exists(spsSuiteRoot))
            {
                MessageBox.Show(
                    "Please select the SPSSuite folder.\n\n" +
                    "This is typically located at:\n" +
                    "SyMenu\\ProgramFiles\\SPSSuite\n\n" +
                    "This will be saved for future use.",
                    AppInfo.ShortName + " — Set SPSSuite Path",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                folderDialog.Description = "Select the SPSSuite folder (contains SyMenuSuite, etc.)";
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    spsSuiteRoot = folderDialog.SelectedPath;

                    // If user selected a suite subfolder or _Cache, go up
                    string selectedName = Path.GetFileName(spsSuiteRoot);
                    if (selectedName.Equals("_Cache", StringComparison.OrdinalIgnoreCase))
                        spsSuiteRoot = Path.GetDirectoryName(Path.GetDirectoryName(spsSuiteRoot));
                    else if (selectedName.Equals("SyMenuSuite", StringComparison.OrdinalIgnoreCase) ||
                             selectedName.Equals("NirSoftSuite", StringComparison.OrdinalIgnoreCase))
                        spsSuiteRoot = Path.GetDirectoryName(spsSuiteRoot);

                    // Save to settings
                    windowSettings.SpsSuiteRootPath = spsSuiteRoot;
                    windowSettings.Save(windowSettingsPath);
                    UpdateSpsVisibility();
                    UpdateSpsSettingsEnabled();
                }
                else
                {
                    return;
                }
            }

            // Step 2: Get selected suites
            List<SuiteInfo> allSuites = SpsParser.DetectSuites(spsSuiteRoot);
            List<SuiteInfo> selectedSuites = new List<SuiteInfo>();

            if (windowSettings.SelectedSuites.Count == 0)
            {
                // No suites selected yet — ask user to select
                SuiteSelectionWindow selWin = new SuiteSelectionWindow(allSuites, windowSettings.SelectedSuites, currentTheme);
                selWin.Owner = this;
                if (selWin.ShowDialog() == true)
                {
                    windowSettings.SelectedSuites = selWin.SelectedSuiteNames;
                    windowSettings.Save(windowSettingsPath);
                }
                else
                {
                    return;
                }
            }

            // Build list of selected SuiteInfo objects
            foreach (SuiteInfo suite in allSuites)
            {
                if (windowSettings.SelectedSuites.Contains(suite.Name))
                    selectedSuites.Add(suite);
            }

            if (selectedSuites.Count == 0)
            {
                MessageBox.Show(
                    "No suites selected.\n\nUse the Select Suites button to choose suites first.",
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Validate all selected suites exist
            foreach (SuiteInfo suite in selectedSuites)
            {
                string cachePath = Path.Combine(suite.FullPath, "_Cache");
                if (!Directory.Exists(cachePath))
                {
                    MessageBox.Show(
                        "No _Cache folder found in:\n" + suite.FullPath +
                        "\n\nSuite '" + suite.Name + "' will be skipped.",
                        AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // Step 3: Check selected category
            TreeViewItem selectedNode = categoryTree.SelectedItem as TreeViewItem;
            string targetFolder = null;
            string targetName = null;

            if (selectedNode != null)
            {
                targetFolder = (string)selectedNode.Tag;
                targetName = Path.GetFileName(targetFolder);

                // Check what's in the category
                bool hasTrackFiles = Directory.GetFiles(targetFolder, "*.track").Length > 0;

                if (hasTrackFiles)
                {
                    // Category has .track files — can't put SPS data here
                    MessageBoxResult choice = MessageBox.Show(
                        "Category '" + targetName + "' contains .track files.\n\n" +
                        "SPS data must go in an empty or existing SPS category.\n\n" +
                        "Create a new category for this SPS data?",
                        AppInfo.ShortName, MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (choice == MessageBoxResult.Yes)
                    {
                        targetFolder = null; // fall through to create new
                    }
                    else
                    {
                        return;
                    }
                }
            }

            // Step 4: If no suitable category, create one
            if (string.IsNullOrEmpty(targetFolder))
            {
                string defaultName = string.Join("+", windowSettings.SelectedSuites);
                string newName = ShowInputDialog("New SPS Category",
                    "Enter category name for SPS data:\n(default: " + defaultName + ")");

                if (string.IsNullOrWhiteSpace(newName))
                    return;

                foreach (char c in Path.GetInvalidFileNameChars())
                    newName = newName.Replace(c.ToString(), "");

                if (string.IsNullOrWhiteSpace(newName))
                {
                    MessageBox.Show("Invalid folder name.", AppInfo.ShortName,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                targetFolder = Path.Combine(categoriesPath, newName);
                targetName = newName;

                if (!Directory.Exists(targetFolder))
                    Directory.CreateDirectory(targetFolder);
            }

            // Step 5: Publisher filter
            string publisherFilter = txtSettingsPublisherFilter != null
                ? txtSettingsPublisherFilter.Text.Trim()
                : "";

            // Confirm
            string suiteNames = string.Join(", ", windowSettings.SelectedSuites);
            string confirmMsg = "Rebuild SPS category '" + targetName + "'?";
            confirmMsg += "\n\nSuites: " + suiteNames;
            if (!string.IsNullOrEmpty(publisherFilter))
                confirmMsg += "\nPublisher filter: " + publisherFilter;
            else
                confirmMsg += "\nNo publisher filter — all SPS entries will be included.";

            if (MessageBox.Show(confirmMsg, "ReBuild from SPS",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            // Step 6: Extract and parse from all selected suites
            statusFile.Text = "Processing suites...";
            statusProgress.Value = 10;

            try
            {
                List<TrackItem> allSpsItems = new List<TrackItem>();
                List<string> suitesToCleanup = new List<string>();

                int suiteIndex = 0;
                foreach (SuiteInfo suite in selectedSuites)
                {
                    suiteIndex++;
                    double progressBase = 10 + (40.0 * suiteIndex / selectedSuites.Count);
                    statusFile.Text = "Processing suite: " + suite.Name + " (" + suiteIndex + "/" + selectedSuites.Count + ")...";
                    statusProgress.Value = progressBase;

                    string cachePath = Path.Combine(suite.FullPath, "_Cache");
                    if (!Directory.Exists(cachePath))
                        continue;

                    string parseFolder;

                    if (suite.RequiresExtraction)
                    {
                        // Default suite — extract zips from _Cache into _TmpET
                        statusFile.Text = "Extracting " + suite.Name + "...";
                        parseFolder = SpsParser.ExtractSpsCache(suite.FullPath);
                        if (string.IsNullOrEmpty(parseFolder))
                            continue;
                        suitesToCleanup.Add(suite.FullPath);
                    }
                    else
                    {
                        // User suite — read .sps files directly from _Cache
                        parseFolder = cachePath;
                    }

                    statusFile.Text = "Parsing " + suite.Name + "...";
                    List<TrackItem> suiteItems = SpsParser.ParseSpsFiles(parseFolder, publisherFilter);

                    // Set suite name on all items from this suite
                    foreach (TrackItem item in suiteItems)
                    {
                        item.SuiteName = suite.Name;
                        item.TrackMode = windowSettings.DefaultTrackMode ?? "html";
                    }

                    allSpsItems.AddRange(suiteItems);
                }

				// Remove ignored items
				if (ignoredSpsFiles.Count > 0)
				{
				    allSpsItems.RemoveAll(item =>
				    {
				        string key1 = item.SpsFileName ?? "";
				        string key2 = item.TrackName + "|" + (item.SuiteName ?? "");
				        return ignoredSpsFiles.Contains(key1) || ignoredSpsFiles.Contains(key2);
				    });
				}

                statusProgress.Value = 50;
                statusFile.Text = "Processing " + allSpsItems.Count + " SPS entries from " + selectedSuites.Count + " suite(s)...";

                // Step 7: Merge with existing SPS data if category already has an XML
                string[] existingXml = Directory.GetFiles(targetFolder, "*.xml");
                if (existingXml.Length > 0)
                {
                    string existPf, existSp;
                    List<string> existSuites;
					List<string> existIgnored;
					List<TrackItem> existingItems = TrackItem.LoadSpsListFromFile(existingXml[0], out existPf, out existSp, out existSuites, out existIgnored);
					ignoredSpsFiles = existIgnored;

                    // Build lookup of existing items by name+suite for preserving user data
                    Dictionary<string, TrackItem> existingByKey = new Dictionary<string, TrackItem>(
                        StringComparer.OrdinalIgnoreCase);
                    foreach (TrackItem existing in existingItems)
                    {
                        if (!string.IsNullOrEmpty(existing.TrackName))
                        {
                            string key = existing.TrackName + "|" + (existing.SuiteName ?? "");
                            if (!existingByKey.ContainsKey(key))
                            {
                                existingByKey[key] = existing;
                            }
                        }
                    }

                    // Start from the filtered allSpsItems list (not existing),
                    // but preserve user-defined tracking data from existing items
                    List<TrackItem> merged = new List<TrackItem>();

                    foreach (TrackItem spsItem in allSpsItems)
                    {
                        string key = spsItem.TrackName + "|" + (spsItem.SuiteName ?? "");
                        if (existingByKey.ContainsKey(key))
                        {
                            // Use existing item but update SPS metadata
                            TrackItem existing = existingByKey[key];
                            existing.Version = spsItem.Version;
                            existing.ReleaseDate = spsItem.ReleaseDate;
                            existing.DownloadURL = spsItem.DownloadURL;
                            existing.DownloadSizeKb = spsItem.DownloadSizeKb;
                            existing.PublisherName = spsItem.PublisherName;
                            existing.SuiteName = spsItem.SuiteName;
                            existing.SpsFileName = spsItem.SpsFileName;
                            merged.Add(existing);
                        }
                        else
                        {
                            merged.Add(spsItem);
                        }
                    }

                    allSpsItems = merged;
                }

                // Step 8: Populate the in-memory list (do NOT auto-save)
                currentItems.Clear();
                foreach (TrackItem item in allSpsItems)
                {
                    currentItems.Add(item);
                }

                // Switch to the target category
                currentCategoryPath = targetFolder;

                // Clean up temp files for default suites that were extracted
                foreach (string suitePath in suitesToCleanup)
                {
                    SpsParser.CleanupTmpFolder(suitePath);
                }

                statusProgress.Value = 100;

                // Refresh tree without triggering LoadTrackFiles
                suppressCategoryReload = true;
                RefreshCategoryTree();
                suppressCategoryReload = false;

                // Re-populate the list from memory
                suppressAutoDownload = true;
                itemList.ItemsSource = null;
                itemList.ItemsSource = currentItems;
                if (currentItems.Count > 0)
                    itemList.SelectedIndex = 0;
                suppressAutoDownload = false;

                // Always mark dirty after rebuild so new metadata gets saved
                bool rebuildIsDirty = true;

                isLoadingFields = false;
                if (rebuildIsDirty)
                {
                    isDirty = true;
                    UpdateSaveButtonStates();
                }
                else
                {
                    ClearDirty();
                }

                // Update the tree label to show the count from memory
                foreach (TreeViewItem treeItem in categoryTree.Items)
                {
                    if ((string)treeItem.Tag == targetFolder)
                    {
                        StackPanel panel = treeItem.Header as StackPanel;
                        if (panel != null)
                        {
                            foreach (var child in panel.Children)
                            {
                                TextBlock tb = child as TextBlock;
                                if (tb != null)
                                {
                                    tb.Text = targetName + " (" + currentItems.Count + ") [SPS]";
                                    break;
                                }
                            }
                        }
                        break;
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("ReBuild error: " + ex.Message, AppInfo.ShortName,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                statusFile.Text = "ReBuild failed.";
            }
            finally
            {
                statusProgress.Value = 0;
                statusFile.Text = "ReBuild done: " + currentItems.Count + " entries from " + selectedSuites.Count + " suite(s). Save to keep changes.";
                if (txtSettingsSearchCount != null) txtSettingsSearchCount.Text = currentItems.Count + " items";
            }
        }

        private void txtPublisherFilter_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ReBuild_Click(sender, new RoutedEventArgs());
            }
        }

        private async void InstallUBlock_Click(object sender, RoutedEventArgs e)
        {
            string ublockFolder = Path.Combine(extensionsPath, "uBlock0.chromium");

            statusFile.Text = "Downloading uBlock Origin...";
            statusProgress.IsIndeterminate = true;

            try
            {
                // Download latest uBlock Origin for Chromium from GitHub releases
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", UserAgent);

                // Get latest release info from GitHub API
                string apiUrl = "https://api.github.com/repos/gorhill/uBlock/releases/latest";
                string json = await client.GetStringAsync(apiUrl);

                // Find the chromium zip asset URL
                string assetUrl = null;
                string[] lines = json.Split(',');
                foreach (string line in lines)
                {
                    if (line.Contains("browser_download_url") && line.Contains("uBlock0") && line.Contains(".chromium.zip"))
                    {
                        int urlStart = line.IndexOf("https://");
                        int urlEnd = line.IndexOf("\"", urlStart);
                        if (urlStart >= 0 && urlEnd > urlStart)
                        {
                            assetUrl = line.Substring(urlStart, urlEnd - urlStart);
                        }
                        break;
                    }
                }

                if (assetUrl == null)
                {
                    MessageBox.Show("Could not find uBlock Origin Chromium release.\n" +
                        "You can manually download from:\nhttps://github.com/gorhill/uBlock/releases\n\n" +
                        "Extract the uBlock0.chromium folder to:\n" + extensionsPath,
                        AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                statusFile.Text = "Downloading: " + assetUrl;

                // Download the zip
                byte[] zipBytes = await client.GetByteArrayAsync(assetUrl);
                string zipPath = Path.Combine(extensionsPath, "ublock_chromium.zip");
                File.WriteAllBytes(zipPath, zipBytes);

                // Remove old folder if exists
                if (Directory.Exists(ublockFolder))
                    Directory.Delete(ublockFolder, true);

                // Extract
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extensionsPath);

                // Clean up zip
                File.Delete(zipPath);

                if (!Directory.Exists(ublockFolder))
                {
                    // Check if extracted to a different folder name
                    string[] dirs = Directory.GetDirectories(extensionsPath, "uBlock*");
                    if (dirs.Length > 0)
                    {
                        ublockFolder = dirs[0];
                    }
                }

                if (!File.Exists(Path.Combine(ublockFolder, "manifest.json")))
                {
                    MessageBox.Show("Download succeeded but manifest.json not found.\n" +
                        "Check: " + extensionsPath,
                        AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Switch to WebView tab so WebView2 can initialise
                rightTabs.SelectedIndex = 1;
                await System.Threading.Tasks.Task.Delay(100);

                // Install into WebView2 — remove old version first
                await webView.EnsureCoreWebView2Async();

                bool alreadyRegistered = false;
                try
                {
                    var extensions = await webView.CoreWebView2.Profile.GetBrowserExtensionsAsync();
                    foreach (var ext in extensions)
                    {
                        if (ext.Name.Contains("uBlock", StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyRegistered = true;
                            await ext.RemoveAsync();
                            break;
                        }
                    }
                }
                catch { }

                try
                {
                    var newExtension = await webView.CoreWebView2.Profile.AddBrowserExtensionAsync(ublockFolder);
                    statusFile.Text = "uBlock Origin installed successfully! Extension ID: " + newExtension.Id;
                    MessageBox.Show("uBlock Origin installed/updated successfully!\n\n" +
                        "The extension will be active for all WebView navigation.",
                        AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception addEx)
                {
                    if (addEx.Message.Contains("0x80070032"))
                    {
                        if (alreadyRegistered)
                        {
                            statusFile.Text = "uBlock files updated — restart app to load new version";
                            MessageBox.Show("uBlock Origin files have been updated on disk.\n\n" +
                                "However, WebView2 could not re-register the extension in this session.\n" +
                                "Please restart the application to load the updated version.",
                                AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            statusFile.Text = "uBlock downloaded but WebView2 rejected MV2 extension";
                            MessageBox.Show("uBlock Origin was downloaded successfully, but WebView2 rejected it.\n\n" +
                                "Your WebView2 runtime may no longer support Manifest V2 extensions.\n" +
                                "When Microsoft adds Manifest V3 support to WebView2, the extension can be updated.\n\n" +
                                "In the meantime, the built-in cookie popup blocker is available as an alternative.",
                                AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Error installing uBlock Origin:\n" + addEx.Message +
                            "\n\nYou can manually download from:\nhttps://github.com/gorhill/uBlock/releases\n\n" +
                            "Extract the uBlock0.chromium folder to:\n" + extensionsPath,
                            AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
                        statusFile.Text = "Extension install failed: " + addEx.Message;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error installing uBlock Origin:\n" + ex.Message +
                    "\n\nYou can manually download from:\nhttps://github.com/gorhill/uBlock/releases\n\n" +
                    "Extract the uBlock0.chromium folder to:\n" + extensionsPath,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
                statusFile.Text = "Extension install failed: " + ex.Message;
            }
            finally
            {
                statusProgress.IsIndeterminate = false;
                statusProgress.Value = 0;
            }
        }

        private async void ListExtensions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await webView.EnsureCoreWebView2Async();
                var extensions = await webView.CoreWebView2.Profile.GetBrowserExtensionsAsync();

                if (extensions.Count == 0)
                {
                    MessageBox.Show("No extensions installed.", AppInfo.ShortName,
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Installed Extensions:");
                sb.AppendLine();

                foreach (var ext in extensions)
                {
                    sb.AppendLine("Name: " + ext.Name);
                    sb.AppendLine("ID: " + ext.Id);
                    sb.AppendLine("Enabled: " + ext.IsEnabled);
                    sb.AppendLine();
                }

                MessageBox.Show(sb.ToString(), "WebView2 Extensions",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error listing extensions:\n" + ex.Message,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

		private void AddSelectAllOnRightClick(TextBox textBox)
		{
		    textBox.PreviewMouseRightButtonDown += (s, e) =>
		    {
		        textBox.SelectAll();
		        textBox.Focus();
		    };
		}

		private void About_Click(object sender, RoutedEventArgs e)
		{
		    Window aboutWindow = new Window();
		    aboutWindow.Title = "About " + AppInfo.AppName;
		    aboutWindow.Width = 800;
		    aboutWindow.Height = 670;
		    aboutWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		    aboutWindow.Owner = this;
		    aboutWindow.ResizeMode = ResizeMode.NoResize;
		    aboutWindow.Background = new SolidColorBrush(currentTheme.ListBackground);

		    // Main grid: left nav + right content
		    Grid mainGrid = new Grid();
		    mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
		    mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

		    // ---- Left panel: icon, version, nav list ----
		    Border leftBorder = new Border();
		    leftBorder.Background = new SolidColorBrush(currentTheme.WindowBackground);
		    leftBorder.BorderBrush = new SolidColorBrush(currentTheme.SplitterColor);
		    leftBorder.BorderThickness = new Thickness(0, 0, 1, 0);
		    Grid.SetColumn(leftBorder, 0);

		    StackPanel leftPanel = new StackPanel();
		    leftPanel.Margin = new Thickness(12);

		    // App icon
		    Image appIcon = new Image();
		    try
		    {
		        Uri iconUri = new Uri("pack://application:,,,/icons/app.ico");
		        BitmapDecoder decoder = BitmapDecoder.Create(iconUri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
		        BitmapFrame bestFrame = decoder.Frames[0];
		        foreach (var frame in decoder.Frames)
		        {
		            if (frame.PixelWidth > bestFrame.PixelWidth)
		                bestFrame = frame;
		        }
		        appIcon.Source = bestFrame;
		    }
		    catch (Exception) { }
		    appIcon.Width = 64;
		    appIcon.Height = 64;
		    appIcon.HorizontalAlignment = HorizontalAlignment.Center;
		    appIcon.Margin = new Thickness(0, 0, 0, 6);
		    leftPanel.Children.Add(appIcon);

		    TextBlock appName = new TextBlock();
		    appName.Text = AppInfo.AppName;
		    appName.FontSize = 15;
		    appName.FontWeight = FontWeights.Bold;
		    appName.Foreground = new SolidColorBrush(currentTheme.WindowForeground);
		    appName.HorizontalAlignment = HorizontalAlignment.Center;
		    leftPanel.Children.Add(appName);

		    string version = System.Reflection.Assembly.GetExecutingAssembly()
		        .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
		        ?.InformationalVersion ?? "Unknown";
		    TextBlock versionText = new TextBlock();
		    versionText.Text = "Version " + version;
		    versionText.FontSize = 11;
		    versionText.Foreground = new SolidColorBrush(currentTheme.StatusBarForeground);
		    versionText.HorizontalAlignment = HorizontalAlignment.Center;
		    versionText.Margin = new Thickness(0, 2, 0, 16);
		    leftPanel.Children.Add(versionText);

		    // Navigation list
		    ListBox navList = new ListBox();
		    navList.Background = Brushes.Transparent;
		    navList.BorderThickness = new Thickness(0);
		    navList.Foreground = new SolidColorBrush(currentTheme.WindowForeground);

		    // Style the ListBox items
		    Style navItemStyle = new Style(typeof(ListBoxItem));
		    navItemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(8, 6, 8, 6)));
		    navItemStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0, 1, 0, 1)));
		    navItemStyle.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(currentTheme.WindowForeground)));
		    navItemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
		    navItemStyle.Setters.Add(new Setter(ListBoxItem.CursorProperty, System.Windows.Input.Cursors.Hand));

		    Trigger navSelTrigger = new Trigger();
		    navSelTrigger.Property = ListBoxItem.IsSelectedProperty;
		    navSelTrigger.Value = true;
		    navSelTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(currentTheme.TabSelectedBackground)));
		    navSelTrigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(currentTheme.TabSelectedForeground)));
		    navItemStyle.Triggers.Add(navSelTrigger);

		    Trigger navHoverTrigger = new Trigger();
		    navHoverTrigger.Property = ListBoxItem.IsMouseOverProperty;
		    navHoverTrigger.Value = true;
		    navHoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(currentTheme.ListHoverBackground)));
		    navHoverTrigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(currentTheme.ListHoverForeground)));
		    navItemStyle.Triggers.Add(navHoverTrigger);

		    navList.ItemContainerStyle = navItemStyle;

		    string[] sections = new string[]
		    {
		        "Getting Started",
		        "Track Data Fields",
		        "Track Settings Fields",
		        "Download URL Placeholders",
		        "Toolbar Reference",
		        "Status Icons",
		        "Batch Checking",
		        "Source View",
		        "Web Browser",
		        "Themes",
		        "App Settings",
		        "SPS Categories",
		        "Keyboard Shortcuts",
		        "Tips & Tricks",
		        "About"
		    };

		    foreach (string section in sections)
		    {
		        navList.Items.Add(section);
		    }

		    leftPanel.Children.Add(navList);
		    leftBorder.Child = leftPanel;
		    mainGrid.Children.Add(leftBorder);

		    // ---- Right panel: content area ----
		    ScrollViewer contentScroll = new ScrollViewer();
		    contentScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
		    contentScroll.Padding = new Thickness(24);
		    Grid.SetColumn(contentScroll, 1);

		    TextBlock contentText = new TextBlock();
		    contentText.TextWrapping = TextWrapping.Wrap;
		    contentText.FontSize = 13;
		    contentText.Foreground = new SolidColorBrush(currentTheme.WindowForeground);
		    contentText.LineHeight = 22;
		    contentScroll.Content = contentText;
		    mainGrid.Children.Add(contentScroll);

		    // Content dictionary
		    Dictionary<string, string> helpContent = new Dictionary<string, string>
		    {
		        { "Getting Started",
		            "GETTING STARTED\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "Elemental Tracker monitors web pages for version changes, release dates, " +
		            "and other information by scanning page source code between user-defined markers.\n\n" +
		            "Basic Workflow:\n\n" +
		            "1. Create a Category\n" +
		            "   Right-click the category tree and select New Category to organise your tracks.\n\n" +
		            "2. Create a New Track\n" +
		            "   Click the New Track button in the toolbar, or right-click a category.\n\n" +
		            "3. Set the Track URL\n" +
		            "   Enter the web page URL that contains the version or release information.\n\n" +
		            "4. Download the Page Source\n" +
		            "   Click the Download button to fetch the page. The source appears in the Source tab.\n\n" +
		            "5. Define Start and Stop Strings\n" +
		            "   In the source view, find the text that surrounds the version number. " +
		            "Right-click to set the Start String (the text before the version) and the " +
		            "Stop String (the text after the version). The app extracts everything between " +
		            "these two markers as the \"latest version\".\n\n" +
		            "6. Save the Track\n" +
		            "   Click Save to store the track file. The extracted version is saved for comparison.\n\n" +
		            "7. Check for Updates\n" +
		            "   Use Batch Check to scan all tracks at once. Items that have changed since " +
		            "the last check are highlighted in the list."
		        },

		        { "Track Data Fields",
		            "TRACK DATA FIELDS\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "These fields store the core information about each tracked item.\n\n" +
		            "Track Name\n" +
		            "  The display name for this track. This is shown in the list view and is also " +
		            "used as the filename when saving. It does not need to match the software's " +
		            "actual name.\n\n" +
		            "Track URL\n" +
		            "  The web page to monitor. This is the page whose source code will be " +
		            "downloaded and searched for version information. Enter the full URL " +
		            "including https://.\n\n" +
		            "Download URL\n" +
		            "  An optional direct link to download the software. Supports version " +
		            "placeholders — see the Download URL Placeholders section for details.\n\n" +
		            "Version\n" +
		            "  The currently saved version number. This is compared against the latest " +
		            "detected version during a batch check. Use the Update Version button to " +
		            "copy the latest detected version into this field.\n\n" +
		            "Latest Version (auto-detected)\n" +
		            "  Displays the version extracted from the page source using the Start and " +
		            "Stop strings. This field is read-only and updates automatically when the " +
		            "source is downloaded.\n\n" +
		            "Release Date\n" +
		            "  The release date of the current version. Can be set manually, from the " +
		            "source view context menu, or automatically using Date Start/Stop strings.\n\n" +
		            "Publisher Name\n" +
		            "  The software publisher. Used for display and filtering in SPS categories.\n\n" +
		            "Suite Name\n" +
		            "  If the software belongs to a suite (e.g. Microsoft Office), enter the " +
		            "suite name here. Used for grouping in SPS categories."
		        },

		        { "Track Settings Fields",
		            "TRACK SETTINGS FIELDS\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "These fields control how the app extracts information from the page source.\n\n" +
		            "Version Start String\n" +
		            "  A unique string that appears in the page source immediately before the " +
		            "version number. The app searches for this text and begins reading after it.\n\n" +
		            "  You can use plain text or a regex pattern. Regex patterns must be wrapped " +
		            "in forward slashes, e.g.  /Version:\\s*/\n\n" +
		            "  Tip: Right-click text in the Source or Browser tab and select " +
		            "\"Set as Start String\" to set this quickly.\n\n" +
		            "Version Stop String\n" +
		            "  A unique string that appears immediately after the version number. The app " +
		            "stops reading when it encounters this text. Supports plain text or regex.\n\n" +
		            "  The extracted version is everything between the end of the Start String " +
		            "and the beginning of the Stop String.\n\n" +
		            "Release Date Start String\n" +
		            "  Works like the Version Start String but for extracting the release date. " +
		            "Set this to the text that appears just before the date on the page.\n\n" +
		            "Release Date Stop String\n" +
		            "  The text that appears just after the release date. The date is extracted " +
		            "between the Date Start and Date Stop strings.\n\n" +
		            "USING REGEX PATTERNS\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "Wrap your pattern in forward slashes to use regex:\n\n" +
		            "  /Version\\s*:\\s*/        matches \"Version:\" with flexible whitespace\n" +
		            "  /<span class=\"ver\">/    matches a specific HTML tag\n" +
		            "  /v\\d+\\.\\d+/            matches patterns like v1.0, v12.34\n\n" +
		            "Regex is useful when the surrounding text varies slightly between updates."
		        },

		        { "Download URL Placeholders",
		            "DOWNLOAD URL PLACEHOLDERS\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "The Download URL field supports special placeholders that are automatically " +
		            "replaced with the current version number when downloading.\n\n" +
		            "Available Placeholders:\n\n" +
		            "  {VERSION}\n" +
		            "    Inserts the version number as-is.\n" +
		            "    Example: 2.18 → 2.18\n\n" +
		            "  {VERSION_}\n" +
		            "    Replaces dots with underscores.\n" +
		            "    Example: 2.18 → 2_18\n\n" +
		            "  {VERSION-}\n" +
		            "    Replaces dots with hyphens.\n" +
		            "    Example: 2.18 → 2-18\n\n" +
		            "EXAMPLES\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "If the current version is 2.18:\n\n" +
		            "  URL Template:\n" +
		            "  https://example.com/downloads/App_{VERSION_}.exe\n\n" +
		            "  Resolves to:\n" +
		            "  https://example.com/downloads/App_2_18.exe\n\n" +
		            "  URL Template:\n" +
		            "  https://example.com/releases/{VERSION}/app-{VERSION-}.zip\n\n" +
		            "  Resolves to:\n" +
		            "  https://example.com/releases/2.18/app-2-18.zip\n\n" +
		            "You can combine multiple placeholders in a single URL. The placeholders " +
		            "are case-sensitive and must include the curly braces."
		        },

		        { "Toolbar Reference",
		            "TOOLBAR REFERENCE\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "MAIN TOOLBAR\n\n" +
		            "  • Save — save the current track\n" +
		            "  • Save All — save all modified tracks\n" +
		            "  • Check — check the selected track for updates\n" +
		            "  • Batch Check — check all tracks in the current category\n" +
		            "  • Stop — cancel a running batch check\n\n" +
		            "TRACK SETTINGS TOOLBAR  (vertical, left side)\n\n" +
		            "  • New Track — create a new track file in the current category\n" +
		            "  • Go to Version Start — scroll the source view to the Start String position\n" +
		            "  • Go to Version Stop — scroll the source view to the Stop String position\n" +
		            "  • Go to Date Start — scroll to the Date Start String position\n" +
		            "  • Go to Date Stop — scroll to the Date Stop String position\n" +
		            "  • Track Mode (</> or Aa) — toggle between HTML source mode and rendered " +
		            "text mode. HTML mode parses the raw source; Text mode extracts visible text only\n" +
		            "  • Download — download the page source for the current track\n" +
		            "  • Open in Browser — open the Track URL in the built-in browser tab\n" +
		            "  • Update Version — copy the latest detected version to the Version field\n" +
		            "  • Download File — download a file using the Download URL\n\n" +
		            "SOURCE VIEW TOOLBAR\n\n" +
		            "  • Search field — type to search within the page source\n" +
		            "  • Search Down / Up — find the next or previous match\n" +
		            "  • Font Size +/- — increase or decrease the source view font size\n\n" +
		            "WEB BROWSER TOOLBAR\n\n" +
		            "  • Back / Forward — browser navigation\n" +
		            "  • Home — navigate to the Track URL\n" +
		            "  • Zoom In / Out / Reset / Fit — control the browser zoom level\n" +
		            "  • Clear Cookies — remove cookies for the current site\n" +
		            "  • Clear All — clear all browsing data"
		        },

		        { "Status Icons",
		            "STATUS ICONS\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "The coloured circle in the Status column indicates the result of the " +
		            "last check:\n\n" +
		            "  ● Green (changed)\n" +
		            "    The latest version detected on the page is different from the saved " +
		            "version. An update is available.\n\n" +
		            "  ● Grey (unchanged)\n" +
		            "    The version on the page matches the saved version. No update.\n\n" +
		            "  ● Blue (new)\n" +
		            "    This track has been newly created and has not been checked yet.\n\n" +
		            "  ● Red (error)\n" +
		            "    An error occurred when trying to download or parse the page. This " +
		            "could be a network error, an invalid URL, or the Start/Stop strings " +
		            "were not found in the source.\n\n" +
		            "After a batch check, the category tree also shows a summary of how many " +
		            "items have changed within each category."
		        },

		        { "Batch Checking",
		            "BATCH CHECKING\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "Batch Check downloads and checks all tracks in the current category " +
		            "(or all categories) in one operation.\n\n" +
		            "How It Works:\n\n" +
		            "1. Click the Batch Check button in the main toolbar.\n" +
		            "2. The app downloads each track's URL and extracts the latest version.\n" +
		            "3. The status column updates for each track as it completes.\n" +
		            "4. A progress bar shows the overall progress.\n" +
		            "5. Click Stop to cancel a running batch check.\n\n" +
		            "After Checking:\n\n" +
		            "  • Items marked \"changed\" have a new version available.\n" +
		            "  • Click Update Version to accept the new version.\n" +
		            "  • Save the track to store the updated version.\n" +
		            "  • The category tree updates to show how many items changed.\n\n" +
		            "Tip: You can also check a single track by selecting it and clicking Check."
		        },

		        { "Source View",
		            "SOURCE VIEW\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "The Source tab displays the downloaded page source with syntax highlighting.\n\n" +
		            "HIGHLIGHTING\n\n" +
		            "  • Start String — highlighted in the Start String colour\n" +
		            "  • Extracted content — highlighted in the Info String colour (the version " +
		            "or date that will be extracted)\n" +
		            "  • Stop String — highlighted in the Stop String colour\n" +
		            "  • HTML tags, attributes, and values are syntax-coloured\n\n" +
		            "Both version and date start/stop regions are highlighted simultaneously " +
		            "so you can see both extractions at once.\n\n" +
		            "CONTEXT MENU\n\n" +
		            "Right-click selected text in the source view for quick actions:\n\n" +
		            "  • Copy — copy selected text to clipboard\n" +
		            "  • Set as Start String — use selected text as the Version Start String\n" +
		            "  • Set as Stop String — use selected text as the Version Stop String\n" +
		            "  • Set as Date Start String — use selected text as the Date Start String\n" +
		            "  • Set as Date Stop String — use selected text as the Date Stop String\n" +
		            "  • Set to Release Date — copy selected text to the Release Date field " +
		            "(appears when the selected text looks like a date)\n" +
		            "  • Set as Download URL — copy selected text to the Download URL field " +
		            "(appears when the selected text looks like a URL)\n\n" +
		            "TRACK MODE\n\n" +
		            "Use the Track Mode button (</> or Aa) to switch between:\n\n" +
		            "  • HTML mode (</>) — works with the raw HTML source. Best for most sites.\n" +
		            "  • Text mode (Aa) — extracts only the visible rendered text. Useful when " +
		            "the version appears in plain text but is hard to locate in raw HTML."
		        },

		        { "Web Browser",
		            "WEB BROWSER\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "The Browser tab provides a built-in web browser for viewing tracked pages.\n\n" +
		            "NAVIGATION\n\n" +
		            "  • The browser automatically loads the Track URL when you select a track.\n" +
		            "  • Use Back, Forward, and Home buttons to navigate.\n" +
		            "  • Type a URL in the address bar and press Enter to navigate directly.\n\n" +
		            "CONTEXT MENU\n\n" +
		            "Right-click in the browser for quick actions:\n\n" +
		            "  • Set Version Start String — set selected text as Start String\n" +
		            "  • Set Version Stop String — set selected text as Stop String\n" +
		            "  • Set Date Start String — set selected text as Date Start String\n" +
		            "  • Set Date Stop String — set selected text as Date Stop String\n" +
		            "  • Set Release Date — set selected text as Release Date (when it looks " +
		            "like a date)\n" +
		            "  • Set Download URL — set a link as the Download URL (when right-clicking " +
		            "a file download link)\n\n" +
		            "ZOOM\n\n" +
		            "  • Zoom In/Out — manually adjust the zoom level\n" +
		            "  • Fit — automatically fit the page width to the browser panel\n" +
		            "  • Reset — return to 100% zoom"
		        },

		        { "Themes",
		            "THEMES\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "Use the Theme tab to fully customise the appearance of the application.\n\n" +
		            "PRESET THEMES\n\n" +
		            "Select from built-in themes using the preset dropdown. Presets include " +
		            "various dark and light themes to suit your preference.\n\n" +
		            "CUSTOM COLOURS\n\n" +
		            "Click any colour swatch to open a colour picker and change that element's " +
		            "colour. Changes are applied immediately so you can preview them.\n\n" +
		            "Colour groups include:\n" +
		            "  • Window — main window background and foreground\n" +
		            "  • Menu — menu bar colours\n" +
		            "  • Toolbar — toolbar colours for main, track, and tab toolbars\n" +
		            "  • Category Tree — tree view colours including selection\n" +
		            "  • List View — item list colours including selection and hover\n" +
		            "  • Tabs — tab handle and content colours\n" +
		            "  • Source View — syntax highlighting colours\n" +
		            "  • Status Bar — status bar colours\n" +
		            "  • Buttons, Scrollbars, Checkboxes, and more\n\n" +
		            "SAVING & LOADING THEMES\n\n" +
		            "  • Save Theme — save your current colour scheme to a .theme file\n" +
		            "  • Load Theme — load a previously saved theme file\n" +
		            "  • Themes are stored as simple text files that can be shared with others."
		        },

		        { "App Settings",
		            "APP SETTINGS\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "The App Settings tab lets you configure application-wide settings.\n\n" +
		            "COLUMN SETTINGS\n\n" +
		            "  • Show/hide columns using the checkboxes\n" +
		            "  • Drag to reorder columns\n" +
		            "  • Adjust column widths\n" +
		            "  • Click Apply to update the list view\n" +
		            "  • Click Restore Defaults to reset column settings\n\n" +
		            "GENERAL SETTINGS\n\n" +
		            "  • Editor Path — path to an external text editor for viewing source files\n" +
		            "  • Default Publisher Name — automatically applied to new tracks\n" +
		            "  • Default Track Mode — choose whether new tracks default to HTML or Text mode\n" +
		            "  • SyMenu Path — path to a SyMenu installation for integration\n" +
		            "  • Publisher Filter — filter SPS categories by publisher name\n\n" +
		            "SUITE SELECTION\n\n" +
		            "  Select which software suites to include when working with SPS categories. " +
		            "This filters the items shown based on their Suite Name field."
		        },

		        { "SPS Categories",
		            "SPS CATEGORIES\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "SPS (Single-file Package Store) categories store all their track items " +
		            "in a single XML file rather than individual .track files.\n\n" +
		            "HOW THEY WORK\n\n" +
		            "  • An SPS category is detected when the folder contains an .xml file.\n" +
		            "  • All items are stored in one XML file within the category folder.\n" +
		            "  • The Publisher Filter setting can be used to filter displayed items.\n" +
		            "  • Suite selection controls which suites are shown.\n\n" +
		            "WHEN TO USE SPS\n\n" +
		            "  SPS categories are useful when you have a large number of items from a " +
		            "single source and want to manage them as a group rather than individual " +
		            "track files.\n\n" +
		            "SAVING\n\n" +
		            "  When you save in an SPS category, all items are written to the single " +
		            "XML file. The save operation updates the entire category at once."
		        },

		        { "Keyboard Shortcuts",
		            "KEYBOARD SHORTCUTS\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "  Ctrl+S          Save the current track\n" +
		            "  Ctrl+Shift+S    Save all modified tracks\n" +
		            "  Ctrl+N          Create a new track\n" +
		            "  Ctrl+D          Download page source\n" +
		            "  Ctrl+B          Open URL in browser tab\n" +
		            "  F5              Batch check current category\n" +
		            "  Escape          Stop a running batch check\n" +
		            "  Delete          Delete the selected track\n\n" +
		            "SOURCE VIEW\n\n" +
		            "  Ctrl+F          Focus the search field\n" +
		            "  F3              Find next match\n" +
		            "  Shift+F3        Find previous match\n\n" +
		            "Note: Some shortcuts may vary depending on your configuration. " +
		            "Hover over toolbar buttons to see their assigned shortcuts."
		        },

		        { "Tips & Tricks",
		            "TIPS & TRICKS\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            "SELECTING START/STOP STRINGS\n\n" +
		            "  • Choose text that is unique on the page and unlikely to change between " +
		            "updates. HTML tag attributes, IDs, and class names are often good choices.\n" +
		            "  • Avoid selecting text that includes the version number itself — the " +
		            "Start String should end just before the version, and the Stop String " +
		            "should begin just after it.\n" +
		            "  • Use the Go To buttons to verify your strings are matching correctly.\n" +
		            "  • The position text next to each label shows \"pos: N\" when found, or " +
		            "\"NOT FOUND\" if the string doesn't match.\n\n" +
		            "REGEX TIPS\n\n" +
		            "  • Use /pattern/ syntax for flexible matching.\n" +
		            "  • \\s* matches optional whitespace (spaces, tabs, newlines).\n" +
		            "  • .* matches any characters (greedy).\n" +
		            "  • .*? matches any characters (non-greedy / shortest match).\n\n" +
		            "GENERAL TIPS\n\n" +
		            "  • Use the checkbox column to select multiple items for batch operations.\n" +
		            "  • Right-click items in the list for context menu options.\n" +
		            "  • Track files (.track) are simple text files that can be backed up, " +
		            "edited, or shared.\n" +
		            "  • The source view highlights both version and date strings simultaneously.\n" +
		            "  • Right-click in both the Source tab and Browser tab to quickly set " +
		            "Start/Stop strings from selected text.\n" +
		            "  • Use Track Mode to switch between raw HTML and rendered text when " +
		            "the version is hard to find in the source.\n" +
		            "  • After a batch check, look at the category tree for a quick summary " +
		            "of which categories have updates."
		        },

		        { "About",
		            "ABOUT " + AppInfo.AppName.ToUpper() + "\n" +
		            "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
		            AppInfo.AppName + " is a desktop application for tracking software versions " +
		            "and release information from web pages.\n\n" +
		            "Version: " + version + "\n\n" +
		            "© 2026 sl23. All rights reserved."
		        }
		    };

		    // Handle navigation selection
		    navList.SelectionChanged += (s, ev) =>
		    {
		        if (navList.SelectedItem is string selectedSection && helpContent.ContainsKey(selectedSection))
		        {
		            contentText.Text = helpContent[selectedSection];
		            contentScroll.ScrollToTop();
		        }
		    };

		    // Select the first item by default
		    navList.SelectedIndex = 0;

		    aboutWindow.Content = mainGrid;
		    aboutWindow.ShowDialog();
		}

        // ============================
        // Browser + Download
        // ============================

        private async void ReloadSourceForCurrentMode()
        {
            if (currentTrackItem == null) return;

            if (currentTrackItem.TrackMode == "text")
            {
                try
                {
                    if (webView.CoreWebView2 != null)
                    {
                        // Ensure cookie popups are blocked before extracting text
                        if (blockCookiePopups)
                            await InjectCookiePopupBlocker();

                        await System.Threading.Tasks.Task.Delay(500);

                        string text = await webView.CoreWebView2.ExecuteScriptAsync(
                            "document.body.innerText");

                        if (text != null && text.StartsWith("\"") && text.EndsWith("\""))
                        {
                            text = System.Text.Json.JsonSerializer.Deserialize<string>(text);
                        }

                        if (!string.IsNullOrEmpty(text))
                        {
                            currentSource = text;
                            DisplaySource(currentSource);
                            statusFile.Text = "Source: rendered text mode";
                        }
                        else
                        {
                            currentSource = "";
                            sourceView.Document.Blocks.Clear();
                            statusFile.Text = "No rendered text available — navigate to a page first";
                        }
                    }
                    else
                    {
                        statusFile.Text = "WebView2 not ready — navigate to a page first";
                    }
                }
                catch (Exception ex)
                {
                    statusFile.Text = "Error getting rendered text: " + ex.Message;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(currentTrackItem.TrackURL))
                {
                    AutoDownloadSource(currentTrackItem.TrackURL);
                }
            }
        }

        /// <summary>
        /// Downloads page source using WebView2 as a fallback when HttpClient fails (e.g. Cloudflare, sgcaptcha).
        /// WebView2 is a real browser and can pass JavaScript challenges and meta-refresh redirects.
        /// </summary>
        private async System.Threading.Tasks.Task<string> DownloadViaWebView(string url)
        {
            try
            {
                await webView.EnsureCoreWebView2Async();

                var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
                int navigationCount = 0;
                const int maxNavigations = 10;

                EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs> handler = null;
                handler = async (s, e) =>
                {
                    try
                    {
                        navigationCount++;

                        // Wait for page to settle
                        await System.Threading.Tasks.Task.Delay(2000);

                        // Check document title and source for challenges
                        string title = webView.CoreWebView2.DocumentTitle ?? "";
                        string currentUrl = webView.CoreWebView2.Source ?? "";

                        // Cloudflare challenge detection
                        bool isCloudflare = title.Contains("Just a moment") ||
                                            title.Contains("Checking your browser") ||
                                            title.Contains("Attention Required");

                        // Get the page source
                        string source = await webView.CoreWebView2.ExecuteScriptAsync(
                            "document.documentElement.outerHTML");

                        if (!string.IsNullOrEmpty(source) && source != "null")
                        {
                            source = System.Text.Json.JsonSerializer.Deserialize<string>(source);
                        }

                        // Check if this is still a challenge/captcha page
                        bool isChallenge = isCloudflare || IsChallengePage(source);

                        if (isChallenge && navigationCount < maxNavigations)
                        {
                            // Still on a challenge page — wait longer and let the
                            // meta-refresh or JS redirect complete; don't unhook,
                            // the next NavigationCompleted will fire after redirect
                            statusFile.Text = "Challenge redirect detected, waiting... (nav " +
                                navigationCount + ")";
                            await System.Threading.Tasks.Task.Delay(3000);

                            // If no new navigation fires (meta-refresh might not trigger one),
                            // re-check the source after the extra wait
                            string retrySource = await webView.CoreWebView2.ExecuteScriptAsync(
                                "document.documentElement.outerHTML");
                            if (!string.IsNullOrEmpty(retrySource) && retrySource != "null")
                            {
                                retrySource = System.Text.Json.JsonSerializer.Deserialize<string>(retrySource);
                            }

                            if (!IsChallengePage(retrySource) && retrySource.Length > 512)
                            {
                                webView.CoreWebView2.NavigationCompleted -= handler;
                                tcs.TrySetResult(retrySource);
                            }
                            // Otherwise keep waiting for the next NavigationCompleted
                            return;
                        }

                        webView.CoreWebView2.NavigationCompleted -= handler;

                        if (!string.IsNullOrEmpty(source) && source.Length > 100 && !IsChallengePage(source))
                        {
                            tcs.TrySetResult(source);
                        }
                        else
                        {
                            tcs.TrySetResult(null);
                        }
                    }
                    catch (Exception)
                    {
                        webView.CoreWebView2.NavigationCompleted -= handler;
                        tcs.TrySetResult(null);
                    }
                };

                webView.CoreWebView2.NavigationCompleted += handler;
                webView.CoreWebView2.Navigate(url);

                // Timeout after 45 seconds (longer to allow for challenge redirects)
                var timeoutTask = System.Threading.Tasks.Task.Delay(45000);
                var completedTask = await System.Threading.Tasks.Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    webView.CoreWebView2.NavigationCompleted -= handler;

                    // Last-ditch: grab whatever is in the WebView now
                    try
                    {
                        string lastSource = await webView.CoreWebView2.ExecuteScriptAsync(
                            "document.documentElement.outerHTML");
                        if (!string.IsNullOrEmpty(lastSource) && lastSource != "null")
                        {
                            lastSource = System.Text.Json.JsonSerializer.Deserialize<string>(lastSource);
                            if (!IsChallengePage(lastSource) && lastSource.Length > 100)
                                return lastSource;
                        }
                    }
                    catch (Exception) { }

                    return null;
                }

                return await tcs.Task;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void SetupHttpHeaders(string url = null)
        {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    Uri uri = new Uri(url);
                    httpClient.DefaultRequestHeaders.Add("Referer", uri.GetLeftPart(UriPartial.Authority) + "/");
                }
                catch (Exception) { }
            }
        }

        /// <summary>
        /// Detects bot challenge / captcha redirect pages that some servers return
        /// instead of the real content (e.g. sgcaptcha, Cloudflare, etc.)
        /// </summary>
        private static bool IsChallengePage(string source)
        {
            if (string.IsNullOrEmpty(source))
                return false;

            // Very short response is suspicious
            if (source.Length < 512)
            {
                string lower = source.ToLowerInvariant();

                // sgcaptcha redirect (as seen with mutools.com)
                if (lower.Contains("sgcaptcha"))
                    return true;

                // Generic meta-refresh to a challenge page
                if (lower.Contains("meta") && lower.Contains("http-equiv=\"refresh\"") &&
                    (lower.Contains("captcha") || lower.Contains("challenge")))
                    return true;
            }

            // Cloudflare challenges
            string lowerFull = source.Length > 2000 ? source.Substring(0, 2000).ToLowerInvariant() : source.ToLowerInvariant();
            if (lowerFull.Contains("just a moment") && lowerFull.Contains("cloudflare"))
                return true;
            if (lowerFull.Contains("checking your browser"))
                return true;
            if (lowerFull.Contains("attention required") && lowerFull.Contains("cloudflare"))
                return true;

            return false;
        }

        private void OpenBrowser_Click(object sender, RoutedEventArgs e)
        {
            string url = editTrackURL.Text;

            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(url)
                        { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error opening browser: " + ex.Message,
                        AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void DownloadPage_Click(object sender, RoutedEventArgs e)
        {
            string url = editTrackURL.Text;

            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Enter a Track URL first.", AppInfo.ShortName,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
                editTrackURL.Text = url;
            }

			// Switch to Source tab
			rightTabs.SelectedIndex = 0;

            statusFile.Text = "Downloading: " + url;
            statusProgress.IsIndeterminate = true;

            try
            {
                SetupHttpHeaders(url);

                currentSource = await httpClient.GetStringAsync(url);

                // Detect bot challenge / captcha redirect pages
                if (IsChallengePage(currentSource))
                {
                    statusFile.Text = "Bot challenge detected, using WebView2...";
                    string webViewSource = await DownloadViaWebView(url);
                    if (!string.IsNullOrEmpty(webViewSource) && !IsChallengePage(webViewSource))
                    {
                        currentSource = webViewSource;
                    }
                }

                long downloadBytes = System.Text.Encoding.UTF8.GetByteCount(currentSource);

                DisplaySource(currentSource);

                // Apply tracking check
                TrackItem selected = currentTrackItem;
                if (selected != null)
                {
                    selected.TrackName = editName.Text;
                    selected.TrackURL = editTrackURL.Text;
                    selected.StartString = editStartString.Text;
                    selected.StopString = editStopString.Text;
                    selected.DownloadURL = editDownloadURL.Text;
                    selected.Version = editVersion.Text;

                    CheckResult result = selected.ApplyCheck(currentSource, downloadBytes);

					if (!string.IsNullOrEmpty(selected.FilePath))
						selected.SaveToFile();

                    // Refresh list but preserve selection
                    int selectedIndex = currentItems.IndexOf(selected);
                    suppressAutoDownload = true;
                    itemList.ItemsSource = null;
                    itemList.ItemsSource = currentItems;
					if (selectedIndex >= 0)
					{
					    itemList.SelectedIndex = selectedIndex;
					    itemList.ScrollIntoView(itemList.SelectedItem);
					}
                    suppressAutoDownload = false;

                    // Update UI fields immediately
                    editVersion.Text = selected.Version;

                    // Only show latest version if we actually have start/stop strings
                    if (string.IsNullOrEmpty(selected.StartString) ||
                        string.IsNullOrEmpty(selected.StopString))
                    {
                        selected.LatestVersion = "";
                    }
                    UpdateVersionDisplay();
                    statusFile.Text = "Checked: " + result.TrackName +
                        " — " + result.Status.ToUpper() + " — " + result.Note;
                    editReleaseDate.Text = selected.ReleaseDate;
                }
                else
                {
                    statusFile.Text = "Downloaded: " + url +
                        " (" + currentSource.Length.ToString("N0") + " chars)" +
                        " — Save the track first to apply checks";
                }

                // Stop progress bar before WebView navigation (which can take time)
                statusProgress.IsIndeterminate = false;
                statusProgress.Value = 0;

                // Navigate WebView
                try
                {
                    await webView.EnsureCoreWebView2Async();
                    webView.CoreWebView2.Navigate(url);
                }
                catch (Exception)
                {
                }
            }
            catch (Exception ex)
            {
                // HttpClient failed — try WebView2 fallback
                statusFile.Text = "HttpClient blocked (" + ex.Message + "), trying WebView2...";

                try
                {
                    string source = await DownloadViaWebView(url);
                    if (!string.IsNullOrEmpty(source))
                    {
                        currentSource = source;
                        long downloadBytes = System.Text.Encoding.UTF8.GetByteCount(currentSource);
                        DisplaySource(currentSource);

                        TrackItem selected = currentTrackItem;
                        if (selected != null)
                        {
                            selected.TrackName = editName.Text;
                            selected.TrackURL = editTrackURL.Text;
                            selected.StartString = editStartString.Text;
                            selected.StopString = editStopString.Text;
                            selected.DownloadURL = editDownloadURL.Text;
                            selected.Version = editVersion.Text;

                            CheckResult result = selected.ApplyCheck(currentSource, downloadBytes);
                            if (!string.IsNullOrEmpty(selected.FilePath))
                                selected.SaveToFile();

                            int selectedIndex = currentItems.IndexOf(selected);
                            suppressAutoDownload = true;
                            itemList.ItemsSource = null;
                            itemList.ItemsSource = currentItems;
							if (selectedIndex >= 0)
							{
							    itemList.SelectedIndex = selectedIndex;
							    itemList.ScrollIntoView(itemList.SelectedItem);
							}
                            suppressAutoDownload = false;

                            editVersion.Text = selected.Version;
                            if (string.IsNullOrEmpty(selected.StartString) ||
                                string.IsNullOrEmpty(selected.StopString))
                            {
                                selected.LatestVersion = "";
                            }
                            UpdateVersionDisplay();
                            statusFile.Text = "Checked (WebView2): " + result.TrackName +
                                " — " + result.Status.ToUpper() + " — " + result.Note;
                            editReleaseDate.Text = selected.ReleaseDate;
                        }
                        else
                        {
                            statusFile.Text = "Downloaded via WebView2: " + url;
                        }
                    }
                    else
                    {
                        TrackItem selected = currentTrackItem;
                        if (selected != null)
                        {
                            selected.TrackStatus = "error";
                            selected.LastChecked = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                            if (!string.IsNullOrEmpty(selected.FilePath))
                                selected.SaveToFile();
                            itemList.ItemsSource = null;
                            itemList.ItemsSource = currentItems;
                        }
                        MessageBox.Show("Download failed:\n" + ex.Message +
                            "\n\nWebView2 fallback also failed.",
                            AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
                        statusFile.Text = "Download failed";
                    }
                }
                catch (Exception ex2)
                {
                    TrackItem selected = currentTrackItem;
                    if (selected != null)
                    {
                        selected.TrackStatus = "error";
                        selected.LastChecked = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                            if (!string.IsNullOrEmpty(selected.FilePath))
                                selected.SaveToFile();
                        itemList.ItemsSource = null;
                        itemList.ItemsSource = currentItems;
                    }
                    MessageBox.Show("Download failed:\n" + ex.Message +
                        "\n\nWebView2 fallback error:\n" + ex2.Message,
                        AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
                    statusFile.Text = "Download failed";
                }
            }
            finally
            {
                statusProgress.IsIndeterminate = false;
                statusProgress.Value = 0;
            }
        }

		private void FindHighlightPositions(string normalizedSource, string startStr, string stopStr,
		    out int startIdx, out int startLen, out int stopIdx, out int stopLen)
		{
		    startIdx = -1; startLen = 0; stopIdx = -1; stopLen = 0;

		    if (string.IsNullOrEmpty(startStr)) return;

		    if (TrackItem.IsRegexPattern(startStr))
		    {
		        var match = TrackItem.FindRegexMatch(normalizedSource, startStr, 0);
		        if (match != null && match.Success)
		        {
		            startIdx = match.Index;
		            startLen = match.Length;
		        }
		    }
		    else
		    {
		        string ns = startStr.Replace("\r\n", "\n").Replace("\r", "\n");
		        startIdx = normalizedSource.IndexOf(ns, StringComparison.Ordinal);
		        startLen = ns.Length;

		        if (startIdx < 0)
		        {
		            string cs = NormalizeWhitespace(normalizedSource);
		            string cst = NormalizeWhitespace(ns);
		            int ci = cs.IndexOf(cst, StringComparison.Ordinal);
		            if (ci >= 0)
		            {
		                startIdx = MapNormalizedToRaw(normalizedSource, ci);
		                int endRaw = MapNormalizedToRaw(normalizedSource, ci + cst.Length);
		                startLen = endRaw - startIdx;
		            }
		        }
		    }

		    if (startIdx < 0 || string.IsNullOrEmpty(stopStr)) return;

		    int searchAfter = startIdx + startLen;

		    if (TrackItem.IsRegexPattern(stopStr))
		    {
		        var match = TrackItem.FindRegexMatch(normalizedSource, stopStr, searchAfter);
		        if (match != null && match.Success)
		        {
		            stopIdx = match.Index;
		            stopLen = match.Length;
		        }
		    }
		    else
		    {
		        string ns = stopStr.Replace("\r\n", "\n").Replace("\r", "\n");
		        stopIdx = normalizedSource.IndexOf(ns, searchAfter, StringComparison.Ordinal);
		        stopLen = ns.Length;

		        if (stopIdx < 0)
		        {
		            string cs = NormalizeWhitespace(normalizedSource);
		            string cst = NormalizeWhitespace(ns);
		            string collapsedBefore = NormalizeWhitespace(normalizedSource.Substring(0, searchAfter));
		            int collapsedSearchAfter = collapsedBefore.Length;
		            int ci = cs.IndexOf(cst, collapsedSearchAfter, StringComparison.Ordinal);
		            if (ci >= 0)
		            {
		                stopIdx = MapNormalizedToRaw(normalizedSource, ci);
		                int endRaw = MapNormalizedToRaw(normalizedSource, ci + cst.Length);
		                stopLen = endRaw - stopIdx;
		            }
		        }
		    }
		}

		private void DisplaySource(string source)
		{
		    sourceView.Document.Blocks.Clear();

		    if (string.IsNullOrEmpty(source))
		        return;

		    Paragraph para = new Paragraph();
		    para.FontFamily = new FontFamily("Consolas");
		    para.FontSize = sourceView.FontSize;

		    string startStr = editStartString.Text ?? "";
		    string stopStr = editStopString.Text ?? "";
		    string dateStartStr = editReleaseDateStartString.Text ?? "";
		    string dateStopStr = editReleaseDateStopString.Text ?? "";

		    try
		    {
		        string normalizedSource = source.Replace("\r\n", "\n").Replace("\r", "\n");

		        // Find version start/stop positions
		        int vStartIdx, vStartLen, vStopIdx, vStopLen;
		        FindHighlightPositions(normalizedSource, startStr, stopStr,
		            out vStartIdx, out vStartLen, out vStopIdx, out vStopLen);

		        // Find date start/stop positions
		        int dStartIdx, dStartLen, dStopIdx, dStopLen;
		        FindHighlightPositions(normalizedSource, dateStartStr, dateStopStr,
		            out dStartIdx, out dStartLen, out dStopIdx, out dStopLen);

		        // Build highlight regions: (start, end, colour)
		        var regions = new List<(int start, int end, SolidColorBrush color)>();

		        // Version regions
		        if (vStartIdx >= 0 && vStartLen > 0 && vStartIdx + vStartLen <= normalizedSource.Length)
		        {
		            regions.Add((vStartIdx, vStartIdx + vStartLen,
		                new SolidColorBrush(currentTheme.SourceStartStringColor)));

		            if (vStopIdx >= 0 && vStopLen > 0 && vStopIdx + vStopLen <= normalizedSource.Length)
		            {
		                regions.Add((vStartIdx + vStartLen, vStopIdx,
		                    new SolidColorBrush(currentTheme.SourceInfoStringColor)));
		                regions.Add((vStopIdx, vStopIdx + vStopLen,
		                    new SolidColorBrush(currentTheme.SourceStopStringColor)));
		            }
		        }

		        // Date regions
		        if (dStartIdx >= 0 && dStartLen > 0 && dStartIdx + dStartLen <= normalizedSource.Length)
		        {
		            regions.Add((dStartIdx, dStartIdx + dStartLen,
		                new SolidColorBrush(currentTheme.SourceStartStringColor)));

		            if (dStopIdx >= 0 && dStopLen > 0 && dStopIdx + dStopLen <= normalizedSource.Length)
		            {
		                regions.Add((dStartIdx + dStartLen, dStopIdx,
		                    new SolidColorBrush(currentTheme.SourceInfoStringColor)));
		                regions.Add((dStopIdx, dStopIdx + dStopLen,
		                    new SolidColorBrush(currentTheme.SourceStopStringColor)));
		            }
		        }

		        // Sort by start position
		        regions.Sort((a, b) => a.start.CompareTo(b.start));

		        // Render source with highlighting
		        if (regions.Count > 0)
		        {
		            int pos = 0;
		            foreach (var region in regions)
		            {
		                if (region.start < pos) continue; // skip overlapping
		                if (region.end <= region.start) continue; // skip empty

		                // Unhighlighted text before this region
		                if (region.start > pos)
		                {
		                    foreach (Run r in CreateSyntaxHighlightedRuns(
		                        normalizedSource.Substring(pos, region.start - pos)))
		                        para.Inlines.Add(r);
		                }

		                // Highlighted region
		                Run highlightRun = new Run(
		                    normalizedSource.Substring(region.start, region.end - region.start));
		                highlightRun.Foreground = region.color;
		                highlightRun.FontWeight = FontWeights.Bold;
		                para.Inlines.Add(highlightRun);

		                pos = region.end;
		            }

		            // Remaining text after last region
		            if (pos < normalizedSource.Length)
		            {
		                foreach (Run r in CreateSyntaxHighlightedRuns(
		                    normalizedSource.Substring(pos)))
		                    para.Inlines.Add(r);
		            }
		        }
		        else
		        {
		            // No highlighting — plain syntax colouring
		            foreach (Run r in CreateSyntaxHighlightedRuns(normalizedSource))
		                para.Inlines.Add(r);
		        }

		        // Update version position texts
		        if (vStartIdx >= 0 && vStopIdx >= 0)
		        {
		            startPositionText.Text = "pos: " + vStartIdx;
		            startPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMatchColor);
		            stopPositionText.Text = "pos: " + vStopIdx;
		            stopPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMatchColor);
		        }
		        else if (vStartIdx >= 0)
		        {
		            startPositionText.Text = "pos: " + vStartIdx;
		            startPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMatchColor);
		            stopPositionText.Text = !string.IsNullOrEmpty(stopStr) ? "NOT FOUND" : "";
		            stopPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMismatchColor);
		        }
		        else if (!string.IsNullOrEmpty(startStr))
		        {
		            startPositionText.Text = "NOT FOUND";
		            startPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMismatchColor);
		            stopPositionText.Text = "";
		        }
		        else
		        {
		            startPositionText.Text = "";
		            stopPositionText.Text = "";
		        }

		        // Update date position texts
		        if (dStartIdx >= 0 && dStopIdx >= 0)
		        {
		            dateStartPositionText.Text = "pos: " + dStartIdx;
		            dateStartPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMatchColor);
		            dateStopPositionText.Text = "pos: " + dStopIdx;
		            dateStopPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMatchColor);
		        }
		        else if (dStartIdx >= 0)
		        {
		            dateStartPositionText.Text = "pos: " + dStartIdx;
		            dateStartPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMatchColor);
		            dateStopPositionText.Text = !string.IsNullOrEmpty(dateStopStr) ? "NOT FOUND" : "";
		            dateStopPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMismatchColor);
		        }
		        else if (!string.IsNullOrEmpty(dateStartStr))
		        {
		            dateStartPositionText.Text = "NOT FOUND";
		            dateStartPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMismatchColor);
		            dateStopPositionText.Text = "";
		        }
		        else
		        {
		            dateStartPositionText.Text = "";
		            dateStopPositionText.Text = "";
		        }

		        // Extract version from version track block
		        if (vStartIdx >= 0 && vStopIdx >= 0 && vStopIdx + vStopLen <= normalizedSource.Length)
		        {
		            int vEnd = vStopIdx + vStopLen;
		            string trackBlock = normalizedSource.Substring(vStartIdx, vEnd - vStartIdx);
		            string extractedVersion = TrackItem.ExtractVersion(trackBlock);
		            if (!string.IsNullOrEmpty(extractedVersion) && currentTrackItem != null)
		            {
		                currentTrackItem.LatestVersion = extractedVersion;
		                UpdateVersionDisplay();
		            }
		        }
		    }
		    catch (Exception ex)
		    {
		        para.Inlines.Clear();
		        try
		        {
		            foreach (Run r in CreateSyntaxHighlightedRuns(source))
		                para.Inlines.Add(r);
		        }
		        catch (Exception)
		        {
		            Run fallbackRun = new Run(source);
		            fallbackRun.Foreground = new SolidColorBrush(currentTheme.SourceTagColor);
		            para.Inlines.Add(fallbackRun);
		        }

		        startPositionText.Text = "Error: " + ex.Message;
		        startPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMismatchColor);
		        stopPositionText.Text = "";
		        dateStartPositionText.Text = "";
		        dateStopPositionText.Text = "";
		    }

		    sourceView.Document.Blocks.Add(para);

		    // Scroll to the version start string position
		    if (!string.IsNullOrEmpty(startStr))
		    {
		        try
		        {
		            string normalizedForScroll = source.Replace("\r\n", "\n").Replace("\r", "\n");
		            int scrollIdx = -1;

		            if (TrackItem.IsRegexPattern(startStr))
		            {
		                var match = TrackItem.FindRegexMatch(normalizedForScroll, startStr, 0);
		                if (match != null && match.Success)
		                    scrollIdx = match.Index;
		            }
		            else
		            {
		                string normalizedStartForScroll = startStr.Replace("\r\n", "\n").Replace("\r", "\n");
		                scrollIdx = normalizedForScroll.IndexOf(normalizedStartForScroll,
		                    StringComparison.Ordinal);

		                if (scrollIdx < 0)
		                {
		                    string cs = NormalizeWhitespace(normalizedForScroll);
		                    string cst = NormalizeWhitespace(normalizedStartForScroll);
		                    int ci = cs.IndexOf(cst, StringComparison.Ordinal);
		                    if (ci >= 0)
		                        scrollIdx = MapNormalizedToRaw(normalizedForScroll, ci);
		                }
		            }

		            if (scrollIdx >= 0)
		            {
		                ScrollSourceViewToPosition(scrollIdx);
		            }
		        }
		        catch (Exception) { }
		    }
		}

		private async void GoToStartString_Click(object sender, RoutedEventArgs e)
		{
		    if (string.IsNullOrEmpty(editStartString.Text))
		    {
		        statusFile.Text = "Start String is empty";
		        return;
		    }

		    // If no source loaded yet, download it first
		    if (string.IsNullOrEmpty(currentSource) && currentTrackItem != null && !string.IsNullOrWhiteSpace(currentTrackItem.TrackURL))
		    {
		        statusFile.Text = "Downloading source...";
		        string url = currentTrackItem.TrackURL;
		        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
		            url = "https://" + url;

		        try
		        {
		            SetupHttpHeaders(url);
		            currentSource = await httpClient.GetStringAsync(url);
		        }
		        catch (Exception)
		        {
		            statusFile.Text = "Failed to download source";
		            return;
		        }
		    }

		    if (string.IsNullOrEmpty(currentSource))
		    {
		        statusFile.Text = "No source loaded";
		        return;
		    }

		    // Display source with highlighting
		    DisplaySource(currentSource);

		    // Switch to Source tab AFTER source is displayed
		    rightTabs.SelectedIndex = 0;

		    // Now scroll to the start position
		    string normalizedSource = currentSource.Replace("\r\n", "\n").Replace("\r", "\n");
		    string searchStr = editStartString.Text;
		    int idx = -1;

		    if (TrackItem.IsRegexPattern(searchStr))
		    {
		        var match = TrackItem.FindRegexMatch(normalizedSource, searchStr, 0);
		        if (match != null && match.Success)
		            idx = match.Index;
		    }
		    else
		    {
		        string normalizedSearch = searchStr.Replace("\r\n", "\n").Replace("\r", "\n");
		        idx = normalizedSource.IndexOf(normalizedSearch, StringComparison.Ordinal);

		        if (idx < 0)
		        {
		            string cs = NormalizeWhitespace(normalizedSource);
		            string cst = NormalizeWhitespace(normalizedSearch);
		            int ci = cs.IndexOf(cst, StringComparison.Ordinal);
		            if (ci >= 0)
		                idx = MapNormalizedToRaw(normalizedSource, ci);
		        }
		    }

		    if (idx < 0)
		    {
		        statusFile.Text = "Start String not found in source";
		        startPositionText.Text = "NOT FOUND";
		        startPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMismatchColor);
		        return;
		    }

		    startPositionText.Text = "pos: " + idx;
		    startPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMatchColor);

		    // Delay slightly to let the tab render before scrolling
		    await System.Threading.Tasks.Task.Delay(100);
		    ScrollSourceViewToPosition(idx);

		    string regexNote = TrackItem.IsRegexPattern(searchStr) ? " (regex)" : "";
		    statusFile.Text = "Start String found at position " + idx + regexNote;
		}

        private async void GoToStopString_Click(object sender, RoutedEventArgs e)
        {
		    // If no source loaded yet, download it first
		    if (string.IsNullOrEmpty(currentSource) && currentTrackItem != null && !string.IsNullOrWhiteSpace(currentTrackItem.TrackURL))
		    {
		        statusFile.Text = "Downloading source...";
		        string url = currentTrackItem.TrackURL;
		        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
		            url = "https://" + url;

		        try
		        {
		            SetupHttpHeaders(url);
		            currentSource = await httpClient.GetStringAsync(url);
		        }
		        catch (Exception)
		        {
		            statusFile.Text = "Failed to download source";
		            return;
		        }
		    }

            if (string.IsNullOrEmpty(currentSource) || string.IsNullOrEmpty(editStopString.Text))
            {
                statusFile.Text = "No source loaded or Stop String is empty";
                return;
            }

            // Re-display source (this highlights and sets position texts)
            DisplaySource(currentSource);

		    // Switch to Source tab AFTER source is displayed
		    rightTabs.SelectedIndex = 0;

            // Now scroll to the stop position
            string normalizedSource = currentSource.Replace("\r\n", "\n").Replace("\r", "\n");
            string startStr = editStartString.Text ?? "";
            string stopStr = editStopString.Text;
            int idx = -1;

            // Find start first so we search for stop after it
            int searchAfter = 0;
            if (!string.IsNullOrEmpty(startStr))
            {
                if (TrackItem.IsRegexPattern(startStr))
                {
                    var match = TrackItem.FindRegexMatch(normalizedSource, startStr, 0);
                    if (match != null && match.Success)
                        searchAfter = match.Index + match.Length;
                }
                else
                {
                    string ns = startStr.Replace("\r\n", "\n").Replace("\r", "\n");
                    int si = normalizedSource.IndexOf(ns, StringComparison.Ordinal);
                    if (si >= 0)
                        searchAfter = si + ns.Length;
                    else
                    {
                        string cs = NormalizeWhitespace(normalizedSource);
                        string cst = NormalizeWhitespace(ns);
                        int ci = cs.IndexOf(cst, StringComparison.Ordinal);
                        if (ci >= 0)
                        {
                            int rawStart = MapNormalizedToRaw(normalizedSource, ci);
                            int rawEnd = MapNormalizedToRaw(normalizedSource, ci + cst.Length);
                            searchAfter = rawEnd;
                        }
                    }
                }
            }

            if (TrackItem.IsRegexPattern(stopStr))
            {
                var match = TrackItem.FindRegexMatch(normalizedSource, stopStr, searchAfter);
                if (match != null && match.Success)
                    idx = match.Index;
            }
            else
            {
                string normalizedSearch = stopStr.Replace("\r\n", "\n").Replace("\r", "\n");
                idx = normalizedSource.IndexOf(normalizedSearch, searchAfter, StringComparison.Ordinal);

                if (idx < 0)
                {
                    string cs = NormalizeWhitespace(normalizedSource);
                    string cst = NormalizeWhitespace(normalizedSearch);
                    string collapsedBefore = NormalizeWhitespace(normalizedSource.Substring(0, searchAfter));
                    int collapsedSearchAfter = collapsedBefore.Length;

                    int ci = cs.IndexOf(cst, collapsedSearchAfter, StringComparison.Ordinal);
                    if (ci >= 0)
                        idx = MapNormalizedToRaw(normalizedSource, ci);
                }
            }

            if (idx < 0)
            {
                statusFile.Text = "Stop String not found in source";
                stopPositionText.Text = "NOT FOUND";
                stopPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMismatchColor);
                return;
            }

            stopPositionText.Text = "pos: " + idx;
            stopPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMatchColor);
            ScrollSourceViewToPosition(idx);

            string regexNote = TrackItem.IsRegexPattern(stopStr) ? " (regex)" : "";
            statusFile.Text = "Stop String found at position " + idx + regexNote;
        }

		private async void GoToDateStartString_Click(object sender, RoutedEventArgs e)
		{
		    if (string.IsNullOrEmpty(editReleaseDateStartString.Text))
		    {
		        statusFile.Text = "Release Date Start String is empty";
		        return;
		    }

		    if (string.IsNullOrEmpty(currentSource) && currentTrackItem != null && !string.IsNullOrWhiteSpace(currentTrackItem.TrackURL))
		    {
		        statusFile.Text = "Downloading source...";
		        string url = currentTrackItem.TrackURL;
		        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
		            url = "https://" + url;

		        try
		        {
		            SetupHttpHeaders(url);
		            currentSource = await httpClient.GetStringAsync(url);
		        }
		        catch (Exception)
		        {
		            statusFile.Text = "Failed to download source";
		            return;
		        }
		    }

		    if (string.IsNullOrEmpty(currentSource))
		    {
		        statusFile.Text = "No source loaded";
		        return;
		    }

			DisplaySource(currentSource);
			rightTabs.SelectedIndex = 0;

		    string normalizedSource = currentSource.Replace("\r\n", "\n").Replace("\r", "\n");
		    string searchStr = editReleaseDateStartString.Text;
		    int idx = -1;

		    if (TrackItem.IsRegexPattern(searchStr))
		    {
		        var match = TrackItem.FindRegexMatch(normalizedSource, searchStr, 0);
		        if (match != null && match.Success)
		            idx = match.Index;
		    }
		    else
		    {
		        string normalizedSearch = searchStr.Replace("\r\n", "\n").Replace("\r", "\n");
		        idx = normalizedSource.IndexOf(normalizedSearch, StringComparison.Ordinal);

		        if (idx < 0)
		        {
		            string cs = NormalizeWhitespace(normalizedSource);
		            string cst = NormalizeWhitespace(normalizedSearch);
		            int ci = cs.IndexOf(cst, StringComparison.Ordinal);
		            if (ci >= 0)
		                idx = MapNormalizedToRaw(normalizedSource, ci);
		        }
		    }

		    if (idx < 0)
		    {
				statusFile.Text = "Release Date Start String not found in source";
				dateStartPositionText.Text = "NOT FOUND";
				dateStartPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMismatchColor);
				return;
		    }

		    await System.Threading.Tasks.Task.Delay(100);
		    ScrollSourceViewToPosition(idx);

		    string regexNote = TrackItem.IsRegexPattern(searchStr) ? " (regex)" : "";
		    dateStartPositionText.Text = "pos: " + idx;
			dateStartPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMatchColor);
		    statusFile.Text = "Release Date Start String found at position " + idx + regexNote;
		}

		private async void GoToDateStopString_Click(object sender, RoutedEventArgs e)
		{
		    if (string.IsNullOrEmpty(editReleaseDateStopString.Text))
		    {
		        statusFile.Text = "Release Date Stop String is empty";
		        return;
		    }

		    if (string.IsNullOrEmpty(currentSource) && currentTrackItem != null && !string.IsNullOrWhiteSpace(currentTrackItem.TrackURL))
		    {
		        statusFile.Text = "Downloading source...";
		        string url = currentTrackItem.TrackURL;
		        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
		            url = "https://" + url;

		        try
		        {
		            SetupHttpHeaders(url);
		            currentSource = await httpClient.GetStringAsync(url);
		        }
		        catch (Exception)
		        {
		            statusFile.Text = "Failed to download source";
		            return;
		        }
		    }

		    if (string.IsNullOrEmpty(currentSource))
		    {
		        statusFile.Text = "No source loaded";
		        return;
		    }

			DisplaySource(currentSource);
			rightTabs.SelectedIndex = 0;

		    string normalizedSource = currentSource.Replace("\r\n", "\n").Replace("\r", "\n");
		    string startStr = editReleaseDateStartString.Text ?? "";
		    string stopStr = editReleaseDateStopString.Text;
		    int idx = -1;

		    int searchAfter = 0;
		    if (!string.IsNullOrEmpty(startStr))
		    {
		        if (TrackItem.IsRegexPattern(startStr))
		        {
		            var match = TrackItem.FindRegexMatch(normalizedSource, startStr, 0);
		            if (match != null && match.Success)
		                searchAfter = match.Index + match.Length;
		        }
		        else
		        {
		            string ns = startStr.Replace("\r\n", "\n").Replace("\r", "\n");
		            int si = normalizedSource.IndexOf(ns, StringComparison.Ordinal);
		            if (si >= 0)
		                searchAfter = si + ns.Length;
		            else
		            {
		                string cs = NormalizeWhitespace(normalizedSource);
		                string cst = NormalizeWhitespace(ns);
		                int ci = cs.IndexOf(cst, StringComparison.Ordinal);
		                if (ci >= 0)
		                {
		                    int rawStart = MapNormalizedToRaw(normalizedSource, ci);
		                    int rawEnd = MapNormalizedToRaw(normalizedSource, ci + cst.Length);
		                    searchAfter = rawEnd;
		                }
		            }
		        }
		    }

		    if (TrackItem.IsRegexPattern(stopStr))
		    {
		        var match = TrackItem.FindRegexMatch(normalizedSource, stopStr, searchAfter);
		        if (match != null && match.Success)
		            idx = match.Index;
		    }
		    else
		    {
		        string normalizedSearch = stopStr.Replace("\r\n", "\n").Replace("\r", "\n");
		        idx = normalizedSource.IndexOf(normalizedSearch, searchAfter, StringComparison.Ordinal);

		        if (idx < 0)
		        {
		            string cs = NormalizeWhitespace(normalizedSource);
		            string cst = NormalizeWhitespace(normalizedSearch);
		            string collapsedBefore = NormalizeWhitespace(normalizedSource.Substring(0, searchAfter));
		            int collapsedSearchAfter = collapsedBefore.Length;

		            int ci = cs.IndexOf(cst, collapsedSearchAfter, StringComparison.Ordinal);
		            if (ci >= 0)
		                idx = MapNormalizedToRaw(normalizedSource, ci);
		        }
		    }

		    if (idx < 0)
		    {
				statusFile.Text = "Release Date Stop String not found in source";
				dateStopPositionText.Text = "NOT FOUND";
				dateStopPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMismatchColor);
				return;
		    }

		    await System.Threading.Tasks.Task.Delay(100);
		    ScrollSourceViewToPosition(idx);

		    string regexNote = TrackItem.IsRegexPattern(stopStr) ? " (regex)" : "";
		    dateStopPositionText.Text = "pos: " + idx;
			dateStopPositionText.Foreground = new SolidColorBrush(currentTheme.VersionMatchColor);
		    statusFile.Text = "Release Date Stop String found at position " + idx + regexNote;
		}

		/// <summary>
		/// Scrolls the source view so that the text at the given character position
		/// in currentSource is visible, approximately 1/3 from the top of the view.
		/// Works by walking the RichTextBox runs and counting only text characters.
		/// </summary>
		private void ScrollSourceViewToPosition(int charPosition)
		{
		    try
		    {
		        TextPointer current = sourceView.Document.ContentStart;
		        int counted = 0;

		        while (current != null && current.CompareTo(sourceView.Document.ContentEnd) < 0)
		        {
		            if (current.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
		            {
		                int runLength = current.GetTextRunLength(LogicalDirection.Forward);

		                if (counted + runLength >= charPosition)
		                {
		                    int offsetInRun = charPosition - counted;
		                    TextPointer target = current.GetPositionAtOffset(offsetInRun);
		                    if (target != null)
		                    {
		                        Rect rect = target.GetCharacterRect(LogicalDirection.Forward);
		                        if (!rect.IsEmpty)
		                        {
		                            sourceView.ScrollToVerticalOffset(
		                                sourceView.VerticalOffset + rect.Top - sourceView.ActualHeight / 3);
		                        }
		                    }
		                    return;
		                }

		                counted += runLength;
		            }
		            current = current.GetNextContextPosition(LogicalDirection.Forward);
		        }
		    }
		    catch (Exception) { }
		}

		/// <summary>
		/// Collapses all runs of whitespace (spaces, tabs, \r, \n) into a single space.
		/// This makes string matching work regardless of how line endings or indentation differ.
		/// </summary>
		private static string NormalizeWhitespace(string input)
		{
		    if (string.IsNullOrEmpty(input))
		        return "";

		    StringBuilder sb = new StringBuilder(input.Length);
		    bool inWhitespace = false;

		    for (int i = 0; i < input.Length; i++)
		    {
		        char c = input[i];
		        if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
		        {
		            if (!inWhitespace)
		            {
		                sb.Append(' ');
		                inWhitespace = true;
		            }
		            // Skip additional whitespace chars
		        }
		        else
		        {
		            sb.Append(c);
		            inWhitespace = false;
		        }
		    }

		    return sb.ToString();
		}

		/// <summary>
		/// Maps a position in whitespace-normalized text back to the position in the raw text.
		/// </summary>
		private static int MapNormalizedToRaw(string raw, int normalizedPos)
		{
		    int normIdx = 0;
		    bool inWhitespace = false;

		    for (int rawI = 0; rawI < raw.Length; rawI++)
		    {
		        if (normIdx >= normalizedPos)
		            return rawI;

		        char c = raw[rawI];
		        if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
		        {
		            if (!inWhitespace)
		            {
		                normIdx++; // The single space we collapsed to
		                inWhitespace = true;
		            }
		        }
		        else
		        {
		            normIdx++;
		            inWhitespace = false;
		        }
		    }

		    return raw.Length;
		}

        private void ScrollToPosition(int charPosition, int length)
        {
            try
            {
                TextPointer start = sourceView.Document.ContentStart;
                TextPointer current = start;
                int count = 0;

                // Walk through the document to find the right position
                while (current != null && count < charPosition)
                {
                    if (current.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                    {
                        string text = current.GetTextInRun(LogicalDirection.Forward);
                        int remaining = charPosition - count;

                        if (remaining <= text.Length)
                        {
                            TextPointer target = current.GetPositionAtOffset(remaining);
                            if (target != null)
                            {
                                sourceView.CaretPosition = target;
                                Rect rect = target.GetCharacterRect(LogicalDirection.Forward);
                                sourceView.ScrollToVerticalOffset(
                                    sourceView.VerticalOffset + rect.Top - sourceView.ActualHeight / 3);
                            }
                            return;
                        }

                        count += text.Length;
                    }
                    current = current.GetNextContextPosition(LogicalDirection.Forward);
                }
            }
            catch (Exception)
            {
            }
        }

        private async void InitWebView()
        {
            try
            {
                rightTabs.SelectedIndex = 1;
                await System.Threading.Tasks.Task.Delay(100);

                var options = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions();
                options.AreBrowserExtensionsEnabled = true;

                var environment = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                    null, webViewPath, options);

                await webView.EnsureCoreWebView2Async(environment);
                // Enables password save confirmation
                webView.CoreWebView2.Profile.IsPasswordAutosaveEnabled = true;

                webView.CoreWebView2.Profile.IsPasswordAutosaveEnabled = true;

                // Load installed browser extensions
                try
                {
                    var extensions = await webView.CoreWebView2.Profile.GetBrowserExtensionsAsync();
                    bool ublockFound = false;
                    foreach (var ext in extensions)
                    {
                        if (ext.Name.Contains("uBlock", StringComparison.OrdinalIgnoreCase))
                        {
                            ublockFound = true;
                            break;
                        }
                    }

                    if (!ublockFound)
                    {
                        string ublockFolder = Path.Combine(extensionsPath, "uBlock0.chromium");
                        if (Directory.Exists(ublockFolder) &&
                            File.Exists(Path.Combine(ublockFolder, "manifest.json")))
                        {
                            await webView.CoreWebView2.Profile.AddBrowserExtensionAsync(ublockFolder);
                        }
                    }
                }
                catch (Exception) { }

                // Pre-inject cookie blocker CSS so popups are hidden before page renders
                cookieBlockerScriptId = await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    (function() {
                        var style = document.createElement('style');
                        style.id = '__patCookieBlockerEarlyCSS';
                        style.textContent = `
                            [id*='cookie' i], [class*='cookie' i],
                            [id*='consent' i], [class*='consent' i],
                            [id*='gdpr' i], [class*='gdpr' i],
                            [id*='onetrust' i], [class*='onetrust' i],
                            [id*='CybotCookiebot' i],
                            .cookie-banner, .cookie-notice, .cookie-popup,
                            #cookie-law-info-bar, .cli-modal,
                            [class*='cc-banner' i], [class*='cc-window' i],
                            [class*='cookiewall' i], [id*='cookiewall' i] {
                                display: none !important;
                                visibility: hidden !important;
                                opacity: 0 !important;
                                pointer-events: none !important;
                            }
                            body, html {
                                overflow: auto !important;
                            }
                        `;
                        if (document.head) {
                            document.head.appendChild(style);
                        } else {
                            document.addEventListener('DOMContentLoaded', function() {
                                document.head.appendChild(style);
                            });
                        }
                    })();
                ");

                // Inject cookie popup blocker after each page loads
                webView.CoreWebView2.NavigationCompleted += async (s2, e2) =>
                {
                    if (blockCookiePopups)
                    {
                        await InjectCookiePopupBlocker();
                    }
                };

                // Switch back to source tab and load the first item's source
                rightTabs.SelectedIndex = 0;
                await System.Threading.Tasks.Task.Delay(200);

                if (currentTrackItem != null && !string.IsNullOrWhiteSpace(currentTrackItem.TrackURL))
                {
                    AutoDownloadSource(currentTrackItem.TrackURL);
                }
            }
            catch (Exception)
            {
            }
        }

        private async System.Threading.Tasks.Task InjectCookiePopupBlocker()
        {
            try
            {
                string script = @"
                    (function() {
                        if (window.__patCookieBlockerActive) return;
                        window.__patCookieBlockerActive = true;

                        var selectors = [
                            '[id*=""cookie"" i]', '[class*=""cookie"" i]',
                            '[id*=""consent"" i]', '[class*=""consent"" i]',
                            '[id*=""gdpr"" i]', '[class*=""gdpr"" i]',
                            '[class*=""cc-banner"" i]', '[class*=""cc-window"" i]',
                            '[id*=""onetrust"" i]', '[class*=""onetrust"" i]',
                            '[id*=""CybotCookiebot"" i]',
                            '[class*=""cookiewall"" i]', '[id*=""cookiewall"" i]',
                            '.cookie-banner', '.cookie-notice', '.cookie-popup',
                            '#cookie-law-info-bar', '.cli-modal',
                            '[aria-label*=""cookie"" i]', '[aria-label*=""consent"" i]'
                        ];

                        var rejectKeywords = ['reject', 'decline', 'deny', 'refuse', 'necessary only', 'essential only'];
                        var acceptKeywords = ['accept all', 'accept', 'agree', 'got it', 'i understand'];

                        function killPopups() {
                            selectors.forEach(function(sel) {
                                try {
                                    document.querySelectorAll(sel).forEach(function(el) {
                                        if (el.offsetHeight > 0) {
                                            el.style.setProperty('display', 'none', 'important');
                                            el.style.setProperty('visibility', 'hidden', 'important');
                                            el.style.setProperty('opacity', '0', 'important');
                                            el.style.setProperty('pointer-events', 'none', 'important');
                                        }
                                    });
                                } catch(e) {}
                            });

                            document.body.style.setProperty('overflow', 'auto', 'important');
                            document.documentElement.style.setProperty('overflow', 'auto', 'important');

                            var allButtons = document.querySelectorAll('button, a[role=""button""], input[type=""button""], input[type=""submit""]');
                            var clicked = false;

                            for (var i = 0; i < allButtons.length && !clicked; i++) {
                                var text = (allButtons[i].innerText || allButtons[i].value || '').toLowerCase().trim();
                                for (var j = 0; j < rejectKeywords.length; j++) {
                                    if (text.includes(rejectKeywords[j])) {
                                        allButtons[i].click();
                                        clicked = true;
                                        break;
                                    }
                                }
                            }
                        }

                        // Run immediately
                        killPopups();

                        // Run on short delays for lazy-loaded popups
                        setTimeout(killPopups, 500);
                        setTimeout(killPopups, 1500);
                        setTimeout(killPopups, 3000);
                        setTimeout(killPopups, 5000);

                        // Watch for new elements being added to the DOM
                        var observer = new MutationObserver(function(mutations) {
                            killPopups();
                        });
                        observer.observe(document.body, { childList: true, subtree: true });

                        // Also inject CSS to hide common patterns immediately
                        var style = document.createElement('style');
                        style.id = '__patCookieBlockerCSS';
                        style.textContent = `
                            [id*='cookie' i], [class*='cookie' i],
                            [id*='consent' i], [class*='consent' i],
                            [id*='gdpr' i], [class*='gdpr' i],
                            [id*='onetrust' i], [class*='onetrust' i],
                            [id*='CybotCookiebot' i],
                            .cookie-banner, .cookie-notice, .cookie-popup,
                            #cookie-law-info-bar, .cli-modal,
                            [class*='cc-banner' i], [class*='cc-window' i],
                            [class*='cookiewall' i], [id*='cookiewall' i] {
                                display: none !important;
                                visibility: hidden !important;
                                opacity: 0 !important;
                                pointer-events: none !important;
                            }
                            body, html {
                                overflow: auto !important;
                            }
                        `;
                        document.head.appendChild(style);
                    })();
                ";

                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception)
            {
            }
        }

        private async void ClearCookies_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirm = MessageBox.Show(
                "Delete all cookies?\n\n" +
                "This will sign you out of all websites in the WebView.",
                "Clear Cookies", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                await webView.EnsureCoreWebView2Async();
                webView.CoreWebView2.CookieManager.DeleteAllCookies();

                MessageBox.Show("All cookies have been deleted.\n\n" +
                    "You may need to reload any open pages.",
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Information);
                statusFile.Text = "All cookies cleared";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error clearing cookies:\n" + ex.Message,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ClearAllData_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirm = MessageBox.Show(
                "Clear ALL browsing data?\n\n" +
                "This will delete:\n" +
                "• All cookies\n" +
                "• Browser cache\n" +
                "• Local storage & session storage\n" +
                "• All cached files\n\n" +
                "You will be signed out of all websites.",
                "Clear All Data", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                await webView.EnsureCoreWebView2Async();

                // Delete cookies
                webView.CoreWebView2.CookieManager.DeleteAllCookies();

                // Clear cache and data via DevTools protocol
                await webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Network.clearBrowserCache", "{}");
                await webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Network.clearBrowserCookies", "{}");

                // Clear storage via JavaScript
                await webView.CoreWebView2.ExecuteScriptAsync(@"
                    try { localStorage.clear(); } catch(e) {}
                    try { sessionStorage.clear(); } catch(e) {}
                    try {
                        if (window.caches) {
                            caches.keys().then(function(names) {
                                names.forEach(function(name) { caches.delete(name); });
                            });
                        }
                    } catch(e) {}
                ");

                MessageBox.Show("All browsing data has been cleared.\n\n" +
                    "You may need to reload any open pages.",
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Information);
                statusFile.Text = "All browsing data and cookies cleared";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error clearing data:\n" + ex.Message,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================
        // Column Settings
        // ============================

        private void SaveColumnSettings()
        {
            GridView gv = itemList.View as GridView;
            if (gv == null) return;

            // Get the full list of columns (visible + hidden) from defaults
            List<ColumnSetting> allDefaults = WindowSettings.GetDefaultColumns();
            List<ColumnSetting> saved = new List<ColumnSetting>();

            // First, add visible columns in their current order with current widths
            int displayIndex = 0;
            foreach (GridViewColumn col in gv.Columns)
            {
                // Skip checkbox and status columns (first two)
                string header = col.Header as string;
                if (header == null) continue;
                if (header == "St") continue;

                // Find the binding for this column
                string binding = "";
                foreach (ColumnSetting def in allDefaults)
                {
                    if (def.Header == header)
                    {
                        binding = def.Binding;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(binding))
                {
                    saved.Add(new ColumnSetting
                    {
                        Header = header,
                        Binding = binding,
                        Width = col.Width > 0 ? col.Width : col.ActualWidth,
                        Visible = true,
                        DisplayIndex = displayIndex++
                    });
                }
            }

            // Now add hidden columns (those in defaults but not in the gridview)
            foreach (ColumnSetting def in allDefaults)
            {
                bool found = false;
                foreach (ColumnSetting s in saved)
                {
                    if (s.Binding == def.Binding)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    saved.Add(new ColumnSetting
                    {
                        Header = def.Header,
                        Binding = def.Binding,
                        Width = def.Width,
                        Visible = false,
                        DisplayIndex = displayIndex++
                    });
                }
            }

            windowSettings.ColumnSettings = saved;
        }

        private void ApplyColumnSettings_Click(object sender, RoutedEventArgs e)
        {
            // Read checkbox states and update settings
            List<ColumnSetting> allDefaults = WindowSettings.GetDefaultColumns();

            // First save current column widths/order for visible columns
            SaveColumnSettings();

            // Now apply checkbox visibility changes
            List<ColumnSetting> updated = new List<ColumnSetting>();
            int displayIndex = 0;

            foreach (var child in columnCheckboxPanel.Children)
            {
                CheckBox chk = child as CheckBox;
                if (chk == null) continue;

                string binding = chk.Tag as string;
                if (string.IsNullOrEmpty(binding)) continue;

                // Find existing setting or default
                ColumnSetting existing = null;
                foreach (ColumnSetting cs in windowSettings.ColumnSettings)
                {
                    if (cs.Binding == binding)
                    {
                        existing = cs;
                        break;
                    }
                }

                if (existing == null)
                {
                    foreach (ColumnSetting def in allDefaults)
                    {
                        if (def.Binding == binding)
                        {
                            existing = def;
                            break;
                        }
                    }
                }

                if (existing != null)
                {
                    updated.Add(new ColumnSetting
                    {
                        Header = existing.Header,
                        Binding = existing.Binding,
                        Width = existing.Width,
                        Visible = chk.IsChecked == true,
                        DisplayIndex = displayIndex++
                    });
                }
            }

            windowSettings.ColumnSettings = updated;
            RebuildColumns();
            statusFile.Text = "Column settings applied";
        }

        private void SaveAppSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveColumnSettings();
                SaveWindowSettings();
                statusFile.Text = "All settings saved";
                MessageBox.Show("Settings saved successfully.",
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving settings:\n" + ex.Message,
                    AppInfo.ShortName, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreDefaultColumns_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirm = MessageBox.Show(
                "Restore all column settings to defaults?\n\n" +
                "This will reset visibility, widths, and order.",
                "Restore Defaults", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            windowSettings.ColumnSettings = WindowSettings.GetDefaultColumns();

            // Update checkboxes
            columnCheckboxPanel.Children.Clear();
            List<ColumnSetting> defaults = WindowSettings.GetDefaultColumns();
            foreach (ColumnSetting cs in defaults)
            {
                CheckBox chk = new CheckBox();
                chk.Content = cs.Header + "  (" + cs.Binding + ")";
                chk.IsChecked = cs.Visible;
                chk.Tag = cs.Binding;
                chk.Margin = new Thickness(0, 2, 0, 2);
                columnCheckboxPanel.Children.Add(chk);
            }

            RebuildColumns();
            statusFile.Text = "Column settings restored to defaults";
        }

        // ============================
        // Source Tab - Search
        // ============================
        /// <summary>
        /// Parses an HTML string and returns a list of Runs with simple syntax highlighting.
        /// Everything inside angle brackets is one colour, everything outside is another.
        /// </summary>
		private List<Run> CreateSyntaxHighlightedRuns(string html)
		{
		    List<Run> runs = new List<Run>();
		    if (string.IsNullOrEmpty(html))
		        return runs;

		    SolidColorBrush tagBrush = new SolidColorBrush(currentTheme.SourceTagColor);
		    SolidColorBrush textBrush = new SolidColorBrush(currentTheme.SourceTextColor);

            int i = 0;

            while (i < html.Length)
            {
                if (html[i] == '<')
                {
                    // Find the closing >
                    int end = html.IndexOf('>', i);
                    if (end < 0) end = html.Length - 1;

                    runs.Add(MakeRun(html.Substring(i, end - i + 1), tagBrush));
                    i = end + 1;
                }
                else
                {
                    // Find the next <
                    int next = html.IndexOf('<', i);
                    if (next < 0) next = html.Length;

                    runs.Add(MakeRun(html.Substring(i, next - i), textBrush));
                    i = next;
                }
            }

            return runs;
        }

        private Run MakeRun(string text, SolidColorBrush brush)
        {
            Run run = new Run(text);
            run.Foreground = brush;
            return run;
        }

		private void SearchDown_Click(object sender, RoutedEventArgs e)
		{
		    if (string.IsNullOrEmpty(findString.Text) || string.IsNullOrEmpty(currentSource))
		        return;

		    if (searchHitPositions.Count == 0)
		    {
		        searchHitPositions.Clear();
		        int pos = 0;
		        string lowerSource = currentSource.ToLower();
		        string lowerSearch = findString.Text.ToLower();

		        while ((pos = lowerSource.IndexOf(lowerSearch, pos)) >= 0)
		        {
		            searchHitPositions.Add(pos);
		            pos += lowerSearch.Length;
		        }

		        findCount.Text = searchHitPositions.Count + " found";

		        if (searchHitPositions.Count > 0)
		        {
		            currentSearchIndex = 0;
		            HighlightSearch(findString.Text);
		        }
		        return;
		    }

		    currentSearchIndex++;
		    if (currentSearchIndex >= searchHitPositions.Count)
		        currentSearchIndex = 0;

		    HighlightSearch(findString.Text);
		}

		private void SearchUp_Click(object sender, RoutedEventArgs e)
		{
		    if (string.IsNullOrEmpty(findString.Text) || string.IsNullOrEmpty(currentSource))
		        return;

		    if (searchHitPositions.Count == 0)
		    {
		        searchHitPositions.Clear();
		        int pos = 0;
		        string lowerSource = currentSource.ToLower();
		        string lowerSearch = findString.Text.ToLower();

		        while ((pos = lowerSource.IndexOf(lowerSearch, pos)) >= 0)
		        {
		            searchHitPositions.Add(pos);
		            pos += lowerSearch.Length;
		        }

		        findCount.Text = searchHitPositions.Count + " found";

		        if (searchHitPositions.Count > 0)
		        {
		            currentSearchIndex = searchHitPositions.Count - 1;
		            HighlightSearch(findString.Text);
		        }
		        return;
		    }

		    currentSearchIndex--;
		    if (currentSearchIndex < 0)
		        currentSearchIndex = searchHitPositions.Count - 1;

		    HighlightSearch(findString.Text);
		}

        private void HighlightSearch(string searchText)
        {
            if (searchHitPositions.Count == 0 || currentSearchIndex < 0)
                return;

            sourceView.Document.Blocks.Clear();

            Paragraph para = new Paragraph();
            para.FontFamily = new FontFamily("Consolas");
            para.FontSize = sourceView.FontSize;
            para.Margin = new Thickness(0);

            int lastPos = 0;
            string lowerSource = currentSource.ToLower();
            string lowerSearch = searchText.ToLower();
            int hitIndex = 0;
            int pos = 0;

            while ((pos = lowerSource.IndexOf(lowerSearch, lastPos)) >= 0)
            {
                if (pos > lastPos)
                {
                    para.Inlines.Add(new Run(currentSource.Substring(lastPos, pos - lastPos)));
                }

                Run hitRun = new Run(currentSource.Substring(pos, searchText.Length));

                if (hitIndex == currentSearchIndex)
                {
                    hitRun.Background = Brushes.Orange;
                    hitRun.Foreground = Brushes.Black;
                    hitRun.FontWeight = FontWeights.Bold;
                }
                else
                {
                    hitRun.Background = Brushes.LightYellow;
                    hitRun.Foreground = Brushes.Black;
                }

                para.Inlines.Add(hitRun);

                lastPos = pos + searchText.Length;
                hitIndex++;
            }

            if (lastPos < currentSource.Length)
            {
                para.Inlines.Add(new Run(currentSource.Substring(lastPos)));
            }

            sourceView.Document.Blocks.Add(para);

            findCount.Text = (currentSearchIndex + 1) + " / " + searchHitPositions.Count;

            try
            {
                TextPointer start = sourceView.Document.ContentStart;
                TextPointer pointer = start.GetPositionAtOffset(
                    searchHitPositions[currentSearchIndex] + hitIndex * 2);
                if (pointer != null)
                {
                    sourceView.CaretPosition = pointer;
                    Rect rect = pointer.GetCharacterRect(LogicalDirection.Forward);
                    sourceView.ScrollToVerticalOffset(
                        sourceView.VerticalOffset + rect.Top - sourceView.ActualHeight / 3);
                }
            }
            catch (Exception)
            {
            }
        }

		// Helper method — add at class level
		private bool LooksLikeJsOnlyPage(string html)
		{
		    if (string.IsNullOrEmpty(html))
		        return false;

		    // Count how much is inside <script> tags vs total
		    int scriptContentLength = 0;
		    int idx = 0;
		    while (true)
		    {
		        int start = html.IndexOf("<script", idx, StringComparison.OrdinalIgnoreCase);
		        if (start < 0) break;
		        int end = html.IndexOf("</script>", start, StringComparison.OrdinalIgnoreCase);
		        if (end < 0) break;
		        end += "</script>".Length;
		        scriptContentLength += (end - start);
		        idx = end;
		    }

		    // What percentage of the page is script tags?
		    double scriptRatio = (double)scriptContentLength / html.Length;

		    // Strip all HTML tags to see how much visible text remains
		    string textOnly = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
		    textOnly = System.Text.RegularExpressions.Regex.Replace(textOnly, @"\s+", " ").Trim();

		    // Known JS framework markers
		    bool hasFrameworkMarker =
		        html.Contains("<div id=\"app\"") ||
		        html.Contains("<div id=\"root\"") ||
		        html.Contains("<div id=\"__next\"") ||
		        html.Contains("<div id=\"__nuxt\"") ||
		        html.Contains("__NUXT__") ||
		        html.Contains("__NEXT_DATA__") ||
		        html.Contains("window.__INITIAL_STATE__") ||
		        html.Contains("<noscript>You need to enable JavaScript");

		    // Page is likely JS-only if:
		    // - More than 70% of the page is script tags, OR
		    // - Known framework marker AND very little visible text, OR
		    // - Very little visible text (under 200 chars) but page has scripts
		    bool mostlyScripts = scriptRatio > 0.70;
		    bool frameworkWithNoContent = hasFrameworkMarker && textOnly.Length < 500;
		    bool tinyTextWithScripts = textOnly.Length < 200 && html.Contains("<script");

		    return mostlyScripts || frameworkWithNoContent || tinyTextWithScripts;
		}

        // ============================
        // WebView Tab
        // ============================

        private bool webViewAutoZoom = true;
        private double webViewBaseWidth = 1024.0;
        private bool webViewReady = false;

        private void SetupWebViewAutoZoom()
        {
            webView.CoreWebView2InitializationCompleted += (s, e) =>
            {
                if (e.IsSuccess)
                {
                    webViewReady = true;

                    // Custom context menu: "Add to Download URL" for file links
                    webView.CoreWebView2.ContextMenuRequested += (cmSender, cmArgs) =>
                    {
                        var menuItems = cmArgs.MenuItems;
                        var contextInfo = cmArgs.ContextMenuTarget;

                        // "Set as Download URL" for file links
                        if (contextInfo.HasLinkUri && LooksLikeDownloadUrl(contextInfo.LinkUri))
                        {
                            string linkUrl = contextInfo.LinkUri;

                            var menuItem = webView.CoreWebView2.Environment
                                .CreateContextMenuItem(
                                    "Set Download URL",
                                    null,
                                    Microsoft.Web.WebView2.Core.CoreWebView2ContextMenuItemKind.Command);

                            menuItem.CustomItemSelected += (miSender, miArgs) =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    editDownloadURL.Text = linkUrl;
                                    MarkDirty();
                                    statusFile.Text = "Download URL set to: " + linkUrl;
                                });
                            };

                            menuItems.Insert(0, webView.CoreWebView2.Environment
                                .CreateContextMenuItem(
                                    "",
                                    null,
                                    Microsoft.Web.WebView2.Core.CoreWebView2ContextMenuItemKind.Separator));
                            menuItems.Insert(0, menuItem);
                        }

                        // "Set Version Start String" / "Set as Stop String" for selected text
                        if (contextInfo.HasSelection)
                        {
                            string selectedText = contextInfo.SelectionText;

                            if (!string.IsNullOrEmpty(selectedText))
                            {
                                var startMenuItem = webView.CoreWebView2.Environment
                                    .CreateContextMenuItem(
                                        "Set Version Start String",
                                        null,
                                        Microsoft.Web.WebView2.Core.CoreWebView2ContextMenuItemKind.Command);

                                startMenuItem.CustomItemSelected += (miSender, miArgs) =>
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        editStartString.Text = selectedText;
                                        MarkDirty();
                                        statusFile.Text = "Start String set from selection";

                                        if (!string.IsNullOrEmpty(currentSource))
                                            DisplaySource(currentSource);
                                    });
                                };

                                var stopMenuItem = webView.CoreWebView2.Environment
                                    .CreateContextMenuItem(
                                        "Set Version Stop String",
                                        null,
                                        Microsoft.Web.WebView2.Core.CoreWebView2ContextMenuItemKind.Command);

                                stopMenuItem.CustomItemSelected += (miSender, miArgs) =>
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        editStopString.Text = selectedText;
                                        MarkDirty();
                                        statusFile.Text = "Stop String set from selection";

                                        if (!string.IsNullOrEmpty(currentSource))
                                            DisplaySource(currentSource);
                                    });
                                };

								var dateStartMenuItem = webView.CoreWebView2.Environment
								    .CreateContextMenuItem(
								        "Set Date Start String",
								        null,
								        Microsoft.Web.WebView2.Core.CoreWebView2ContextMenuItemKind.Command);

								dateStartMenuItem.CustomItemSelected += (miSender, miArgs) =>
								{
								    Dispatcher.Invoke(() =>
								    {
								        editReleaseDateStartString.Text = selectedText;
								        MarkDirty();
								        statusFile.Text = "Date Start String set from selection";

								        if (!string.IsNullOrEmpty(currentSource))
								            DisplaySource(currentSource);
								    });
								};

								var dateStopMenuItem = webView.CoreWebView2.Environment
								    .CreateContextMenuItem(
								        "Set Date Stop String",
								        null,
								        Microsoft.Web.WebView2.Core.CoreWebView2ContextMenuItemKind.Command);

								dateStopMenuItem.CustomItemSelected += (miSender, miArgs) =>
								{
								    Dispatcher.Invoke(() =>
								    {
								        editReleaseDateStopString.Text = selectedText;
								        MarkDirty();
								        statusFile.Text = "Date Stop String set from selection";

								        if (!string.IsNullOrEmpty(currentSource))
								            DisplaySource(currentSource);
								    });
								};

                                menuItems.Insert(0, webView.CoreWebView2.Environment
                                    .CreateContextMenuItem(
                                        "",
                                        null,
                                        Microsoft.Web.WebView2.Core.CoreWebView2ContextMenuItemKind.Separator));

                                if (LooksLikeDate(selectedText))
                                {
                                    var releaseDateMenuItem = webView.CoreWebView2.Environment
                                        .CreateContextMenuItem(
                                            "Set Release Date",
                                            null,
                                            Microsoft.Web.WebView2.Core.CoreWebView2ContextMenuItemKind.Command);

                                    releaseDateMenuItem.CustomItemSelected += (miSender, miArgs) =>
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            editReleaseDate.Text = selectedText;
                                            MarkDirty();
                                            statusFile.Text = "Release Date set from selection";
                                        });
                                    };

                                    menuItems.Insert(0, releaseDateMenuItem);
                                }

                                menuItems.Insert(0, stopMenuItem);
                                menuItems.Insert(0, startMenuItem);
                                menuItems.Insert(0, dateStopMenuItem);
								menuItems.Insert(0, dateStartMenuItem);
                            }
                        }
                    };

                    webView.CoreWebView2.NavigationCompleted += (s2, e2) =>
                    {
                        if (webViewAutoZoom)
                        {
                            // Defer zoom until layout is complete so ActualWidth is correct
                            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                                new Action(() =>
                                {
                                    ApplyAutoZoom();
                                }));
                        }
                    };
                }
            };

            webView.SizeChanged += (s, e) =>
            {
                if (webViewAutoZoom && webViewReady && e.NewSize.Width > 0)
                {
                    ApplyAutoZoom();
                }
            };
        }

		private bool LooksLikeDate(string text)
		{
		    if (string.IsNullOrWhiteSpace(text)) return false;
		    text = text.Trim();

		    // Try standard .NET date parsing
		    if (DateTime.TryParse(text, out _)) return true;

		    // Try common date formats explicitly
		    string[] formats = new string[]
		    {
		        "yyyy-MM-dd", "dd-MM-yyyy", "MM-dd-yyyy",
		        "yyyy/MM/dd", "dd/MM/yyyy", "MM/dd/yyyy",
		        "yyyy.MM.dd", "dd.MM.yyyy", "MM.dd.yyyy",
		        "d MMM yyyy", "d MMMM yyyy", "dd MMM yyyy", "dd MMMM yyyy",
		        "MMM d, yyyy", "MMMM d, yyyy", "MMM dd, yyyy", "MMMM dd, yyyy",
		        "MMM d yyyy", "MMMM d yyyy", "MMM dd yyyy", "MMMM dd yyyy",
		        "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss",
		        "yyyyMMdd",
		        "d-MMM-yyyy", "d-MMMM-yyyy", "dd-MMM-yyyy", "dd-MMMM-yyyy",
		    };

		    if (DateTime.TryParseExact(text, formats,
		        System.Globalization.CultureInfo.InvariantCulture,
		        System.Globalization.DateTimeStyles.AllowWhiteSpaces, out _))
		        return true;

		    // Match patterns like "12 Jan 2025", "January 5, 2025", "2025-01-12", etc.
		    if (System.Text.RegularExpressions.Regex.IsMatch(text,
		        @"^\d{1,4}[\-/\.]\d{1,2}[\-/\.]\d{1,4}$")) return true;

		    if (System.Text.RegularExpressions.Regex.IsMatch(text,
		        @"^\d{1,2}\s+(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec|January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{2,4}$",
		        System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return true;

		    if (System.Text.RegularExpressions.Regex.IsMatch(text,
		        @"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec|January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{2,4}$",
		        System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return true;

		    return false;
		}

		private bool LooksLikeDownloadUrl(string text)
		{
		    if (string.IsNullOrWhiteSpace(text)) return false;
		    text = text.Trim();

		    // Must look like a URL
		    if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
		        !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
		        !text.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
		        return false;

		    string[] fileExtensions = {
		        ".exe", ".msi", ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2", ".xz",
		        ".dmg", ".pkg", ".deb", ".rpm", ".appimage", ".apk",
		        ".iso", ".img",
		        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
		        ".cab", ".dll", ".sys", ".jar", ".war",
		        ".bin", ".dat", ".run", ".sh", ".bat", ".cmd", ".ps1",
		        ".torrent", ".nupkg", ".vsix", ".crx", ".xpi"
		    };

		    string lowerUrl = text.ToLowerInvariant();

		    string urlPath = lowerUrl;
		    int queryIdx = urlPath.IndexOf('?');
		    if (queryIdx >= 0) urlPath = urlPath.Substring(0, queryIdx);
		    int fragIdx = urlPath.IndexOf('#');
		    if (fragIdx >= 0) urlPath = urlPath.Substring(0, fragIdx);

		    foreach (string ext in fileExtensions)
		    {
		        if (urlPath.EndsWith(ext))
		            return true;
		    }

		    if (lowerUrl.Contains("/download/") ||
		        lowerUrl.Contains("/releases/download/") ||
		        lowerUrl.Contains("sourceforge.net/projects/") ||
		        lowerUrl.Contains("/dl/"))
		        return true;

		    return false;
		}

        private void ApplyAutoZoom()
        {
            if (webView.ActualWidth > 0 && webView.CoreWebView2 != null)
            {
                double zoom = webView.ActualWidth / webViewBaseWidth;
                zoom = Math.Max(0.25, Math.Min(2.0, zoom));
                webView.ZoomFactor = zoom;
            }
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            webViewAutoZoom = false;
            if (webView.CoreWebView2 != null)
                webView.ZoomFactor += 0.1;
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            webViewAutoZoom = false;
            if (webView.CoreWebView2 != null)
                webView.ZoomFactor = Math.Max(0.1, webView.ZoomFactor - 0.1);
        }

        private void ZoomReset_Click(object sender, RoutedEventArgs e)
        {
            webViewAutoZoom = false;
            if (webView.CoreWebView2 != null)
                webView.ZoomFactor = 1.0;
        }

        private void ZoomFit_Click(object sender, RoutedEventArgs e)
        {
            webViewAutoZoom = true;
            ApplyAutoZoom();
        }
    }
}