﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CodeFixes.AddImport
{
    internal abstract partial class AbstractAddImportCodeFixProvider<TSimpleNameSyntax>
    {
        private partial class SymbolReference
        {
            /// <summary>
            /// Code action we use when just adding a using, possibly with a project or
            /// metadata reference.  We don't use the standard code action types because
            /// we want to do things like show a glyph if this will do more than just add
            /// an import.
            /// </summary>
            protected class SymbolReferenceCodeAction : CodeAction
            {
                public override string Title { get; }
                public override ImmutableArray<string> Tags { get; }
                internal override CodeActionPriority Priority { get; }

                public override string EquivalenceKey => this.Title;

                private readonly Document _contextDocument;
                private readonly ImmutableArray<TextChange> _textChanges;
                private readonly ProjectId _projectReferenceToAdd;
                private readonly ProjectId _portableExecutableReferenceProjectId;
                private readonly string _portableExecutableReferenceFilePathToAdd;

                private SymbolReferenceCodeAction(
                    Document contextDocument,
                    ImmutableArray<TextChange> textChanges,
                    string title, ImmutableArray<string> tags,
                    CodeActionPriority priority)
                {
                    _contextDocument = contextDocument;
                    _textChanges = textChanges;
                    Title = title;
                    Tags = tags;
                    Priority = priority;
                }

                public SymbolReferenceCodeAction(
                    Document contextDocument,
                    ImmutableArray<TextChange> textChanges,
                    string title, ImmutableArray<string> tags,
                    CodeActionPriority priority,
                    ProjectId projectReferenceToAdd)
                    : this(contextDocument, textChanges, title, tags, priority)
                {
                    _projectReferenceToAdd = projectReferenceToAdd;
                }

                public SymbolReferenceCodeAction(
                    Document contextDocument,
                    ImmutableArray<TextChange> textChanges,
                    string title, ImmutableArray<string> tags,
                    CodeActionPriority priority,

                    ProjectId portableExecutableReferenceProjectId,
                    string portableExecutableReferenceFilePathToAdd)
                    : this(contextDocument, textChanges, title, tags, priority)
                {
                    _portableExecutableReferenceProjectId = portableExecutableReferenceProjectId;
                    _portableExecutableReferenceFilePathToAdd = portableExecutableReferenceFilePathToAdd;
                }

                protected override async Task<Solution> GetChangedSolutionAsync(CancellationToken cancellationToken)
                {
                    var oldText = await _contextDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    var newText = oldText.WithChanges(_textChanges);

                    var updatedProject = _contextDocument.WithText(newText).Project;

                    if (WillAddProjectReference())
                    {
                        updatedProject = updatedProject.AddProjectReference(new ProjectReference(_projectReferenceToAdd));
                    }
                    else if (_portableExecutableReferenceFilePathToAdd != null)
                    {
                        var projectWithReference = updatedProject.Solution.GetProject(_portableExecutableReferenceProjectId);
                        var reference = projectWithReference.MetadataReferences
                                                            .OfType<PortableExecutableReference>()
                                                            .First(pe => pe.FilePath == _portableExecutableReferenceFilePathToAdd);

                        updatedProject = updatedProject.AddMetadataReference(reference);
                    }

                    var updatedSolution = updatedProject.Solution;
                    return updatedSolution;
                }

                private bool WillAddProjectReference()
                    => _projectReferenceToAdd != null && _contextDocument.Project.Id != _projectReferenceToAdd;

                internal override bool PerformFinalApplicabilityCheck
                    => WillAddProjectReference();

                internal override bool IsApplicable(Workspace workspace)
                    => WillAddProjectReference() && workspace.CanAddProjectReference(_contextDocument.Project.Id, _projectReferenceToAdd);
            }
        }
    }
}