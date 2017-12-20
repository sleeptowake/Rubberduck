﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rubberduck.UI;
using Rubberduck.UI.Command;
using Rubberduck.VBEditor.SafeComWrappers;
using Rubberduck.VBEditor.SafeComWrappers.Abstract;

namespace Rubberduck.ProjectReferences
{
    public class AddRemoveReferencesModel : ViewModelBase
    {
        public AddRemoveReferencesModel(IReadOnlyList<ReferenceViewModel> references)
        {
            AvailableComLibraries = references.Where(r => r.Type == ReferenceKind.TypeLibrary).ToList();
            AvailableVbaProjects = references.Where(r => r.Type == ReferenceKind.Project).ToList();
            PinnedLibraries = new HashSet<object>(references.Where(r => r.IsPinned));
        }

        public IEnumerable<object> AvailableComLibraries { get; private set; }

        public IEnumerable<object> AvailableVbaProjects { get; private set; }

        public HashSet<object> PinnedLibraries { get; private set; }
    }

    public class ReferenceViewModel : ViewModelBase
    {
        public ReferenceViewModel(IVBProject project)
        {
            Name = project.Name;
            Guid = string.Empty;
            Description = project.Description;
            Version = default(Version);
            FullPath = project.FileName;
            IsBuiltIn = false;
            Type = ReferenceKind.Project;
        }

        public ReferenceViewModel(dynamic info) // todo figure out what type that should be
        {
            Name = info.Name;
            Guid = info.Guid;
            Description = info.Description;
            Version = info.Version;
            FullPath = info.FullPath;
            IsBuiltIn = false;
            Type = ReferenceKind.TypeLibrary;
        }

        public ReferenceViewModel(IReference reference)
        {
            IsSelected = true;
            Name = reference.Name;
            Guid = reference.Guid;
            Description = reference.Description;
            Version = new Version(reference.Major, reference.Minor);
            FullPath = reference.FullPath;
            IsBuiltIn = reference.IsBuiltIn;
            IsBroken = reference.IsBroken;
            Type = reference.Type;
        }

        public bool IsSelected { get; set; }
        public bool IsRemoved { get; set; }
        public bool IsPinned { get; set; }

        public string Name { get; }
        public string Guid { get; }
        public string Description { get; }
        public Version Version { get; }
        public string FullPath { get; }
        public bool IsBuiltIn { get; }
        public bool IsBroken { get; }
        public ReferenceKind Type { get; }
    }
}
