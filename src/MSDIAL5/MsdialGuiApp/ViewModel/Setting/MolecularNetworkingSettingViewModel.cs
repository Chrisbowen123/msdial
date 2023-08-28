﻿using CompMs.App.Msdial.Model.Setting;
using CompMs.Common.Enum;
using CompMs.CommonMVVM;
using CompMs.CommonMVVM.Validator;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.ViewModel.Setting {
    class MolecularNetworkingSettingViewModel : ViewModelBase {
        private readonly MolecularNetworkingSettingModel model;

        public MolecularNetworkingSettingViewModel(MolecularNetworkingSettingModel model) {
            if (model == null) {
                throw new ArgumentNullException(nameof(model));
            }
            this.model = model;

            IsAlignSpotViewSelected = this.model.ToReactivePropertySlimAsSynchronized(m => m.IsAlignSpotViewSelected).AddTo(Disposables);

            RtTolerance = this.model.ToReactivePropertyAsSynchronized(
                m => m.RtTolerance,
                m => m.ToString(),
                vm => double.Parse(vm),
                ignoreValidationErrorValue: true
            ).SetValidateAttribute(() => RtTolerance).AddTo(Disposables);

            IonCorrelationSimilarityCutoff = this.model.ToReactivePropertyAsSynchronized(
                m => m.IonCorrelationSimilarityCutOff,
                m => m.ToString(),
                vm => double.Parse(vm),
                ignoreValidationErrorValue: true
            ).SetValidateAttribute(() => IonCorrelationSimilarityCutoff).AddTo(Disposables);

            SpectrumSimilarityCutOff = this.model.ToReactivePropertyAsSynchronized(
                m => m.SpectrumSimilarityCutOff,
                m => m.ToString(),
                vm => double.Parse(vm),
                ignoreValidationErrorValue: true
            ).SetValidateAttribute(() => SpectrumSimilarityCutOff).AddTo(Disposables);

            RelativeAbundanceCutoff = this.model.ToReactivePropertyAsSynchronized(
                m => m.RelativeAbundanceCutoff,
                m => m.ToString(),
                vm => double.Parse(vm),
                ignoreValidationErrorValue: true
            ).SetValidateAttribute(() => RelativeAbundanceCutoff).AddTo(Disposables);

            AbsoluteAbundanceCutoff = this.model.ToReactivePropertyAsSynchronized(
                m => m.AbsluteAbundanceCutoff,
                m => m.ToString(),
                vm => double.Parse(vm),
                ignoreValidationErrorValue: true
            ).SetValidateAttribute(() => AbsoluteAbundanceCutoff).AddTo(Disposables);

            MassTolerance = this.model.ToReactivePropertyAsSynchronized(
                m => m.MassTolerance,
                m => m.ToString(),
                vm => double.Parse(vm),
                ignoreValidationErrorValue: true
            ).SetValidateAttribute(() => MassTolerance).AddTo(Disposables);

            IsExportIonCorrelation = this.model.ToReactivePropertySlimAsSynchronized(m => m.IsExportIonCorrelation).AddTo(Disposables);

            MinimumPeakMatch = this.model.ToReactivePropertyAsSynchronized(
               m => m.MinimumPeakMatch,
               m => m.ToString(),
               vm => double.Parse(vm),
               ignoreValidationErrorValue: true
            ).SetValidateAttribute(() => MinimumPeakMatch).AddTo(Disposables);

            MaxEdgeNumberPerNode = this.model.ToReactivePropertyAsSynchronized(
               m => m.MaxEdgeNumberPerNode,
               m => m.ToString(),
               vm => double.Parse(vm),
               ignoreValidationErrorValue: true
            ).SetValidateAttribute(() => MaxEdgeNumberPerNode).AddTo(Disposables);

            MaxPrecursorDifference = this.model.ToReactivePropertyAsSynchronized(
               m => m.MaxPrecursorDifference,
               m => m.ToString(),
               vm => double.Parse(vm),
               ignoreValidationErrorValue: true
            ).SetValidateAttribute(() => MaxPrecursorDifference).AddTo(Disposables);

            MaxPrecursorDifferenceAsPercent = this.model.ToReactivePropertyAsSynchronized(
               m => m.MaxPrecursorDifferenceAsPercent,
               m => m.ToString(),
               vm => double.Parse(vm),
               ignoreValidationErrorValue: true
            ).SetValidateAttribute(() => MaxPrecursorDifferenceAsPercent).AddTo(Disposables);

            MsmsSimilarityCalc = this.model.ToReactivePropertySlimAsSynchronized(m => m.MsmsSimilarityCalc).AddTo(Disposables);
            ExportFolderPath = this.model.ExportFolderPath;

            ObserveHasErrors = new[]
            {
                RtTolerance.ObserveHasErrors,
                IonCorrelationSimilarityCutoff.ObserveHasErrors,
                SpectrumSimilarityCutOff.ObserveHasErrors,
                RelativeAbundanceCutoff.ObserveHasErrors,
                AbsoluteAbundanceCutoff.ObserveHasErrors,
                MassTolerance.ObserveHasErrors,
                MinimumPeakMatch.ObserveHasErrors,
                MaxEdgeNumberPerNode.ObserveHasErrors,
                MaxPrecursorDifference.ObserveHasErrors,
                MaxPrecursorDifferenceAsPercent.ObserveHasErrors,
            }.CombineLatestValuesAreAllFalse()
            .Inverse()
            .ToReadOnlyReactivePropertySlim()
            .AddTo(Disposables);
           
        }

        [Required(ErrorMessage = "Required field")]
        [RegularExpression(@"\d*\.?\d+", ErrorMessage = "Invalid character entered.")]
        [Range(0, int.MaxValue, ErrorMessage = "Tolerance should be positive value.")]
        public ReactiveProperty<string> RtTolerance { get; }

        [Required(ErrorMessage = "Required field")]
        [RegularExpression(@"\d*\.?\d+", ErrorMessage = "Invalid character entered.")]
        [Range(0, int.MaxValue, ErrorMessage = "Similarity should be positive value.")]
        public ReactiveProperty<string> IonCorrelationSimilarityCutoff { get; }

        [Required(ErrorMessage = "Required field")]
        [RegularExpression(@"\d*\.?\d+", ErrorMessage = "Invalid character entered.")]
        [Range(0, int.MaxValue, ErrorMessage = "Similarity should be positive value.")]
        public ReactiveProperty<string> SpectrumSimilarityCutOff { get; }

        [Required(ErrorMessage = "Required field")]
        [RegularExpression(@"\d*\.?\d+", ErrorMessage = "Invalid character entered.")]
        [Range(0, int.MaxValue, ErrorMessage = "Cut-off value should be positive value.")]
        public ReactiveProperty<string> RelativeAbundanceCutoff { get; }

        [Required(ErrorMessage = "Required field")]
        [RegularExpression(@"\d*\.?\d+", ErrorMessage = "Invalid character entered.")]
        [Range(0, int.MaxValue, ErrorMessage = "Cut-off value should be positive value.")]
        public ReactiveProperty<string> AbsoluteAbundanceCutoff { get; }

        [Required(ErrorMessage = "Required field")]
        [RegularExpression(@"\d*\.?\d+", ErrorMessage = "Invalid character entered.")]
        [Range(0, double.MaxValue, ErrorMessage = "Tolerance should be positive value.")]
        public ReactiveProperty<string> MassTolerance { get; }

        [Required(ErrorMessage = "Required field")]
        [RegularExpression(@"\d*\.?\d+", ErrorMessage = "Invalid character entered.")]
        [Range(0, int.MaxValue, ErrorMessage = "Tolerance should be positive value.")]
        public ReactivePropertySlim<bool> IsExportIonCorrelation { get; }

        [Required(ErrorMessage = "Required field")]
        [RegularExpression(@"\d*\.?\d+", ErrorMessage = "Invalid character entered.")]
        [Range(0, int.MaxValue, ErrorMessage = "Peak match count should be positive value.")]
        public ReactiveProperty<string> MinimumPeakMatch { get; }

        [Required(ErrorMessage = "Required field")]
        [RegularExpression(@"\d*\.?\d+", ErrorMessage = "Invalid character entered.")]
        [Range(0, int.MaxValue, ErrorMessage = "The number should be positive value.")]
        public ReactiveProperty<string> MaxEdgeNumberPerNode { get; }

        [Required(ErrorMessage = "Required field")]
        [RegularExpression(@"\d*\.?\d+", ErrorMessage = "Invalid character entered.")]
        [Range(0, int.MaxValue, ErrorMessage = "The difference value should be positive value.")]
        public ReactiveProperty<string> MaxPrecursorDifference { get; }

        [Required(ErrorMessage = "Required field")]
        [RegularExpression(@"\d*\.?\d+", ErrorMessage = "Invalid character entered.")]
        [Range(0, int.MaxValue, ErrorMessage = "The percent value should be positive value.")]
        public ReactiveProperty<string> MaxPrecursorDifferenceAsPercent { get; }

        public ReactivePropertySlim<MsmsSimilarityCalc> MsmsSimilarityCalc { get; }

        [Required(ErrorMessage = "Please enter the folder which the results will be exported.")]
        [PathExists(ErrorMessage = "This folder does not exist.", IsDirectory = true)]
        public string ExportFolderPath {
            get => _exportFolderPath;
            set {
                if (SetProperty(ref _exportFolderPath, value)) {
                    if (!ContainsError(nameof(ExportFolderPath))) {
                        model.ExportFolderPath = _exportFolderPath;
                    }
                }
            }
        }
        private string _exportFolderPath;

        public ReactivePropertySlim<bool> IsAlignSpotViewSelected { get; }

        public DelegateCommand MolecularNetworkingCommand => molecularNetworkingCommand ?? 
            (molecularNetworkingCommand = new DelegateCommand(model.RunMolecularNetworking, () => !HasValidationErrors)); 
        private DelegateCommand molecularNetworkingCommand;

        public ReadOnlyReactivePropertySlim<bool> ObserveHasErrors { get; }

        public DelegateCommand BrowseDirectoryCommand => _browseDirectoryCommand ?? (_browseDirectoryCommand = new DelegateCommand(BrowseDirectory));
        private DelegateCommand _browseDirectoryCommand;

        private void BrowseDirectory() {
            var fbd = new Graphics.Window.SelectFolderDialog {
                Title = "Chose a export folder.",
            };

            if (fbd.ShowDialog() == Graphics.Window.DialogResult.OK) {
                ExportFolderPath = fbd.SelectedPath;
            }
        }


    }
}
