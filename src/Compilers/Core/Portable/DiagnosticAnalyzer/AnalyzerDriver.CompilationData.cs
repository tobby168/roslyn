﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.CodeAnalysis.Diagnostics
{
    internal abstract partial class AnalyzerDriver : IDisposable
    {
        internal class CompilationData
        {
            /// <summary>
            /// Cached semantic model for the compilation trees.
            /// PERF: This cache enables us to re-use semantic model's bound node cache across analyzer execution and diagnostic queries.
            /// </summary>
            private readonly Dictionary<SyntaxTree, SemanticModel> _semanticModelsMap;

            private readonly Dictionary<SyntaxReference, DeclarationAnalysisData> _declarationAnalysisDataMap;

            public CompilationData(Compilation comp)
            {
                _semanticModelsMap = new Dictionary<SyntaxTree, SemanticModel>();
                this.SuppressMessageAttributeState = new SuppressMessageAttributeState(comp);
                _declarationAnalysisDataMap = new Dictionary<SyntaxReference, DeclarationAnalysisData>();
            }

            public SuppressMessageAttributeState SuppressMessageAttributeState { get; }

            public SemanticModel GetOrCreateCachedSemanticModel(SyntaxTree tree, Compilation compilation, CancellationToken cancellationToken)
            {
                SemanticModel? model;
                lock (_semanticModelsMap)
                {
                    if (_semanticModelsMap.TryGetValue(tree, out model) && model.Compilation == compilation)
                    {
                        return model;
                    }
                }

                model = compilation.GetSemanticModel(tree);

                lock (_semanticModelsMap)
                {
                    _semanticModelsMap[tree] = model;
                }

                return model;
            }

            public bool RemoveCachedSemanticModel(SyntaxTree tree)
            {
                lock (_semanticModelsMap)
                {
                    return _semanticModelsMap.Remove(tree);
                }
            }

            internal DeclarationAnalysisData GetOrComputeDeclarationAnalysisData(
                SyntaxReference declaration,
                Func<DeclarationAnalysisData> computeDeclarationAnalysisData,
                bool cacheAnalysisData)
            {
                if (!cacheAnalysisData)
                {
                    return computeDeclarationAnalysisData();
                }

                lock (_declarationAnalysisDataMap)
                {
                    if (_declarationAnalysisDataMap.TryGetValue(declaration, out var cachedData))
                    {
                        return cachedData;
                    }
                }

                DeclarationAnalysisData data = computeDeclarationAnalysisData();

                lock (_declarationAnalysisDataMap)
                {
                    if (!_declarationAnalysisDataMap.TryGetValue(declaration, out var existingData))
                    {
                        _declarationAnalysisDataMap.Add(declaration, data);
                    }
                    else
                    {
                        data = existingData;
                    }
                }

                return data;
            }

            internal void ClearDeclarationAnalysisData(SyntaxReference declaration)
            {
                lock (_declarationAnalysisDataMap)
                {
                    _declarationAnalysisDataMap.Remove(declaration);
                }
            }
        }
    }
}
