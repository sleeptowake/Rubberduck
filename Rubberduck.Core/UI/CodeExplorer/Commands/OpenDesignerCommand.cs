using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rubberduck.Navigation.CodeExplorer;
using Rubberduck.Parsing.Symbols;
using Rubberduck.VBEditor.ComManagement;
using Rubberduck.VBEditor.SafeComWrappers;
using Rubberduck.VBEditor.SafeComWrappers.Abstract;

namespace Rubberduck.UI.CodeExplorer.Commands
{
    public class OpenDesignerCommand : CodeExplorerCommandBase
    {
        private static readonly Type[] ApplicableNodes =
        {
            typeof(CodeExplorerComponentViewModel),
            typeof(CodeExplorerMemberViewModel)
        };

        private readonly IProjectsProvider _projectsProvider;
        private readonly IVBE _vbe;

        public OpenDesignerCommand(IProjectsProvider projectsProvider, IVBE vbe)
        {
            _projectsProvider = projectsProvider;
            _vbe = vbe;
        }

        public sealed override IEnumerable<Type> ApplicableNodeTypes => ApplicableNodes;

        protected override bool EvaluateCanExecute(object parameter)
        {
            if (!base.EvaluateCanExecute(parameter) || !(parameter is CodeExplorerItemViewModel node))
            {
                return false;   
            }

            try
            {
                var declaration = node.Declaration;
                if (declaration == null)
                {
                    return false;
                }

                var qualifiedModuleName = declaration.QualifiedModuleName;

                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (declaration.DeclarationType)
                {
                    case DeclarationType.ClassModule:
                        return _projectsProvider.Component(qualifiedModuleName).HasDesigner;
                    case DeclarationType.Document:
                        using (var app = _vbe.HostApplication())
                        {
                            return app?.CanOpenDocumentDesigner(qualifiedModuleName) ?? false;
                        }
                    default:
                        return false;
                }
            }
            catch (COMException)
            {
                // thrown when the component reference is stale
                return false;
            }
        }

        protected override void OnExecute(object parameter)
        {
            if (!base.EvaluateCanExecute(parameter) || 
                !(parameter is CodeExplorerItemViewModel node) ||
                node.Declaration == null ||
                !node.Declaration.DeclarationType.HasFlag(DeclarationType.ClassModule))
            {
                return;
            }

            if(node.Declaration.QualifiedModuleName.ComponentType == ComponentType.Document)
            {
                using (var app = _vbe.HostApplication())
                {
                    if (app?.TryOpenDocumentDesigner(node.Declaration.QualifiedModuleName) ?? false)
                    {
                        return;
                    }
                }
            }

            var component = _projectsProvider.Component(node.Declaration.QualifiedName.QualifiedModuleName);
            using (var designer = component.DesignerWindow())
            {
                if (!designer.IsWrappingNullReference)
                {
                    designer.IsVisible = true;
                }
            }
        }
    }
}
