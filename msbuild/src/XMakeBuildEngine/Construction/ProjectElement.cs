﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>An unevaluated element in MSBuild XML.</summary>
//-----------------------------------------------------------------------

using System;
using System.Text;
using System.Collections.Generic;
using System.Xml;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Collections.ObjectModel;
using Microsoft.Build.Construction;
using Microsoft.Build.Collections;

using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Abstract base class for MSBuild construction object model elements. 
    /// </summary>
    public abstract class ProjectElement
    {
        /// <summary>
        /// Parent container object.
        /// </summary>
        private ProjectElementContainer _parent;

        /// <summary>
        /// Condition value cached for performance
        /// </summary>
        private string _condition;

        /// <summary>
        /// Constructor called by ProjectRootElement only.
        /// XmlElement is set directly after construction.
        /// </summary>
        /// <comment>
        /// Should be protected+internal.
        /// </comment>
        internal ProjectElement()
        {
        }

        /// <summary>
        /// Constructor called by derived classes, except from ProjectRootElement.
        /// Parameters may not be null, except parent.
        /// </summary>
        internal ProjectElement(XmlElement xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
        {
            ErrorUtilities.VerifyThrowArgumentNull(xmlElement, "xmlElement");
            ProjectXmlUtilities.VerifyThrowProjectValidNamespace((XmlElementWithLocation)xmlElement);
            ErrorUtilities.VerifyThrowArgumentNull(containingProject, "containingProject");

            this.XmlElement = (XmlElementWithLocation)xmlElement;
            _parent = parent;
            this.ContainingProject = containingProject;
        }

        /// <summary>
        /// Gets or sets the Condition value. 
        /// It will return empty string IFF a condition attribute is legal but it’s not present or has no value. 
        /// It will return null IFF a Condition attribute is illegal on that element.
        /// Removes the attribute if the value to set is empty.
        /// It is possible for derived classes to throw an <see cref="InvalidOperationException"/> if setting the condition is
        /// not applicable for those elements.
        /// </summary>
        /// <example> For the "ProjectExtensions" element, the getter returns null and the setter
        /// throws an exception for any value. </example>
        public virtual string Condition
        {
            [DebuggerStepThrough]
            get
            {
                // No thread-safety lock required here because many reader threads would set the same value to the field.
                if (_condition == null)
                {
                    _condition = ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.condition);
                }

                return _condition;
            }

            [DebuggerStepThrough]
            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.condition, value);
                _condition = value;
                MarkDirty("Set condition {0}", _condition);
            }
        }

        /// <summary>
        /// Gets or sets the Label value. 
        /// Returns empty string if it is not present.
        /// Removes the attribute if the value to set is empty.
        /// </summary>
        public string Label
        {
            [DebuggerStepThrough]
            get
            {
                return ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.label);
            }

            [DebuggerStepThrough]
            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.label, value);
                MarkDirty("Set label {0}", value);
            }
        }

        /// <summary>
        /// Null if this is a ProjectRootElement.
        /// Null if this has not been attached to a parent yet.
        /// </summary>
        /// <remarks>
        /// Parent should only be set by ProjectElementContainer.
        /// </remarks>
        public ProjectElementContainer Parent
        {
            [DebuggerStepThrough]
            get
            {
                if (_parent is WrapperForProjectRootElement)
                {
                    // We hijacked the field to store the owning PRE. This element is actually unparented.
                    return null;
                }

                return _parent;
            }

            internal set
            {
                if (value == null)
                {
                    // We're about to lose the parent. Hijack the field to store the owning PRE.
                    _parent = new WrapperForProjectRootElement(ContainingProject);
                }
                else
                {
                    _parent = value;
                }

                OnAfterParentChanged(value);
            }
        }

        /// <summary>
        /// All parent elements of this element, going up to the ProjectRootElement.
        /// None if this itself is a ProjectRootElement.
        /// None if this itself has not been attached to a parent yet.
        /// </summary>
        public IEnumerable<ProjectElementContainer> AllParents
        {
            get
            {
                ProjectElementContainer currentParent = Parent;
                while (currentParent != null)
                {
                    yield return currentParent;
                    currentParent = currentParent.Parent;
                }
            }
        }

        /// <summary>
        /// Previous sibling element.
        /// May be null.
        /// </summary>
        /// <remarks>
        /// Setter should ideally be "protected AND internal"
        /// </remarks>
        public ProjectElement PreviousSibling
        {
            [DebuggerStepThrough]
            get;
            [DebuggerStepThrough]
            internal set;
        }

        /// <summary>
        /// Next sibling element.
        /// May be null.
        /// </summary>
        /// <remarks>
        /// Setter should ideally be "protected AND internal"
        /// </remarks>
        public ProjectElement NextSibling
        {
            [DebuggerStepThrough]
            get;
            [DebuggerStepThrough]
            internal set;
        }

        /// <summary>
        /// ProjectRootElement (possibly imported) that contains this Xml.
        /// Cannot be null.
        /// </summary>
        /// <remarks>
        /// Setter ideally would be "protected and internal"
        /// There are some tricks here in order to save the space of a field: there are a lot of these objects.
        /// </remarks>
        public ProjectRootElement ContainingProject
        {
            get
            {
                // If this element is unparented, we have hijacked the 'parent' field and stored the owning PRE in a special wrapper; get it from that.
                var wrapper = _parent as WrapperForProjectRootElement;
                if (wrapper != null)
                {
                    return wrapper.ContainingProject;
                }

                // If this element is parented, the parent field is the true parent, and we ask that for the PRE.
                // It will call into this same getter on itself and figure it out.
                return Parent.ContainingProject;
            }

            // ContainingProject is set ONLY when an element is first constructed.
            internal set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, "ContainingProject");

                if (_parent == null)
                {
                    // Not parented yet, hijack the field to store the ContainingProject
                    _parent = new WrapperForProjectRootElement(value);
                }
            }
        }

        /// <summary>
        /// Location of the "Condition" attribute on this element, if any.
        /// If there is no such attribute, returns null.
        /// </summary>
        public virtual ElementLocation ConditionLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.condition); }
        }

        /// <summary>
        /// Location of the "Label" attribute on this element, if any.
        /// If there is no such attribute, returns null;
        /// </summary>
        public ElementLocation LabelLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.label); }
        }

        /// <summary>
        /// Location of the corresponding Xml element.
        /// May not be correct if file is not saved, or 
        /// file has been edited since it was last saved.
        /// In the case of an unsaved edit, the location only
        /// contains the path to the file that the element originates from.
        /// </summary>
        public ElementLocation Location
        {
            get { return XmlElement.Location; }
        }

        /// <summary>
        /// Gets the name of the associated element. 
        /// Useful for display in some circumstances.
        /// </summary>
        internal string ElementName
        {
            get { return XmlElement.Name; }
        }

        /// <summary>
        /// Gets the XmlElement associated with this project element.
        /// The setter is used when adding new elements.
        /// Never null except during load or creation.
        /// </summary>
        /// <remarks>
        /// This should be protected, but "protected internal" means "OR" not "AND",
        /// so this is not possible.
        /// </remarks>
        internal XmlElementWithLocation XmlElement
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the XmlDocument associated with this project element.
        /// </summary>
        /// <remarks>
        /// Never null except during load or creation.
        /// This should be protected, but "protected internal" means "OR" not "AND",
        /// so this is not possible.
        /// </remarks>
        internal XmlDocumentWithLocation XmlDocument
        {
            [DebuggerStepThrough]
            get
            {
                return (XmlElement == null) ? null : (XmlDocumentWithLocation)XmlElement.OwnerDocument;
            }
        }

        /// <summary>
        /// Returns a shallow clone of this project element.
        /// </summary>
        /// <returns>The cloned element.</returns>
        public ProjectElement Clone()
        {
            return this.Clone(this.ContainingProject);
        }

        /// <summary>
        /// Applies properties from the specified type to this instance.
        /// </summary>
        /// <param name="element">The element to act as a template to copy from.</param>
        public virtual void CopyFrom(ProjectElement element)
        {
            ErrorUtilities.VerifyThrowArgumentNull(element, "element");
            ErrorUtilities.VerifyThrowArgument(this.GetType().IsEquivalentTo(element.GetType()), "element");

            if (this == element)
            {
                return;
            }

            // Remove all the current attributes and textual content.
            this.XmlElement.RemoveAllAttributes();
            if (this.XmlElement.ChildNodes.Count == 1 && this.XmlElement.FirstChild.NodeType == XmlNodeType.Text)
            {
                this.XmlElement.RemoveChild(this.XmlElement.FirstChild);
            }

            // Ensure the element name itself matches.
            this.ReplaceElement(XmlUtilities.RenameXmlElement(this.XmlElement, element.XmlElement.Name, XMakeAttributes.defaultXmlNamespace));

            // Copy over the attributes from the template element.
            foreach (XmlAttribute attribute in element.XmlElement.Attributes)
            {
                this.XmlElement.SetAttribute(attribute.LocalName, attribute.NamespaceURI, attribute.Value);
            }

            // If this element has pure text content, copy that over.
            if (element.XmlElement.ChildNodes.Count == 1 && element.XmlElement.FirstChild.NodeType == XmlNodeType.Text)
            {
                this.XmlElement.AppendChild(this.XmlElement.OwnerDocument.CreateTextNode(element.XmlElement.FirstChild.Value));
            }

            this.MarkDirty("CopyFrom", null);
        }

        /// <summary>
        /// Called only by the parser to tell the ProjectRootElement its backing XmlElement and its own parent project (itself)
        /// This can't be done during construction, as it hasn't loaded the document at that point and it doesn't have a 'this' pointer either.
        /// </summary>
        internal void SetProjectRootElementFromParser(XmlElementWithLocation xmlElement, ProjectRootElement projectRootElement)
        {
            this.XmlElement = xmlElement;
            this.ContainingProject = projectRootElement;
        }

        /// <summary>
        /// Called by ProjectElementContainer to clear the parent when
        /// removing an element from its parent.
        /// </summary>
        internal void ClearParent()
        {
            Parent = null;
        }

        /// <summary>
        /// Called by a DERIVED CLASS to indicate its XmlElement has changed.
        /// This normally shouldn't happen, so it's broken out into an explicit method.
        /// An example of when it has to happen is when an item's type is changed.
        /// We trust the caller to have fixed up the XmlDocument properly.
        /// We ASSUME that attributes were copied verbatim. If this is not the case,
        /// any cached attribute values would have to be cleared.
        /// If the new element is actually the existing element, does nothing, and does
        /// not mark the project dirty.
        /// </summary>
        /// <remarks>
        /// This should be protected, but "protected internal" means "OR" not "AND",
        /// so this is not possible.
        /// </remarks>
        internal void ReplaceElement(XmlElementWithLocation newElement)
        {
            if (Object.ReferenceEquals(newElement, XmlElement))
            {
                return;
            }

            XmlElement = newElement;
            MarkDirty("Replace element {0}", newElement.Name);
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal abstract void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer proposedParent, ProjectElement previousSibling, ProjectElement nextSibling);

        /// <summary>
        /// Marks this element as dirty.
        /// The default implementation simply marks the parent as dirty.
        /// If there is no parent, because the element has not been parented, do nothing. The parent
        /// will be dirtied when the element is added.
        /// Accepts a reason for debugging purposes only, and optional reason parameter.
        /// </summary>
        /// <comment>
        /// Should ideally be protected+internal.
        /// </comment>
        internal virtual void MarkDirty(string reason, string param)
        {
            if (Parent != null)
            {
                Parent.MarkDirty(reason, param);
            }
        }

        /// <summary>
        /// Called after a new parent is set. Parent may be null.
        /// By default does nothing.
        /// </summary>
        internal virtual void OnAfterParentChanged(ProjectElementContainer newParent)
        {
        }

        /// <summary>
        /// Returns a shallow clone of this project element.
        /// </summary>
        /// <param name="factory">The factory to use for creating the new instance.</param>
        /// <returns>The cloned element.</returns>
        protected internal virtual ProjectElement Clone(ProjectRootElement factory)
        {
            var clone = this.CreateNewInstance(factory);
            if (!clone.GetType().IsEquivalentTo(this.GetType()))
            {
                ErrorUtilities.ThrowInternalError("{0}.Clone() returned an instance of type {1}.", this.GetType().Name, clone.GetType().Name);
            }

            clone.CopyFrom(this);
            return clone;
        }

        /// <summary>
        /// Returns a new instance of this same type.
        /// Any properties that cannot be set after creation should be set to copies of values
        /// as set for this instance.
        /// </summary>
        /// <param name="owner">The factory to use for creating the new instance.</param>
        protected abstract ProjectElement CreateNewInstance(ProjectRootElement owner);

        /// <summary>
        /// Special derived variation of ProjectElementContainer used to wrap a ProjectRootElement.
        /// This is part of a trick used in ProjectElement to avoid using a separate field for the containing PRE.
        /// </summary>
        private class WrapperForProjectRootElement : ProjectElementContainer
        {
            /// <summary>
            /// Constructor
            /// </summary>
            internal WrapperForProjectRootElement(ProjectRootElement containingProject)
            {
                ErrorUtilities.VerifyThrowInternalNull(containingProject, "containingProject");
                this.ContainingProject = containingProject;
            }

            /// <summary>
            /// Wrapped ProjectRootElement
            /// </summary>
            internal new ProjectRootElement ContainingProject
            {
                get;
                private set;
            }

            /// <summary>
            /// Dummy required implementation
            /// </summary>
            internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
            {
                ErrorUtilities.ThrowInternalErrorUnreachable();
            }

            /// <inheritdoc />
            protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
            {
                return new WrapperForProjectRootElement(this.ContainingProject);
            }
        }
    }
}
