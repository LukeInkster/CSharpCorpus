﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Represents the ProjectExtensions element in the MSBuild project.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml;
using System.Reflection;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

using Utilities = Microsoft.Build.Internal.Utilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// ProjectExtensionsElement represents the ProjectExtensions element in the MSBuild project.
    /// ProjectExtensions can contain arbitrary XML content.
    /// The ProjectExtensions element is deprecated and provided only for backward compatibility.
    /// Use a property instead. Properties can also contain XML content.
    /// </summary>
    public class ProjectExtensionsElement : ProjectElement
    {
        /// <summary>
        /// Initialize a parented ProjectExtensionsElement instance
        /// </summary>
        internal ProjectExtensionsElement(XmlElement xmlElement, ProjectRootElement parent, ProjectRootElement project)
            : base(xmlElement, parent, project)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
        }

        /// <summary>
        /// Initialize an unparented ProjectExtensionsElement instance
        /// </summary>
        private ProjectExtensionsElement(XmlElement xmlElement, ProjectRootElement project)
            : base(xmlElement, null, project)
        {
        }

        /// <summary>
        /// Condition should never be set, but the getter returns null instead of throwing 
        /// because a nonexistent condition is implicitly true
        /// </summary>
        public override string Condition
        {
            get
            {
                return null;
            }

            set
            {
                ErrorUtilities.ThrowInvalidOperation("OM_CannotGetSetCondition");
            }
        }

        /// <summary>
        /// Gets and sets the raw XML content
        /// </summary>
        public string Content
        {
            [DebuggerStepThrough]
            get
            {
                return XmlElement.InnerXml;
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "Content");
                XmlElement.InnerXml = value;
                MarkDirty("Set ProjectExtensions raw {0}", value);
            }
        }

        /// <summary>
        /// This does not allow conditions, so it should not be called.
        /// </summary>
        public override ElementLocation ConditionLocation
        {
            get
            {
                ErrorUtilities.ThrowInternalError("Should not evaluate this");
                return null;
            }
        }

        /// <summary>
        /// Get or set the content of the first sub-element 
        /// with the provided name.
        /// </summary>
        public string this[string name]
        {
            get
            {
                ErrorUtilities.VerifyThrowArgumentLength(name, "name");

                XmlElement idElement = XmlElement[name];

                if (idElement == null)
                {
                    return String.Empty;
                }

                // remove the xmlns attribute, because the IDE's not expecting that
                return Microsoft.Build.Internal.Utilities.RemoveXmlNamespace(idElement.InnerXml);
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(name, "name");
                ErrorUtilities.VerifyThrowArgumentNull(value, "value");

                XmlElement idElement = XmlElement[name];

                if (idElement == null)
                {
                    if (value.Length == 0)
                    {
                        return;
                    }

                    idElement = XmlDocument.CreateElement(name, XMakeAttributes.defaultXmlNamespace);
                    XmlElement.AppendChild(idElement);
                }

                if (idElement.InnerXml != value)
                {
                    if (value.Length == 0)
                    {
                        XmlElement.RemoveChild(idElement);
                    }
                    else
                    {
                        idElement.InnerXml = value;
                    }

                    MarkDirty("Set ProjectExtensions content {0}", value);
                }
            }
        }

        /// <inheritdoc/>
        public override void CopyFrom(ProjectElement element)
        {
            ErrorUtilities.VerifyThrowArgumentNull(element, "element");
            ErrorUtilities.VerifyThrowArgument(this.GetType().IsEquivalentTo(element.GetType()), "element");

            if (this == element)
            {
                return;
            }

            this.Label = element.Label;

            var other = (ProjectExtensionsElement)element;
            this.Content = other.Content;

            this.MarkDirty("CopyFrom", null);
        }

        /// <summary>
        /// Creates a ProjectExtensionsElement parented by a project
        /// </summary>
        internal static ProjectExtensionsElement CreateParented(XmlElementWithLocation element, ProjectRootElement parent, ProjectRootElement containingProject)
        {
            return new ProjectExtensionsElement(element, parent, containingProject);
        }

        /// <summary>
        /// Creates an unparented ProjectExtensionsElement, wrapping an unparented XmlElement.
        /// Caller should then ensure the element is added to a parent
        /// </summary>
        internal static ProjectExtensionsElement CreateDisconnected(ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.projectExtensions);

            return new ProjectExtensionsElement(element, containingProject);
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(parent is ProjectRootElement, "OM_CannotAcceptParent");
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateProjectExtensionsElement();
        }
    }
}
