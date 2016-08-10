﻿using Microsoft.Build.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        class EvaluatorData : IEvaluatorData<P, I, M, D>
        {
            IEvaluatorData<P, I, M, D> _wrappedData;
            Func<string, ICollection<I>> _itemGetter;

            public EvaluatorData(IEvaluatorData<P, I, M, D> wrappedData, Func<string, ICollection<I>> itemGetter)
            {
                _wrappedData = wrappedData;
                _itemGetter = itemGetter;
            }

            public ItemDictionary<I> Items
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public List<ProjectItemElement> EvaluatedItemElements
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public ICollection<I> GetItems(string itemType)
            {
                return _itemGetter(itemType);
            }


            public IDictionary<string, List<TargetSpecification>> AfterTargets
            {
                get
                {
                    return _wrappedData.AfterTargets;
                }

                set
                {
                    _wrappedData.AfterTargets = value;
                }
            }

            public IDictionary<string, List<TargetSpecification>> BeforeTargets
            {
                get
                {
                    return _wrappedData.BeforeTargets;
                }

                set
                {
                    _wrappedData.BeforeTargets = value;
                }
            }

            public Dictionary<string, List<string>> ConditionedProperties
            {
                get
                {
                    return _wrappedData.ConditionedProperties;
                }
            }

            public List<string> DefaultTargets
            {
                get
                {
                    return _wrappedData.DefaultTargets;
                }

                set
                {
                    _wrappedData.DefaultTargets = value;
                }
            }

            public string Directory
            {
                get
                {
                    return _wrappedData.Directory;
                }
            }

            public string ExplicitToolsVersion
            {
                get
                {
                    return _wrappedData.ExplicitToolsVersion;
                }
            }

            public PropertyDictionary<ProjectPropertyInstance> GlobalPropertiesDictionary
            {
                get
                {
                    return _wrappedData.GlobalPropertiesDictionary;
                }
            }

            public ISet<string> GlobalPropertiesToTreatAsLocal
            {
                get
                {
                    return _wrappedData.GlobalPropertiesToTreatAsLocal;
                }
            }

            public List<string> InitialTargets
            {
                get
                {
                    return _wrappedData.InitialTargets;
                }

                set
                {
                    _wrappedData.InitialTargets = value;
                }
            }

            public IEnumerable<D> ItemDefinitionsEnumerable
            {
                get
                {
                    return _wrappedData.ItemDefinitionsEnumerable;
                }
            }

           

            public PropertyDictionary<P> Properties
            {
                get
                {
                    return _wrappedData.Properties;
                }
            }

            public bool ShouldEvaluateForDesignTime
            {
                get
                {
                    return _wrappedData.ShouldEvaluateForDesignTime;
                }
            }

            public string SubToolsetVersion
            {
                get
                {
                    return _wrappedData.SubToolsetVersion;
                }
            }

            public TaskRegistry TaskRegistry
            {
                get
                {
                    return _wrappedData.TaskRegistry;
                }

                set
                {
                    _wrappedData.TaskRegistry = value;
                }
            }

            public Toolset Toolset
            {
                get
                {
                    return _wrappedData.Toolset;
                }
            }

            public void AddItem(I item)
            {
                throw new NotSupportedException();
            }

            public IItemDefinition<M> AddItemDefinition(string itemType)
            {
                throw new NotSupportedException();
            }

            public void AddItemIgnoringCondition(I item)
            {
                throw new NotSupportedException();
            }

            public void AddTarget(ProjectTargetInstance target)
            {
                throw new NotSupportedException();
            }

            public void AddToAllEvaluatedItemDefinitionMetadataList(M itemDefinitionMetadatum)
            {
                throw new NotSupportedException();
            }

            public void AddToAllEvaluatedItemsList(I item)
            {
                throw new NotSupportedException();
            }

            public void AddToAllEvaluatedPropertiesList(P property)
            {
                throw new NotSupportedException();
            }

            public bool EvaluateCondition(string condition)
            {
                throw new NotSupportedException();
            }

            public string ExpandString(string unexpandedValue)
            {
                throw new NotSupportedException();
            }

            public void FinishEvaluation()
            {
                _wrappedData.FinishEvaluation();
            }

            public IItemDefinition<M> GetItemDefinition(string itemType)
            {
                return _wrappedData.GetItemDefinition(itemType);
            }

            public P GetProperty(string name)
            {
                return _wrappedData.GetProperty(name);
            }

            public P GetProperty(string name, int startIndex, int endIndex)
            {
                return _wrappedData.GetProperty(name, startIndex, endIndex);
            }

            public ProjectTargetInstance GetTarget(string targetName)
            {
                return _wrappedData.GetTarget(targetName);
            }

            public void InitializeForEvaluation(IToolsetProvider toolsetProvider)
            {
                _wrappedData.InitializeForEvaluation(toolsetProvider);
            }

            public void RecordImport(ProjectImportElement importElement, ProjectRootElement import, int versionEvaluated)
            {
                _wrappedData.RecordImport(importElement, import, versionEvaluated);
            }

            public void RecordImportWithDuplicates(ProjectImportElement importElement, ProjectRootElement import, int versionEvaluated)
            {
                _wrappedData.RecordImportWithDuplicates(importElement, import, versionEvaluated);
            }

            public P SetProperty(ProjectPropertyElement propertyElement, string evaluatedValueEscaped, P predecessor)
            {
                return _wrappedData.SetProperty(propertyElement, evaluatedValueEscaped, predecessor);
            }

            public P SetProperty(string name, string evaluatedValueEscaped, bool isGlobalProperty, bool mayBeReserved)
            {
                return _wrappedData.SetProperty(name, evaluatedValueEscaped, isGlobalProperty, mayBeReserved);
            }
        }
    }

    
}
