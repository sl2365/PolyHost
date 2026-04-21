using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ElementalTracker
{
    public partial class SuiteSelectionWindow : Window
    {
        private List<SuiteInfo> suites;

        public List<string> SelectedSuiteNames { get; private set; } = new List<string>();

        public SuiteSelectionWindow(List<SuiteInfo> availableSuites, List<string> previouslySelected, ThemeSettings theme = null)
        {
            InitializeComponent();
            suites = availableSuites;

            if (theme != null)
                ApplyTheme(theme);

            foreach (SuiteInfo suite in suites)
            {
                CheckBox cb = new CheckBox();
                cb.Content = suite.ToString();
                cb.Tag = suite.Name;
                cb.Margin = new Thickness(4);
                cb.FontSize = 13;

                if (theme != null)
                {
                    cb.Foreground = new SolidColorBrush(theme.WindowForeground);
                }

                // Tick the checkbox if it was previously selected
                if (previouslySelected != null && previouslySelected.Contains(suite.Name))
                    cb.IsChecked = true;

                suiteListBox.Items.Add(cb);
            }
        }

		private void ApplyTheme(ThemeSettings theme)
		{
		    // Window
		    this.Background = new SolidColorBrush(theme.WindowBackground);

		    // Header text
		    headerText.Foreground = new SolidColorBrush(theme.WindowForeground);

		    // ListBox
		    suiteListBox.Background = new SolidColorBrush(theme.WindowBackground);
		    suiteListBox.Foreground = new SolidColorBrush(theme.WindowForeground);
		    suiteListBox.BorderBrush = new SolidColorBrush(theme.CheckBoxBorder);

		    // Compute button hover color (same logic as MainWindow)
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

		    SolidColorBrush btnBg = new SolidColorBrush(theme.ButtonBackground);
		    SolidColorBrush btnFg = new SolidColorBrush(theme.ButtonForeground);
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

		    // Hover trigger
		    Trigger hoverTrigger = new Trigger();
		    hoverTrigger.Property = UIElement.IsMouseOverProperty;
		    hoverTrigger.Value = true;
		    hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, btnHoverBrush));
		    hoverTrigger.Setters.Add(new Setter(Button.BorderBrushProperty, btnHoverBrush));
		    btnTemplate.Triggers.Add(hoverTrigger);

		    buttonStyle.Setters.Add(new Setter(Button.TemplateProperty, btnTemplate));

		    // Apply to both buttons
		    btnOK.Style = buttonStyle;
		    btnCancel.Style = buttonStyle;
		}

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SelectedSuiteNames = new List<string>();
            foreach (object item in suiteListBox.Items)
            {
                CheckBox cb = item as CheckBox;
                if (cb != null && cb.IsChecked == true)
                {
                    SelectedSuiteNames.Add((string)cb.Tag);
                }
            }
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}