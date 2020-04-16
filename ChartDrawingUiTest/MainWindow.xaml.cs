﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using ChartDrawingUiTest.Default;
using ChartDrawingUiTest.Dendrogram;
using ChartDrawingUiTest.Chromatogram;

namespace ChartDrawingUiTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            pageType = new Dictionary<string, Type>
            {
                {"Default1", typeof(Default1) },
                {"Dendrogram1", typeof(Dendrogram1) },
                {"Chromatogram1", typeof(Chromatogram1) },
                // {"DrawingTest1", typeof(DrawingTest1) },
                // {"BindingTest1", typeof(BindingTest1) },
                // {"ClipTest1", typeof(ClipTest1) },
            };
            names = pageType.Keys.ToList();
            pageMemo = new Dictionary<string, Page>();
            navbar.ItemsSource = names;
            pageMemo[names[0]] = (Page)Activator.CreateInstance(pageType[names[0]]);
            sampleFrame.Navigate(pageMemo[names[0]]);
        }

        private List<string> names;
        private Dictionary<string, Page> pageMemo;
        private Dictionary<string, Type> pageType;

        private void navbar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string item = (string)navbar.SelectedItem;
            if (!pageMemo.ContainsKey(item))
                pageMemo[item] = (Page)Activator.CreateInstance(pageType[item]); 
            sampleFrame.Navigate(pageMemo[item]);
        }
    }
}
