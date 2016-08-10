﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectUsingTaskBodyElement class.</summary>
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
    /// ProjectUsingTaskBodyElement class represents the Task element under the using task element in the MSBuild project.
    /// </summary>
    [DebuggerDisplay("Evaluate={Evaluate} TaskBody={TaskBody}")]
    public class ProjectUsingTaskBodyElement : ProjectElement
    {
        /// <summary>
        /// Initialize a parented ProjectUsingTaskBodyElement
        /// </summary>
        internal ProjectUsingTaskBodyElement(XmlElementWithLocation xmlElement, ProjectUsingTaskElement parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parent, "parent");
            VerifyCorrectParent(parent);
        }

        /// <summary>
        /// Initialize an unparented ProjectUsingTaskBodyElement
        /// </summary>
        private ProjectUsingTaskBodyElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
            : base(xmlElement, null, containingProject)
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
        /// Gets or sets the unevaluated value of the contents of the task xml 
        /// Returns empty string if it is not present.
        /// </summary>
        public string TaskBody
        {
            get
            {
                return Microsoft.Build.Internal.Utilities.GetXmlNodeInnerContents(XmlElement);
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "TaskBody");
                Microsoft.Build.Internal.Utilities.SetXmlNodeInnerContents(XmlElement, value);
                MarkDirty("Set usingtask body {0}", value);
            }
        }

        /// <summary>
        /// Gets the value of the Evaluate attribute.
        /// Returns true if it is not present.
        /// </summary>
        public string Evaluate
        {
            get
            {
                string evaluateAttribute = ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.evaluate);

                if (evaluateAttribute.Length == 0)
                {
                    return bool.TrueString;
                }

                return evaluateAttribute;
            }

            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.evaluate, value);
                MarkDirty("Set usingtask Evaluate {0}", value);
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
        /// Location of the "Condition" attribute on this element, if any.
        /// If there is no such attribute, returns the location of the element,
        /// in lieu of the default value it uses for the attribute.
        /// </summary>
        public ElementLocation EvaluateLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.evaluate) ?? Location; }
        }

        /// <summary>
        /// Creates an unparented ProjectUsingTaskBodyElement, wrapping an unparented XmlElement.
        /// Validates name.
        /// Caller should then ensure the element is added to the XmlDocument in the appropriate location.
        /// </summary>
        internal static ProjectUsingTaskBodyElement CreateDisconnected(string evaluate, string body, ProjectRootElement containingProject)
        {
            XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.usingTaskBody);
            ProjectUsingTaskBodyElement taskElement = new ProjectUsingTaskBodyElement(element, containingProject);
            taskElement.Evaluate = evaluate;
            taskElement.TaskBody = body;
            return taskElement;
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            VerifyCorrectParent(parent);
        }

        /// <inheritdoc />
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return owner.CreateUsingTaskBodyElement(this.Evaluate, this.TaskBody);
        }

        /// <summary>
        /// Verify the parent is a usingTaskElement and that the taskFactory attribute is set
        /// </summary>
        private static void VerifyCorrectParent(ProjectElementContainer parent)
        {
            ProjectUsingTaskElement parentUsingTask = parent as ProjectUsingTaskElement;
            ErrorUtilities.VerifyThrowInvalidOperation(parentUsingTask != null, "OM_CannotAcceptParent");

            // Since there is not going to be a TaskElement on the using task we need to validate and make sure there is a TaskFactory attribute on the parent element and 
            // that it is not empty
            if (parentUsingTask.TaskFactory.Length == 0)
            {
                ProjectXmlUtilities.VerifyThrowProjectRequiredAttribute(parent.XmlElement, "TaskFactory");
            }

            // UNDONE: Do check to make sure the task body is the last child
        }
    }
}
