﻿using CompMs.CommonMVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompMs.Graphics.UI.ProgressBar
{
    public class ProgressBarMultiContainerVM : ViewModelBase
    {
        public int MaxValue {
            get => maxValue;
            set => SetProperty(ref maxValue, value);
        }

        public int CurrentValue {
            get => currentValue;
            set => SetProperty(ref currentValue, value);
        }

        public ObservableCollection<ProgressBarVM> ProgressBarVMs {
            get => progressBarVMs;
            set => SetProperty(ref progressBarVMs, value);
        }

        private int maxValue = 100, currentValue = 0;
        private ObservableCollection<ProgressBarVM> progressBarVMs;

        public ProgressBarMultiContainerVM() {
            ProgressBarVMs = new ObservableCollection<ProgressBarVM>();
        }

        private List<Func<Task>> _actions = new List<Func<Task>>();
        public void AddAction(Func<Task> action) {
            _actions.Add(action);
        }

        public Task RunAsync() {
            var tasks = new List<Task>();
            foreach (var action in _actions) {
                tasks.Add(action?.Invoke() ?? Task.CompletedTask);
            }
            return Task.WhenAll(tasks);
        }

        public bool? Result {
            get => _result;
            set => SetProperty(ref _result, value);
        }
        private bool? _result = null;
    }
}
