﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Definition of ProjectRootElement class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
#if (!STANDALONEBUILD)
using Microsoft.Internal.Performance;
#if MSBUILDENABLEVSPROFILING 
using Microsoft.VisualStudio.Profiler;
#endif
#endif
using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;
using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Event handler for the event fired after this project file is named or renamed.
    /// If the project file has not previously had a name, oldFullPath is null.
    /// </summary>
    internal delegate void RenameHandlerDelegate(string oldFullPath);

    /// <summary>
    /// ProjectRootElement class represents an MSBuild project, an MSBuild targets file or any other file that conforms to MSBuild
    /// project file schema.
    /// This class and its related classes allow a complete MSBuild project or targets file to be read and written.
    /// Comments and whitespace cannot be edited through this model at present.
    /// 
    /// Each project root element is associated with exactly one ProjectCollection. This allows the owner of that project collection
    /// to control its lifetime and not be surprised by edits via another project collection.
    /// </summary>
    [DebuggerDisplay("{FullPath} #Children={Count} DefaultTargets={DefaultTargets} ToolsVersion={ToolsVersion} InitialTargets={InitialTargets} ExplicitlyLoaded={IsExplicitlyLoaded}")]
    public class ProjectRootElement : ProjectElementContainer
    {
        /// <summary>
        /// Constant for default (empty) project file.
        /// </summary>
        private const string EmptyProjectFileContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<Project ToolsVersion=\"" + MSBuildConstants.CurrentToolsVersion + "\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">\r\n</Project>";

        /// <summary>
        /// The singleton delegate that loads projects into the ProjectRootElement
        /// </summary>
        private static readonly ProjectRootElementCache.OpenProjectRootElement s_openLoaderDelegate = OpenLoader;

        /// <summary>
        /// The default encoding to use / assume for a new project.
        /// </summary>
        private static readonly Encoding s_defaultEncoding = Encoding.UTF8;

        /// <summary>
        /// A global counter used to ensure each project version is distinct from every other.
        /// </summary>
        /// <remarks>
        /// This number is static so that it is unique across the appdomain. That is so that a host
        /// can know when a ProjectRootElement has been unloaded (perhaps after modification) and
        /// reloaded -- the version won't reset to '0'.
        /// </remarks>
        private static int s_globalVersionCounter = 0;

        /// <summary>
        /// Version number of this object that was last saved to disk, or last loaded from disk.
        /// Used to figure whether this object is dirty for saving.
        /// Saving to or loading from a provided stream reader does not modify this value, only saving to or loading from disk.
        /// The actual value is meaningless (since the counter is shared with all projects) --
        /// it should only be compared to a stored value.
        /// Immediately after loading from disk, this has the same value as <see cref="_version">version</see>.
        /// </summary>
        private int _versionOnDisk;

        /// <summary>
        /// Current version number of this object.
        /// Used to figure whether this object is dirty for saving, or projects evaluated from
        /// this object need to be re-evaluated.
        /// The actual value is meaningless (since the counter is shared with all projects) --
        /// it should only be compared to a stored value.
        /// </summary>
        /// <remarks>
        /// Set this only through <see cref="MarkDirty(string, string)"/>.
        /// </remarks>
        private int _version;

        /// <summary>
        /// The encoding of the project that was (if applicable) loaded off disk, and that will be used to save the project.
        /// </summary>
        /// <value>Defaults to UTF8 for new projects.</value>
        private Encoding _encoding;

        /// <summary>
        /// The project file's location. It can be null if the project is not directly loaded from a file.
        /// </summary>
        private ElementLocation _projectFileLocation;

        /// <summary>
        /// The directory that the project is in. 
        /// Essential for evaluting relative paths.
        /// If the project is not loaded from disk, returns the current-directory from 
        /// the time the project was loaded - this is the same behavior as Whidbey/Orcas.
        /// </summary>
        private string _directory;

        /// <summary>
        /// The time that this object was last changed. If it hasn't
        /// been changed since being loaded or created, its value is <see cref="DateTime.MinValue"/>.
        /// Stored as UTC as this is faster when there are a large number of rapid edits.
        /// </summary>
        private DateTime _timeLastChangedUtc;

        /// <summary>
        /// The last-write-time of the file that was read, when it was read.
        /// This can be used to see whether the file has been changed on disk
        /// by an external means.
        /// </summary>
        private DateTime _lastWriteTimeWhenRead;

        /// <summary>
        /// The cache in which this project root element is stored.
        /// </summary>
        private ProjectRootElementCache _projectRootElementCache;

        /// <summary>
        /// Reason it was last marked dirty; unlocalized, for debugging
        /// </summary>
        private string _dirtyReason = "first created project {0}";

        /// <summary>
        /// Parameter to be formatted into the dirty reason
        /// </summary>
        private string _dirtyParameter = String.Empty;

        /// <summary>
        /// The build event context errors should be logged in.
        /// </summary>
        private BuildEventContext _buildEventContext;

        /// <summary>
        /// Initialize a ProjectRootElement instance from a XmlReader.
        /// May throw InvalidProjectFileException.
        /// Leaves the project dirty, indicating there are unsaved changes.
        /// Used to create a root element for solutions loaded by the 3.5 version of the solution wrapper.
        /// </summary>
        internal ProjectRootElement(XmlReader xmlReader, ProjectRootElementCache projectRootElementCache, bool isExplicitlyLoaded)
            : base()
        {
            ErrorUtilities.VerifyThrowArgumentNull(xmlReader, "xmlReader");
            ErrorUtilities.VerifyThrowArgumentNull(projectRootElementCache, "projectRootElementCache");

            this.IsExplicitlyLoaded = isExplicitlyLoaded;
            _projectRootElementCache = projectRootElementCache;
            _directory = NativeMethodsShared.GetCurrentDirectory();
            IncrementVersion();

            XmlDocumentWithLocation document = LoadDocument(xmlReader);

            ProjectParser.Parse(document, this);
        }

        /// <summary>
        /// Initialize an in-memory, empty ProjectRootElement instance that can be saved later.
        /// Leaves the project dirty, indicating there are unsaved changes.
        /// </summary>
        private ProjectRootElement(ProjectRootElementCache projectRootElementCache)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectRootElementCache, "projectRootElementCache");

            _projectRootElementCache = projectRootElementCache;
            _directory = NativeMethodsShared.GetCurrentDirectory();
            IncrementVersion();

            XmlDocumentWithLocation document = new XmlDocumentWithLocation();

            XmlReaderSettings xrs = new XmlReaderSettings();
            xrs.DtdProcessing = DtdProcessing.Ignore;

            using (XmlReader xr = XmlReader.Create(new StringReader(ProjectRootElement.EmptyProjectFileContent), xrs))
            {
                document.Load(xr);
            }

            ProjectParser.Parse(document, this);
        }

        /// <summary>
        /// Initialize a ProjectRootElement instance over a project with the specified file path.
        /// Assumes path is already normalized.
        /// May throw InvalidProjectFileException.
        /// </summary>
        private ProjectRootElement(string path, ProjectRootElementCache projectRootElementCache, BuildEventContext buildEventContext)
            : base()
        {
            ErrorUtilities.VerifyThrowArgumentLength(path, "path");
            ErrorUtilities.VerifyThrowInternalRooted(path);
            ErrorUtilities.VerifyThrowArgumentNull(projectRootElementCache, "projectRootElementCache");
            ErrorUtilities.VerifyThrowArgumentNull(buildEventContext, "buildEventContext");
            _projectRootElementCache = projectRootElementCache;
            _buildEventContext = buildEventContext;

            IncrementVersion();
            _versionOnDisk = _version;
            _timeLastChangedUtc = DateTime.UtcNow;

            XmlDocumentWithLocation document = LoadDocument(path);

            ProjectParser.Parse(document, this);

            projectRootElementCache.AddEntry(this);
        }

        /// <summary>
        /// Initialize a ProjectRootElement instance from an existing document.
        /// May throw InvalidProjectFileException.
        /// Leaves the project dirty, indicating there are unsaved changes.
        /// </summary>
        /// <remarks>
        /// Do not make public: we do not wish to expose particular XML API's.
        /// </remarks>
        private ProjectRootElement(XmlDocumentWithLocation document, ProjectRootElementCache projectRootElementCache)
            : base()
        {
            ErrorUtilities.VerifyThrowArgumentNull(document, "document");
            ErrorUtilities.VerifyThrowArgumentNull(projectRootElementCache, "projectRootElementCache");

            _projectRootElementCache = projectRootElementCache;
            _directory = NativeMethodsShared.GetCurrentDirectory();
            IncrementVersion();

            ProjectParser.Parse(document, this);
        }

        /// <summary>
        /// Event raised after this project is renamed
        /// </summary>
        internal event RenameHandlerDelegate OnAfterProjectRename;

        /// <summary>
        /// Event raised after the project XML is changed.
        /// </summary>
        internal event EventHandler<ProjectXmlChangedEventArgs> OnProjectXmlChanged;

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

        #region ChildEnumerators
        /// <summary>
        /// Get a read-only collection of the child chooses, if any
        /// </summary>
        /// <remarks>
        /// The name is inconsistent to make it more understandable, per API review.
        /// </remarks>
        public ICollection<ProjectChooseElement> ChooseElements
        {
            get
            {
                return new ReadOnlyCollection<ProjectChooseElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectChooseElement>(Children)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection of the child item definition groups, if any
        /// </summary>
        public ICollection<ProjectItemDefinitionGroupElement> ItemDefinitionGroups
        {
            get
            {
                return new ReadOnlyCollection<ProjectItemDefinitionGroupElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectItemDefinitionGroupElement>(Children)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection of the child item definitions, if any, in all item definition groups anywhere in the project file.
        /// </summary>
        public ICollection<ProjectItemDefinitionElement> ItemDefinitions
        {
            get
            {
                return new ReadOnlyCollection<ProjectItemDefinitionElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectItemDefinitionElement>(AllChildren)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection over the child item groups, if any.
        /// Does not include any that may not be at the root, i.e. inside Choose elements.
        /// </summary>
        public ICollection<ProjectItemGroupElement> ItemGroups
        {
            get
            {
                return new ReadOnlyCollection<ProjectItemGroupElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectItemGroupElement>(Children)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection of the child items, if any, in all item groups anywhere in the project file.
        /// Not restricted to root item groups: traverses through Choose elements.
        /// </summary>
        public ICollection<ProjectItemElement> Items
        {
            get
            {
                return new ReadOnlyCollection<ProjectItemElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectItemElement>(AllChildren)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection of the child import groups, if any.
        /// </summary>
        public ICollection<ProjectImportGroupElement> ImportGroups
        {
            get
            {
                return new ReadOnlyCollection<ProjectImportGroupElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectImportGroupElement>(Children)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection of the child imports
        /// </summary>
        public ICollection<ProjectImportElement> Imports
        {
            get
            {
                return new ReadOnlyCollection<ProjectImportElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectImportElement>(AllChildren)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection of the child property groups, if any.
        /// Does not include any that may not be at the root, i.e. inside Choose elements.
        /// </summary>
        public ICollection<ProjectPropertyGroupElement> PropertyGroups
        {
            get
            {
                return new ReadOnlyCollection<ProjectPropertyGroupElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectPropertyGroupElement>(Children)
                    );
            }
        }

        /// <summary>
        /// Geta read-only collection of the child properties, if any, in all property groups anywhere in the project file.
        /// Not restricted to root property groups: traverses through Choose elements.
        /// </summary>
        public ICollection<ProjectPropertyElement> Properties
        {
            get
            {
                return new ReadOnlyCollection<ProjectPropertyElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectPropertyElement>(AllChildren)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection of the child targets
        /// </summary>
        public ICollection<ProjectTargetElement> Targets
        {
            get
            {
                return new ReadOnlyCollection<ProjectTargetElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectTargetElement>(Children)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection of the child usingtasks, if any
        /// </summary>
        public ICollection<ProjectUsingTaskElement> UsingTasks
        {
            get
            {
                return new ReadOnlyCollection<ProjectUsingTaskElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectUsingTaskElement>(Children)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection of the child item groups, if any, in reverse order
        /// </summary>
        public ICollection<ProjectItemGroupElement> ItemGroupsReversed
        {
            get
            {
                return new ReadOnlyCollection<ProjectItemGroupElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectItemGroupElement>(ChildrenReversed)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection of the child item definition groups, if any, in reverse order
        /// </summary>
        public ICollection<ProjectItemDefinitionGroupElement> ItemDefinitionGroupsReversed
        {
            get
            {
                return new ReadOnlyCollection<ProjectItemDefinitionGroupElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectItemDefinitionGroupElement>(ChildrenReversed)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection of the child import groups, if any, in reverse order
        /// </summary>
        public ICollection<ProjectImportGroupElement> ImportGroupsReversed
        {
            get
            {
                return new ReadOnlyCollection<ProjectImportGroupElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectImportGroupElement>(ChildrenReversed)
                    );
            }
        }

        /// <summary>
        /// Get a read-only collection of the child property groups, if any, in reverse order
        /// </summary>
        public ICollection<ProjectPropertyGroupElement> PropertyGroupsReversed
        {
            get
            {
                return new ReadOnlyCollection<ProjectPropertyGroupElement>
                    (
                        new FilteringEnumerable<ProjectElement, ProjectPropertyGroupElement>(ChildrenReversed)
                    );
            }
        }

        #endregion

        /// <summary>
        /// The directory that the project is in. 
        /// Essential for evaluting relative paths.
        /// Is never null, even if the FullPath does not contain directory information.
        /// If the project has not been loaded from disk and has not been given a path, returns the current-directory from 
        /// the time the project was loaded - this is the same behavior as Whidbey/Orcas.
        /// If the project has not been loaded from disk but has been given a path, this path may not exist.
        /// </summary>
        public string DirectoryPath
        {
            [DebuggerStepThrough]
            get
            { return _directory ?? String.Empty; }
            internal set { _directory = value; } // Used during solution load to ensure solutions which were created from a file have a location.
        }

        /// <summary>
        /// Full path to the project file.
        /// If the project has not been loaded from disk and has not been given a path, returns null.
        /// If the project has not been loaded from disk but has been given a path, this path may not exist.
        /// Setter renames the project, if it already had a name.
        /// </summary>
        /// <remarks>
        /// Updates the ProjectRootElement cache.
        /// </remarks>
        public string FullPath
        {
            get
            {
                return (_projectFileLocation != null) ? _projectFileLocation.File : null;
            }

            set
            {
                ErrorUtilities.VerifyThrowArgumentLength(value, "value");

                string oldFullPath = (_projectFileLocation != null) ? _projectFileLocation.File : null;

                // We do not control the current directory at this point, but assume that if we were
                // passed a relative path, the caller assumes we will prepend the current directory.
                string newFullPath = FileUtilities.NormalizePath(value);

                if (String.Equals(oldFullPath, newFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _projectFileLocation = ElementLocation.Create(newFullPath);
                _directory = Path.GetDirectoryName(newFullPath);

                if (XmlDocument != null)
                {
                    XmlDocument.FullPath = newFullPath;
                }

                if (oldFullPath == null)
                {
                    _projectRootElementCache.AddEntry(this);
                }
                else
                {
                    _projectRootElementCache.RenameEntry(oldFullPath, this);
                }

                RenameHandlerDelegate rename = OnAfterProjectRename;
                if (rename != null)
                {
                    rename(oldFullPath);
                }

                MarkDirty("Set project FullPath to '{0}'", FullPath);
            }
        }

        /// <summary>
        /// Encoding that the project file is saved in, or will be saved in, unless
        /// otherwise specified.
        /// </summary>
        /// <remarks>
        /// Returns the encoding from the Xml declaration if any, otherwise UTF8.
        /// </remarks>
        public Encoding Encoding
        {
            get
            {
                // No thread-safety lock required here because many reader threads would set the same value to the field.
                if (_encoding == null)
                {
                    XmlDeclaration declaration = XmlDocument.FirstChild as XmlDeclaration;

                    if (declaration != null)
                    {
                        if (declaration.Encoding.Length > 0)
                        {
                            _encoding = Encoding.GetEncoding(declaration.Encoding);
                        }
                    }
                }

                // Ensure we never return null, in case there was no xml declaration that we could find above.
                return _encoding ?? s_defaultEncoding;
            }
        }

        /// <summary>
        /// Gets or sets the value of DefaultTargets. If there is no DefaultTargets, returns empty string.
        /// If the value is null or empty, removes the attribute.
        /// </summary>
        public string DefaultTargets
        {
            [DebuggerStepThrough]
            get
            {
                return ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.defaultTargets);
            }

            [DebuggerStepThrough]
            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.defaultTargets, value);
                MarkDirty("Set Project DefaultTargets to '{0}'", value);
            }
        }

        /// <summary>
        /// Gets or sets the value of InitialTargets. If there is no InitialTargets, returns empty string.
        /// If the value is null or empty, removes the attribute.
        /// </summary>
        public string InitialTargets
        {
            [DebuggerStepThrough]
            get
            {
                return ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.initialTargets);
            }

            [DebuggerStepThrough]
            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.initialTargets, value);
                MarkDirty("Set project InitialTargets to '{0}'", value);
            }
        }

        /// <summary>
        /// Gets or sets the value of TreatAsLocalProperty. If there is no tag, returns empty string.
        /// If the value being set is null or empty, removes the attribute.
        /// </summary>
        public string TreatAsLocalProperty
        {
            [DebuggerStepThrough]
            get
            {
                return ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.treatAsLocalProperty);
            }

            [DebuggerStepThrough]
            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.treatAsLocalProperty, value);
                MarkDirty("Set project TreatAsLocalProperty to '{0}'", value);
            }
        }

        /// <summary>
        /// Gets or sets the value of ToolsVersion. If there is no ToolsVersion, returns empty string.
        /// If the value is null or empty, removes the attribute.
        /// </summary>
        public string ToolsVersion
        {
            [DebuggerStepThrough]
            get
            {
                return ProjectXmlUtilities.GetAttributeValue(XmlElement, XMakeAttributes.toolsVersion);
            }

            [DebuggerStepThrough]
            set
            {
                ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, XMakeAttributes.toolsVersion, value);
                MarkDirty("Set project ToolsVersion {0}", value);
            }
        }

        /// <summary>
        /// Gets the XML representing this project as a string.
        /// Does not remove any dirty flag.
        /// </summary>
        /// <remarks>
        /// Useful for debugging.
        /// Note that we do not expose an XmlDocument or any other specific XML API.
        /// </remarks>
        public string RawXml
        {
            get
            {
                using (StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture))
                {
                    using (ProjectWriter projectWriter = new ProjectWriter(stringWriter))
                    {
                        projectWriter.Initialize(XmlDocument);
                        XmlDocument.Save(projectWriter);
                    }

                    return stringWriter.ToString();
                }
            }
        }

        /// <summary>
        /// Whether the XML has been modified since it was last loaded or saved.
        /// </summary>
        public bool HasUnsavedChanges
        {
            get
            {
                if (Version != _versionOnDisk)
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Version number of this object.
        /// A host can compare this to a stored version number to determine whether
        /// a project's XML has changed, even if it has also been saved since.
        /// 
        /// The actual value is meaningless: an edit may increment it more than once,
        /// so it should only be compared to a stored value.
        /// </summary>
        /// <remarks>
        /// Used by the Project class to figure whether changes have occurred that 
        /// it might want to pick up by reevaluation.
        /// 
        /// Used by the ProjectRootElement class to determine whether it needs to save.
        /// 
        /// This number is unique to the appdomain. That means that it is possible
        /// to know when a ProjectRootElement has been unloaded (perhaps after modification) and
        /// reloaded -- the version won't reset to '0'.
        /// 
        /// We're assuming we don't have over 2 billion edits.
        /// </remarks>
        public int Version
        {
            get
            {
                return _version;
            }
        }

        /// <summary>
        /// The time that this object was last changed. If it hasn't
        /// been changed since being loaded or created, its value is <see cref="DateTime.MinValue"/>.
        /// </summary>
        /// <remarks>
        /// This is used by the VB/C# project system.
        /// </remarks>
        public DateTime TimeLastChanged
        {
            [DebuggerStepThrough]
            get
            { return _timeLastChangedUtc.ToLocalTime(); }
        }

        /// <summary>
        /// The last-write-time of the file that was read, when it was read.
        /// This can be used to see whether the file has been changed on disk
        /// by an external means.
        /// </summary>
        public DateTime LastWriteTimeWhenRead
        {
            [DebuggerStepThrough]
            get
            { return _lastWriteTimeWhenRead; }
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
        /// Location of the originating file itself, not any specific content within it.
        /// If the file has not been given a name, returns an empty location.
        /// This is a case where it is legitimate to "not have a location".
        /// </summary>
        public ElementLocation ProjectFileLocation
        {
            get { return _projectFileLocation ?? ElementLocation.EmptyLocation; }
        }

        /// <summary>
        /// Location of the toolsversion attribute, if any
        /// </summary>
        public ElementLocation ToolsVersionLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.toolsVersion); }
        }

        /// <summary>
        /// Location of the defaulttargets attribute, if any
        /// </summary>
        public ElementLocation DefaultTargetsLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.defaultTargets); }
        }

        /// <summary>
        /// Location of the initialtargets attribute, if any
        /// </summary>
        public ElementLocation InitialTargetsLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.initialTargets); }
        }

        /// <summary>
        /// Location of the TreatAsLocalProperty attribute, if any
        /// </summary>
        public ElementLocation TreatAsLocalPropertyLocation
        {
            get { return XmlElement.GetAttributeLocation(XMakeAttributes.treatAsLocalProperty); }
        }

        /// <summary>
        /// Has the project root element been explicitly loaded for a build or has it been implicitly loaded
        /// as part of building another project.
        /// </summary>
        /// <remarks>
        /// Internal code that wants to set this to true should call <see cref="MarkAsExplicitlyLoaded"/>.
        /// The setter is private to make it more difficult to downgrade an existing PRE to an implicitly loaded state, which should never happen.
        /// </remarks>
        internal bool IsExplicitlyLoaded
        {
            get;
            private set;
        }

        /// <summary>
        /// Retrieves the root element cache with which this root element is associated.
        /// </summary>
        internal ProjectRootElementCache ProjectRootElementCache
        {
            [DebuggerStepThrough]
            get
            { return _projectRootElementCache; }
        }

        /// <summary>
        /// Gets a value indicating whether this PRE is known by its containing collection.
        /// </summary>
        internal bool IsMemberOfProjectCollection
        {
            get
            {
                // We call AddEntry on the ProjectRootElementCache when we first get our filename set.
                return _projectFileLocation != null;
            }
        }

        /// <summary>
        /// Indicates whether there are any targets in this project 
        /// that use the "Returns" attribute.  If so, then this project file
        /// is automatically assumed to be "Returns-enabled", and the default behaviour
        /// for targets without Returns attributes changes from using the Outputs to 
        /// returning nothing by default. 
        /// </summary>
        internal bool ContainsTargetsWithReturnsAttribute
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the ProjectExtensions child, if any, otherwise null.
        /// </summary>
        /// <remarks>
        /// Not public as we do not wish to encourage the use of ProjectExtensions.
        /// </remarks>
        internal ProjectExtensionsElement ProjectExtensions
        {
            get
            {
                foreach (ProjectElement child in ChildrenReversed)
                {
                    ProjectExtensionsElement extensions = child as ProjectExtensionsElement;

                    if (extensions != null)
                    {
                        return extensions;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Returns an unlocalized indication of how this file was last dirtied.
        /// This is for debugging purposes only.
        /// String formatting only occurs when retrieved.
        /// </summary>
        internal string LastDirtyReason
        {
            get
            {
                if (_dirtyReason == null)
                {
                    return null;
                }

                return String.Format(CultureInfo.InvariantCulture, _dirtyReason, _dirtyParameter);
            }
        }

        /// <summary>
        /// Initialize an in-memory, empty ProjectRootElement instance that can be saved later.
        /// Uses the global project collection.
        /// </summary>
        public static ProjectRootElement Create()
        {
            return Create(ProjectCollection.GlobalProjectCollection);
        }

        /// <summary>
        /// Initialize an in-memory, empty ProjectRootElement instance that can be saved later.
        /// Uses the specified project collection.
        /// </summary>
        public static ProjectRootElement Create(ProjectCollection projectCollection)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectCollection, "projectCollection");

            return Create(projectCollection.ProjectRootElementCache);
        }

        /// <summary>
        /// Initialize an in-memory, empty ProjectRootElement instance that can be saved later.
        /// Uses the global project collection.
        /// </summary>
        public static ProjectRootElement Create(string path)
        {
            return Create(path, ProjectCollection.GlobalProjectCollection);
        }

        /// <summary>
        /// Initialize an in-memory, empty ProjectRootElement instance that can be saved later.
        /// Uses the specified project collection.
        /// </summary>
        public static ProjectRootElement Create(string path, ProjectCollection projectCollection)
        {
            ErrorUtilities.VerifyThrowArgumentLength(path, "path");
            ErrorUtilities.VerifyThrowArgumentNull(projectCollection, "projectCollection");

            ProjectRootElement projectRootElement = new ProjectRootElement(projectCollection.ProjectRootElementCache);
            projectRootElement.FullPath = path;

            return projectRootElement;
        }

        /// <summary>
        /// Initialize a ProjectRootElement instance from an XmlReader.
        /// Uses the global project collection.
        /// May throw InvalidProjectFileException.
        /// </summary>
        public static ProjectRootElement Create(XmlReader xmlReader)
        {
            return Create(xmlReader, ProjectCollection.GlobalProjectCollection);
        }

        /// <summary>
        /// Initialize a ProjectRootElement instance from an XmlReader.
        /// Uses the specified project collection.
        /// May throw InvalidProjectFileException.
        /// </summary>
        public static ProjectRootElement Create(XmlReader xmlReader, ProjectCollection projectCollection)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectCollection, "projectCollection");

            return new ProjectRootElement(xmlReader, projectCollection.ProjectRootElementCache, true /*Explicitly loaded*/);
        }

        /// <summary>
        /// Initialize a ProjectRootElement instance by loading from the specified file path.
        /// Uses the global project collection.
        /// May throw InvalidProjectFileException.
        /// </summary>
        public static ProjectRootElement Open(string path)
        {
            return Open(path, ProjectCollection.GlobalProjectCollection);
        }

        /// <summary>
        /// Initialize a ProjectRootElement instance by loading from the specified file path.
        /// Uses the specified project collection.
        /// May throw InvalidProjectFileException.
        /// </summary>
        public static ProjectRootElement Open(string path, ProjectCollection projectCollection)
        {
            ErrorUtilities.VerifyThrowArgumentLength(path, "path");
            ErrorUtilities.VerifyThrowArgumentNull(projectCollection, "projectCollection");

            path = FileUtilities.NormalizePath(path);

            return Open(path, projectCollection.ProjectRootElementCache, true /*Is explicitly loaded*/);
        }

        /// <summary>
        /// Returns the ProjectRootElement for the given path if it has been loaded, or null if it is not currently in memory.
        /// Uses the global project collection.
        /// </summary>
        /// <param name="path">The path of the ProjectRootElement, cannot be null.</param>
        /// <returns>The loaded ProjectRootElement, or null if it is not currently in memory.</returns>
        /// <remarks>
        /// It is possible for ProjectRootElements to be brought into memory and discarded due to memory pressure. Therefore
        /// this method returning false does not indicate that it has never been loaded, only that it is not currently in memory.
        /// </remarks>
        public static ProjectRootElement TryOpen(string path)
        {
            ErrorUtilities.VerifyThrowArgumentLength(path, "path");

            return TryOpen(path, ProjectCollection.GlobalProjectCollection);
        }

        /// <summary>
        /// Returns the ProjectRootElement for the given path if it has been loaded, or null if it is not currently in memory.
        /// Uses the specified project collection.
        /// </summary>
        /// <param name="path">The path of the ProjectRootElement, cannot be null.</param>
        /// <returns>The loaded ProjectRootElement, or null if it is not currently in memory.</returns>
        /// <remarks>
        /// It is possible for ProjectRootElements to be brought into memory and discarded due to memory pressure. Therefore
        /// this method returning false does not indicate that it has never been loaded, only that it is not currently in memory.
        /// </remarks>
        public static ProjectRootElement TryOpen(string path, ProjectCollection projectCollection)
        {
            ErrorUtilities.VerifyThrowArgumentLength(path, "path");
            ErrorUtilities.VerifyThrowArgumentNull(projectCollection, "projectCollection");

            path = FileUtilities.NormalizePath(path);

            ProjectRootElement projectRootElement = projectCollection.ProjectRootElementCache.TryGet(path);

            return projectRootElement;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// If import groups exist, inserts into the last one without a condition on it.
        /// Otherwise, creates an import at the end of the project.
        /// </summary>
        public ProjectImportElement AddImport(string project)
        {
            ErrorUtilities.VerifyThrowArgumentLength(project, "project");

            ProjectImportGroupElement importGroupToAddTo = null;

            foreach (ProjectImportGroupElement importGroup in ImportGroupsReversed)
            {
                if (importGroup.Condition.Length > 0)
                {
                    continue;
                }

                importGroupToAddTo = importGroup;
                break;
            }

            ProjectImportElement import;

            if (importGroupToAddTo != null)
            {
                import = importGroupToAddTo.AddImport(project);
            }
            else
            {
                import = CreateImportElement(project);
                AppendChild(import);
            }

            return import;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Creates an import group at the end of the project.
        /// </summary>
        public ProjectImportGroupElement AddImportGroup()
        {
            ProjectImportGroupElement importGroup = CreateImportGroupElement();
            AppendChild(importGroup);

            return importGroup;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Finds item group with no condition with at least one item of same type, or else adds a new item group;
        /// adds the item to that item group with items of the same type, ordered by include.
        /// </summary>
        /// <remarks>
        /// Per the previous implementation, it actually finds the last suitable item group, not the first.
        /// </remarks>
        public ProjectItemElement AddItem(string itemType, string include)
        {
            return AddItem(itemType, include, null);
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Finds first item group with no condition with at least one item of same type, or else an empty item group; or else adds a new item group;
        /// adds the item to that item group with items of the same type, ordered by include.
        /// Does not attempt to check whether the item matches an existing wildcard expression; that is only possible
        /// in the evaluated world.
        /// </summary>
        /// <remarks>
        /// Per the previous implementation, it actually finds the last suitable item group, not the first.
        /// </remarks>
        public ProjectItemElement AddItem(string itemType, string include, IEnumerable<KeyValuePair<string, string>> metadata)
        {
            ErrorUtilities.VerifyThrowArgumentLength(itemType, "itemType");
            ErrorUtilities.VerifyThrowArgumentLength(include, "include");

            ProjectItemGroupElement itemGroupToAddTo = null;

            foreach (ProjectItemGroupElement itemGroup in ItemGroups)
            {
                if (itemGroup.Condition.Length > 0)
                {
                    continue;
                }

                if (itemGroupToAddTo == null && itemGroup.Count == 0)
                {
                    itemGroupToAddTo = itemGroup;
                }

                foreach (ProjectItemElement item in itemGroup.Items)
                {
                    if (MSBuildNameIgnoreCaseComparer.Default.Equals(itemType, item.ItemType))
                    {
                        itemGroupToAddTo = itemGroup;
                        break;
                    }
                }

                if (itemGroupToAddTo != null && itemGroupToAddTo.Count > 0)
                {
                    break;
                }
            }

            if (itemGroupToAddTo == null)
            {
                itemGroupToAddTo = AddItemGroup();
            }

            ProjectItemElement newItem = itemGroupToAddTo.AddItem(itemType, include, metadata);

            return newItem;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Adds an item group after the last existing item group, if any; otherwise
        /// adds an item group after the last existing property group, if any; otherwise
        /// adds a new item group at the end of the project.
        /// </summary>
        public ProjectItemGroupElement AddItemGroup()
        {
            ProjectElement reference = null;

            foreach (ProjectItemGroupElement itemGroup in ItemGroupsReversed)
            {
                reference = itemGroup;
                break;
            }

            if (reference == null)
            {
                foreach (ProjectPropertyGroupElement propertyGroup in PropertyGroupsReversed)
                {
                    reference = propertyGroup;
                    break;
                }
            }

            ProjectItemGroupElement newItemGroup = CreateItemGroupElement();

            if (reference == null)
            {
                AppendChild(newItemGroup);
            }
            else
            {
                InsertAfterChild(newItemGroup, reference);
            }

            return newItemGroup;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Finds first item definition group with no condition with at least one item definition of same item type, or else adds a new item definition group.
        /// </summary>
        public ProjectItemDefinitionElement AddItemDefinition(string itemType)
        {
            ErrorUtilities.VerifyThrowArgumentLength(itemType, "itemType");

            ProjectItemDefinitionGroupElement itemDefinitionGroupToAddTo = null;

            foreach (ProjectItemDefinitionGroupElement itemDefinitionGroup in ItemDefinitionGroups)
            {
                if (itemDefinitionGroup.Condition.Length > 0)
                {
                    continue;
                }

                foreach (ProjectItemDefinitionElement itemDefinition in itemDefinitionGroup.ItemDefinitions)
                {
                    if (MSBuildNameIgnoreCaseComparer.Default.Equals(itemType, itemDefinition.ItemType))
                    {
                        itemDefinitionGroupToAddTo = itemDefinitionGroup;
                        break;
                    }
                }

                if (itemDefinitionGroupToAddTo != null)
                {
                    break;
                }
            }

            if (itemDefinitionGroupToAddTo == null)
            {
                itemDefinitionGroupToAddTo = AddItemDefinitionGroup();
            }

            ProjectItemDefinitionElement newItemDefinition = CreateItemDefinitionElement(itemType);

            itemDefinitionGroupToAddTo.AppendChild(newItemDefinition);

            return newItemDefinition;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Adds an item definition group after the last existing item definition group, if any; otherwise
        /// adds an item definition group after the last existing property group, if any; otherwise
        /// adds a new item definition group at the end of the project.
        /// </summary>
        public ProjectItemDefinitionGroupElement AddItemDefinitionGroup()
        {
            ProjectElement reference = null;

            foreach (ProjectItemDefinitionGroupElement itemDefinitionGroup in ItemDefinitionGroupsReversed)
            {
                reference = itemDefinitionGroup;
                break;
            }

            if (reference == null)
            {
                foreach (ProjectPropertyGroupElement propertyGroup in PropertyGroupsReversed)
                {
                    reference = propertyGroup;
                    break;
                }
            }

            ProjectItemDefinitionGroupElement newItemDefinitionGroup = CreateItemDefinitionGroupElement();

            InsertAfterChild(newItemDefinitionGroup, reference);

            return newItemDefinitionGroup;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Adds a new property group after the last existing property group, if any; otherwise
        /// at the start of the project.
        /// </summary>
        public ProjectPropertyGroupElement AddPropertyGroup()
        {
            ProjectPropertyGroupElement reference = null;

            foreach (ProjectPropertyGroupElement propertyGroup in PropertyGroupsReversed)
            {
                reference = propertyGroup;
                break;
            }

            ProjectPropertyGroupElement newPropertyGroup = CreatePropertyGroupElement();

            InsertAfterChild(newPropertyGroup, reference);

            return newPropertyGroup;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic.
        /// Updates the last existing property with the specified name that has no condition on itself or its property group, if any.
        /// Otherwise, adds a new property in the first property group without a condition, creating a property group if necessary after
        /// the last existing property group, else at the start of the project.
        /// </summary>
        public ProjectPropertyElement AddProperty(string name, string value)
        {
            ProjectPropertyGroupElement matchingPropertyGroup = null;
            ProjectPropertyElement matchingProperty = null;

            foreach (ProjectPropertyGroupElement propertyGroup in PropertyGroups)
            {
                if (propertyGroup.Condition.Length > 0)
                {
                    continue;
                }

                if (matchingPropertyGroup == null)
                {
                    matchingPropertyGroup = propertyGroup;
                }

                foreach (ProjectPropertyElement property in propertyGroup.Properties)
                {
                    if (property.Condition.Length > 0)
                    {
                        continue;
                    }

                    if (MSBuildNameIgnoreCaseComparer.Default.Equals(property.Name, name))
                    {
                        matchingProperty = property;
                    }
                }
            }

            if (matchingProperty != null)
            {
                matchingProperty.Value = value;

                return matchingProperty;
            }

            if (matchingPropertyGroup == null)
            {
                matchingPropertyGroup = AddPropertyGroup();
            }

            ProjectPropertyElement newProperty = matchingPropertyGroup.AddProperty(name, value);

            return newProperty;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Creates a target at the end of the project.
        /// </summary>
        public ProjectTargetElement AddTarget(string name)
        {
            ProjectTargetElement target = CreateTargetElement(name);
            AppendChild(target);

            return target;
        }

        /// <summary>
        /// Convenience method that picks a location based on a heuristic:
        /// Creates a usingtask at the end of the project.
        /// Exactly one of assemblyName or assemblyFile must be null.
        /// </summary>
        public ProjectUsingTaskElement AddUsingTask(string name, string assemblyFile, string assemblyName)
        {
            ProjectUsingTaskElement usingTask = CreateUsingTaskElement(name, assemblyFile, assemblyName);
            AppendChild(usingTask);

            return usingTask;
        }

        /// <summary>
        /// Creates a choose.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectChooseElement CreateChooseElement()
        {
            return ProjectChooseElement.CreateDisconnected(this);
        }

        /// <summary>
        /// Creates an import.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectImportElement CreateImportElement(string project)
        {
            return ProjectImportElement.CreateDisconnected(project, this);
        }

        /// <summary>
        /// Creates an item node.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectItemElement CreateItemElement(string itemType)
        {
            return ProjectItemElement.CreateDisconnected(itemType, this);
        }

        /// <summary>
        /// Creates an item node with an include.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectItemElement CreateItemElement(string itemType, string include)
        {
            ProjectItemElement item = ProjectItemElement.CreateDisconnected(itemType, this);

            item.Include = include;

            return item;
        }

        /// <summary>
        /// Creates an item definition.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectItemDefinitionElement CreateItemDefinitionElement(string itemType)
        {
            return ProjectItemDefinitionElement.CreateDisconnected(itemType, this);
        }

        /// <summary>
        /// Creates an item definition group.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectItemDefinitionGroupElement CreateItemDefinitionGroupElement()
        {
            return ProjectItemDefinitionGroupElement.CreateDisconnected(this);
        }

        /// <summary>
        /// Creates an item group.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectItemGroupElement CreateItemGroupElement()
        {
            return ProjectItemGroupElement.CreateDisconnected(this);
        }

        /// <summary>
        /// Creates an import group. 
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectImportGroupElement CreateImportGroupElement()
        {
            return ProjectImportGroupElement.CreateDisconnected(this);
        }

        /// <summary>
        /// Creates a metadata node.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectMetadataElement CreateMetadataElement(string name)
        {
            return ProjectMetadataElement.CreateDisconnected(name, this);
        }

        /// <summary>
        /// Creates a metadata node.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectMetadataElement CreateMetadataElement(string name, string unevaluatedValue)
        {
            ProjectMetadataElement metadatum = ProjectMetadataElement.CreateDisconnected(name, this);

            metadatum.Value = unevaluatedValue;

            return metadatum;
        }

        /// <summary>
        /// Creates an on error node.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectOnErrorElement CreateOnErrorElement(string executeTargets)
        {
            return ProjectOnErrorElement.CreateDisconnected(executeTargets, this);
        }

        /// <summary>
        /// Creates an otherwise node.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectOtherwiseElement CreateOtherwiseElement()
        {
            return ProjectOtherwiseElement.CreateDisconnected(this);
        }

        /// <summary>
        /// Creates an output node.
        /// Exactly one of itemType and propertyName must be specified.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectOutputElement CreateOutputElement(string taskParameter, string itemType, string propertyName)
        {
            return ProjectOutputElement.CreateDisconnected(taskParameter, itemType, propertyName, this);
        }

        /// <summary>
        /// Creates a project extensions node.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectExtensionsElement CreateProjectExtensionsElement()
        {
            return ProjectExtensionsElement.CreateDisconnected(this);
        }

        /// <summary>
        /// Creates a property group.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectPropertyGroupElement CreatePropertyGroupElement()
        {
            return ProjectPropertyGroupElement.CreateDisconnected(this);
        }

        /// <summary>
        /// Creates a property.
        /// Caller must add it to the location of choice in the project.
        /// </summary>
        public ProjectPropertyElement CreatePropertyElement(string name)
        {
            return ProjectPropertyElement.CreateDisconnected(name, this);
        }

        /// <summary>
        /// Creates a target.
        /// Caller must add it to the location of choice in this project.
        /// </summary>
        public ProjectTargetElement CreateTargetElement(string name)
        {
            return ProjectTargetElement.CreateDisconnected(name, this);
        }

        /// <summary>
        /// Creates a task.
        /// Caller must add it to the location of choice in this project.
        /// </summary>
        public ProjectTaskElement CreateTaskElement(string name)
        {
            return ProjectTaskElement.CreateDisconnected(name, this);
        }

        /// <summary>
        /// Creates a using task.
        /// Caller must add it to the location of choice in the project.
        /// Exactly one of assembly file and assembly name must be provided.
        /// </summary>
        public ProjectUsingTaskElement CreateUsingTaskElement(string taskName, string assemblyFile, string assemblyName)
        {
            return CreateUsingTaskElement(taskName, assemblyFile, assemblyName, null, null);
        }

        /// <summary>
        /// Creates a using task.
        /// Caller must add it to the location of choice in the project.
        /// Exactly one of assembly file and assembly name must be provided.
        /// Also allows providing optional runtime and architecture specifiers.  Null is OK. 
        /// </summary>
        public ProjectUsingTaskElement CreateUsingTaskElement(string taskName, string assemblyFile, string assemblyName, string runtime, string architecture)
        {
            return ProjectUsingTaskElement.CreateDisconnected(taskName, assemblyFile, assemblyName, runtime, architecture, this);
        }

        /// <summary>
        /// Creates a ParameterGroup for use in a using task.
        /// Caller must add it to the location of choice in the project under a using task.
        /// </summary>
        public UsingTaskParameterGroupElement CreateUsingTaskParameterGroupElement()
        {
            return UsingTaskParameterGroupElement.CreateDisconnected(this);
        }

        /// <summary>
        /// Creates a Parameter for use in a using ParameterGroup.
        /// Caller must add it to the location of choice in the project under a using task.
        /// </summary>
        public ProjectUsingTaskParameterElement CreateUsingTaskParameterElement(string name, string output, string required, string parameterType)
        {
            return ProjectUsingTaskParameterElement.CreateDisconnected(name, output, required, parameterType, this);
        }

        /// <summary>
        /// Creates a Task element for use in a using task.
        /// Caller must add it to the location of choice in the project under a using task.
        /// </summary>
        public ProjectUsingTaskBodyElement CreateUsingTaskBodyElement(string evaluate, string body)
        {
            return ProjectUsingTaskBodyElement.CreateDisconnected(evaluate, body, this);
        }

        /// <summary>
        /// Creates a when.
        /// Caller must add it to the location of choice in this project.
        /// </summary>
        public ProjectWhenElement CreateWhenElement(string condition)
        {
            return ProjectWhenElement.CreateDisconnected(condition, this);
        }

        /// <summary>
        /// Save the project to the file system, if dirty.
        /// Uses the Encoding returned by the Encoding property.
        /// Creates any necessary directories.
        /// May throw IO-related exceptions.
        /// Clears the dirty flag.
        /// </summary>
        public void Save()
        {
            Save(Encoding);
        }

        /// <summary>
        /// Save the project to the file system, if dirty.
        /// Creates any necessary directories.
        /// May throw IO-related exceptions.
        /// Clears the dirty flag.
        /// </summary>
        public void Save(Encoding saveEncoding)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(_projectFileLocation != null, "OM_MustSetFileNameBeforeSave");

#if MSBUILDENABLEVSPROFILING 
            try
            {
                string beginProjectSave = String.Format(CultureInfo.CurrentCulture, "Save Project {0} To File - Begin", projectFileLocation.File);
                DataCollection.CommentMarkProfile(8810, beginProjectSave);
#endif

            Directory.CreateDirectory(DirectoryPath);
#if (!STANDALONEBUILD)
            using (new CodeMarkerStartEnd(CodeMarkerEvent.perfMSBuildProjectSaveToFileBegin, CodeMarkerEvent.perfMSBuildProjectSaveToFileEnd))
#endif
            {
                if (HasUnsavedChanges || saveEncoding != Encoding)
                {
                    using (ProjectWriter projectWriter = new ProjectWriter(_projectFileLocation.File, saveEncoding))
                    {
                        projectWriter.Initialize(XmlDocument);
                        XmlDocument.Save(projectWriter);
                    }

                    _encoding = saveEncoding;

                    FileInfo fileInfo = FileUtilities.GetFileInfoNoThrow(_projectFileLocation.File);

                    // If the file was deleted by a race with someone else immediately after it was written above
                    // then we obviously can't read the write time. In this obscure case, we'll retain the 
                    // older last write time, which at worst would cause the next load to unnecessarily 
                    // come from disk.
                    if (fileInfo != null)
                    {
                        _lastWriteTimeWhenRead = fileInfo.LastWriteTime;
                    }

                    _versionOnDisk = Version;
                }
            }
#if MSBUILDENABLEVSPROFILING 
            }
            finally
            {
                string endProjectSave = String.Format(CultureInfo.CurrentCulture, "Save Project {0} To File - End", projectFileLocation.File);
                DataCollection.CommentMarkProfile(8811, endProjectSave);
            }
#endif
        }

        /// <summary>
        /// Save the project to the file system, if dirty or the path is different.
        /// Creates any necessary directories.
        /// May throw IO related exceptions.
        /// Clears the Dirty flag.
        /// </summary>
        public void Save(string path)
        {
            Save(path, Encoding);
        }

        /// <summary>
        /// Save the project to the file system, if dirty or the path is different.
        /// Creates any necessary directories.
        /// May throw IO related exceptions.
        /// Clears the Dirty flag.
        /// </summary>
        public void Save(string path, Encoding encoding)
        {
            FullPath = path;

            Save(encoding);
        }

        /// <summary>
        /// Save the project to the provided TextWriter, whether or not it is dirty.
        /// Uses the encoding of the TextWriter.
        /// Clears the Dirty flag.
        /// </summary>
        public void Save(TextWriter writer)
        {
            using (ProjectWriter projectWriter = new ProjectWriter(writer))
            {
                projectWriter.Initialize(XmlDocument);
                XmlDocument.Save(projectWriter);
            }

            _versionOnDisk = Version;
        }

        /// <summary>
        /// Returns a clone of this project.
        /// </summary>
        /// <returns>The cloned element.</returns>
        public ProjectRootElement DeepClone()
        {
            return (ProjectRootElement)this.DeepClone(this, null);
        }

        /// <summary>
        /// Initialize an in-memory, empty ProjectRootElement instance that can be saved later.
        /// Uses the specified project root element cache.
        /// </summary>
        internal static ProjectRootElement Create(ProjectRootElementCache projectRootElementCache)
        {
            return new ProjectRootElement(projectRootElementCache);
        }

        /// <summary>
        /// Initialize a ProjectRootElement instance by loading from the specified file path.
        /// Assumes path is already normalized.
        /// Uses the specified project root element cache.
        /// May throw InvalidProjectFileException.
        /// </summary>
        internal static ProjectRootElement Open(string path, ProjectRootElementCache projectRootElementCache, bool isExplicitlyLoaded)
        {
            ErrorUtilities.VerifyThrowInternalRooted(path);

            ProjectRootElement projectRootElement = projectRootElementCache.Get(path, s_openLoaderDelegate, isExplicitlyLoaded);

            return projectRootElement;
        }

        /// <summary>
        /// Initialize a ProjectRootElement instance from an existing document.
        /// Uses the global project collection.
        /// May throw InvalidProjectFileException.
        /// </summary>
        /// <remarks>
        /// This is ultimately for unit testing.
        /// Do not make public: we do not wish to expose particular XML API's.
        /// </remarks>
        internal static ProjectRootElement Open(XmlDocumentWithLocation document)
        {
            ErrorUtilities.VerifyThrow(document.FullPath == null, "Only virtual documents supported");

            return new ProjectRootElement(document, ProjectCollection.GlobalProjectCollection.ProjectRootElementCache);
        }

        /// <summary>
        /// Gets a ProjectRootElement representing an MSBuild file.
        /// Path provided must be a canonicalized full path.
        /// May throw InvalidProjectFileException or an IO-related exception.
        /// </summary>
        internal static ProjectRootElement OpenProjectOrSolution(string fullPath, IDictionary<string, string> globalProperties, string toolsVersion, ILoggingService loggingService, ProjectRootElementCache projectRootElementCache, BuildEventContext buildEventContext, bool isExplicitlyLoaded)
        {
            ErrorUtilities.VerifyThrowInternalRooted(fullPath);

            ProjectRootElement projectRootElement = projectRootElementCache.Get(
                fullPath,
                (path, cache) => CreateProjectFromPath(path, globalProperties, toolsVersion, loggingService, cache, buildEventContext),
                isExplicitlyLoaded);

            return projectRootElement;
        }

        /// <summary>
        /// Creates a XmlElement with the specified name in the document
        /// containing this project.
        /// </summary>
        internal XmlElementWithLocation CreateElement(string name)
        {
            return (XmlElementWithLocation)XmlDocument.CreateElement(name, XMakeAttributes.defaultXmlNamespace);
        }

        /// <summary>
        /// Overridden to verify that the potential parent and siblings
        /// are acceptable. Throws InvalidOperationException if they are not.
        /// </summary>
        internal override void VerifyThrowInvalidOperationAcceptableLocation(ProjectElementContainer parent, ProjectElement previousSibling, ProjectElement nextSibling)
        {
            ErrorUtilities.ThrowInvalidOperation("OM_CannotAcceptParent");
        }

        /// <summary>
        /// Marks this project as dirty.
        /// Typically called by child elements to indicate that they themselves have become dirty.
        /// Accepts a reason for debugging purposes only, and optional reason parameter.
        /// </summary>
        /// <remarks>
        /// This is sealed because it is virtual and called in a constructor; by sealing it we
        /// satisfy FXCop that nobody will override it to do something that would rely on
        /// unconstructed state.
        /// Should be protected+internal.
        /// </remarks>
        internal sealed override void MarkDirty(string reason, string param)
        {
            IncrementVersion();

            _dirtyReason = reason;
            _dirtyParameter = param;

            _timeLastChangedUtc = DateTime.UtcNow;

            var changedEventArgs = new ProjectXmlChangedEventArgs(this, reason, param);
            var projectXmlChanged = OnProjectXmlChanged;
            if (projectXmlChanged != null)
            {
                projectXmlChanged(this, changedEventArgs);
            }

            // Only bubble this event up if the cache knows about this PRE.
            if (this.IsMemberOfProjectCollection)
            {
                _projectRootElementCache.OnProjectRootElementDirtied(this, changedEventArgs);
            }
        }

        /// <summary>
        /// Bubbles a Project dirty notification up to the ProjectRootElementCache and ultimately to the ProjectCollection.
        /// </summary>
        /// <param name="project">The dirtied project.</param>
        internal void MarkProjectDirty(Project project)
        {
            ErrorUtilities.VerifyThrowArgumentNull(project, "project");

            // Only bubble this event up if the cache knows about this PRE, which is equivalent to
            // whether this PRE has a path.
            if (_projectFileLocation != null)
            {
                _projectRootElementCache.OnProjectDirtied(project, new ProjectChangedEventArgs(project));
            }
        }

        /// <summary>
        /// Sets the <see cref="IsExplicitlyLoaded"/> property to <c>true</c> to indicate that this PRE
        /// should not be removed from the cache until it is explicitly unloaded by some MSBuild client.
        /// </summary>
        internal void MarkAsExplicitlyLoaded()
        {
            IsExplicitlyLoaded = true;
        }

        /// <summary>
        /// Returns a new instance of ProjectRootElement that is affiliated with the same ProjectRootElementCache.
        /// </summary>
        /// <param name="owner">The factory to use for creating the new instance.</param>
        protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        {
            return ProjectRootElement.Create(owner._projectRootElementCache);
        }

        /// <summary>
        /// Creates a new ProjectRootElement for a specific PRE cache
        /// </summary>
        /// <param name="path">The path to the file to load.</param>
        /// <param name="projectRootElementCache">The cache to load the PRE into.</param>
        private static ProjectRootElement OpenLoader(string path, ProjectRootElementCache projectRootElementCache)
        {
            return new ProjectRootElement(
                path,
                projectRootElementCache,
                new BuildEventContext(0, BuildEventContext.InvalidNodeId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId));
        }

        /// <summary>
        /// Creates a ProjectRootElement representing a file, where the file may be a .sln instead of
        /// an MSBuild format file.
        /// Assumes path is already normalized.
        /// If the file is in MSBuild format, may throw InvalidProjectFileException.
        /// If the file is a solution, will throw an IO-related exception if the file cannot be read.
        /// </summary>
        private static ProjectRootElement CreateProjectFromPath
            (
                string projectFile,
                IDictionary<string, string> globalProperties,
                string toolsVersion,
                ILoggingService loggingService,
                ProjectRootElementCache projectRootElementCache,
                BuildEventContext buildEventContext
            )
        {
            ErrorUtilities.VerifyThrowInternalRooted(projectFile);

            try
            {
                if (FileUtilities.IsVCProjFilename(projectFile))
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(projectFile), "ProjectUpgradeNeededToVcxProj", projectFile);
                }

                // OK it's a regular project file, load it normally.
                return new ProjectRootElement(projectFile, projectRootElementCache, buildEventContext);
            }
            catch (InvalidProjectFileException)
            {
                throw;
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(projectFile), ex, "InvalidProjectFile", ex.Message);
                throw; // Without this there's a spurious CS0161 because csc 1.2.0.60317 can't see that the above is an unconditional throw.
            }
        }

        /// <summary>
        /// Constructor helper to load an XmlDocumentWithLocation from a path.
        /// Assumes path is already normalized.
        /// May throw InvalidProjectFileException.
        /// Never returns null.
        /// Does NOT add to the ProjectRootElementCache. Caller should add after verifying subsequent MSBuild parsing succeeds.
        /// </summary>
        /// <param name="fullPath">The full path to the document to load.</param>
        private XmlDocumentWithLocation LoadDocument(string fullPath)
        {
            ErrorUtilities.VerifyThrowInternalRooted(fullPath);

            XmlDocumentWithLocation document = new XmlDocumentWithLocation();
#if (!STANDALONEBUILD)
            using (new CodeMarkerStartEnd(CodeMarkerEvent.perfMSBuildProjectLoadFromFileBegin, CodeMarkerEvent.perfMSBuildProjectLoadFromFileEnd))
#endif
            {
                try
                {
#if MSBUILDENABLEVSPROFILING 
                    string beginProjectLoad = String.Format(CultureInfo.CurrentCulture, "Load Project {0} From File - Start", fullPath);
                    DataCollection.CommentMarkProfile(8806, beginProjectLoad);
#endif
                    using (XmlTextReader xtr = new XmlTextReader(fullPath))
                    {
                        // Start the reader so it has an idea of what the encoding is.
                        xtr.DtdProcessing = DtdProcessing.Ignore;
                        xtr.Read();
                        _encoding = xtr.Encoding;
                        document.Load(xtr);
                    }

                    document.FullPath = fullPath;
                    _projectFileLocation = ElementLocation.Create(fullPath);
                    _directory = Path.GetDirectoryName(fullPath);

                    if (XmlDocument != null)
                    {
                        XmlDocument.FullPath = fullPath;
                    }

                    _lastWriteTimeWhenRead = FileUtilities.GetFileInfoNoThrow(fullPath).LastWriteTime;
                }
                catch (Exception ex)
                {
                    if (ExceptionHandling.NotExpectedIoOrXmlException(ex))
                    {
                        throw;
                    }

                    XmlException xmlException = ex as XmlException;

                    BuildEventFileInfo fileInfo;

                    if (xmlException != null)
                    {
                        fileInfo = new BuildEventFileInfo(xmlException);
                    }
                    else
                    {
                        fileInfo = new BuildEventFileInfo(fullPath);
                    }

                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(fileInfo, ex, "InvalidProjectFile", ex.Message);
                }
#if MSBUILDENABLEVSPROFILING 
                finally
                {
                    string endProjectLoad = String.Format(CultureInfo.CurrentCulture, "Load Project {0} From File - End", fullPath);
                    DataCollection.CommentMarkProfile(8807, endProjectLoad);
                }
#endif
            }

            return document;
        }

        /// <summary>
        /// Constructor helper to load an XmlDocumentWithLocation from an XmlReader.
        /// May throw InvalidProjectFileException.
        /// Never returns null.
        /// </summary>
        private XmlDocumentWithLocation LoadDocument(XmlReader reader)
        {
            XmlDocumentWithLocation document = new XmlDocumentWithLocation();

            try
            {
                document.Load(reader);
            }
            catch (XmlException ex)
            {
                BuildEventFileInfo fileInfo = new BuildEventFileInfo(ex);

                ProjectFileErrorUtilities.ThrowInvalidProjectFile(fileInfo, "InvalidProjectFile", ex.Message);
            }

            return document;
        }

        /// <summary>
        /// Boost the appdomain-unique version counter for this object.
        /// This is done when it is modified, and also when it is loaded.
        /// </summary>
        private void IncrementVersion()
        {
            _version = Interlocked.Increment(ref s_globalVersionCounter);
        }
    }
}
