﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectMetadataElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using Microsoft.Build.Shared;

using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;
using Utilities = Microsoft.Build.Internal.Utilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectMetadataElement class represents a Metadata element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("{Name} Value={Value} Condition={Condition}")]
    public class ProjectMetadataElement : ProjectElement
    {
        /// <summary>
        /// Initialize a parented ProjectMetadataElement
        /// </summary>
        internal ProjectMetadataElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement project)
            : base(xmlElement, parent, project)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
        }

        /// <summary>
        /// Initialize an unparented ProjectMetadataElement
        /// </summary>
        private ProjectMetadataElement(XmlElementWithLocation xmlElement, ProjectRootElement project)
            : base(xmlElement, null, project)
        {
        }

        /// <summary>
        /// Gets or sets the metadata's type.
        /// </summary>
        public string Name
        {
            get { return XmlElement.Name; }
            set { ChangeName(value); }
        }

        /// <summary>
        /// Gets or sets the unevaluated value. 
        /// Returns empty string if it is not present.
        /// </summary>
        public string Value
        {
            get
            {
                return Microsoft.Build.Internal.Utilities.GetXmlNodeInnerContents(XmlElement);
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "Value");
                Microsoft.Build.Internal.Utilities.SetXmlNodeInnerContents(XmlElement, value);
                MarkDirty("Set metadata Value {0}", value);
            }
        }

        /// <summary>
        /// Creates an unparented ProjectMetadataElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent.
        /// </summary>
        internal static ProjectMetadataElement CreateDisconnected(string name, ProjectRootElement containingProject)
        {
            XmlUtilities.VerifyThrowArgumentValidElementName(name);
            ErrorUtilities.VerifyThrowArgument(!FileUtilities.ItemSpecModifiers.IsItemSpecModifier(name), "ItemSpecModifierCannotBeCustomMetadata", name);
            ErrorUtilities.VerifyThrowInvalidOperation(XMakeElements.IllegalItemPropertyNames[name] == null, "CannotModifyReservedItemMetadata", name);

            XmlElementWithLocation element = containingProject.CreateElement(name);

            return new ProjectMetadataElement(element, containingProject);
        }

        /// <summary>
        /// Changes the name.
        /// </summary>
        /// <remarks>
        /// The implementation has to actually replace the element to do this.
        /// </remarks>
        internal void ChangeName(string newName)
        {
            ErrorUtilities.VerifyThrowArgumentLength(newName, "newName");
            XmlUtilities.VerifyThrowArgumentValidElementName(newName);
            ErrorUtilities.VerifyThrowArgument(XMakeElements.IllegalItemPropertyNames[newName] == null, "CannotModifyReservedItemMetadata", newName);

            // Because the element was created from our special XmlDocument, we know it's
            // an XmlElementWithLocation.
            XmlElementWithLocation newElement = (XmlElementWithLocation)XmlUtilities.RenameXmlElement(XmlElement, newName, XMakeAttributes.defaultXmlNamespace);

            ReplaceElement(newElement);
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectItemElement || parent is ProjectItemDefinitionElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateMetadataElement(this.Name);
        }
    }
}
