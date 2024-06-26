﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Search;
using CompMs.Common.DataObj.Property;
using CompMs.Common.Enum;
using CompMs.CommonMVVM;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using Reactive.Bindings.Notifiers;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace CompMs.App.Msdial.ViewModel.Search
{
    internal sealed class InternalMsFinderViewModel : ViewModelBase {
        private InternalMsFinder _model;
        private readonly IMessageBroker _broker;

        public InternalMsFinderViewModel(InternalMsFinder model, IMessageBroker broker) {
            _model = model;
            _broker = broker;

            MsfinderObservedMetabolites = model.MsfinderObservedMetabolites;
            MsfinderSelectedMetabolite = model.ToReactivePropertySlimAsSynchronized(m => m.MsfinderSelectedMetabolite).AddTo(Disposables);
            
            LoadAsyncCommand = new DelegateCommand(LoadAsync);
        }

        public InternalMsFinderMetaboliteList InternalMsFinderMetaboliteList { get; set; }
        public ReadOnlyObservableCollection<MsfinderObservedMetabolite> MsfinderObservedMetabolites { get; }
        public ReactivePropertySlim<MsfinderObservedMetabolite?> MsfinderSelectedMetabolite { get; }
        public ObservableCollection<string> MetaboliteList { get; }

        public DelegateCommand LoadAsyncCommand { get; }

        private void LoadAsync()  {
            Mouse.OverrideCursor = Cursors.Wait;

            Mouse.OverrideCursor = null;
        }
    }
}
