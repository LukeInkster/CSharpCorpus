﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Parses a project from raw XML into strongly typed objects</summary>
//-----------------------------------------------------------------------

using Microsoft.Build.Shared;
using System;
using System.Xml;
using System.Collections.Generic;
using System.Globalization;
#if (!STANDALONEBUILD)
using Microsoft.Internal.Performance;

#if MSBUILDENABLEVSPROFILING 
using Microsoft.VisualStudio.Profiler;
#endif
#endif
using Expander = Microsoft.Build.Evaluation.Expander<Microsoft.Build.Evaluation.ProjectProperty, Microsoft.Build.Evaluation.ProjectItem>;
using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Parses a project from raw XML into strongly typed objects
    /// </summary>
    internal class ProjectParser
    {
        /// <summary>
        /// Maximum nesting level of Choose elements. No reasonable project needs more than this
        /// </summary>
        internal const int MaximumChooseNesting = 50;

        /// <summary>
        /// Valid attribute list when only Condition and Label are valid
        /// </summary>
        private readonly static string[] s_validAttributesOnlyConditionAndLabel = new string[] { XMakeAttributes.condition, XMakeAttributes.label };

        /// <summary>
        /// Valid attribute list for item
        /// </summary>
        private readonly static string[] s_validAttributesOnItem = new string[] { XMakeAttributes.condition, XMakeAttributes.label, XMakeAttributes.include, XMakeAttributes.exclude, XMakeAttributes.remove, XMakeAttributes.keepMetadata, XMakeAttributes.removeMetadata, XMakeAttributes.keepDuplicates };

        /// <summary>
        /// Valid attributes on import element
        /// </summary>
        private readonly static string[] s_validAttributesOnImport = new string[] { XMakeAttributes.condition, XMakeAttributes.label, XMakeAttributes.project };

        /// <summary>
        /// Valid attributes on usingtask element
        /// </summary>
        private readonly static string[] s_validAttributesOnUsingTask = new string[] { XMakeAttributes.condition, XMakeAttributes.label, XMakeAttributes.taskName, XMakeAttributes.assemblyFile, XMakeAttributes.assemblyName, XMakeAttributes.taskFactory, XMakeAttributes.architecture, XMakeAttributes.runtime, XMakeAttributes.requiredPlatform, XMakeAttributes.requiredRuntime };

        /// <summary>
        /// Valid attributes on target element
        /// </summary>
        private readonly static string[] s_validAttributesOnTarget = new string[] { XMakeAttributes.condition, XMakeAttributes.label, XMakeAttributes.name, XMakeAttributes.inputs, XMakeAttributes.outputs, XMakeAttributes.keepDuplicateOutputs, XMakeAttributes.dependsOnTargets, XMakeAttributes.beforeTargets, XMakeAttributes.afterTargets, XMakeAttributes.returns };

        /// <summary>
        /// Valid attributes on on error element
        /// </summary>
        private readonly static string[] s_validAttributesOnOnError = new string[] { XMakeAttributes.condition, XMakeAttributes.label, XMakeAttributes.executeTargets };

        /// <summary>
        /// Valid attributes on output element
        /// </summary>
        private readonly static string[] s_validAttributesOnOutput = new string[] { XMakeAttributes.condition, XMakeAttributes.label, XMakeAttributes.taskParameter, XMakeAttributes.itemName, XMakeAttributes.propertyName };

        /// <summary>
        /// Valid attributes on UsingTaskParameter element
        /// </summary>
        private readonly static string[] s_validAttributesOnUsingTaskParameter = new string[] { XMakeAttributes.parameterType, XMakeAttributes.output, XMakeAttributes.required };

        /// <summary>
        /// Valid attributes on UsingTaskTask element
        /// </summary>
        private readonly static string[] s_validAttributesOnUsingTaskBody = new string[] { XMakeAttributes.evaluate };

        /// <summary>
        /// The ProjectRootElement to parse into
        /// </summary>
        private ProjectRootElement _project;

        /// <summary>
        /// The document to parse from
        /// </summary>
        private XmlDocumentWithLocation _document;

        /// <summary>
        /// Whether a ProjectExtensions node has been encountered already.
        /// It's not supposed to appear more than once.
        /// </summary>
        private bool _seenProjectExtensions;

        /// <summary>
        /// Private constructor to give static semantics
        /// </summary>
        private ProjectParser(XmlDocumentWithLocation document, ProjectRootElement project)
        {
            ErrorUtilities.VerifyThrowInternalNull(project, "project");
            ErrorUtilities.VerifyThrowInternalNull(document, "document");

            _document = document;
            _project = project;
        }

        /// <summary>
        /// Parses the document into the provided ProjectRootElement.
        /// Throws InvalidProjectFileExceptions for syntax errors.
        /// </summary>
        /// <remarks>
        /// The code markers here used to be around the Project class constructor in the old code.
        /// In the new code, that's not very interesting; we are repurposing to wrap parsing the XML.
        /// </remarks>
        internal static void Parse(XmlDocumentWithLocation document, ProjectRootElement projectRootElement)
        {
#if MSBUILDENABLEVSPROFILING
            try
            {
                string projectFile = String.IsNullOrEmpty(projectRootElement.ProjectFileLocation.File) ? "(null)" : projectRootElement.ProjectFileLocation.File;
                string projectParseBegin = String.Format(CultureInfo.CurrentCulture, "Parse Project {0} - Begin", projectFile);
                DataCollection.CommentMarkProfile(8808, projectParseBegin);
#endif
#if (!STANDALONEBUILD)
                using (new CodeMarkerStartEnd(CodeMarkerEvent.perfMSBuildProjectConstructBegin, CodeMarkerEvent.perfMSBuildProjectConstructEnd))
#endif
            {
                ProjectParser parser = new ProjectParser(document, projectRootElement);
                parser.Parse();
            }
#if MSBUILDENABLEVSPROFILING 
            }
            finally
            {
                string projectFile = String.IsNullOrEmpty(projectRootElement.ProjectFileLocation.File) ? "(null)" : projectRootElement.ProjectFileLocation.File;
                string projectParseEnd = String.Format(CultureInfo.CurrentCulture, "Parse Project {0} - End", projectFile);
                DataCollection.CommentMarkProfile(8809, projectParseEnd);
            }
#endif
        }

        /// <summary>
        /// Parses the project into the ProjectRootElement
        /// </summary>
        private void Parse()
        {
            // XML guarantees exactly one root element
            XmlElementWithLocation element = null;
            foreach (XmlNode childNode in _document.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element)
                {
                    element = (XmlElementWithLocation)childNode;
                    break;
                }
            }

            ProjectErrorUtilities.VerifyThrowInvalidProject(element != null, ElementLocation.Create(_document.FullPath), "NoRootProjectElement", XMakeElements.project);
            ProjectErrorUtilities.VerifyThrowInvalidProject(element.Name != XMakeElements.visualStudioProject, element.Location, "ProjectUpgradeNeeded", _project.FullPath);
            ProjectErrorUtilities.VerifyThrowInvalidProject(element.LocalName == XMakeElements.project, element.Location, "UnrecognizedElement", element.Name);

            if (element.Prefix.Length > 0 || !String.Equals(element.NamespaceURI, XMakeAttributes.defaultXmlNamespace, StringComparison.OrdinalIgnoreCase))
            {
                ProjectErrorUtilities.ThrowInvalidProject(element.Location, "ProjectMustBeInMSBuildXmlNamespace", XMakeAttributes.defaultXmlNamespace);
            }

            ParseProjectElement(element);
        }

        /// <summary>
        /// Parse a ProjectRootElement from an element
        /// </summary>
        private void ParseProjectElement(XmlElementWithLocation element)
        {
            // Historically, we allow any attribute on the Project element

            // The element wasn't available to the ProjectRootElement constructor,
            // so we have to set it now
            _project.SetProjectRootElementFromParser(element, _project);

            ParseProjectRootElementChildren(element);
        }

        /// <summary>
        /// Parse the child of a ProjectRootElement
        /// </summary>
        private void ParseProjectRootElementChildren(XmlElementWithLocation element)
        {
            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                ProjectElement child = null;

                switch (childElement.Name)
                {
                    case XMakeElements.propertyGroup:
                        child = ParseProjectPropertyGroupElement(childElement, _project);
                        break;

                    case XMakeElements.itemGroup:
                        child = ParseProjectItemGroupElement(childElement, _project);
                        break;

                    case XMakeElements.importGroup:
                        child = ParseProjectImportGroupElement(childElement, _project);
                        break;

                    case XMakeElements.import:
                        child = ParseProjectImportElement(childElement, _project);
                        break;

                    case XMakeElements.usingTask:
                        child = ParseProjectUsingTaskElement(childElement);
                        break;

                    case XMakeElements.target:
                        child = ParseProjectTargetElement(childElement);
                        break;

                    case XMakeElements.itemDefinitionGroup:
                        child = ParseProjectItemDefinitionGroupElement(childElement);
                        break;

                    case XMakeElements.choose:
                        child = ParseProjectChooseElement(childElement, _project, 0 /* nesting depth */);
                        break;

                    case XMakeElements.projectExtensions:
                        child = ParseProjectExtensionsElement(childElement);
                        break;

                    // Obsolete
                    case XMakeElements.error:
                    case XMakeElements.warning:
                    case XMakeElements.message:
                        ProjectErrorUtilities.ThrowInvalidProject(childElement.Location, "ErrorWarningMessageNotSupported", childElement.Name);
                        break;

                    default:
                        ProjectXmlUtilities.ThrowProjectInvalidChildElement(childElement.Name, childElement.ParentNode.Name, childElement.Location);
                        break;
                }

                _project.AppendParentedChildNoChecks(child);
            }
        }

        /// <summary>
        /// Parse a ProjectPropertyGroupElement from the element
        /// </summary>
        private ProjectPropertyGroupElement ParseProjectPropertyGroupElement(XmlElementWithLocation element, ProjectElementContainer parent)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnlyConditionAndLabel);

            ProjectPropertyGroupElement propertyGroup = new ProjectPropertyGroupElement(element, parent, _project);

            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                ProjectPropertyElement property = ParseProjectPropertyElement(childElement, propertyGroup);

                propertyGroup.AppendParentedChildNoChecks(property);
            }

            return propertyGroup;
        }

        /// <summary>
        /// Parse a ProjectPropertyElement from the element
        /// </summary>
        private ProjectPropertyElement ParseProjectPropertyElement(XmlElementWithLocation element, ProjectPropertyGroupElement parent)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnlyConditionAndLabel);

            XmlUtilities.VerifyThrowProjectValidElementName(element);
            ProjectErrorUtilities.VerifyThrowInvalidProject(XMakeElements.IllegalItemPropertyNames[element.Name] == null && !ReservedPropertyNames.IsReservedProperty(element.Name), element.Location, "CannotModifyReservedProperty", element.Name);

            // All children inside a property are ignored, since they are only part of its value
            return new ProjectPropertyElement(element, parent, _project);
        }

        /// <summary>
        /// Parse a ProjectItemGroupElement
        /// </summary>
        private ProjectItemGroupElement ParseProjectItemGroupElement(XmlElementWithLocation element, ProjectElementContainer parent)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnlyConditionAndLabel);

            ProjectItemGroupElement itemGroup = new ProjectItemGroupElement(element, parent, _project);

            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                ProjectItemElement item = ParseProjectItemElement(childElement, itemGroup);

                itemGroup.AppendParentedChildNoChecks(item);
            }

            return itemGroup;
        }

        /// <summary>
        /// Parse a ProjectItemElement
        /// </summary>
        private ProjectItemElement ParseProjectItemElement(XmlElementWithLocation element, ProjectItemGroupElement parent)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnItem);

            bool belowTarget = parent.Parent is ProjectTargetElement;

            string itemType = element.Name;
            string include = element.GetAttribute(XMakeAttributes.include);
            string exclude = element.GetAttribute(XMakeAttributes.exclude);
            string remove = element.GetAttribute(XMakeAttributes.remove);

            // Remove must be missing, unless inside a target and Include is missing
            ProjectXmlUtilities.VerifyThrowProjectInvalidAttribute((remove.Length == 0 || (belowTarget && include.Length == 0)), (XmlAttributeWithLocation)element.Attributes[XMakeAttributes.remove]);

            // Include must be present, unless inside a target
            ProjectErrorUtilities.VerifyThrowInvalidProject(include.Length > 0 || belowTarget, element.Location, "MissingRequiredAttribute", XMakeAttributes.include, itemType);

            // Exclude must be missing, unless Include exists
            ProjectXmlUtilities.VerifyThrowProjectInvalidAttribute(exclude.Length == 0 || include.Length > 0, (XmlAttributeWithLocation)element.Attributes[XMakeAttributes.exclude]);

            // If we have an Include attribute at all, it must have non-zero length
            ProjectErrorUtilities.VerifyThrowInvalidProject(include.Length > 0 || element.Attributes[XMakeAttributes.include] == null, element.Location, "MissingRequiredAttribute", XMakeAttributes.include, itemType);

            XmlUtilities.VerifyThrowProjectValidElementName(element);
            ProjectErrorUtilities.VerifyThrowInvalidProject(XMakeElements.IllegalItemPropertyNames[itemType] == null, element.Location, "CannotModifyReservedItem", itemType);

            ProjectItemElement item = new ProjectItemElement(element, parent, _project);

            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                ProjectMetadataElement metadatum = ParseProjectMetadataElement(childElement, item);

                item.AppendParentedChildNoChecks(metadatum);
            }

            return item;
        }

        /// <summary>
        /// Parse a ProjectMetadataElement 
        /// </summary>
        private ProjectMetadataElement ParseProjectMetadataElement(XmlElementWithLocation element, ProjectElementContainer parent)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnlyConditionAndLabel);

            XmlUtilities.VerifyThrowProjectValidElementName(element);

            ProjectErrorUtilities.VerifyThrowInvalidProject(!(parent is ProjectItemElement) || ((ProjectItemElement)parent).Remove.Length == 0, element.Location, "ChildElementsBelowRemoveNotAllowed", element.Name);
            ProjectErrorUtilities.VerifyThrowInvalidProject(!FileUtilities.ItemSpecModifiers.IsItemSpecModifier(element.Name), element.Location, "ItemSpecModifierCannotBeCustomMetadata", element.Name);
            ProjectErrorUtilities.VerifyThrowInvalidProject(XMakeElements.IllegalItemPropertyNames[element.Name] == null, element.Location, "CannotModifyReservedItemMetadata", element.Name);

            ProjectMetadataElement metadatum = new ProjectMetadataElement(element, parent, _project);

            // If the parent is an item definition, we don't allow expressions like @(foo) in the value, as no items exist at that point
            if (parent is ProjectItemDefinitionElement)
            {
                bool containsItemVector = Expander.ExpressionContainsItemVector(metadatum.Value);
                ProjectErrorUtilities.VerifyThrowInvalidProject(!containsItemVector, element.Location, "MetadataDefinitionCannotContainItemVectorExpression", metadatum.Value, metadatum.Name);
            }

            return metadatum;
        }

        /// <summary>
        /// Parse a ProjectImportGroupElement
        /// </summary>
        /// <param name="element">The XML element to parse</param>
        /// <returns>A ProjectImportGroupElement derived from the XML element passed in</returns>
        private ProjectImportGroupElement ParseProjectImportGroupElement(XmlElementWithLocation element, ProjectRootElement parent)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnlyConditionAndLabel);

            ProjectImportGroupElement importGroup = new ProjectImportGroupElement(element, parent, _project);

            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject
                    (
                    childElement.Name == XMakeElements.import,
                    childElement.Location,
                    "UnrecognizedChildElement",
                    childElement.Name,
                    element.Name
                    );

                ProjectImportElement item = ParseProjectImportElement(childElement, importGroup);

                importGroup.AppendParentedChildNoChecks(item);
            }

            return importGroup;
        }

        /// <summary>
        /// Parse a ProjectImportElement that is contained in an ImportGroup
        /// </summary>
        private ProjectImportElement ParseProjectImportElement(XmlElementWithLocation element, ProjectElementContainer parent)
        {
            ProjectErrorUtilities.VerifyThrowInvalidProject
                (
                parent is ProjectRootElement || parent is ProjectImportGroupElement,
                element.Location,
                "UnrecognizedParentElement",
                parent,
                element
                );

            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnImport);

            ProjectXmlUtilities.VerifyThrowProjectRequiredAttribute(element, XMakeAttributes.project);

            ProjectXmlUtilities.VerifyThrowProjectNoChildElements(element);

            return new ProjectImportElement(element, parent, _project);
        }

        /// <summary>
        /// Parse a UsingTaskParameterGroupElement from the element
        /// </summary>
        private UsingTaskParameterGroupElement ParseUsingTaskParameterGroupElement(XmlElementWithLocation element, ProjectElementContainer parent)
        {
            // There should be no attributes
            ProjectXmlUtilities.VerifyThrowProjectNoAttributes(element);

            UsingTaskParameterGroupElement parameterGroup = new UsingTaskParameterGroupElement(element, parent, _project);

            HashSet<String> listOfChildElementNames = new HashSet<string>();
            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                // The parameter already exists this means there is a duplicate child item. Throw an exception.
                if (listOfChildElementNames.Contains(childElement.Name))
                {
                    ProjectXmlUtilities.ThrowProjectInvalidChildElementDueToDuplicate(childElement);
                }
                else
                {
                    ProjectUsingTaskParameterElement parameter = ParseUsingTaskParameterElement(childElement, parameterGroup);
                    parameterGroup.AppendParentedChildNoChecks(parameter);

                    // Add the name of the child element to the hashset so we can check for a duplicate child element
                    listOfChildElementNames.Add(childElement.Name);
                }
            }

            return parameterGroup;
        }

        /// <summary>
        /// Parse a UsingTaskBodyElement from the element
        /// </summary>
        private ProjectUsingTaskBodyElement ParseUsingTaskBodyElement(XmlElementWithLocation element, ProjectUsingTaskElement parent)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnUsingTaskBody);
            XmlUtilities.VerifyThrowProjectValidElementName(element);
            return new ProjectUsingTaskBodyElement(element, parent, _project);
        }

        /// <summary>
        /// Parse a UsingTaskParameterElement from the element
        /// </summary>
        private ProjectUsingTaskParameterElement ParseUsingTaskParameterElement(XmlElementWithLocation element, UsingTaskParameterGroupElement parent)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnUsingTaskParameter);
            XmlUtilities.VerifyThrowProjectValidElementName(element);
            return new ProjectUsingTaskParameterElement(element, parent, _project);
        }

        /// <summary>
        /// Parse a ProjectUsingTaskElement
        /// </summary>
        private ProjectUsingTaskElement ParseProjectUsingTaskElement(XmlElementWithLocation element)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnUsingTask);
            ProjectErrorUtilities.VerifyThrowInvalidProject(element.GetAttribute(XMakeAttributes.taskName).Length > 0, element.Location, "ProjectTaskNameEmpty");

            string assemblyName = element.GetAttribute(XMakeAttributes.assemblyName);
            string assemblyFile = element.GetAttribute(XMakeAttributes.assemblyFile);

            ProjectErrorUtilities.VerifyThrowInvalidProject
                (
                ((assemblyName.Length > 0) ^ (assemblyFile.Length > 0)),
                element.Location,
                "UsingTaskAssemblySpecification",
                XMakeElements.usingTask,
                XMakeAttributes.assemblyName,
                XMakeAttributes.assemblyFile
                );

            ProjectXmlUtilities.VerifyThrowProjectAttributeEitherMissingOrNotEmpty(element, XMakeAttributes.assemblyName);
            ProjectXmlUtilities.VerifyThrowProjectAttributeEitherMissingOrNotEmpty(element, XMakeAttributes.assemblyFile);

            ProjectUsingTaskElement usingTask = new ProjectUsingTaskElement(element, _project, _project);

            bool foundTaskElement = false;
            bool foundParameterGroup = false;

            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                ProjectElement child = null;
                string childElementName = childElement.Name;
                switch (childElementName)
                {
                    case XMakeElements.usingTaskParameterGroup:
                        if (foundParameterGroup)
                        {
                            ProjectXmlUtilities.ThrowProjectInvalidChildElementDueToDuplicate(childElement);
                        }

                        child = ParseUsingTaskParameterGroupElement(childElement, usingTask);
                        foundParameterGroup = true;
                        break;
                    case XMakeElements.usingTaskBody:
                        if (foundTaskElement)
                        {
                            ProjectXmlUtilities.ThrowProjectInvalidChildElementDueToDuplicate(childElement);
                        }

                        child = ParseUsingTaskBodyElement(childElement, usingTask);
                        foundTaskElement = true;
                        break;
                    default:
                        ProjectXmlUtilities.ThrowProjectInvalidChildElement(childElement.Name, element.Name, element.Location);
                        break;
                }

                usingTask.AppendParentedChildNoChecks(child);
            }

            return usingTask;
        }

        /// <summary>
        /// Parse a ProjectTargetElement
        /// </summary>
        private ProjectTargetElement ParseProjectTargetElement(XmlElementWithLocation element)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnTarget);
            ProjectXmlUtilities.VerifyThrowProjectRequiredAttribute(element, XMakeAttributes.name);

            string targetName = ProjectXmlUtilities.GetAttributeValue(element, XMakeAttributes.name);

            // Orcas compat: all target names are automatically unescaped
            targetName = EscapingUtilities.UnescapeAll(targetName);

            int indexOfSpecialCharacter = targetName.IndexOfAny(XMakeElements.illegalTargetNameCharacters);
            if (indexOfSpecialCharacter >= 0)
            {
                ProjectErrorUtilities.ThrowInvalidProject(element.GetAttributeLocation(XMakeAttributes.name), "NameInvalid", targetName, targetName[indexOfSpecialCharacter]);
            }

            ProjectTargetElement target = new ProjectTargetElement(element, _project, _project);
            ProjectOnErrorElement onError = null;

            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                ProjectElement child = null;

                switch (childElement.Name)
                {
                    case XMakeElements.propertyGroup:
                        if (onError != null)
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(onError.Location, "NodeMustBeLastUnderElement", XMakeElements.onError, XMakeElements.target, childElement.Name);
                        }

                        child = ParseProjectPropertyGroupElement(childElement, target);
                        break;

                    case XMakeElements.itemGroup:
                        if (onError != null)
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(onError.Location, "NodeMustBeLastUnderElement", XMakeElements.onError, XMakeElements.target, childElement.Name);
                        }

                        child = ParseProjectItemGroupElement(childElement, target);
                        break;

                    case XMakeElements.onError:
                        onError = ParseProjectOnErrorElement(childElement, target);
                        child = onError;
                        break;

                    case XMakeElements.itemDefinitionGroup:
                        ProjectErrorUtilities.ThrowInvalidProject(childElement.Location, "ItemDefinitionGroupNotLegalInsideTarget", childElement.Name);
                        break;

                    default:
                        if (onError != null)
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(onError.Location, "NodeMustBeLastUnderElement", XMakeElements.onError, XMakeElements.target, childElement.Name);
                        }

                        child = ParseProjectTaskElement(childElement, target);
                        break;
                }

                target.AppendParentedChildNoChecks(child);
            }

            return target;
        }

        /// <summary>
        /// Parse a ProjectTaskElement
        /// </summary>
        private ProjectTaskElement ParseProjectTaskElement(XmlElementWithLocation element, ProjectTargetElement parent)
        {
            foreach (XmlAttributeWithLocation attribute in element.Attributes)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject
                (
                !XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(attribute.Name),
                attribute.Location,
                "BadlyCasedSpecialTaskAttribute",
                attribute.Name,
                element.Name,
                element.Name
                );
            }

            ProjectTaskElement task = new ProjectTaskElement(element, parent, _project);

            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(childElement.Name == XMakeElements.output, childElement.Location, "UnrecognizedChildElement", childElement.Name, task.Name);

                ProjectOutputElement output = ParseProjectOutputElement(childElement, task);

                task.AppendParentedChildNoChecks(output);
            }

            return task;
        }

        /// <summary>
        /// Parse a ProjectOutputElement
        /// </summary>
        private ProjectOutputElement ParseProjectOutputElement(XmlElementWithLocation element, ProjectTaskElement parent)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnOutput);
            ProjectXmlUtilities.VerifyThrowProjectRequiredAttribute(element, XMakeAttributes.taskParameter);
            ProjectXmlUtilities.VerifyThrowProjectNoChildElements(element);

            string taskParameter = element.GetAttribute(XMakeAttributes.taskParameter);
            string itemName = element.GetAttribute(XMakeAttributes.itemName);
            string propertyName = element.GetAttribute(XMakeAttributes.propertyName);

            ProjectErrorUtilities.VerifyThrowInvalidProject
                (
                (itemName.Length > 0 || propertyName.Length > 0) && (itemName.Length == 0 || propertyName.Length == 0),
                element.Location,
                "InvalidTaskOutputSpecification",
                parent.Name
                );

            ProjectXmlUtilities.VerifyThrowProjectAttributeEitherMissingOrNotEmpty(element, XMakeAttributes.itemName);
            ProjectXmlUtilities.VerifyThrowProjectAttributeEitherMissingOrNotEmpty(element, XMakeAttributes.propertyName);

            ProjectErrorUtilities.VerifyThrowInvalidProject(!ReservedPropertyNames.IsReservedProperty(propertyName), element.Location, "CannotModifyReservedProperty", propertyName);

            return new ProjectOutputElement(element, parent, _project);
        }

        /// <summary>
        /// Parse a ProjectOnErrorElement
        /// </summary>
        private ProjectOnErrorElement ParseProjectOnErrorElement(XmlElementWithLocation element, ProjectTargetElement parent)
        {
            // Previous OM accidentally didn't verify ExecuteTargets on parse,
            // but we do, as it makes no sense 
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnOnError);
            ProjectXmlUtilities.VerifyThrowProjectRequiredAttribute(element, XMakeAttributes.executeTargets);
            ProjectXmlUtilities.VerifyThrowProjectNoChildElements(element);

            return new ProjectOnErrorElement(element, parent, _project);
        }

        /// <summary>
        /// Parse a ProjectItemDefinitionGroupElement
        /// </summary>
        private ProjectItemDefinitionGroupElement ParseProjectItemDefinitionGroupElement(XmlElementWithLocation element)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnlyConditionAndLabel);

            ProjectItemDefinitionGroupElement itemDefinitionGroup = new ProjectItemDefinitionGroupElement(element, _project, _project);

            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                ProjectItemDefinitionElement itemDefinition = ParseProjectItemDefinitionXml(childElement, itemDefinitionGroup);

                itemDefinitionGroup.AppendParentedChildNoChecks(itemDefinition);
            }

            return itemDefinitionGroup;
        }

        /// <summary>
        /// Pasre a ProjectItemDefinitionElement
        /// </summary>
        private ProjectItemDefinitionElement ParseProjectItemDefinitionXml(XmlElementWithLocation element, ProjectItemDefinitionGroupElement parent)
        {
            ProjectXmlUtilities.VerifyThrowProjectAttributes(element, s_validAttributesOnlyConditionAndLabel);

            // Orcas inadvertently did not check for reserved item types (like "Choose") in item definitions,
            // as we do for item types in item groups. So we do not have a check here.
            // Although we could perhaps add one, as such item definitions couldn't be used 
            // since no items can have the reserved itemType.
            ProjectItemDefinitionElement itemDefinition = new ProjectItemDefinitionElement(element, parent, _project);

            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                ProjectMetadataElement metadatum = ParseProjectMetadataElement(childElement, itemDefinition);

                itemDefinition.AppendParentedChildNoChecks(metadatum);
            }

            return itemDefinition;
        }

        /// <summary>
        /// Parse a ProjectChooseElement
        /// </summary>
        private ProjectChooseElement ParseProjectChooseElement(XmlElementWithLocation element, ProjectElementContainer parent, int nestingDepth)
        {
            ProjectXmlUtilities.VerifyThrowProjectNoAttributes(element);

            ProjectChooseElement choose = new ProjectChooseElement(element, parent, _project);

            nestingDepth++;
            ProjectErrorUtilities.VerifyThrowInvalidProject(nestingDepth <= MaximumChooseNesting, element.Location, "ChooseOverflow", MaximumChooseNesting);

            bool foundWhen = false;
            bool foundOtherwise = false;

            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                ProjectElement child = null;

                switch (childElement.Name)
                {
                    case XMakeElements.when:
                        ProjectErrorUtilities.VerifyThrowInvalidProject(!foundOtherwise, childElement.Location, "WhenNotAllowedAfterOtherwise");
                        child = ParseProjectWhenElement(childElement, choose, nestingDepth);
                        foundWhen = true;
                        break;

                    case XMakeElements.otherwise:
                        ProjectErrorUtilities.VerifyThrowInvalidProject(!foundOtherwise, childElement.Location, "MultipleOtherwise");
                        foundOtherwise = true;
                        child = ParseProjectOtherwiseElement(childElement, choose, nestingDepth);
                        break;

                    default:
                        ProjectXmlUtilities.ThrowProjectInvalidChildElement(childElement.Name, element.Name, element.Location);
                        break;
                }

                choose.AppendParentedChildNoChecks(child);
            }

            nestingDepth--;
            ProjectErrorUtilities.VerifyThrowInvalidProject(foundWhen, element.Location, "ChooseMustContainWhen");

            return choose;
        }

        /// <summary>
        /// Parse a ProjectWhenElement
        /// </summary>
        private ProjectWhenElement ParseProjectWhenElement(XmlElementWithLocation element, ProjectChooseElement parent, int nestingDepth)
        {
            ProjectXmlUtilities.VerifyThrowProjectRequiredAttribute(element, XMakeAttributes.condition);

            ProjectWhenElement when = new ProjectWhenElement(element, parent, _project);

            ParseWhenOtherwiseChildren(element, when, nestingDepth);

            return when;
        }

        /// <summary>
        /// Parse a ProjectOtherwiseElement
        /// </summary>
        private ProjectOtherwiseElement ParseProjectOtherwiseElement(XmlElementWithLocation element, ProjectChooseElement parent, int nestingDepth)
        {
            ProjectXmlUtilities.VerifyThrowProjectNoAttributes(element);

            ProjectOtherwiseElement otherwise = new ProjectOtherwiseElement(element, parent, _project);

            ParseWhenOtherwiseChildren(element, otherwise, nestingDepth);

            return otherwise;
        }

        /// <summary>
        /// Parse the children of a When or Otherwise
        /// </summary>
        private void ParseWhenOtherwiseChildren(XmlElementWithLocation element, ProjectElementContainer parent, int nestingDepth)
        {
            foreach (XmlElementWithLocation childElement in ProjectXmlUtilities.GetVerifyThrowProjectChildElements(element))
            {
                ProjectElement child = null;

                switch (childElement.Name)
                {
                    case XMakeElements.propertyGroup:
                        child = ParseProjectPropertyGroupElement(childElement, parent);
                        break;

                    case XMakeElements.itemGroup:
                        child = ParseProjectItemGroupElement(childElement, parent);
                        break;

                    case XMakeElements.choose:
                        child = ParseProjectChooseElement(childElement, parent, nestingDepth);
                        break;

                    default:
                        ProjectXmlUtilities.ThrowProjectInvalidChildElement(childElement.Name, element.Name, element.Location);
                        break;
                }

                parent.AppendParentedChildNoChecks(child);
            }
        }

        /// <summary>
        /// Parse a ProjectExtensionsElement
        /// </summary>
        private ProjectExtensionsElement ParseProjectExtensionsElement(XmlElementWithLocation element)
        {
            // ProjectExtensions are only found in the main project file - in fact, the code used to ignore them in imported
            // files. We don't.
            ProjectXmlUtilities.VerifyThrowProjectNoAttributes(element);

            ProjectErrorUtilities.VerifyThrowInvalidProject(!_seenProjectExtensions, element.Location, "DuplicateProjectExtensions");
            _seenProjectExtensions = true;

            // All children inside ProjectExtensions are ignored, since they are only part of its value
            return new ProjectExtensionsElement(element, _project, _project);
        }
    }
}
