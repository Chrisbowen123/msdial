﻿using CompMs.CommonMVVM;
using CompMs.MsdialCore.Parser;
using System.Windows;

namespace CompMs.App.Msdial.ViewModel
{
    public abstract class MethodVM : ViewModelBase {
        public abstract void InitializeNewProject(Window window);
        public abstract void LoadProject();
        public abstract void SaveProject();
        public MsdialSerializer Serializer { get; }

        public MethodVM(MsdialSerializer serializer) {
            Serializer = serializer;
        }
    }
}
