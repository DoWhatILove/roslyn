﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CodeFixes.AddImport
{
    internal abstract partial class AbstractAddImportCodeFixProvider<TSimpleNameSyntax>
    {
        private abstract partial class SymbolReference : Reference
        {
            public readonly SymbolResult<INamespaceOrTypeSymbol> SymbolResult;

            protected abstract bool ShouldAddWithExistingImport(Document document);

            public SymbolReference(
                AbstractAddImportCodeFixProvider<TSimpleNameSyntax> provider,
                SymbolResult<INamespaceOrTypeSymbol> symbolResult)
                : base(provider, new SearchResult(symbolResult))
            {
                this.SymbolResult = symbolResult;
            }

            protected abstract ImmutableArray<string> GetTags(Document document);

            public override bool Equals(object obj)
            {
                var equals = base.Equals(obj);
                if (!equals)
                {
                    return false;
                }

                var name1 = this.SymbolResult.DesiredName;
                var name2 = (obj as SymbolReference)?.SymbolResult.DesiredName;
                return StringComparer.Ordinal.Equals(name1, name2);
            }

            public override int GetHashCode()
                => Hash.Combine(this.SymbolResult.DesiredName, base.GetHashCode());

            //private async Task<CodeActionOperation> GetOperationAsync(
            //    Document document, SyntaxNode node, 
            //    bool placeSystemNamespaceFirst, bool hasExistingImport,
            //    CancellationToken cancellationToken)
            //{
            //    var newDocument = await UpdateDocumentAsync(
            //        document, node, placeSystemNamespaceFirst, hasExistingImport, cancellationToken).ConfigureAwait(false);
            //    var updatedSolution = GetUpdatedSolution(newDocument);

            //    var operation = new ApplyChangesOperation(updatedSolution);
            //    return operation;
            //}

            //protected virtual Solution GetUpdatedSolution(Document newDocument)
            //    => newDocument.Project.Solution;

            private async Task<Document> UpdateDocumentAsync(
                Document document, SyntaxNode contextNode, 
                bool placeSystemNamespaceFirst, bool hasExistingImport,
                CancellationToken cancellationToken)
            {
                // Defer to the language to add the actual import/using.
                if (hasExistingImport)
                {
                    return document;
                }

                (var newContextNode, var newDocument) = await ReplaceNameNodeAsync(
                    contextNode, document, cancellationToken).ConfigureAwait(false);

                return await provider.AddImportAsync(
                    newContextNode, this.SymbolResult.Symbol, newDocument, 
                    placeSystemNamespaceFirst, cancellationToken).ConfigureAwait(false);
            }

            public sealed override async Task<CodeAction> CreateCodeActionAsync(
                Document document, SyntaxNode node,
                bool placeSystemNamespaceFirst, CancellationToken cancellationToken)
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                var (description, hasExistingImport) = GetDescription(document, node, semanticModel, cancellationToken);
                if (description == null)
                {
                    return null;
                }

                if (hasExistingImport && !this.ShouldAddWithExistingImport(document))
                {
                    return null;
                }

                if (!this.SearchResult.DesiredNameMatchesSourceName(document))
                {
                    // The name is going to change.  Make it clear in the description that 
                    // this is going to happen.
                    description = $"{this.SearchResult.DesiredName} - {description}";
                }

                var updatedDocument = await UpdateDocumentAsync(
                    document, node, placeSystemNamespaceFirst, hasExistingImport, cancellationToken).ConfigureAwait(false);

                var cleanedDocument = await CodeAction.CleanupDocumentAsync(
                    updatedDocument, cancellationToken).ConfigureAwait(false);

                var textChanges = await cleanedDocument.GetTextChangesAsync(
                    document, cancellationToken).ConfigureAwait(false);

                return CreateCodeAction(
                    document, textChanges.ToImmutableArray(), description,
                    GetTags(document), GetPriority(document));
            }

            protected abstract CodeAction CreateCodeAction(
                Document document, ImmutableArray<TextChange> textChanges, 
                string description, ImmutableArray<string> tags, CodeActionPriority priority);

            protected abstract CodeActionPriority GetPriority(Document document);

            protected virtual (string description, bool hasExistingImport) GetDescription(
                Document document, SyntaxNode node,
                SemanticModel semanticModel, CancellationToken cancellationToken)
            {
                return provider.GetDescription(
                    document, SymbolResult.Symbol, semanticModel, node, cancellationToken);
            }
        }
    }
}