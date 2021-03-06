﻿using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using Rubberduck.Parsing.Symbols;
using Rubberduck.VBEditor;

namespace Rubberduck.Parsing.ComReflection
{
    public class LibraryReferencedDeclarationsCollector : ReferencedDeclarationsCollectorBase
    {
        private readonly IComLibraryProvider _comLibraryProvider;

        public LibraryReferencedDeclarationsCollector(IComLibraryProvider comLibraryProvider)
        {
            _comLibraryProvider = comLibraryProvider;
        }

        public override IReadOnlyCollection<Declaration> CollectedDeclarations(ReferenceInfo reference)
        {
            return LoadDeclarationsFromLibrary(reference);
        }

        private List<Declaration> LoadDeclarationsFromLibrary(ReferenceInfo reference)
        {
            var libraryPath = reference.FullPath;
            // Failure to load might mean that it's a "normal" VBProject that will get parsed by us anyway.
            var typeLibrary = GetTypeLibrary(libraryPath);
            if (typeLibrary == null)
            {
                return new List<Declaration>();
            }

            var type = new ComProject(typeLibrary, libraryPath);
            var declarations = LoadDeclarationsFromComProject(type);

            return declarations;
        }

        private ITypeLib GetTypeLibrary(string libraryPath)
        {
            return _comLibraryProvider.LoadTypeLibrary(libraryPath);
        }
    }
}
