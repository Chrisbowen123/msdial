﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CompMs.App.Msdial.View.Imms
{
    /// <summary>
    /// Interaction logic for ImmsAnalysisPeakTableView.xaml
    /// </summary>
    public partial class ImmsAnalysisPeakTableView : UserControl
    {
        public ImmsAnalysisPeakTableView() {
            InitializeComponent();
        }

        private void DataGrid_Unloaded(object sender, RoutedEventArgs e) {
            if (sender is DataGrid grid) {
                grid.CommitEdit();
            }
        }

        private void PeakSpotView_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!(PeakSpotView.SelectedItem is null)){
                PeakSpotView.ScrollIntoView(PeakSpotView.SelectedItem);
            }
        }
    }
}
