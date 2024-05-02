using CompMs.App.Msdial.ViewModel.Service;
using CompMs.App.Msdial.Model.Export;
using CompMs.CommonMVVM;
using CompMs.Common.Enum;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using RDotNet;
using Reactive.Bindings.Notifiers;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.ObjectModel;

namespace CompMs.App.Msdial.Model.Statistics {
    internal sealed class Notame : BindableBase {
        public Notame(AlignmentFilesForExport alignmentFilesForExport, AlignmentPeakSpotSupplyer peakSpotSupplyer, AlignmentExportGroupModel exportModel, DataExportBaseParameter dataExportParameter, ParameterBase parameterBase) {
            AlignmentFilesForExport = alignmentFilesForExport;
            PeakSpotSupplyer = peakSpotSupplyer ?? throw new ArgumentNullException(nameof(peakSpotSupplyer));
            ExportModel = exportModel;
            ExportDirectory = dataExportParameter.ExportFolderPath;
            IonMode = parameterBase.IonMode;
        }

        public string ExportDirectory {
            get => _exportDirectory;
            set => SetProperty(ref _exportDirectory, value);
        }
        private string _exportDirectory = string.Empty;

        public string GetExportFolder() {
            var folder = ExportDirectory.Replace("\\", "/");
            return folder;
        }

        public AlignmentExportGroupModel ExportModel { get; }

        public AlignmentFilesForExport AlignmentFilesForExport { get; }
        public AlignmentPeakSpotSupplyer PeakSpotSupplyer { get; }
        public ExportMethod ExportMethod => ExportModel.ExportMethod;
        public ReadOnlyObservableCollection<ExportType> ExportTypes => ExportModel.Types;

        public Task ExportAlignmentResultAsync(IMessageBroker broker) {
            return Task.Run(() => {
                if (AlignmentFilesForExport.SelectedFile is null) {
                    return;
                }
                var publisher = new TaskProgressPublisher(broker, $"Exporting {AlignmentFilesForExport.SelectedFile.FileName}");
                using (publisher.Start()) {
                    var alignmentFile = AlignmentFilesForExport.SelectedFile;
                    if (ExportTypes.FirstOrDefault(type => type.IsSelected) is not { } type) {
                        throw new Exception("Export type (Height, Area, ...) is not selected.");
                    }
                    var fileName = $"{type.TargetLabel}_{((IFileBean)alignmentFile).FileID}_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}";
                    FileName = ExportMethod.Format.WithExtension(fileName);
                    ExportModel.Export(alignmentFile, ExportDirectory, fileName, null);
                }
            });
        }

        private readonly IonMode IonMode;

        public string GetIonMode() {
            if (IonMode == IonMode.Positive) {
                return "pos";
            }
            else if (IonMode == IonMode.Negative) {
                return "neg";
            }
            return string.Empty;
        }

        private string NotameIonMode = string.Empty;
        private string NotameExport = string.Empty;
        private string FileName = string.Empty;

        public void Run() {
            NotameIonMode = GetIonMode();
            NotameExport = GetExportFolder();
            MessageBox.Show("Please wait until drift correction and batch correction are done.");
            RunNotame();
        }

        private void RunNotame() {
            var rReader = new NotameRReader();
            rReader.Read();
            var NotameR = rReader.rScript;
            var muvrRReader = new MuvrRReader();
            muvrRReader.Read();
            var MUVR = muvrRReader.muvrRScript;
            REngine.SetEnvironmentVariables();
            REngine.SetEnvironmentVariables("c:/program files/r/r-4.3.2/bin/x64", "c:/program files/r/r-4.3.2");
            var engine = REngine.GetInstance();
            engine.Evaluate("Sys.setenv(PATH = paste(\"C:/Program Files/R/R-4.3.2/bin/x64\", Sys.getenv(\"PATH\"), sep=\";\"))");
            engine.Evaluate("library(notame)");
            engine.Evaluate("library(doParallel)");
            engine.Evaluate("library(dplyr)");
            engine.Evaluate("library(openxlsx)");
            engine.SetSymbol("path", engine.CreateCharacter(NotameExport));
            engine.SetSymbol("file_name", engine.CreateCharacter(FileName));
            engine.SetSymbol("ion_mod", engine.CreateCharacter(NotameIonMode));

            engine.Evaluate(NotameR);
            MessageBox.Show("Drift correction and batch correction files are saved. MUVR processing started. Please wait for a minute.");
            engine.Evaluate(MUVR);
            MessageBox.Show("Output files are successfully created.");
            engine.Dispose();
        }
    }
}
