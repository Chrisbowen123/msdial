﻿using CompMs.App.Msdial.Model.Search;
using CompMs.App.Msdial.ViewModel.Search;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;

namespace CompMs.App.Msdial.ViewModel.Imms
{
    internal sealed class ImmsCompoundSearchVM : CompoundSearchVM
    {
        public ImmsCompoundSearchVM(ICompoundSearchModel model, ICommand setUnknownCommand) : base(model, setUnknownCommand) {
            ParameterHasErrors = ParameterVM.Select(parameter =>
                parameter is null
                    ? Observable.Return(true)
                    : new[]
                    {
                        parameter.Ms1Tolerance.ObserveHasErrors,
                        parameter.Ms2Tolerance.ObserveHasErrors,
                        parameter.CcsTolerance.ObserveHasErrors,
                    }.CombineLatestValuesAreAllFalse()
                    .Inverse())
            .Switch()
            .ToReadOnlyReactivePropertySlim()
            .AddTo(Disposables);

            SearchCommand = new IObservable<bool>[]{
                IsBusy,
                ParameterHasErrors,
            }.CombineLatestValuesAreAllFalse()
            .ToReactiveCommand().AddTo(Disposables);

            Compounds = ParameterVM.Select(parameter =>
                parameter is null
                    ? Observable.Never<Unit>()
                    : new[]
                    {
                        parameter.Ms1Tolerance.ToUnit(),
                        parameter.Ms2Tolerance.ToUnit(),
                        parameter.CcsTolerance.ToUnit(),
                        SearchCommand.ToUnit(),
                    }.Merge())
            .Switch()
            .Where(_ => !ParameterHasErrors.Value)
            .Select(_ => Observable.FromAsync(SearchAsync))
            .Switch()
            .ToReadOnlyReactivePropertySlim()
            .AddTo(Disposables);
            
            SearchCommand.Execute();
        }
    }
}
