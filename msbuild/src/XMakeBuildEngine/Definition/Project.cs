﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Represents an definition model project.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using ObjectModel = System.Collections.ObjectModel;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Constants = Microsoft.Build.Internal.Constants;
using Utilities = Microsoft.Build.Internal.Utilities;
using ForwardingLoggerRecord = Microsoft.Build.Logging.ForwardingLoggerRecord;
using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using ProjectItemFactory = Microsoft.Build.Evaluation.ProjectItem.ProjectItemFactory;
using System.Globalization;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Flags for controlling the project load.
    /// </summary>
    /// <remarks>
    /// This is a "flags" enum, allowing future settings to be added
    /// in an additive, non breaking fashion.
    /// </remarks>
    [Flags]
    [SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue", Justification = "Public API.  'Default' is roughly equivalent to 'None'. ")]
    public enum ProjectLoadSettings
    {
        /// <summary>
        /// Normal load. This is the default.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Ignore nonexistent targets files when evaluating the project
        /// </summary>
        IgnoreMissingImports = 1,

        /// <summary>
        /// Record imports including duplicate, but not circular, imports on the ImportsIncludingDuplicates property
        /// </summary>
        RecordDuplicateButNotCircularImports = 2,

        /// <summary>
        /// Throw an exception and stop the evaluation of a project if any circular imports are detected
        /// </summary>
        RejectCircularImports = 4
    }

    /// <summary>
    /// Represents an evaluated project with design time semantics.
    /// Always backed by XML; can be built directly, or an instance can be cloned off to add virtual items/properties and build.
    /// Edits to this project always update the backing XML.
    /// </summary>
    /// <remarks>
    /// UNDONE: (Multiple configurations.) Protect against problems when attempting to edit, after edits were made to the same ProjectRootElement either directly or through other projects evaluated from that ProjectRootElement.
    /// </remarks>
    [DebuggerDisplay("{FullPath} EffectiveToolsVersion={ToolsVersion} #GlobalProperties={_data._globalProperties.Count} #Properties={_data.Properties.Count} #ItemTypes={_data.ItemTypes.Count} #ItemDefinitions={_data.ItemDefinitions.Count} #Items={_data.Items.Count} #Targets={_data.Targets.Count}")]
    public class Project
    {
        /// <summary>
        /// Whether to write information about why we evaluate to debug output.
        /// </summary>
        private static readonly bool s_debugEvaluation = (Environment.GetEnvironmentVariable("MSBUILDDEBUGEVALUATION") != null);

        /// <summary>
        /// Backing XML object.
        /// Can never be null: projects must always be backed by XML
        /// </summary>
        private readonly ProjectRootElement _xml;

        /// <summary>
        /// Project collection in which this Project is a member.
        /// All Project's are a member of exactly one ProjectCollection.
        /// Their backing ProjectRootElement may be shared with Projects in another ProjectCollection.
        /// </summary>
        private readonly ProjectCollection _projectCollection = ProjectCollection.GlobalProjectCollection;

        /// <summary>
        /// Context to log messages and events in
        /// </summary>
        private static BuildEventContext s_buildEventContext = new BuildEventContext(0 /* node ID */, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId);

        /// <summary>
        /// The last used evaluation counter anywhere in this appdomain.
        /// Used such that even after unload and reload, the evaluation counter changes.
        /// </summary>
        private static int s_globalEvaluationCounter;

        /// <summary>
        /// Locking object.
        /// </summary>
        private static object s_locker = new Object();

        /// <summary>
        /// Backing data; stored in a nested class so it can be passed to the Evaluator to fill
        /// in on re-evaluation, without having to expose property setters for that purpose.
        /// Also it makes it easy to re-evaluate this project without creating a new project object.
        /// </summary>
        private Data _data;

        /// <summary>
        /// The highest version of the backing ProjectRootElements (including imports) that this object was last evaluated from.
        /// Edits to the ProjectRootElement either by this Project or another Project increment the number.
        /// If that number is different from this one a reevaluation is necessary at some point.
        /// </summary>
        private int _evaluatedVersion;

        /// <summary>
        /// The version of the tools information in the project collection against we were last evaluated.
        /// </summary>
        private int _evaluatedToolsetCollectionVersion;

        /// <summary>
        /// The number of evaluations that have occurred to this project object since it was created.
        /// Hosts don't know whether an evaluation actually happened in an interval, but they can compare this number to
        /// their previously stored value to find out, and if so perhaps decide to update their own state.
        /// </summary>
        private int _evaluationCounter;

        /// <summary>
        /// Whether the project has been explicitly marked as dirty. Generally this is not necessary to set; all edits affecting
        /// this project will automatically make it dirty. However there are potential corner cases where it is necessary to mark it dirty
        /// directly. For example, if the project has an import conditioned on a file existing on disk, and the file did not exist at
        /// evaluation time, then someone subsequently writes the file, the project will not know that reevaluation would be productive,
        /// and would not dirty itself. In such a case the host should help us by setting the dirty flag explicitly.
        /// </summary>
        private bool _explicitlyMarkedDirty;

        /// <summary>
        /// This controls whether or not the building of targets/tasks is enabled for this
        /// project.  This is for security purposes in case a host wants to closely
        /// control which projects it allows to run targets/tasks.
        /// </summary>
        private BuildEnabledSetting _isBuildEnabled = BuildEnabledSetting.UseProjectCollectionSetting;

        /// <summary>
        /// The load settings, such as to ignore missing imports.
        /// This is retained after construction as it will be needed for reevaluation.
        /// </summary>
        private ProjectLoadSettings _loadSettings;

        /// <summary>
        /// The delegate registered with the ProjectRootElement to be called if the file name
        /// is changed. Retained so that ultimately it can be unregistered.
        /// If it has been set to null, the project has been unloaded from its collection.
        /// </summary>
        private RenameHandlerDelegate _renameHandler;

        /// <summary>
        /// Construct an empty project, evaluating with the global project collection's
        /// global properties and default tools version.
        /// Project will be added to the global project collection when it is named.
        /// </summary>
        public Project()
            : this(ProjectRootElement.Create(ProjectCollection.GlobalProjectCollection))
        {
        }

        /// <summary>
        /// Construct an empty project, evaluating with the specified project collection's
        /// global properties and default tools version.
        /// Project will be added to the specified project collection when it is named.
        /// </summary>
        public Project(ProjectCollection projectCollection)
            : this(ProjectRootElement.Create(projectCollection), null, null, projectCollection)
        {
        }

        /// <summary>
        /// Construct an empty project, evaluating with the specified project collection and
        /// the specified global properties and default tools version, either of which may be null.
        /// Project will be added to the specified project collection when it is named.
        /// </summary>
        /// <param name="globalProperties">Global properties to evaluate with. May be null in which case the containing project collection's global properties will be used.</param>
        public Project(IDictionary<string, string> globalProperties, string toolsVersion, ProjectCollection projectCollection)
            : this(ProjectRootElement.Create(projectCollection), globalProperties, toolsVersion, projectCollection)
        {
        }

        /// <summary>
        /// Construct over a ProjectRootElement object, evaluating with the global project collection's
        /// global properties and default tools version.
        /// Project is added to the global project collection if it has a name, or else when it is named.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// </summary>
        /// <param name="xml">ProjectRootElement to use</param>
        public Project(ProjectRootElement xml)
            : this(xml, null, null)
        {
        }

        /// <summary>
        /// Construct over a ProjectRootElement object, evaluating with specified
        /// global properties and toolset, either or both of which may be null.
        /// Project is added to the global project collection if it has a name, or else when it is named.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// </summary>
        /// <param name="xml">ProjectRootElement to use</param>
        /// <param name="globalProperties">Global properties to evaluate with. May be null in which case the containing project collection's global properties will be used.</param>
        /// <param name="toolsVersion">Tools version to evaluate with. May be null</param>
        public Project(ProjectRootElement xml, IDictionary<string, string> globalProperties, string toolsVersion)
            : this(xml, globalProperties, toolsVersion, ProjectCollection.GlobalProjectCollection)
        {
        }

        /// <summary>
        /// Construct over a ProjectRootElement object, evaluating with specified
        /// global properties and toolset, either or both of which may be null.
        /// Project is added to the global project collection if it has a name, or else when it is named.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// </summary>
        /// <param name="xml">ProjectRootElement to use</param>
        /// <param name="globalProperties">Global properties to evaluate with. May be null in which case the containing project collection's global properties will be used.</param>
        /// <param name="toolsVersion">Tools version to evaluate with. May be null</param>
        public Project(ProjectRootElement xml, IDictionary<string, string> globalProperties, string toolsVersion, ProjectCollection projectCollection)
            : this(xml, globalProperties, toolsVersion, projectCollection, ProjectLoadSettings.Default)
        {
        }

        /// <summary>
        /// Construct over a ProjectRootElement object, evaluating with specified
        /// global properties and toolset, either or both of which may be null.
        /// Project is added to the global project collection if it has a name, or else when it is named.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// </summary>
        /// <param name="xml">ProjectRootElement to use</param>
        /// <param name="globalProperties">Global properties to evaluate with. May be null in which case the containing project collection's global properties will be used.</param>
        /// <param name="toolsVersion">Tools version to evaluate with. May be null</param>
        public Project(ProjectRootElement xml, IDictionary<string, string> globalProperties, string toolsVersion, ProjectCollection projectCollection, ProjectLoadSettings loadSettings)
            : this(xml, globalProperties, toolsVersion, null /* no explicit sub-toolset version */, projectCollection, loadSettings)
        {
        }

        /// <summary>
        /// Construct over a ProjectRootElement object, evaluating with specified
        /// global properties and toolset, either or both of which may be null.
        /// Project is added to the global project collection if it has a name, or else when it is named.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// </summary>
        /// <param name="xml">ProjectRootElement to use</param>
        /// <param name="globalProperties">Global properties to evaluate with. May be null in which case the containing project collection's global properties will be used.</param>
        /// <param name="toolsVersion">Tools version to evaluate with. May be null</param>
        /// <param name="subToolsetVersion">Sub-toolset version to explicitly evaluate the toolset with.  May be null.</param>
        public Project(ProjectRootElement xml, IDictionary<string, string> globalProperties, string toolsVersion, string subToolsetVersion, ProjectCollection projectCollection, ProjectLoadSettings loadSettings)
        {
            ErrorUtilities.VerifyThrowArgumentNull(xml, "xml");
            ErrorUtilities.VerifyThrowArgumentLengthIfNotNull(toolsVersion, "toolsVersion");
            ErrorUtilities.VerifyThrowArgumentNull(projectCollection, "projectCollection");

            _xml = xml;
            _projectCollection = projectCollection;

            Initialize(globalProperties, toolsVersion, subToolsetVersion, loadSettings);
        }

        /// <summary>
        /// Construct over a text reader over project xml, evaluating with the global project collection's
        /// global properties and default tools version.
        /// Project will be added to the global project collection when it is named.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// </summary>
        /// <param name="xmlReader">Xml reader to read project from</param>
        public Project(XmlReader xmlReader)
            : this(xmlReader, null, null)
        {
        }

        /// <summary>
        /// Construct over a text reader over project xml, evaluating with specified
        /// global properties and toolset, either or both of which may be null.
        /// Project will be added to the global project collection when it is named.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// </summary>
        /// <param name="xmlReader">Xml reader to read project from</param>
        /// <param name="globalProperties">Global properties to evaluate with. May be null in which case the containing project collection's global properties will be used.</param>
        /// <param name="toolsVersion">Tools version to evaluate with. May be null</param>
        public Project(XmlReader xmlReader, IDictionary<string, string> globalProperties, string toolsVersion)
            : this(xmlReader, globalProperties, toolsVersion, ProjectCollection.GlobalProjectCollection)
        {
        }

        /// <summary>
        /// Construct over a text reader over project xml, evaluating with specified
        /// global properties and toolset, either or both of which may be null.
        /// Project will be added to the specified project collection when it is named.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// </summary>
        /// <param name="xmlReader">Xml reader to read project from</param>
        /// <param name="globalProperties">Global properties to evaluate with. May be null in which case the containing project collection's global properties will be used.</param>
        /// <param name="toolsVersion">Tools version to evaluate with. May be null</param>
        /// <param name="projectCollection">The collection with which this project should be associated. May not be null.</param>
        public Project(XmlReader xmlReader, IDictionary<string, string> globalProperties, string toolsVersion, ProjectCollection projectCollection)
            : this(xmlReader, globalProperties, toolsVersion, projectCollection, ProjectLoadSettings.Default)
        {
        }

        /// <summary>
        /// Construct over a text reader over project xml, evaluating with specified
        /// global properties and toolset, either or both of which may be null.
        /// Project will be added to the specified project collection when it is named.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// </summary>
        /// <param name="xmlReader">Xml reader to read project from</param>
        /// <param name="globalProperties">Global properties to evaluate with. May be null in which case the containing project collection's global properties will be used.</param>
        /// <param name="toolsVersion">Tools version to evaluate with. May be null</param>
        /// <param name="projectCollection">The collection with which this project should be associated. May not be null.</param>
        public Project(XmlReader xmlReader, IDictionary<string, string> globalProperties, string toolsVersion, ProjectCollection projectCollection, ProjectLoadSettings loadSettings)
            : this(xmlReader, globalProperties, toolsVersion, null /* no explicit sub-toolset version */, projectCollection, loadSettings)
        {
        }

        /// <summary>
        /// Construct over a text reader over project xml, evaluating with specified
        /// global properties and toolset, either or both of which may be null.
        /// Project will be added to the specified project collection when it is named.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// </summary>
        /// <param name="xmlReader">Xml reader to read project from</param>
        /// <param name="globalProperties">Global properties to evaluate with. May be null in which case the containing project collection's global properties will be used.</param>
        /// <param name="toolsVersion">Tools version to evaluate with. May be null</param>
        /// <param name="subToolsetVersion">Sub-toolset version to explicitly evaluate the toolset with.  May be null.</param>
        /// <param name="projectCollection">The collection with which this project should be associated. May not be null.</param>
        /// <param name="loadSettings">The load settings for this project.</param>
        public Project(XmlReader xmlReader, IDictionary<string, string> globalProperties, string toolsVersion, string subToolsetVersion, ProjectCollection projectCollection, ProjectLoadSettings loadSettings)
        {
            ErrorUtilities.VerifyThrowArgumentNull(xmlReader, "xmlReader");
            ErrorUtilities.VerifyThrowArgumentLengthIfNotNull(toolsVersion, "toolsVersion");
            ErrorUtilities.VerifyThrowArgumentNull(projectCollection, "projectCollection");

            _projectCollection = projectCollection;

            try
            {
                _xml = ProjectRootElement.Create(xmlReader, projectCollection);
            }
            catch (InvalidProjectFileException ex)
            {
                LoggingService.LogInvalidProjectFileError(s_buildEventContext, ex);
                throw;
            }

            Initialize(globalProperties, toolsVersion, subToolsetVersion, loadSettings);
        }

        /// <summary>
        /// Construct over an existing project file, evaluating with the global project collection's
        /// global properties and default tools version.
        /// Project is added to the global project collection.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// May throw IO-related exceptions.
        /// </summary>
        /// <exception cref="InvalidProjectFileException">If the evaluation fails.</exception>
        public Project(string projectFile)
            : this(projectFile, null, null)
        {
        }

        /// <summary>
        /// Construct over an existing project file, evaluating with specified
        /// global properties and toolset, either or both of which may be null.
        /// Project is added to the global project collection.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// May throw IO-related exceptions.
        /// </summary>
        public Project(string projectFile, IDictionary<string, string> globalProperties, string toolsVersion)
            : this(projectFile, globalProperties, toolsVersion, ProjectCollection.GlobalProjectCollection)
        {
        }

        /// <summary>
        /// Construct over an existing project file, evaluating with the specified global properties and 
        /// using the tools version provided, either or both of which may be null.
        /// Project is added to the global project collection.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// May throw IO-related exceptions.
        /// </summary>
        /// <param name="projectFile">The project file</param>
        /// <param name="globalProperties">The global properties. May be null.</param>
        /// <param name="toolsVersion">The tools version. May be null.</param>
        /// <param name="projectCollection">The collection with which this project should be associated. May not be null.</param>
        public Project(string projectFile, IDictionary<string, string> globalProperties, string toolsVersion, ProjectCollection projectCollection)
            : this(projectFile, globalProperties, toolsVersion, projectCollection, ProjectLoadSettings.Default)
        {
        }

        /// <summary>
        /// Construct over an existing project file, evaluating with the specified global properties and 
        /// using the tools version provided, either or both of which may be null.
        /// Project is added to the global project collection.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// May throw IO-related exceptions.
        /// </summary>
        /// <param name="projectFile">The project file</param>
        /// <param name="globalProperties">The global properties. May be null.</param>
        /// <param name="toolsVersion">The tools version. May be null.</param>
        /// <param name="projectCollection">The collection with which this project should be associated. May not be null.</param>
        public Project(string projectFile, IDictionary<string, string> globalProperties, string toolsVersion, ProjectCollection projectCollection, ProjectLoadSettings loadSettings)
            : this(projectFile, globalProperties, toolsVersion, null /* no explicitly specified sub-toolset version */, projectCollection, loadSettings)
        {
        }

        /// <summary>
        /// Construct over an existing project file, evaluating with the specified global properties and 
        /// using the tools version provided, either or both of which may be null.
        /// Project is added to the global project collection.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// Throws InvalidOperationException if there is already an equivalent project loaded in the project collection.
        /// May throw IO-related exceptions.
        /// </summary>
        /// <param name="projectFile">The project file</param>
        /// <param name="globalProperties">The global properties. May be null.</param>
        /// <param name="toolsVersion">The tools version. May be null.</param>
        /// <param name="subToolsetVersion">Sub-toolset version to explicitly evaluate the toolset with.  May be null.</param>
        /// <param name="projectCollection">The collection with which this project should be associated. May not be null.</param>
        /// <param name="loadSettings">The load settings for this project.</param>
        public Project(string projectFile, IDictionary<string, string> globalProperties, string toolsVersion, string subToolsetVersion, ProjectCollection projectCollection, ProjectLoadSettings loadSettings)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectFile, "projectFile");
            ErrorUtilities.VerifyThrowArgumentLengthIfNotNull(toolsVersion, "toolsVersion");
            ErrorUtilities.VerifyThrowArgumentNull(projectCollection, "projectCollection");

            _projectCollection = projectCollection;

            // We do not control the current directory at this point, but assume that if we were
            // passed a relative path, the caller assumes we will prepend the current directory.
            projectFile = FileUtilities.NormalizePath(projectFile);

            try
            {
                _xml = ProjectRootElement.OpenProjectOrSolution(projectFile, globalProperties, toolsVersion, LoggingService, projectCollection.ProjectRootElementCache, s_buildEventContext, true /*Explicitly loaded*/);
            }
            catch (InvalidProjectFileException ex)
            {
                LoggingService.LogInvalidProjectFileError(s_buildEventContext, ex);
                throw;
            }

            try
            {
                Initialize(globalProperties, toolsVersion, subToolsetVersion, loadSettings);
            }
            catch (Exception ex)
            {
                // If possible, clear out the XML we just loaded into the XML cache:
                // if we had loaded the XML from disk into the cache within this constructor,
                // and then are are bailing out because there is a typo in the XML such that 
                // evaluation failed, we don't want to leave the bad XML in the cache;
                // the user wouldn't be able to fix the XML file and try again.
                if (!ExceptionHandling.IsCriticalException(ex))
                {
                    projectCollection.TryUnloadProject(_xml);
                }

                throw;
            }
        }

        /// <summary>
        /// Whether build is enabled for this project.
        /// </summary>
        private enum BuildEnabledSetting
        {
            /// <summary>
            /// Explicitly enabled
            /// </summary>
            BuildEnabled,

            /// <summary>
            /// Explicitly disabled
            /// </summary>
            BuildDisabled,

            /// <summary>
            /// No explicit setting, uses the setting on the
            /// project collection.
            /// This is the default.
            /// </summary>
            UseProjectCollectionSetting
        }

        /// <summary>
        /// Gets or sets the project collection which contains this project.
        /// Can never be null.
        /// Cannot be modified.
        /// </summary>
        public ProjectCollection ProjectCollection
        {
            [DebuggerStepThrough]
            get
            { return _projectCollection; }
        }

        /// <summary>
        /// The backing Xml project.
        /// Can never be null
        /// </summary>
        /// <remarks>
        /// There is no setter here as that doesn't make sense. If you have a new ProjectRootElement, evaluate it into a new Project.
        /// </remarks>
        public ProjectRootElement Xml
        {
            [DebuggerStepThrough]
            get
            { return _xml; }
        }

        /// <summary>
        /// Whether this project is dirty such that it needs reevaluation.
        /// This may be because its underlying XML has changed (either through this project or another)
        /// either the XML of the main project or an imported file; 
        /// or because its toolset may have changed.
        /// </summary>
        public bool IsDirty
        {
            get
            {
                if (_explicitlyMarkedDirty)
                {
                    if (s_debugEvaluation)
                    {
                        Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "MSBUILD: Explicitly marked dirty, eg., because a global property was set, or an import, such as a .user file, was created on disk [{0}] [PC Hash {1}]", FullPath, _projectCollection.GetHashCode()));
                    }

                    return true;
                }

                if (_evaluatedVersion < _xml.Version)
                {
                    if (s_debugEvaluation)
                    {
                        if (_xml.Count > 0) // don't log empty projects, evaluation is not interesting
                        {
                            Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "MSBUILD: Is dirty because {0} [{1}] [PC Hash {2}]", _xml.LastDirtyReason, FullPath, _projectCollection.GetHashCode()));
                        }
                    }

                    return true;
                }

                if (_evaluatedToolsetCollectionVersion != ProjectCollection.ToolsetsVersion)
                {
                    if (s_debugEvaluation)
                    {
                        Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "MSBUILD: Is dirty because toolsets updated [{0}] [PC Hash {1}]", FullPath, _projectCollection.GetHashCode()));
                    }

                    return true;
                }

                foreach (Triple<ProjectImportElement, ProjectRootElement, int> triple in _data.ImportClosure)
                {
                    if (triple.Second.Version != triple.Third || _evaluatedVersion < triple.Third)
                    {
                        if (s_debugEvaluation)
                        {
                            string reason = triple.Second.LastDirtyReason;

                            if (reason != null)
                            {
                                Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "MSBUILD: Is dirty because {0} [{1} - {2}] [PC Hash {3}]", reason, FullPath, (triple.Second.FullPath == FullPath ? String.Empty : triple.Second.FullPath), _projectCollection.GetHashCode()));
                            }
                        }

                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// An arbitrary number that changes when this project reevaluates.
        /// Hosts don't know whether an evaluation actually happened in an interval, but they can compare this number to
        /// their previously stored value to find out, and if so perhaps decide to update their own state.
        /// Note that the number may not increase monotonically.
        /// Unloading a project does not reset the number, so it does not break the guarantee.
        /// </summary>
        public int EvaluationCounter
        {
            get { return _evaluationCounter; }
        }

        /// <summary>
        /// Read only dictionary of the global properties used in the evaluation
        /// of this project.
        /// </summary>
        /// <remarks>
        /// This is the publicly exposed getter, that translates into a read-only dead IDictionary&lt;string, string&gt;.
        /// 
        /// In order to easily tell when we're dirtied, setting and removing global properties is done with 
        /// <see cref="SetGlobalProperty">SetGlobalProperty</see> and <see cref="RemoveGlobalProperty">RemoveGlobalProperty</see>.
        /// </remarks>
        public IDictionary<string, string> GlobalProperties
        {
            [DebuggerStepThrough]
            get
            {
                if (_data.GlobalPropertiesDictionary.Count == 0)
                {
                    return ReadOnlyEmptyDictionary<string, string>.Instance;
                }

                Dictionary<string, string> dictionary = new Dictionary<string, string>(_data.GlobalPropertiesDictionary.Count, MSBuildNameIgnoreCaseComparer.Default);

                foreach (ProjectPropertyInstance property in _data.GlobalPropertiesDictionary)
                {
                    dictionary[property.Name] = ((IProperty)property).EvaluatedValueEscaped;
                }

                return new ObjectModel.ReadOnlyDictionary<string, string>(dictionary);
            }
        }

        /// <summary>
        /// Item types in this project.
        /// This is an ordered collection.
        /// </summary>
        /// <comments>
        /// data.ItemTypes is a KeyCollection, so it doesn't need any 
        /// additional read-only protection
        /// </comments>
        public ICollection<string> ItemTypes
        {
            [DebuggerStepThrough]
            get
            { return _data.ItemTypes; }
        }

        /// <summary>
        /// Properties in this project.
        /// Since evaluation has occurred, this is an unordered collection.
        /// </summary>
        public ICollection<ProjectProperty> Properties
        {
            [DebuggerStepThrough]
            get
            { return new ReadOnlyCollection<ProjectProperty>(_data.Properties); }
        }

        /// <summary>
        /// Collection of possible values implied for properties contained in the conditions found on properties,
        /// property groups, imports, and whens.
        /// 
        /// For example, if the following conditions existed on properties in a project:
        /// 
        /// Condition="'$(Configuration)|$(Platform)' == 'Debug|x86'"
        /// Condition="'$(Configuration)' == 'Release'"
        /// 
        /// the table would be populated with
        /// 
        /// { "Configuration", { "Debug", "Release" }}
        /// { "Platform", { "x86" }}
        /// 
        /// This is used by Visual Studio to determine the configurations defined in the project.
        /// </summary>
        public IDictionary<string, List<string>> ConditionedProperties
        {
            [DebuggerStepThrough]
            get
            {
                if (_data.ConditionedProperties == null)
                {
                    return ReadOnlyEmptyDictionary<string, List<string>>.Instance;
                }

                return new ObjectModel.ReadOnlyDictionary<string, List<string>>(_data.ConditionedProperties);
            }
        }

        /// <summary>
        /// Read-only dictionary of item definitions in this project.
        /// Keyed by item type
        /// </summary>
        public IDictionary<string, ProjectItemDefinition> ItemDefinitions
        {
            [DebuggerStepThrough]
            get
            { return _data.ItemDefinitions; }
        }

        /// <summary>
        /// Items in this project, ordered within groups of item types
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods", Justification = "This is a reasonable choice. API review approved")]
        public ICollection<ProjectItem> Items
        {
            [DebuggerStepThrough]
            get
            { return new ReadOnlyCollection<ProjectItem>(_data.Items); }
        }

        /// <summary>
        /// Items in this project, ordered within groups of item types,
        /// including items whose conditions evaluated to false, or that were
        /// contained within item groups who themselves had conditioned evaluated to false.
        /// This is useful for hosts that wish to display all items, even if they might not be part 
        /// of the build in the current configuration.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods", Justification = "This is a reasonable choice. API review approved")]
        public ICollection<ProjectItem> ItemsIgnoringCondition
        {
            [DebuggerStepThrough]
            get
            { return new ReadOnlyCollection<ProjectItem>(_data.ItemsIgnoringCondition); }
        }

        /// <summary>
        /// All the files that during evaluation contributed to this project, as ProjectRootElements,
        /// with the ProjectImportElement that caused them to be imported.
        /// This does not include projects that were never imported because a condition on an Import element was false.
        /// The outer ProjectRootElement that maps to this project itself is not included.
        /// </summary>
        /// <remarks>
        /// This can be used by the host to figure out what projects might be impacted by a change to a particular file.
        /// It could also be used, for example, to find the .user file, and use its ProjectRootElement to modify properties in it.
        /// </remarks>
        public IList<ResolvedImport> Imports
        {
            get
            {
                List<ResolvedImport> imports = new List<ResolvedImport>(_data.ImportClosure.Count - 1 /* outer project */);

                foreach (Triple<ProjectImportElement, ProjectRootElement, int> import in _data.ImportClosure)
                {
                    if (import.First != null) // Exclude outer project itself
                    {
                        imports.Add(new ResolvedImport(this, import.First, import.Second));
                    }
                }

                return imports;
            }
        }

        /// <summary>
        /// This list will contain duplicate imports if an import is imported multiple times. However, only the first import was used in evaluation.
        /// </summary>
        public IList<ResolvedImport> ImportsIncludingDuplicates
        {
            get
            {
                ErrorUtilities.VerifyThrowInvalidOperation((_loadSettings & ProjectLoadSettings.RecordDuplicateButNotCircularImports) != 0, "OM_MustSetRecordDuplicateInputs");

                List<ResolvedImport> imports = new List<ResolvedImport>(_data.ImportClosureWithDuplicates.Count - 1 /* outer project */);

                foreach (Triple<ProjectImportElement, ProjectRootElement, int> import in _data.ImportClosureWithDuplicates)
                {
                    if (import.First != null) // Exclude outer project itself
                    {
                        imports.Add(new ResolvedImport(this, import.First, import.Second));
                    }
                }

                return imports;
            }
        }

        /// <summary>
        /// Targets in the project. The key to the dictionary is the target's name.
        /// Overridden targets are not included in this collection.
        /// This collection is read-only.
        /// </summary>
        public IDictionary<string, ProjectTargetInstance> Targets
        {
            [DebuggerStepThrough]
            get
            {
                if (_data.Targets == null)
                {
                    return ReadOnlyEmptyDictionary<string, ProjectTargetInstance>.Instance;
                }

                return new ObjectModel.ReadOnlyDictionary<string, ProjectTargetInstance>(_data.Targets);
            }
        }

        /// <summary>
        /// Properties encountered during evaluation. These are read during the first evaluation pass.
        /// Unlike those returned by the Properties property, these are ordered, and includes any properties that
        /// were subsequently overridden by others with the same name. It does not include any 
        /// properties whose conditions did not evaluate to true.
        /// It does not include any properties added since the last evaluation.
        /// </summary>
        public ICollection<ProjectProperty> AllEvaluatedProperties
        {
            get
            {
                ICollection<ProjectProperty> allEvaluatedProperties = _data.AllEvaluatedProperties;

                if (allEvaluatedProperties == null)
                {
                    return ReadOnlyEmptyCollection<ProjectProperty>.Instance;
                }

                return new ReadOnlyCollection<ProjectProperty>(allEvaluatedProperties);
            }
        }

        /// <summary>
        /// Item definition metadata encountered during evaluation. These are read during the second evaluation pass.
        /// Unlike those returned by the ItemDefinitions property, these are ordered, and include any metadata that
        /// were subsequently overridden by others with the same name and item type. It does not include any 
        /// elements whose conditions did not evaluate to true.
        /// It does not include any item definition metadata added since the last evaluation.
        /// </summary>
        public ICollection<ProjectMetadata> AllEvaluatedItemDefinitionMetadata
        {
            get
            {
                ICollection<ProjectMetadata> allEvaluatedItemDefinitionMetadata = _data.AllEvaluatedItemDefinitionMetadata;

                if (allEvaluatedItemDefinitionMetadata == null)
                {
                    return ReadOnlyEmptyCollection<ProjectMetadata>.Instance;
                }

                return new ReadOnlyCollection<ProjectMetadata>(allEvaluatedItemDefinitionMetadata);
            }
        }

        /// <summary>
        /// Items encountered during evaluation. These are read during the third evaluation pass.
        /// Unlike those returned by the Items property, these are ordered with respect to all other items 
        /// encountered during evaluation, not just ordered with respect to items of the same item type.
        /// In some applications, like the F# language, this complete mutual ordering is significant, and such hosts
        /// can use this property.
        /// It does not include any elements whose conditions did not evaluate to true.
        /// It does not include any items added since the last evaluation.
        /// </summary>
        public ICollection<ProjectItem> AllEvaluatedItems
        {
            get
            {
                ICollection<ProjectItem> allEvaluatedItems = _data.AllEvaluatedItems;

                if (allEvaluatedItems == null)
                {
                    return ReadOnlyEmptyCollection<ProjectItem>.Instance;
                }

                return new ReadOnlyCollection<ProjectItem>(allEvaluatedItems);
            }
        }

        /// <summary>
        /// The tools version this project was evaluated with, if any.
        /// Not necessarily the same as the tools version on the Project tag, if any;
        /// it may have been externally specified, for example with a /tv switch.
        /// The actual tools version on the Project tag, can be gotten from <see cref="Xml">Xml.ToolsVersion</see>.
        /// Cannot be changed once the project has been created.
        /// </summary>
        /// <remarks>
        /// Set by construction.
        /// </remarks>
        public string ToolsVersion
        {
            get { return _data.Toolset.ToolsVersion; }
        }

        /// <summary>
        /// The sub-toolset version that, combined with the ToolsVersion, was used to determine
        /// the toolset properties for this project.  
        /// </summary>
        public string SubToolsetVersion
        {
            get { return _data.SubToolsetVersion; }
        }

        /// <summary>
        /// The root directory for this project.
        /// Is never null: in-memory projects use the current directory from the time of load.
        /// </summary>
        public string DirectoryPath
        {
            [DebuggerStepThrough]
            get
            { return Xml.DirectoryPath; }
        }

        /// <summary>
        /// The full path to this project's file.
        /// May be null, if the project was not loaded from disk.
        /// Setter renames the project, if it already had a name.
        /// </summary>
        public string FullPath
        {
            [DebuggerStepThrough]
            get
            { return Xml.FullPath; }
            [DebuggerStepThrough]
            set
            { Xml.FullPath = value; }
        }

        /// <summary>
        /// Whether ReevaluateIfNecessary is temporarily disabled.
        /// This is useful when the host expects to make a number of reads and writes 
        /// to the project, and wants to temporarily sacrifice correctness for performance.
        /// </summary>
        public bool SkipEvaluation
        {
            get;
            set;
        }

        /// <summary>
        /// Whether <see cref="MarkDirty()">MarkDirty()</see> is temporarily disabled.
        /// This allows, for example, a global property to be set without the project getting
        /// marked dirty for reevaluation as a consequence.
        /// </summary>
        public bool DisableMarkDirty
        {
            get;
            set;
        }

        /// <summary>
        /// This controls whether or not the building of targets/tasks is enabled for this
        /// project.  This is for security purposes in case a host wants to closely
        /// control which projects it allows to run targets/tasks.  By default, for a newly
        /// created project, we will use whatever setting is in the parent project collection.
        /// When build is disabled, the Build method on this class will fail. However if
        /// the host has already created a ProjectInstance, it can still build it. (It is 
        /// free to put a similar check around where it does this.)
        /// </summary>
        public bool IsBuildEnabled
        {
            get
            {
                switch (_isBuildEnabled)
                {
                    case BuildEnabledSetting.BuildEnabled:
                        return true;

                    case BuildEnabledSetting.BuildDisabled:
                        return false;

                    case BuildEnabledSetting.UseProjectCollectionSetting:
                        return ProjectCollection.IsBuildEnabled;

                    default:
                        ErrorUtilities.ThrowInternalErrorUnreachable();
                        return false;
                }
            }

            set
            {
                _isBuildEnabled = value ? BuildEnabledSetting.BuildEnabled : BuildEnabledSetting.BuildDisabled;
            }
        }

        /// <summary>
        /// Location of the originating file itself, not any specific content within it.
        /// If the file has not been given a name, returns an empty location.
        /// </summary>
        public ElementLocation ProjectFileLocation
        {
            get { return _xml.ProjectFileLocation; }
        }

        /// <summary>
        /// List of names of the properties that, while global, are still treated as overridable 
        /// </summary>
        internal ISet<string> GlobalPropertiesToTreatAsLocal
        {
            [DebuggerStepThrough]
            get
            { return _data.GlobalPropertiesToTreatAsLocal; }
        }

        /// <summary>
        /// The logging service used for evaluation errors
        /// </summary>
        internal ILoggingService LoggingService
        {
            [DebuggerStepThrough]
            get
            { return ProjectCollection.LoggingService; }
        }

        /// <summary>
        /// Returns the evaluated, escaped value of the provided item's include.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Justification = "IItem is an internal interface; this is less confusing to outside customers. ")]
        public static string GetEvaluatedItemIncludeEscaped(ProjectItem item)
        {
            ErrorUtilities.VerifyThrowArgumentNull(item, "item");

            return ((IItem)item).EvaluatedIncludeEscaped;
        }

        /// <summary>
        /// Returns the evaluated, escaped value of the provided item definition's include.
        /// </summary>
        public static string GetEvaluatedItemIncludeEscaped(ProjectItemDefinition item)
        {
            ErrorUtilities.VerifyThrowArgumentNull(item, "item");

            return ((IItem)item).EvaluatedIncludeEscaped;
        }

        /// <summary>
        /// Finds all the item elements in the logical project with itemspecs that match the given string:
        /// - elements that would include (or exclude) the string
        /// - elements that would update the string (not yet implemented)
        /// - elements that would remove the string (not yet implemented)
        /// </summary>
        /// 
        /// <example>
        /// The following snippet shows what <c>GetItemProvenance("a.cs")</c> returns for various item elements
        /// <code>
        /// <A Include="a.cs;*.cs"/> // Occurences:2; Operation: Include; Provenance: StringLiteral | Glob
        /// <B Include="*.cs" Exclude="a.cs"/> // Occurences: 1; Operation: Exclude; Provenance: StringLiteral
        /// <C Include="b.cs"/> // NA
        /// <D Include="@(A)"/> // Occurences: 2; Operation: Include; Provenance: Inconclusive (it is an indirect occurence from a referenced item)
        /// <E Include="$(P)"/> // Occurences: 4; Operation: Include; Provenance: FromLiteral (direct reference in $P) | Glob (direct reference in $P) | Inconclusive (it is an indirect occurence from referenced properties and items)
        /// <PropertyGroup>
        ///     <P>a.cs;*.cs;@(A)</P>
        /// </PropertyGroup>
        /// </code>
        /// 
        /// </example>
        /// 
        /// <remarks>
        /// This method and its overloads are useful for clients that need to inspect all the item elements
        /// that might refer to a specific item instance. For example, Visual Studio uses it to inspect
        /// projects with globs. Upon a file system or IDE file artifact change, VS calls this method to find all the items
        /// that might refer to the detected file change (e.g. 'which item elements refer to "Program.cs"?').
        /// It uses such information to know which elements it should edit to reflect the user or file system changes.
        /// 
        /// Literal string matching tries to first match the strings. If the check fails, it then tries to match
        /// the strings as if they represented files: it normalizes both strings as files relative to the current project directory
        ///
        /// GetItemProvenance suffers from some sources of innacuracy:
        /// - it is performed after evaluation, thus is insensitive to item data flow when item references are present
        /// (it sees items as they are at the end of evaluation)
        /// 
        /// This API and its return types are prone to change.
        /// </remarks>
        /// 
        /// <param name="itemToMatch">The string to perform matching against</param>
        /// 
        /// <returns>
        /// A list of <see cref="ProvenanceResult"/>, sorted in project evaluation order.
        /// </returns>
        public List<ProvenanceResult> GetItemProvenance(string itemToMatch)
        {
            return GetItemProvenance(itemToMatch, _data.EvaluatedItemElements);
        }

        /// <summary>
        /// Overload of <see cref="GetItemProvenance(string)"/>
        /// </summary>
        /// <param name="itemToMatch">The string to perform matching against</param>
        /// <param name="itemType">The item type to constrain the search in</param>
        public List<ProvenanceResult> GetItemProvenance(string itemToMatch, string itemType)
        {
            return GetItemProvenance(itemToMatch, _data.EvaluatedItemElements.Where(i => i.ItemType.Equals(itemType)));
        }

        /// <summary>
        /// Overload of <see cref="GetItemProvenance(string)"/>
        /// </summary>
        /// <param name="item"> 
        /// The ProjectItem object that indicates: the itemspec to match and the item type to constrain the search in.
        /// The search is also constrained on item elements appearing before the item element that produced this <paramref name="item"/>.
        /// The element that produced this <paramref name="item"/> is included in the results.
        /// </param>
        public List<ProvenanceResult> GetItemProvenance(ProjectItem item)
        {
            var itemElementsAbove = _data.EvaluatedItemElements
                                            .Where(i => i.ItemType.Equals(item.ItemType))
                                            .TakeWhile(i => i != item.Xml)
                                            .ToList();
            itemElementsAbove.Add(item.Xml);

            return GetItemProvenance(item.EvaluatedInclude, itemElementsAbove);
        }


        private List<ProvenanceResult> GetItemProvenance(string itemToMatch, IEnumerable<ProjectItemElement> projectItemElements )
        {
            return
                projectItemElements.Select(i => ComputeProvenanceResult(itemToMatch, i))
                    .Where(r => r != null)
                    .ToList();
        }

        private ProvenanceResult ComputeProvenanceResult(string itemToMatch, ProjectItemElement itemElement)
        {
            var expander = new Expander<ProjectProperty, ProjectItem>(_data.Properties, _data.Items);
            Func<IElementLocation, Func<string, ExpanderOptions, string>> expandForXmlLocation = (l) => (s, o) => expander.ExpandIntoStringLeaveEscaped(s, o, l);

            var includeResult = ComputeProvenanceResult(itemToMatch, itemElement.Include, expandForXmlLocation(itemElement.IncludeLocation));

            if (includeResult == null)
            {
                return null;
            }

            var excludeResult = ComputeProvenanceResult(itemToMatch, itemElement.Exclude, expandForXmlLocation(itemElement.ExcludeLocation));

            return excludeResult != null
                ? new ProvenanceResult(itemElement, Operation.Exclude, excludeResult.Item1, excludeResult.Item2)
                : new ProvenanceResult(itemElement, Operation.Include, includeResult.Item1, includeResult.Item2);
        }

        private Tuple<Provenance, int> ComputeProvenanceResult(string itemToMatch, string itemSpecToLookIn, Func<string, ExpanderOptions, string> expand)
        {
            Provenance provenance;
            var matchOccurrences = ItemMatchesInSpecCompareViaExpander(itemToMatch, itemSpecToLookIn, expand, out provenance);

            return matchOccurrences > 0 ? Tuple.Create(provenance, matchOccurrences) : null;
        }

        /// <summary>
        /// Since:
        ///     - we have no proper AST and interpreter for itemspecs that we can do analysis on
        ///     - GetItemProvenance needs to have correct counts for exclude strings (as correct as it can get while doing it after evaluation)
        /// 
        /// The temporary hack is to use the expander to expand the strings, and if any property or item references were encountered, return Provenance.Inconclusive
        /// </summary>
        private int ItemMatchesInSpecCompareViaExpander(string itemToMatch, string itemSpec, Func<string, ExpanderOptions, string> expand, out Provenance provenance)
        {
            if (string.IsNullOrEmpty(itemSpec))
            {
                provenance = Provenance.Undefined;
                return 0;
            }

            // look into the itemspec as if it were expanded by the Expander
            Provenance provenanceFromExpandedPropertiesAndItems;
            var expandedMatches = ItemMatchesInSpec(itemToMatch, expand(itemSpec, ExpanderOptions.ExpandPropertiesAndItems), out provenanceFromExpandedPropertiesAndItems);

            // look into the raw itemspec
            Provenance provenanceFromNonExpandedString;
            var nonExpandedMatches = ItemMatchesInSpec(itemToMatch, itemSpec, out provenanceFromNonExpandedString);

            if (expandedMatches > nonExpandedMatches)
            {
                // return the number of occurences when properties AND items are expanded to get the correct occurence count

                // return the provenance WITHOUT item expansion. Otherwise the items coming from a referenced item get interpreted as StringLiteral
                // include="*.cs;@(Compile)" needs to return Inconclusive|Glob and not Inconclusive|Glob|StringLiteral
                Provenance provenanceFromExpandedProperties;
                ItemMatchesInSpec(itemToMatch, expand(itemSpec, ExpanderOptions.ExpandProperties), out provenanceFromExpandedProperties);

                provenance = Provenance.Inconclusive | provenanceFromExpandedProperties;
                return expandedMatches;
            }
            else
            {
                provenance = provenanceFromNonExpandedString;
                return nonExpandedMatches;
            }
        }

        private int ItemMatchesInSpec(string itemToMatch, string itemSpec, out Provenance provenance)
        {
            provenance = Provenance.Undefined;

            var occurrences = 0;

            if (string.IsNullOrEmpty(itemSpec))
            {
                return occurrences;
            }

            foreach (var itemFragment in ExpressionShredder.SplitSemiColonSeparatedList(itemSpec))
            {
                if (IsPropertyReferenceFragment(itemFragment) || IsItemReferenceFragment(itemFragment))
                {
                    continue;
                }

                if (IsGlobFragment(itemFragment) && ItemMatchesGlob(itemFragment, itemToMatch))
                {
                    provenance |= Provenance.Glob;
                    occurrences++;
                }

                if (ItemMatchesStringLiteral(itemToMatch, itemFragment))
                {
                    provenance |= Provenance.StringLiteral;
                    occurrences++;
                }
            }

            return occurrences;
        }

        private bool ItemMatchesStringLiteral(string itemToMatch, string itemFragment)
        {
            var thisProjectPath = _data.Directory;

            // It is either a direct string match or the two strings refer to the same file, relative to the project directory
            return itemToMatch.Equals(itemFragment) || FileUtilities.ComparePathsNoThrow(itemToMatch, itemFragment, thisProjectPath);
        }

        private static bool ItemMatchesGlob(string globPattern, string file)
        {
            var match = FileMatcher.FileMatch(globPattern, file);
            return match.isLegalFileSpec && match.isMatch;
        }

        private static bool IsGlobFragment(string itemFragment)
        {
            return FileMatcher.HasWildcards(itemFragment);
        }

        private static bool IsItemReferenceFragment(string itemFragment)
        {
            return itemFragment.Contains("@(");
        }

        private static bool IsPropertyReferenceFragment(string itemFragment)
        {
            return itemFragment.Contains("$(");
        }

        /// <summary>
        /// Gets the escaped value of the provided metadatum. 
        /// </summary>
        public static string GetMetadataValueEscaped(ProjectMetadata metadatum)
        {
            ErrorUtilities.VerifyThrowArgumentNull(metadatum, "metadatum");

            return metadatum.EvaluatedValueEscaped;
        }

        /// <summary>
        /// Gets the escaped value of the metadatum with the provided name on the provided item. 
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Justification = "IItem is an internal interface; this is less confusing to outside customers. ")]
        public static string GetMetadataValueEscaped(ProjectItem item, string name)
        {
            ErrorUtilities.VerifyThrowArgumentNull(item, "item");

            return ((IItem)item).GetMetadataValueEscaped(name);
        }

        /// <summary>
        /// Gets the escaped value of the metadatum with the provided name on the provided item definition. 
        /// </summary>
        public static string GetMetadataValueEscaped(ProjectItemDefinition item, string name)
        {
            ErrorUtilities.VerifyThrowArgumentNull(item, "item");

            return ((IItem)item).GetMetadataValueEscaped(name);
        }

        /// <summary>
        /// Get the escaped value of the provided property
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Justification = "IProperty is an internal interface; this is less confusing to outside customers. ")]
        public static string GetPropertyValueEscaped(ProjectProperty property)
        {
            ErrorUtilities.VerifyThrowArgumentNull(property, "property");

            return ((IProperty)property).EvaluatedValueEscaped;
        }

        /// <summary>
        /// Returns an iterator over the "logical project". The logical project is defined as
        /// the unevaluated project obtained from the single MSBuild file that is the result 
        /// of inlining the text of all imports of the original MSBuild project manifest file.
        /// </summary>
        public IEnumerable<ProjectElement> GetLogicalProject()
        {
            IEnumerable<ProjectElement> enumerable = GetLogicalProject(Xml.AllChildren);

            return enumerable;
        }

        /// <summary>
        /// Get any property in the project that has the specified name,
        /// otherwise returns null
        /// </summary>
        [DebuggerStepThrough]
        public ProjectProperty GetProperty(string name)
        {
            return _data.Properties[name];
        }

        /// <summary>
        /// Get the unescaped value of a property in this project, or 
        /// an empty string if it does not exist.
        /// </summary>
        /// <remarks>
        /// A property with a value of empty string and no property
        /// at all are not distinguished between by this method.
        /// That makes it easier to use. To find out if a property is set at
        /// all in the project, use GetProperty(name).
        /// </remarks>
        public string GetPropertyValue(string name)
        {
            return _data.GetPropertyValue(name);
        }

        /// <summary>
        /// Set or add a property with the specified name and value.
        /// Overwrites the value of any property with the same name already in the collection if it did not originate in an imported file.
        /// If there is no such existing property, uses this heuristic:
        /// Updates the last existing property with the specified name that has no condition on itself or its property group, if any,
        /// and is in this project file rather than an imported file.
        /// Otherwise, adds a new property in the first property group without a condition, creating a property group if necessary after
        /// the last existing property group, else at the start of the project.
        /// Returns the property set.
        /// Evaluates on a best-effort basis:
        ///     -expands with all properties. Properties that are defined in the XML below the new property may be used, even though in a real evaluation they would not be.
        ///     -only this property is evaluated. Anything else that would depend on its value is not affected.
        /// This is a convenience that it is understood does not necessarily leave the project in a perfectly self consistent state until reevaluation.
        /// </summary>
        public ProjectProperty SetProperty(string name, string unevaluatedValue)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, "name");
            ErrorUtilities.VerifyThrowArgumentNull(unevaluatedValue, "unevaluatedValue");

            ProjectProperty property = _data.Properties[name];

            ErrorUtilities.VerifyThrowInvalidOperation(property == null || !property.IsReservedProperty, "OM_ReservedName", name);
            ErrorUtilities.VerifyThrowInvalidOperation(property == null || !property.IsGlobalProperty, "OM_GlobalProperty", name);

            // If there's an existing regular property, we can reuse it, unless it's not attached to its XML any more
            if (property != null &&
                !property.IsEnvironmentProperty &&
                property.Xml.Parent != null &&
                property.Xml.Parent.Parent != null &&
                Object.ReferenceEquals(property.Xml.ContainingProject, _xml))
            {
                property.UnevaluatedValue = unevaluatedValue;
            }
            else
            {
                ProjectPropertyElement propertyElement = _xml.AddProperty(name, unevaluatedValue);

                property = ProjectProperty.Create(this, propertyElement, unevaluatedValue, null /* predecessor unknown */);

                _data.Properties[name] = property;
            }

            property.UpdateEvaluatedValue(ExpandPropertyValueBestEffortLeaveEscaped(unevaluatedValue, property.Xml.Location));

            return property;
        }

        /// <summary>
        /// Change a global property after the project has been evaluated.
        /// If the value changes, this makes the project require reevaluation.
        /// If the value changes, returns true, otherwise false.
        /// </summary>
        public bool SetGlobalProperty(string name, string escapedValue)
        {
            ProjectPropertyInstance existing = _data.GlobalPropertiesDictionary[name];

            if (existing == null || ((IProperty)existing).EvaluatedValueEscaped != escapedValue)
            {
                string originalValue = (existing == null) ? String.Empty : ((IProperty)existing).EvaluatedValueEscaped;

                _data.GlobalPropertiesDictionary.Set(ProjectPropertyInstance.Create(name, escapedValue));
                _data.Properties.Set(ProjectProperty.Create(this, name, escapedValue, true /* is global */, false /* may not be reserved name */));

                ProjectCollection.AfterUpdateLoadedProjectGlobalProperties(this);
                MarkDirty();

                if (s_debugEvaluation)
                {
                    string displayValue = escapedValue.Substring(0, Math.Min(escapedValue.Length, 75)) + ((escapedValue.Length > 75) ? "..." : String.Empty);
                    if (existing == null)
                    {
                        Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "MSBUILD: Initially set global property {0} to '{1}' [{2}]", name, displayValue, FullPath));
                    }
                    else
                    {
                        string displayOriginalValue = originalValue.Substring(0, Math.Min(originalValue.Length, 75)) + ((originalValue.Length > 75) ? "..." : String.Empty);
                        Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "MSBUILD: Changed global property {0} from '{1}' to '{2}' [{3}]", name, displayOriginalValue, displayValue, FullPath));
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds an item with no metadata to the project.
        /// Any metadata can be added subsequently.
        /// Does not modify the XML if a wildcard expression would already include the new item.
        /// Evaluates on a best-effort basis:
        ///     -expands with all items. Items that are defined in the XML below the new item may be used, even though in a real evaluation they would not be.
        ///     -only this item is evaluated. Other items that might depend on it is not affected.
        /// This is a convenience that it is understood does not necessarily leave the project in a perfectly self consistent state until reevaluation.
        /// </summary>
        public IList<ProjectItem> AddItem(string itemType, string unevaluatedInclude)
        {
            return AddItem(itemType, unevaluatedInclude, null);
        }

        /// <summary>
        /// Adds an item with metadata to the project.
        /// Metadata may be null, indicating no metadata.
        /// Does not modify the XML if a wildcard expression would already include the new item.
        /// Evaluates on a best-effort basis:
        ///     -expands with all items. Items that are defined in the XML below the new item may be used, even though in a real evaluation they would not be.
        ///     -only this item is evaluated. Other items that might depend on it is not affected.
        /// This is a convenience that it is understood does not necessarily leave the project in a perfectly self consistent state until reevaluation.
        /// </summary>
        public IList<ProjectItem> AddItem(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata)
        {
            // For perf reasons, this method does several jobs in one.
            // If it finds a suitable existing item element, it returns that as the out parameter, otherwise the out parameter returns null.
            // Otherwise, if it finds an item element suitable to be just below our new element, it returns that.
            // Otherwise, if it finds an item group at least that's suitable to put our element in somewhere, it returns that.
            // Otherwise, it returns null.
            ProjectItemElement itemElement;
            ProjectElement element = GetAnySuitableExistingItemXml(itemType, unevaluatedInclude, metadata, out itemElement);

            if (itemElement == null)
            {
                // Didn't find a suitable existing item; maybe the hunt gave us a hint as
                // to where to put a new one.
                ProjectItemElement itemElementToAddBefore = element as ProjectItemElement;

                if (itemElementToAddBefore != null)
                {
                    // It told us an item to add before
                    itemElement = _xml.CreateItemElement(itemType, unevaluatedInclude);
                    itemElementToAddBefore.Parent.InsertBeforeChild(itemElement, itemElementToAddBefore);
                }
                else
                {
                    ProjectItemGroupElement itemGroupElement = element as ProjectItemGroupElement;

                    if (itemGroupElement != null)
                    {
                        // It only told us an item group to add it somewhere within
                        itemElement = itemGroupElement.AddItem(itemType, unevaluatedInclude);
                    }
                    else
                    {
                        // It didn't give any hint at all
                        itemElement = _xml.AddItem(itemType, unevaluatedInclude);
                    }
                }
            }

            // Fix up the evaluated state to match
            return AddItemHelper(itemElement, unevaluatedInclude, metadata);
        }

        /// <summary>
        /// Adds an item with no metadata to the project.
        /// Makes no effort to see if an existing wildcard would already match the new item, unless it is the first item in an item group.
        /// Makes no effort to locate the new item near similar items.
        /// Appends the item to the first item group that does not have a condition and has either no children or whose first child is an item of the same type.
        /// Evaluates on a best-effort basis:
        ///     -expands with all items. Items that are defined in the XML below the new item may be used, even though in a real evaluation they would not be.
        ///     -only this item is evaluated. Other items that might depend on it is not affected.
        /// This is a convenience that it is understood does not necessarily leave the project in a perfectly self consistent state until reevaluation.
        /// </summary>
        public IList<ProjectItem> AddItemFast(string itemType, string unevaluatedInclude)
        {
            return AddItemFast(itemType, unevaluatedInclude, null);
        }

        /// <summary>
        /// Adds an item with metadata to the project.
        /// Metadata may be null, indicating no metadata.
        /// Makes no effort to see if an existing wildcard would already match the new item, unless it is the first item in an item group.
        /// Makes no effort to locate the new item near similar items.
        /// Appends the item to the first item group that does not have a condition and has either no children or whose first child is an item of the same type.
        /// Evaluates on a best-effort basis:
        ///     -expands with all items. Items that are defined in the XML below the new item may be used, even though in a real evaluation they would not be.
        ///     -only this item is evaluated. Other items that might depend on it is not affected.
        /// This is a convenience that it is understood does not necessarily leave the project in a perfectly self consistent state until reevaluation.
        /// </summary>
        public IList<ProjectItem> AddItemFast(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata)
        {
            ErrorUtilities.VerifyThrowArgumentLength(itemType, "itemType");
            ErrorUtilities.VerifyThrowArgumentLength(unevaluatedInclude, "unevalutedInclude");

            ProjectItemGroupElement groupToAppendTo = null;

            foreach (ProjectItemGroupElement group in _xml.ItemGroups)
            {
                if (group.Condition.Length > 0)
                {
                    continue;
                }

                if (group.Count == 0 || MSBuildNameIgnoreCaseComparer.Default.Equals(itemType, group.Items.First().ItemType))
                {
                    groupToAppendTo = group;

                    break;
                }
            }

            if (groupToAppendTo == null)
            {
                groupToAppendTo = _xml.AddItemGroup();
            }

            ProjectItemElement itemElement;

            if (groupToAppendTo.Count == 0 ||
                FileMatcher.HasWildcardsSemicolonItemOrPropertyReferences(unevaluatedInclude) ||
                !IsSuitableExistingItemXml(groupToAppendTo.Items.First(), unevaluatedInclude, metadata))
            {
                itemElement = _xml.CreateItemElement(itemType, unevaluatedInclude);
                groupToAppendTo.AppendChild(itemElement);
            }
            else
            {
                itemElement = groupToAppendTo.Items.First();
            }

            return AddItemHelper(itemElement, unevaluatedInclude, metadata);
        }

        /// <summary>
        /// All the items in the project of the specified
        /// type.
        /// If there are none, returns an empty list.
        /// Use AddItem or RemoveItem to modify items in this project.
        /// </summary>
        /// <comments>
        /// data.GetItems returns a read-only collection, so no need to re-wrap it here. 
        /// </comments>
        public ICollection<ProjectItem> GetItems(string itemType)
        {
            ICollection<ProjectItem> items = _data.GetItems(itemType);
            return items;
        }

        /// <summary>
        /// All the items in the project of the specified
        /// type, irrespective of whether the conditions on them evaluated to true.
        /// This is a read-only list: use AddItem or RemoveItem to modify items in this project.
        /// </summary>
        /// <comments>
        /// ItemDictionary[] returns a read only collection, so no need to wrap it. 
        /// </comments>
        public ICollection<ProjectItem> GetItemsIgnoringCondition(string itemType)
        {
            ICollection<ProjectItem> items = _data.ItemsIgnoringCondition[itemType];
            return items;
        }

        /// <summary>
        /// Returns all items that have the specified evaluated include.
        /// For example, all items that have the evaluated include "bar.cpp".
        /// Typically there will be zero or one, but sometimes there are two items with the
        /// same path and different item types, or even the same item types. This will return
        /// them all.
        /// </summary>
        /// <comments>
        /// data.GetItemsByEvaluatedInclude already returns a read-only collection, so no need
        /// to wrap it further.
        /// </comments>
        public ICollection<ProjectItem> GetItemsByEvaluatedInclude(string evaluatedInclude)
        {
            ICollection<ProjectItem> items = _data.GetItemsByEvaluatedInclude(evaluatedInclude);
            return items;
        }

        /// <summary>
        /// Removes the specified property.
        /// Property must be associated with this project.
        /// Property must not originate from an imported file.
        /// Returns true if the property was in this evaluated project, otherwise false.
        /// As a convenience, if the parent property group becomes empty, it is also removed.
        /// Updates the evaluated project, but does not affect anything else in the project until reevaluation. For example,
        /// if "p" is removed, it will be removed from the evaluated project, but "q" which is evaluated from "$(p)" will not be modified until reevaluation.
        /// This is a convenience that it is understood does not necessarily leave the project in a perfectly self consistent state.
        /// </summary>
        public bool RemoveProperty(ProjectProperty property)
        {
            ErrorUtilities.VerifyThrowArgumentNull(property, "property");
            ErrorUtilities.VerifyThrowInvalidOperation(!property.IsReservedProperty, "OM_ReservedName", property.Name);
            ErrorUtilities.VerifyThrowInvalidOperation(!property.IsGlobalProperty, "OM_GlobalProperty", property.Name);
            ErrorUtilities.VerifyThrowArgument(property.Xml.Parent != null, "OM_IncorrectObjectAssociation", "ProjectProperty", "Project");
            VerifyThrowInvalidOperationNotImported(property.Xml.ContainingProject);

            ProjectElementContainer parent = property.Xml.Parent;

            property.Xml.Parent.RemoveChild(property.Xml);

            if (parent.Count == 0)
            {
                parent.Parent.RemoveChild(parent);
            }

            bool result = _data.Properties.Remove(property.Name);

            return result;
        }

        /// <summary>
        /// Removes a global property.
        /// If it was set, returns true, and marks the project
        /// as requiring reevaluation.
        /// </summary>
        public bool RemoveGlobalProperty(string name)
        {
            ErrorUtilities.VerifyThrowArgumentLength(name, "name");

            bool result = _data.GlobalPropertiesDictionary.Remove(name);

            if (result)
            {
                ProjectCollection.AfterUpdateLoadedProjectGlobalProperties(this);
                MarkDirty();

                if (s_debugEvaluation)
                {
                    Trace.WriteLine(String.Format(CultureInfo.InvariantCulture, "MSBUILD:  Remove global property {0}", name));
                }
            }

            return result;
        }

        /// <summary>
        /// Removes an item from the project.
        /// Item must be associated with this project.
        /// Item must not originate from an imported file.
        /// Returns true if the item was in this evaluated project, otherwise false.
        /// As a convenience, if the parent item group becomes empty, it is also removed.
        /// If the item originated from a wildcard or semicolon separated expression, expands that expression into multiple items first.
        /// Updates the evaluated project, but does not affect anything else in the project until reevaluation. For example,
        /// if an item of type "i" is removed, "j" which is evaluated from "@(i)" will not be modified until reevaluation.
        /// This is a convenience that it is understood does not necessarily leave the project in a perfectly self consistent state until reevaluation.
        /// </summary>
        /// <remarks>
        /// Normally this will return true, since if the item isn't in the project, it will throw.
        /// The exception is removing an item that was only in ItemsIgnoringCondition.
        /// </remarks>
        public bool RemoveItem(ProjectItem item)
        {
            ErrorUtilities.VerifyThrowArgumentNull(item, "item");
            ErrorUtilities.VerifyThrowArgument(item.Project == this, "OM_IncorrectObjectAssociation", "ProjectItem", "Project");

            bool result = RemoveItemHelper(item);

            return result;
        }

        /// <summary>
        /// Removes all the specified items from the project.
        /// Items that are not associated with this project are skipped.
        /// </summary>
        /// <remarks>
        /// Removing one item could cause the backing XML
        /// to be expanded, which could zombie (disassociate) the next item.
        /// To make this case easy for the caller, if an item
        /// is not associated with this project it is simply skipped.
        /// </remarks>
        public void RemoveItems(IEnumerable<ProjectItem> items)
        {
            ErrorUtilities.VerifyThrowArgumentNull(items, "items");

            // Copying to a list makes it possible to remove
            // all items of a particular type with 
            //   RemoveItems(p.GetItems("mytype"))
            // without modifying the collection during enumeration.
            List<ProjectItem> itemsList = new List<ProjectItem>(items);

            foreach (ProjectItem item in itemsList)
            {
                RemoveItemHelper(item);
            }
        }

        /// <summary>
        /// Evaluates the provided string by expanding items and properties,
        /// as if it was found at the very end of the project file.
        /// This is useful for some hosts for which this kind of best-effort
        /// evaluation is sufficient.
        /// Does not expand bare metadata expressions.
        /// </summary>
        public string ExpandString(string unexpandedValue)
        {
            ErrorUtilities.VerifyThrowArgumentNull(unexpandedValue, "unexpandedValue");

            string result = _data.Expander.ExpandIntoStringAndUnescape(unexpandedValue, ExpanderOptions.ExpandPropertiesAndItems, ProjectFileLocation);

            return result;
        }

        /// <summary>
        /// Returns an instance based on this project, but completely disconnected.
        /// This instance can be used to build independently.
        /// Before creating the instance, this will reevaluate the project if necessary, so it will not be dirty.
        /// </summary>
        public ProjectInstance CreateProjectInstance()
        {
            return CreateProjectInstance(LoggingService, ProjectInstanceSettings.None);
        }

        /// <summary>
        /// Returns an instance based on this project, but completely disconnected.
        /// This instance can be used to build independently.
        /// Before creating the instance, this will reevaluate the project if necessary, so it will not be dirty.
        /// The instance is immutable; none of the objects that form it can be modified. This makes it safe to 
        /// access concurrently from multiple threads.
        /// </summary>
        public ProjectInstance CreateProjectInstance(ProjectInstanceSettings settings)
        {
            return CreateProjectInstance(LoggingService, settings);
        }

        /// <summary>
        /// Called to forcibly mark the project as dirty requiring reevaluation. Generally this is not necessary to set; all edits affecting
        /// this project will automatically make it dirty. However there are potential corner cases where it is necessary to mark the project dirty
        /// directly. For example, if the project has an import conditioned on a file existing on disk, and the file did not exist at
        /// evaluation time, then someone subsequently creates that file, the project cannot know that reevaluation would be productive.
        /// In such a case the host can help us by setting the dirty flag explicitly so that <see cref="ReevaluateIfNecessary()">ReevaluateIfNecessary()</see>
        /// will recognize an evaluation is indeed necessary.
        /// Does not mark the underlying project file as requiring saving.
        /// </summary>
        public void MarkDirty()
        {
            if (!DisableMarkDirty && !_projectCollection.DisableMarkDirty)
            {
                _explicitlyMarkedDirty = true;
            }

            // Pass up the MarkDirty call even when DisableMarkDirty is true.
            _xml.MarkProjectDirty(this);
        }

        /// <summary>
        /// Reevaluate the project to get it into a queryable state, if it's dirty.
        /// This incorporates all changes previously made to the backing XML by editing this project.
        /// Throws InvalidProjectFileException if the evaluation fails.
        /// </summary>
        public void ReevaluateIfNecessary()
        {
            ReevaluateIfNecessary(LoggingService);
        }

        /// <summary>
        /// Save the project to the file system, if dirty.
        /// Uses the default encoding.
        /// </summary>
        public void Save()
        {
            Xml.Save();
        }

        /// <summary>
        /// Save the project to the file system, if dirty.
        /// </summary>
        public void Save(Encoding encoding)
        {
            Xml.Save(encoding);
        }

        /// <summary>
        /// Save the project to the file system, if dirty or the path is different.
        /// Uses the default encoding.
        /// </summary>
        public void Save(string path)
        {
            Xml.Save(path);
        }

        /// <summary>
        /// Save the project to the file system, if dirty or the path is different.
        /// </summary>
        public void Save(string path, Encoding encoding)
        {
            Xml.Save(path, encoding);
        }

        /// <summary>
        /// Save the project to the provided TextWriter, whether or not it is dirty.
        /// Uses the encoding of the TextWriter.
        /// Clears the Dirty flag.
        /// </summary>
        public void Save(TextWriter writer)
        {
            Xml.Save(writer);
        }

        /// <summary>
        /// Saves a "logical" or "preprocessed" project file, that includes all the imported 
        /// files as if they formed a single file.
        /// </summary>
        public void SaveLogicalProject(TextWriter writer)
        {
            XmlDocument document = Preprocessor.GetPreprocessedDocument(this);

            using (ProjectWriter projectWriter = new ProjectWriter(writer))
            {
                projectWriter.Initialize(document);
                document.Save(projectWriter);
            }
        }

        /// <summary>
        /// Starts a build using this project, building the default targets.
        /// Returns true on success, false on failure.
        /// Works on a privately cloned instance. To set or get
        /// virtual items for build purposes, clone an instance explicitly and build that.
        /// Does not modify the Project object.
        /// </summary>
        public bool Build()
        {
            return Build((string[])null);
        }

        /// <summary>
        /// Starts a build using this project, building the default targets and the specified logger.
        /// Returns true on success, false on failure.
        /// Works on a privately cloned instance. To set or get
        /// virtual items for build purposes, clone an instance explicitly and build that.
        /// Does not modify the Project object.
        /// </summary>
        public bool Build(ILogger logger)
        {
            List<ILogger> loggers = new List<ILogger>(1);
            loggers.Add(logger);
            return Build((string[])null, loggers, null);
        }

        /// <summary>
        /// Starts a build using this project, building the default targets and the specified loggers.
        /// Returns true on success, false on failure.
        /// Works on a privately cloned instance. To set or get
        /// virtual items for build purposes, clone an instance explicitly and build that.
        /// Does not modify the Project object.
        /// </summary>
        public bool Build(IEnumerable<ILogger> loggers)
        {
            return Build((string[])null, loggers, null);
        }

        /// <summary>
        /// Starts a build using this project, building the default targets and the specified loggers.
        /// Returns true on success, false on failure.
        /// Works on a privately cloned instance. To set or get
        /// virtual items for build purposes, clone an instance explicitly and build that.
        /// Does not modify the Project object.
        /// </summary>
        public bool Build(IEnumerable<ILogger> loggers, IEnumerable<ForwardingLoggerRecord> remoteLoggers)
        {
            return Build((string[])null, loggers, remoteLoggers);
        }

        /// <summary>
        /// Starts a build using this project, building the specified target.
        /// Returns true on success, false on failure.
        /// Works on a privately cloned instance. To set or get
        /// virtual items for build purposes, clone an instance explicitly and build that.
        /// Does not modify the Project object.
        /// </summary>
        public bool Build(string target)
        {
            return Build(target, null, null);
        }

        /// <summary>
        /// Starts a build using this project, building the specified target with the specified loggers.
        /// Returns true on success, false on failure.
        /// Works on a privately cloned instance. To set or get
        /// virtual items for build purposes, clone an instance explicitly and build that.
        /// Does not modify the Project object.
        /// </summary>
        public bool Build(string target, IEnumerable<ILogger> loggers)
        {
            return Build(target, loggers, null);
        }

        /// <summary>
        /// Starts a build using this project, building the specified target with the specified loggers.
        /// Returns true on success, false on failure.
        /// Works on a privately cloned instance. To set or get
        /// virtual items for build purposes, clone an instance explicitly and build that.
        /// Does not modify the Project object.
        /// </summary>
        public bool Build(string target, IEnumerable<ILogger> loggers, IEnumerable<ForwardingLoggerRecord> remoteLoggers)
        {
            // targets may be null, but not an entry within it
            string[] targets = (target == null) ? null : new string[] { target };

            return Build(targets, loggers, remoteLoggers);
        }

        /// <summary>
        /// Starts a build using this project, building the specified targets.
        /// Returns true on success, false on failure.
        /// Works on a privately cloned instance. To set or get
        /// virtual items for build purposes, clone an instance explicitly and build that.
        /// Does not modify the Project object.
        /// </summary>
        public bool Build(string[] targets)
        {
            return Build(targets, null, null);
        }

        /// <summary>
        /// Starts a build using this project, building the specified targets with the specified loggers.
        /// Returns true on success, false on failure.
        /// If build is disabled on this project, does not build, and returns false.
        /// Works on a privately cloned instance. To set or get
        /// virtual items for build purposes, clone an instance explicitly and build that.
        /// Does not modify the Project object.
        /// </summary>
        public bool Build(string[] targets, IEnumerable<ILogger> loggers)
        {
            return Build(targets, loggers, null);
        }

        /// <summary>
        /// Starts a build using this project, building the specified targets with the specified loggers.
        /// Returns true on success, false on failure.
        /// If build is disabled on this project, does not build, and returns false.
        /// Works on a privately cloned instance. To set or get
        /// virtual items for build purposes, clone an instance explicitly and build that.
        /// Does not modify the Project object.
        /// </summary>
        public bool Build(string[] targets, IEnumerable<ILogger> loggers, IEnumerable<ForwardingLoggerRecord> remoteLoggers)
        {
            bool result = false;

            if (!IsBuildEnabled)
            {
                LoggingService.LogError(s_buildEventContext, new BuildEventFileInfo(FullPath), "SecurityProjectBuildDisabled");
                return false;
            }

            ProjectInstance instance = CreateProjectInstance(LoggingService, ProjectInstanceSettings.None);
            IDictionary<string, TargetResult> targetOutputs;

            if (loggers == null && ProjectCollection.Loggers != null)
            {
                loggers = ProjectCollection.Loggers;
            }

            result = instance.Build(targets, loggers, remoteLoggers, null, ProjectCollection.MaxNodeCount, out targetOutputs);

            return result;
        }

        /// <summary>
        /// Tests whether a given project IS or IMPORTS some given project xml root element.
        /// </summary>
        /// <param name="xmlRootElement">The project xml root element in question.</param>
        /// <returns>True if this project is or imports the xml file; false otherwise.</returns>
        internal bool UsesProjectRootElement(ProjectRootElement xmlRootElement)
        {
            if (Object.ReferenceEquals(this.Xml, xmlRootElement))
            {
                return true;
            }

            if (_data.ImportClosure.Any(triple => Object.ReferenceEquals(triple.Second, xmlRootElement)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// If the ProjectItemElement evaluated to more than one ProjectItem, replaces it with a new ProjectItemElement for each one of them.
        /// If the ProjectItemElement did not evaluate into more than one ProjectItem, does nothing.
        /// Returns true if a split occurred, otherwise false.
        /// </summary>
        /// <remarks>
        /// A ProjectItemElement could have resulted in several items if it contains wildcards or item or property expressions.
        /// Before any edit to a ProjectItem (remove, rename, set metadata, or remove metadata) this must be called to make
        /// sure that the edit does not affect any other ProjectItems originating in the same ProjectItemElement.
        /// 
        /// For example, an item xml with an include of "@(x)" could evaluate to items "a", "b", and "c". If "b" is removed, then the original
        /// item xml must be removed and replaced with three, then the one corresponding to "b" can be removed.
        /// 
        /// This is an unsophisticated approach; the best that can be said is that the result will likely be correct, if not ideal.
        /// For example, perhaps the user would rather remove the item from the original list "x" instead of expanding the list.
        /// Or, perhaps the user would rather the property in "$(p)\a;$(p)\b" not be expanded when "$(p)\b" is removed.
        /// If that's important, the host can manipulate the ProjectItemElement's directly, instead, and it can be as fastidious as it wishes.
        /// </remarks>
        internal bool SplitItemElementIfNecessary(ProjectItemElement itemElement)
        {
            if (!FileMatcher.HasWildcardsSemicolonItemOrPropertyReferences(itemElement.Include))
            {
                return false;
            }

            List<ProjectItem> relevantItems = new List<ProjectItem>();

            foreach (ProjectItem item in Items)
            {
                if (item.Xml == itemElement)
                {
                    relevantItems.Add(item);
                }
            }

            if (relevantItems.Count <= 1)
            {
                return false;
            }

            foreach (ProjectItem item in relevantItems)
            {
                item.SplitOwnItemElement();
            }

            itemElement.Parent.RemoveChild(itemElement);

            return true;
        }

        /// <summary>
        /// Examines the provided ProjectItemElement to see if it has a wildcard that would match the 
        /// item we wish to add, and does not have a condition or an exclude.
        /// Works conservatively - if there is anything that might cause doubt, considers the candidate to not be suitable.
        /// Returns true if it is suitable, otherwise false.
        /// </summary>
        /// <remarks>
        /// Outside this class called ONLY from <see cref="ProjectItem.Rename(string)"/>ProjectItem.Rename(string name).
        /// </remarks>
        internal bool IsSuitableExistingItemXml(ProjectItemElement candidateExistingItemXml, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata)
        {
            if (candidateExistingItemXml.Condition.Length != 0 || candidateExistingItemXml.Exclude.Length != 0 || !candidateExistingItemXml.IncludeHasWildcards)
            {
                return false;
            }

            if ((metadata != null && metadata.Any()) || candidateExistingItemXml.Count > 0)
            {
                // Don't try to make sure the metadata are the same.
                return false;
            }

            string evaluatedExistingInclude = _data.Expander.ExpandIntoStringLeaveEscaped(candidateExistingItemXml.Include, ExpanderOptions.ExpandProperties, candidateExistingItemXml.IncludeLocation);

            string[] existingIncludePieces = evaluatedExistingInclude.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string existingIncludePiece in existingIncludePieces)
            {
                if (!FileMatcher.HasWildcards(existingIncludePiece))
                {
                    continue;
                }

                FileMatcher.Result match = FileMatcher.FileMatch(existingIncludePiece, unevaluatedInclude);

                if (match.isLegalFileSpec && match.isMatch)
                {
                    // The wildcard in the original item spec will match the new item that
                    // user is trying to add.
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Before an item changes its item type, it must be removed from
        /// our datastructures, which key off item type. 
        /// This should be called ONLY by ProjectItems, in this situation.
        /// </summary>
        internal void RemoveItemBeforeItemTypeChange(ProjectItem item)
        {
            _data.RemoveItem(item);
        }

        /// <summary>
        /// After an item has changed its item type, it needs to be added back again,
        /// since our data structures key off the item type.
        /// This should be called ONLY by ProjectItems, in this situation.
        /// </summary>
        internal void ReAddExistingItemAfterItemTypeChange(ProjectItem item)
        {
            _data.AddItem(item);
            _data.AddItemIgnoringCondition(item);
        }

        /// <summary>
        /// Provided a property that is already part of this project, does a best-effort expansion
        /// of the unevaluated value provided and sets it as the evaluated value.
        /// </summary>
        /// <remarks>
        /// On project in order to keep Project's expander hidden.
        /// </remarks>
        internal string ExpandPropertyValueBestEffortLeaveEscaped(string unevaluatedValue, ElementLocation propertyLocation)
        {
            string evaluatedValueEscaped = _data.Expander.ExpandIntoStringLeaveEscaped(unevaluatedValue, ExpanderOptions.ExpandProperties, propertyLocation);

            return evaluatedValueEscaped;
        }

        /// <summary>
        /// Provided an item element that has been renamed with a new unevaluated include,
        /// returns a best effort guess at the evaluated include that results.
        /// If the best effort expansion produces anything other than one item, it just
        /// returns the unevaluated include.
        /// This is not at all generalized, but useful for the majority case where an item is a very
        /// simple file name with perhaps a property prefix.
        /// </summary>
        /// <remarks>
        /// On project in order to keep Project's expander hidden.
        /// </remarks>
        internal string ExpandItemIncludeBestEffortLeaveEscaped(ProjectItemElement renamedItemElement)
        {
            if (renamedItemElement.Exclude.Length > 0)
            {
                return renamedItemElement.Include;
            }

            ProjectItemFactory itemFactory = new ProjectItemFactory(this, renamedItemElement);

            List<ProjectItem> items = Evaluator<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition>.CreateItemsFromInclude(DirectoryPath, renamedItemElement, itemFactory, renamedItemElement.Include, _data.Expander);

            if (items.Count != 1)
            {
                return renamedItemElement.Include;
            }

            return ((IItem)items[0]).EvaluatedIncludeEscaped;
        }

        /// <summary>
        /// Provided a metadatum that is already part of this project, does a best-effort expansion
        /// of the unevaluated value provided and returns the resulting value.
        /// This is a interim expansion only: it may not be the value that a full project reevaluation would produce.
        /// The metadata table passed in is that of the parent item or item definition.
        /// </summary>
        /// <remarks>
        /// On project in order to keep Project's expander hidden.
        /// </remarks>
        internal string ExpandMetadataValueBestEffortLeaveEscaped(IMetadataTable metadataTable, string unevaluatedValue, ElementLocation metadataLocation)
        {
            ErrorUtilities.VerifyThrow(_data.Expander.Metadata == null, "Should be null");

            _data.Expander.Metadata = metadataTable;
            string evaluatedValueEscaped = _data.Expander.ExpandIntoStringLeaveEscaped(unevaluatedValue, ExpanderOptions.ExpandAll, metadataLocation);
            _data.Expander.Metadata = null;

            return evaluatedValueEscaped;
        }

        /// <summary>
        /// Called by the project collection to indicate to this project that it is no longer loaded.
        /// </summary>
        internal void Zombify()
        {
            _xml.OnAfterProjectRename -= _renameHandler;
            _xml.OnProjectXmlChanged -= ProjectRootElement_ProjectXmlChangedHandler;
            _xml.XmlDocument.ClearAnyCachedStrings();
            _renameHandler = null;
        }

        /// <summary>
        /// Verify that the project has not been unloaded from its collection.
        /// Once it's been unloaded, it cannot be used.
        /// </summary>
        internal void VerifyThrowInvalidOperationNotZombie()
        {
            ErrorUtilities.VerifyThrow(_renameHandler != null, "OM_ProjectIsNoLongerActive");
        }

        /// <summary>
        /// Verify that the provided object location is in the same file as the project.
        /// If it is not, throws an InvalidOperationException indicating that imported evaluated objects should not be modified.
        /// This prevents, for example, accidentally updating something like the OutputPath property, that you want be in the 
        /// main project, but for some reason was actually read in from an imported targets file.
        /// </summary>
        internal void VerifyThrowInvalidOperationNotImported(ProjectRootElement otherXml)
        {
            ErrorUtilities.VerifyThrowInternalNull(otherXml, "otherXml");
            ErrorUtilities.VerifyThrowInvalidOperation(Object.ReferenceEquals(Xml, otherXml), "OM_CannotModifyEvaluatedObjectInImportedFile", otherXml.Location.File);
        }

        /// <summary>
        /// Get the next global evaluation counter number
        /// in a thread safe fashion.
        /// </summary>
        private static int GetNextEvaluationCounter()
        {
            // We build without /checked, so this 
            // will wrap, which is fine as it's incredibly unlikely
            return Interlocked.Increment(ref s_globalEvaluationCounter);
        }

        /// <summary>
        /// Common code for the AddItem methods.
        /// </summary>
        private List<ProjectItem> AddItemHelper(ProjectItemElement itemElement, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata)
        {
            ProjectItemFactory itemFactory = new ProjectItemFactory(this, itemElement);

            List<ProjectItem> items = Evaluator<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition>.CreateItemsFromInclude(DirectoryPath, itemElement, itemFactory, unevaluatedInclude, _data.Expander);

            foreach (ProjectItem item in items)
            {
                _data.AddItem(item);
                _data.AddItemIgnoringCondition(item);
            }

            if (metadata != null)
            {
                foreach (ProjectItem item in items)
                {
                    foreach (KeyValuePair<string, string> metadatum in metadata)
                    {
                        item.SetMetadataValue(metadatum.Key, metadatum.Value);
                    }
                }
            }

            // The old OM attempted to evaluate and return the resulting item, or if several then whatever was the "first" returned.
            // This was rather arbitrary, and made it impossible for the caller to retrieve the whole set.
            return items;
        }

        /// <summary>
        /// Helper for <see cref="RemoveItem"/> and <see cref="RemoveItems"/>.
        /// If the item is not associated with a project, returns false.
        /// If the item is not present in the evaluated project, returns false.
        /// If the item is associated with another project, throws ArgumentException.
        /// Otherwise removes the item and returns true.
        /// </summary>
        private bool RemoveItemHelper(ProjectItem item)
        {
            ErrorUtilities.VerifyThrowArgumentNull(item, "item");

            if (item.Project == null || item.Xml.Parent == null)
            {
                // Return rather than throwing: this is to make it easier
                // to enumerate over a list of items to remove.
                return false;
            }

            ErrorUtilities.VerifyThrowArgument(item.Project == this, "OM_IncorrectObjectAssociation", "ProjectItem", "Project");

            VerifyThrowInvalidOperationNotImported(item.Xml.ContainingProject);

            SplitItemElementIfNecessary(item.Xml);

            ProjectElementContainer parent = item.Xml.Parent;

            item.Xml.Parent.RemoveChild(item.Xml);

            if (parent.Count == 0)
            {
                parent.Parent.RemoveChild(parent);
            }

            bool result = _data.RemoveItem(item);

            return result;
        }

        /// <summary>
        /// Creates a project instance based on this project using the specified logging service.
        /// </summary>  
        private ProjectInstance CreateProjectInstance(ILoggingService loggingServiceForEvaluation, ProjectInstanceSettings settings)
        {
            ReevaluateIfNecessary(loggingServiceForEvaluation);

            return new ProjectInstance(_data, DirectoryPath, FullPath, ProjectCollection.HostServices, _projectCollection.EnvironmentProperties, settings);
        }

        /// <summary>
        /// Re-evaluates the project using the specified logging service.
        /// </summary>
        private void ReevaluateIfNecessary(ILoggingService loggingServiceForEvaluation)
        {
            // We will skip the evaluation if the flag is set. This will give us better performance on scenarios
            // that we know we don't have to reevaluate. One example is project conversion bulk addfiles and set attributes. 
            if (!SkipEvaluation && !_projectCollection.SkipEvaluation && IsDirty)
            {
                try
                {
                    Evaluator<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition>.Evaluate(_data, _xml, _loadSettings, ProjectCollection.MaxNodeCount, ProjectCollection.EnvironmentProperties, loggingServiceForEvaluation, new ProjectItemFactory(this), _projectCollection as IToolsetProvider, _projectCollection.ProjectRootElementCache, s_buildEventContext, null /* no project instance for debugging */);

                    // We have to do this after evaluation, because evaluation might have changed
                    // the imports being pulled in.
                    int highestXmlVersion = Xml.Version;

                    if (_data.ImportClosure != null)
                    {
                        foreach (Triple<ProjectImportElement, ProjectRootElement, int> triple in _data.ImportClosure)
                        {
                            highestXmlVersion = (highestXmlVersion < triple.Third) ? triple.Third : highestXmlVersion;
                        }
                    }

                    _explicitlyMarkedDirty = false;
                    _evaluatedVersion = highestXmlVersion;
                    _evaluatedToolsetCollectionVersion = ProjectCollection.ToolsetsVersion;
                    _evaluationCounter = GetNextEvaluationCounter();
                    _data.HasUnsavedChanges = false;

                    ErrorUtilities.VerifyThrow(!IsDirty, "Should not be dirty now");
                }
                catch (InvalidProjectFileException ex)
                {
                    loggingServiceForEvaluation.LogInvalidProjectFileError(s_buildEventContext, ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Common code for the constructors.
        /// Applies global properties that are on the collection.
        /// Global properties provided for the project overwrite any global properties from the collection that have the same name.
        /// Global properties may be null.
        /// Tools version may be null.
        /// </summary>
        private void Initialize(IDictionary<string, string> globalProperties, string toolsVersion, string subToolsetVersion, ProjectLoadSettings loadSettings)
        {
            _xml.MarkAsExplicitlyLoaded();

            _evaluationCounter = GetNextEvaluationCounter();

            PropertyDictionary<ProjectPropertyInstance> globalPropertiesCollection = new PropertyDictionary<ProjectPropertyInstance>();

            foreach (ProjectPropertyInstance property in ProjectCollection.GlobalPropertiesCollection)
            {
                ProjectPropertyInstance clone = property.DeepClone();
                globalPropertiesCollection.Set(clone);
            }

            if (globalProperties != null)
            {
                foreach (KeyValuePair<string, string> pair in globalProperties)
                {
                    if (String.Equals(pair.Key, Constants.SubToolsetVersionPropertyName, StringComparison.OrdinalIgnoreCase) && subToolsetVersion != null)
                    {
                        // if we have a sub-toolset version explicitly provided by the ProjectInstance constructor, AND a sub-toolset version provided as a global property, 
                        // make sure that the one passed in with the constructor wins.  If there isn't a matching global property, the sub-toolset version will be set at 
                        // a later point. 
                        globalPropertiesCollection.Set(ProjectPropertyInstance.Create(pair.Key, subToolsetVersion));
                    }
                    else
                    {
                        globalPropertiesCollection.Set(ProjectPropertyInstance.Create(pair.Key, pair.Value));
                    }
                }
            }

            _data = new Data(this, globalPropertiesCollection, toolsVersion, subToolsetVersion);

            _loadSettings = loadSettings;

            ReevaluateIfNecessary();

            // Cause the project to be actually loaded into the collection, and register for
            // rename notifications so we can subsequently update the collection.
            _renameHandler = new RenameHandlerDelegate(delegate (string oldFullPath)
                {
                    _projectCollection.OnAfterRenameLoadedProject(oldFullPath, this);
                });

            _xml.OnAfterProjectRename += _renameHandler;
            _xml.OnProjectXmlChanged += ProjectRootElement_ProjectXmlChangedHandler;

            _renameHandler(null /* not previously named */);
        }

        /// <summary>
        /// Raised when any XML in the underlying ProjectRootElement has changed.
        /// </summary>
        private void ProjectRootElement_ProjectXmlChangedHandler(object sender, ProjectXmlChangedEventArgs args)
        {
            _xml.MarkProjectDirty(this);
        }

        /// <summary>
        /// Tries to find a ProjectItemElement already in the project file XML that has a wildcard that would match the
        /// item we wish to add, does not have a condition or an exclude, and is within an itemgroup without a condition.
        /// 
        /// For perf reasons, this method does several jobs in one.
        /// If it finds a suitable existing item element, it returns that as the out parameter, otherwise the out parameter returns null.
        /// Otherwise, if it finds an item element suitable to be just below our new element, it returns that.
        /// Otherwise, if it finds an item group at least that's suitable to put our element in somewhere, it returns that.
        /// 
        /// Returns null if the include of the item being added itself has wildcards, or semicolons, as the case is too difficult.
        /// </summary>
        private ProjectElement GetAnySuitableExistingItemXml(string itemType, string unevaluatedInclude, IEnumerable<KeyValuePair<string, string>> metadata, out ProjectItemElement suitableExistingItemXml)
        {
            suitableExistingItemXml = null;

            if (FileMatcher.HasWildcardsSemicolonItemOrPropertyReferences(unevaluatedInclude))
            {
                return null;
            }

            if (metadata != null && metadata.Any())
            {
                // Don't bother trying to match up metadata
                return null;
            }

            // In case we don't find a suitable existing item xml, at least find
            // a good item group to add to. Either the first item group with at least one
            // item of the same type, or else the first empty item group without a condition.
            ProjectItemGroupElement itemGroupToAddTo = null;

            ProjectItemElement itemToAddBefore = null;

            foreach (ProjectItemGroupElement itemGroupXml in _xml.ItemGroups)
            {
                if (itemGroupXml.Condition.Length > 0)
                {
                    continue;
                }

                if (itemGroupXml.DefinitelyAreNoChildrenWithWildcards)
                {
                    continue;
                }

                if (itemGroupToAddTo == null && itemGroupXml.Count == 0)
                {
                    itemGroupToAddTo = itemGroupXml;
                }

                foreach (ProjectItemElement existingItemXml in itemGroupXml.Items)
                {
                    if (!MSBuildNameIgnoreCaseComparer.Default.Equals(itemType, existingItemXml.ItemType))
                    {
                        continue;
                    }

                    if (itemGroupToAddTo == null || itemGroupToAddTo.Count == 0)
                    {
                        itemGroupToAddTo = itemGroupXml;
                    }

                    // if the include sorts after us, store this item, so we can add
                    // right after it if need be. For example if the item is "b.cs" and we are planning to add "a.cs"
                    // then we know that we will want to add it just above this item. We can avoid another scan to figure that out.
                    if (itemToAddBefore == null && String.Compare(unevaluatedInclude, existingItemXml.Include, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        itemToAddBefore = existingItemXml;
                    }

                    if (IsSuitableExistingItemXml(existingItemXml, unevaluatedInclude, metadata))
                    {
                        suitableExistingItemXml = existingItemXml;
                        return null;
                    }
                }
            }

            if (itemToAddBefore == null)
            {
                return itemGroupToAddTo;
            }

            return itemToAddBefore;
        }

        /// <summary>
        /// Recursive helper for <see cref="GetLogicalProject()">GetLogicalProject</see>.
        /// </summary>
        private IEnumerable<ProjectElement> GetLogicalProject(IEnumerable<ProjectElement> projectElements)
        {
            foreach (ProjectElement element in projectElements)
            {
                ProjectImportElement import = element as ProjectImportElement;

                if (import == null)
                {
                    yield return element;
                }
                else
                {
                    // Get the project root elements of all the imports resulting from this import statement (there could be multiple if there is a wild card).
                    IEnumerable<ProjectRootElement> children = _data.ImportClosure.Where(triple => Object.ReferenceEquals(triple.First, import)).Select(triple => triple.Second);

                    foreach (ProjectRootElement child in children)
                    {
                        if (child != null)
                        {
                            // The import's condition must have evaluated to true, to traverse into it
                            IEnumerable<ProjectElement> childElements = GetLogicalProject(child.AllChildren);

                            foreach (ProjectElement childElement in childElements)
                            {
                                yield return childElement;
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Encapsulates the backing data of a Project, so that it can be passed to the Evaluator to
        /// fill in on a re-evaluation without having to expose property setters.
        /// </summary>
        /// <remarks>
        /// This object is only passed to the Evaluator.
        /// </remarks>
        internal class Data : IItemProvider<ProjectItem>, IPropertyProvider<ProjectProperty>, IEvaluatorData<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition>
        {
            /// <summary>
            /// Project that owns this data
            /// </summary>
            private readonly Project _project;

            /// <summary>
            /// The global properties to evaluate with, if any.
            /// Can never be null.
            /// </summary>
            private readonly PropertyDictionary<ProjectPropertyInstance> _globalProperties;

            /// <summary>
            /// Almost always, projects have the same set of targets because they all import the same ones.
            /// So we keep around the last set seen and if ours is the same at the end of evaluation, unify the references.
            /// </summary>
            private static System.WeakReference<RetrievableEntryHashSet<ProjectTargetInstance>> s_typicalTargetsCollection;

            /// <summary>
            /// Save off the contents of the environment variable that specifies whether we should treat higher toolsversions as the current 
            /// toolsversion.  (Some hosts require this.)
            /// </summary>
            private static bool s_shouldTreatHigherToolsVersionsAsCurrent = (Environment.GetEnvironmentVariable("MSBUILDTREATHIGHERTOOLSVERSIONASCURRENT") != null);

            /// <summary>
            /// Save off the contents of the environment variable that specifies whether we should treat all toolsversions, regardless of 
            /// whether they are higher or lower, as the current toolsversion.  (Some hosts require this.)
            /// </summary>
            private static bool s_shouldTreatOtherToolsVersionsAsCurrent = (Environment.GetEnvironmentVariable("MSBUILDTREATALLTOOLSVERSIONSASCURRENT") != null);

            /// <summary>
            /// List of names of the properties that, while global, are still treated as overridable 
            /// </summary>
            private ISet<string> _globalPropertiesToTreatAsLocal;

            /// <summary>
            /// List of items that link the XML items and evaluated items.
            /// This is an ordered collection
            /// </summary>
            /// <remarks>
            /// Private so we can make sure that <see cref="_itemsByEvaluatedInclude">itemsByEvaluatedInclude</see> is updated
            /// on changes.
            /// </remarks>
            private ItemDictionary<ProjectItem> _items;

            /// <summary>
            /// Items indexed by their evaluated include value.
            /// Useful for hosts to find an item again after reevaluation.
            /// </summary>
            /// <remarks>
            /// Include value is unescaped 
            /// </remarks>
            private MultiDictionary<string, ProjectItem> _itemsByEvaluatedInclude;

            /// <summary>
            /// Whether when we read a ToolsVersion that does not match the current one from the Project tag, we treat it as though it 
            /// was current.
            /// </summary>
            private bool _usingDifferentToolsVersionFromProjectFile;

            /// <summary>
            /// The toolsversion that was originally on the project's Project root element
            /// </summary>
            private string _originalProjectToolsVersion;

            /// <summary>
            /// Constructor taking the immutable global properties and tools version.
            /// Tools version may be null.
            /// </summary>
            internal Data(Project project, PropertyDictionary<ProjectPropertyInstance> globalProperties, string explicitToolsVersion, string explicitSubToolsetVersion)
            {
                _project = project;
                _globalProperties = globalProperties;
                this.ExplicitToolsVersion = explicitToolsVersion;
                this.ExplicitSubToolsetVersion = explicitSubToolsetVersion;
            }

            /// <summary>
            /// Whether evaluation should collect items ignoring condition,
            /// as well as items respecting condition; and collect
            /// conditioned properties, as well as regular properties
            /// </summary>
            bool IEvaluatorData<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition>.ShouldEvaluateForDesignTime
            {
                get { return true; }
            }

            /// <summary>
            /// Collection of all evaluated item definitions, one per item-type
            /// </summary>
            IEnumerable<ProjectItemDefinition> IEvaluatorData<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition>.ItemDefinitionsEnumerable
            {
                get { return ItemDefinitions.Values; }
            }

            /// <summary>
            /// DefaultTargets specified in the project, or
            /// the logically first target if no DefaultTargets is
            /// specified in the project.
            /// </summary>
            public List<string> DefaultTargets
            {
                get;
                set;
            }

            /// <summary>
            /// The global properties to evaluate with, if any.
            /// Can never be null.
            /// Read-only; to use different global properties, evaluate yourself a new project.
            /// </summary>
            public PropertyDictionary<ProjectPropertyInstance> GlobalPropertiesDictionary
            {
                get { return _globalProperties; }
            }

            /// <summary>
            /// List of names of the properties that, while global, are still treated as overridable 
            /// </summary>
            public ISet<string> GlobalPropertiesToTreatAsLocal
            {
                get
                {
                    if (_globalPropertiesToTreatAsLocal == null)
                    {
                        _globalPropertiesToTreatAsLocal = new HashSet<string>(MSBuildNameIgnoreCaseComparer.Default);
                    }

                    return _globalPropertiesToTreatAsLocal;
                }
            }

            /// <summary>
            /// InitialTargets specified in the project, plus those
            /// in all imports, gathered depth-first.
            /// </summary>
            public List<string> InitialTargets
            {
                get;
                set;
            }

            /// <summary>
            /// Sets or retrieves the list of targets which run before the keyed target.
            /// </summary>
            public IDictionary<string, List<TargetSpecification>> BeforeTargets
            {
                get;
                set;
            }

            /// <summary>
            /// Sets or retrieves the list of targets which run after the keyed target.
            /// </summary>
            public IDictionary<string, List<TargetSpecification>> AfterTargets
            {
                get;
                set;
            }

            /// <summary>
            /// The externally specified tools version, if any.
            /// For example, the tools version from a /tv switch.
            /// Not necessarily the same as the tools version from the project tag or of the toolset used.
            /// May be null.
            /// Flows through to called projects.
            /// </summary>
            public string ExplicitToolsVersion
            {
                get;
                private set;
            }

            /// <summary>
            /// The toolset data used during evaluation.
            /// </summary>
            public Toolset Toolset
            {
                get;
                private set;
            }

            /// <summary>
            /// The externally specified sub-toolset version that, combined with the ToolsVersion, is used to determine
            /// the toolset properties for this project.  
            /// </summary>
            public string ExplicitSubToolsetVersion
            {
                get;
                private set;
            }

            /// <summary>
            /// The sub-toolset version that, combined with the ToolsVersion, was used to determine
            /// the toolset properties for this project.  
            /// </summary>
            public string SubToolsetVersion
            {
                get;
                private set;
            }

            /// <summary>
            /// Items in this project, ordered within groups of item types.
            /// Protected by an upcast to IEnumerable.
            /// </summary>
            public ItemDictionary<ProjectItem> Items
            {
                get { return _items; }
            }

            public List<ProjectItemElement> EvaluatedItemElements
            {
                get;
                private set;
            }

            /// <summary>
            /// List of items that link the XML items and evaluated items,
            /// evaluated as if their conditions were true.
            /// This is useful for hosts that wish to display all items regardless of their condition.
            /// This is an ordered collection.
            /// </summary>
            public ItemDictionary<ProjectItem> ItemsIgnoringCondition
            {
                get;
                private set;
            }

            /// <summary>
            /// Collection of properties that link the XML properties and evaluated properties.
            /// Since evaluation has occurred, this is an unordered collection.
            /// Includes any global and reserved properties.
            /// </summary>
            public PropertyDictionary<ProjectProperty> Properties
            {
                get;
                private set;
            }

            /// <summary>
            /// Collection of possible values implied for properties contained in the conditions found on properties,
            /// property groups, imports, and whens.
            /// 
            /// For example, if the following conditions existed on properties in a project:
            /// 
            /// Condition="'$(Configuration)|$(Platform)' == 'Debug|x86'"
            /// Condition="'$(Configuration)' == 'Release'"
            /// 
            /// the table would be populated with
            /// 
            /// { "Configuration", { "Debug", "Release" }}
            /// { "Platform", { "x86" }}
            /// 
            /// This is used by Visual Studio to determine the configurations defined in the project.
            /// </summary>
            public Dictionary<string, List<string>> ConditionedProperties
            {
                get;
                private set;
            }

            /// <summary>
            /// The root directory for this project
            /// </summary>
            public string Directory
            {
                get
                {
                    return _project.DirectoryPath;
                }
            }

            /// <summary>
            /// Registry of usingtasks, for build
            /// </summary>
            public Execution.TaskRegistry TaskRegistry
            {
                get;
                set;
            }

            /// <summary>
            /// Get the item types that have at least one item.
            /// Read only collection.
            /// </summary>
            /// <comments>
            /// item.ItemTypes is a KeyCollection, so it doesn't need any 
            /// additional read-only protection
            /// </comments>
            public ICollection<string> ItemTypes
            {
                get { return _items.ItemTypes; }
            }

            /// <summary>
            /// Properties encountered during evaluation. These are read during the first evaluation pass.
            /// Unlike those returned by the Properties property, these are ordered, and includes any properties that
            /// were subsequently overridden by others with the same name. It does not include any 
            /// properties whose conditions did not evaluate to true.
            /// It does not include any properties added since the last evaluation.
            /// </summary>
            internal IList<ProjectProperty> AllEvaluatedProperties
            {
                get;
                private set;
            }

            /// <summary>
            /// Item definition metadata encountered during evaluation. These are read during the second evaluation pass.
            /// Unlike those returned by the ItemDefinitions property, these are ordered, and include any metadata that
            /// were subsequently overridden by others with the same name and item type. It does not include any 
            /// elements whose conditions did not evaluate to true.
            /// It does not include any item definition metadata added since the last evaluation.
            /// </summary>
            internal IList<ProjectMetadata> AllEvaluatedItemDefinitionMetadata
            {
                get;
                private set;
            }

            /// <summary>
            /// Items encountered during evaluation. These are read during the third evaluation pass.
            /// Unlike those returned by the Items property, these are ordered.
            /// It does not include any elements whose conditions did not evaluate to true.
            /// It does not include any items added since the last evaluation.
            /// </summary>
            internal IList<ProjectItem> AllEvaluatedItems
            {
                get;
                private set;
            }

            /// <summary>
            /// Expander to use to expand any expressions encountered after the project has been fully evaluated.
            /// For example, to expand the values of any properties added at design time.
            /// It's convenient to store it here.
            /// </summary>
            internal Expander<ProjectProperty, ProjectItem> Expander
            {
                get;
                private set;
            }

            /// <summary>
            /// Whether something in this data has been modified since evaluation.
            /// For example, a global property has been set.
            /// </summary>
            internal bool HasUnsavedChanges
            {
                get;
                set;
            }

            /// <summary>
            /// Collection of all evaluated item definitions, one per item-type
            /// </summary>
            internal RetrievableEntryHashSet<ProjectItemDefinition> ItemDefinitions
            {
                get;
                private set;
            }

            /// <summary>
            /// Project that owns this data
            /// </summary>
            internal Project Project
            {
                get { return _project; }
            }

            /// <summary>
            /// Targets in the project, used to build
            /// </summary>
            internal RetrievableEntryHashSet<ProjectTargetInstance> Targets
            {
                get;
                set;
            }

            /// <summary>
            /// Complete list of all imports pulled in during evaluation.
            /// This includes the outer project itself.
            /// </summary>
            internal List<Triple<ProjectImportElement, ProjectRootElement, int>> ImportClosure
            {
                get;
                private set;
            }

            /// <summary>
            /// Complete list of all imports pulled in during evaluation including duplicate imports.
            /// This includes the outer project itself.
            /// </summary>
            internal List<Triple<ProjectImportElement, ProjectRootElement, int>> ImportClosureWithDuplicates
            {
                get;
                private set;
            }

            /// <summary>
            /// The toolsversion that was originally specified on the project's root element
            /// </summary>
            internal string OriginalProjectToolsVersion
            {
                get { return _originalProjectToolsVersion; }
            }

            /// <summary>
            /// Whether when we read a ToolsVersion other than the current one in the Project tag, we treat it as the current one.
            /// </summary>
            internal bool UsingDifferentToolsVersionFromProjectFile
            {
                get { return _usingDifferentToolsVersionFromProjectFile; }
            }

            /// <summary>
            /// expose mutable precalculated cache to outside so that other can take advantage of the cache as well.
            /// </summary>
            internal MultiDictionary<string, ProjectItem> ItemsByEvaluatedIncludeCache
            {
                get
                {
                    return _itemsByEvaluatedInclude;
                }
            }

            /// <summary>
            /// Prepares the data object for evaluation.
            /// </summary>
            public void InitializeForEvaluation(IToolsetProvider toolsetProvider)
            {
                this.DefaultTargets = null;
                this.Properties = new PropertyDictionary<ProjectProperty>();
                this.ConditionedProperties = new Dictionary<string, List<string>>(MSBuildNameIgnoreCaseComparer.Default);
                _items = new ItemDictionary<ProjectItem>();
                this.ItemsIgnoringCondition = new ItemDictionary<ProjectItem>();
                _itemsByEvaluatedInclude = new MultiDictionary<string, ProjectItem>(StringComparer.OrdinalIgnoreCase);
                this.Expander = new Expander<ProjectProperty, ProjectItem>(this.Properties, _items);
                this.ItemDefinitions = new RetrievableEntryHashSet<ProjectItemDefinition>(MSBuildNameIgnoreCaseComparer.Default);
                this.Targets = new RetrievableEntryHashSet<ProjectTargetInstance>(OrdinalIgnoreCaseKeyedComparer.Instance);
                this.ImportClosure = new List<Triple<ProjectImportElement, ProjectRootElement, int>>();
                this.ImportClosureWithDuplicates = new List<Triple<ProjectImportElement, ProjectRootElement, int>>();
                this.AllEvaluatedProperties = new List<ProjectProperty>();
                this.AllEvaluatedItemDefinitionMetadata = new List<ProjectMetadata>();
                this.AllEvaluatedItems = new List<ProjectItem>();
                this.EvaluatedItemElements = new List<ProjectItemElement>();

                if (_globalPropertiesToTreatAsLocal != null)
                {
                    _globalPropertiesToTreatAsLocal.Clear();
                }

                // Include the main project in the list of imports, as this list is 
                // used to figure out if any of them have changed.
                RecordImport(null, _project._xml, _project._xml.Version);

                string toolsVersionToUse = ExplicitToolsVersion;
                ElementLocation toolsVersionLocation = _project._xml.ProjectFileLocation;
                bool explicitToolsVersionSpecified = (ExplicitToolsVersion != null);

                if (_project._xml.ToolsVersion.Length > 0)
                {
                    _originalProjectToolsVersion = _project._xml.ToolsVersion;
                    toolsVersionLocation = _project._xml.ToolsVersionLocation;
                }

                toolsVersionToUse = Utilities.GenerateToolsVersionToUse
                    (
                        ExplicitToolsVersion,
                        _project._xml.ToolsVersion,
                        Project.ProjectCollection.GetToolset,
                        Project.ProjectCollection.DefaultToolsVersion
                    );

                // Don't log the message if the toolsversion is different because an explicit toolsversion was specified -- 
                // in that case the user already knows what they're doing; the point of this warning is to give them a heads
                // up if we're doing this ourselves for our own reasons. 
                if (!explicitToolsVersionSpecified && !String.Equals(_originalProjectToolsVersion, toolsVersionToUse, StringComparison.OrdinalIgnoreCase))
                {
                    _usingDifferentToolsVersionFromProjectFile = true;
                }

                Toolset = toolsetProvider.GetToolset(toolsVersionToUse);

                if (Toolset == null)
                {
                    string toolsVersionList = Microsoft.Build.Internal.Utilities.CreateToolsVersionListString(Project.ProjectCollection.Toolsets);
                    ProjectErrorUtilities.ThrowInvalidProject(toolsVersionLocation, "UnrecognizedToolsVersion", toolsVersionToUse, toolsVersionList);
                }

                if (this.ExplicitSubToolsetVersion != null)
                {
                    this.SubToolsetVersion = this.ExplicitSubToolsetVersion;
                }
                else
                {
                    this.SubToolsetVersion = this.Toolset.GenerateSubToolsetVersion(_globalProperties);
                }

                // Create a task registry which will fall back on the toolset task registry if necessary.          
                TaskRegistry = new TaskRegistry(Toolset, _project._projectCollection.ProjectRootElementCache);
            }

            /// <summary>
            /// Indicates to the data block that evaluation has completed,
            /// so for example it can mark datastructures read-only.
            /// </summary>
            public void FinishEvaluation()
            {
                // We assume there will be no further changes to the targets collection 
                // This also makes sure that we are thread safe
                Targets.MakeReadOnly();

                if (s_typicalTargetsCollection == null)
                {
                    Targets.TrimExcess();
                    s_typicalTargetsCollection = new System.WeakReference<RetrievableEntryHashSet<ProjectTargetInstance>>(Targets);
                }
                else
                {
                    // Attempt to unify the references, to save space
                    RetrievableEntryHashSet<ProjectTargetInstance> candidate;
                    if (s_typicalTargetsCollection.TryGetTarget(out candidate) && candidate.EntriesAreReferenceEquals(Targets))
                    {
                        // Reuse
                        Targets = candidate;
                    }
                    else
                    {
                        // Else we'll guess that this latest one is a potential match for the next, 
                        // if it actually has any elements (eg., it's not a .user or .filters file)
                        if (Targets.Count > 0)
                        {
                            Targets.TrimExcess();
                            s_typicalTargetsCollection.SetTarget(Targets);
                        }
                    }
                }
            }

            /// <summary>
            /// Adds a new item.
            /// </summary>
            public void AddItem(ProjectItem item)
            {
                _items.Add(item);
                _itemsByEvaluatedInclude.Add(item.EvaluatedInclude, item);
            }

            /// <summary>
            /// Adds a new item to the collection of all items ignoring condition
            /// </summary>
            public void AddItemIgnoringCondition(ProjectItem item)
            {
                ItemsIgnoringCondition.Add(item);
            }

            /// <summary>
            /// Properties encountered during evaluation. These are read during the first evaluation pass.
            /// Unlike those returned by the Properties property, these are ordered, and includes any properties that
            /// were subsequently overridden by others with the same name. It does not include any 
            /// properties whose conditions did not evaluate to true.
            /// </summary>
            public void AddToAllEvaluatedPropertiesList(ProjectProperty property)
            {
                ErrorUtilities.VerifyThrowInternalNull(property, "property");
                AllEvaluatedProperties.Add(property);
            }

            /// <summary>
            /// Item definition metadata encountered during evaluation. These are read during the second evaluation pass.
            /// Unlike those returned by the ItemDefinitions property, these are ordered, and include any metadata that
            /// were subsequently overridden by others with the same name and item type. It does not include any 
            /// elements whose conditions did not evaluate to true.
            /// </summary>
            public void AddToAllEvaluatedItemDefinitionMetadataList(ProjectMetadata itemDefinitionMetadatum)
            {
                ErrorUtilities.VerifyThrowInternalNull(itemDefinitionMetadatum, "itemDefinitionMetadatum");
                AllEvaluatedItemDefinitionMetadata.Add(itemDefinitionMetadatum);
            }

            /// <summary>
            /// Items encountered during evaluation. These are read during the third evaluation pass.
            /// Unlike those returned by the Items property, these are ordered.
            /// It does not include any elements whose conditions did not evaluate to true.
            /// It does not include any items added since the last evaluation.
            /// </summary>
            public void AddToAllEvaluatedItemsList(ProjectItem item)
            {
                ErrorUtilities.VerifyThrowInternalNull(item, "item");
                AllEvaluatedItems.Add(item);
            }

            /// <summary>
            /// Adds a new item definition
            /// </summary>
            public IItemDefinition<ProjectMetadata> AddItemDefinition(string itemType)
            {
                ProjectItemDefinition newItemDefinition = new ProjectItemDefinition(this.Project, itemType);

                ItemDefinitions.Add(newItemDefinition);

                return newItemDefinition;
            }

            /// <summary>
            /// Gets an existing item definition, if any.
            /// </summary>
            public IItemDefinition<ProjectMetadata> GetItemDefinition(string itemType)
            {
                ProjectItemDefinition itemDefinition;
                ItemDefinitions.TryGetValue(itemType, out itemDefinition);

                return itemDefinition;
            }

            /// <summary>
            /// Sets a property which is not derived from Xml.
            /// </summary>
            public ProjectProperty SetProperty(string name, string evaluatedValueEscaped, bool isGlobalProperty, bool mayBeReserved)
            {
                ProjectProperty property = ProjectProperty.Create(this.Project, name, evaluatedValueEscaped, isGlobalProperty, mayBeReserved);
                Properties.Set(property);

                AddToAllEvaluatedPropertiesList(property);

                return property;
            }

            /// <summary>
            /// Sets a property derived from Xml.
            /// Predecessor is any immediately previous property that was overridden by this one during evaluation.
            /// This would include all properties with the same name that lie above in the logical
            /// project file, and whose conditions evaluated to true.
            /// If there are none above this is null.
            /// </summary>
            public ProjectProperty SetProperty(ProjectPropertyElement propertyElement, string evaluatedValueEscaped, ProjectProperty predecessor)
            {
                ProjectProperty property = ProjectProperty.Create(this.Project, propertyElement, evaluatedValueEscaped, predecessor);
                Properties.Set(property);

                AddToAllEvaluatedPropertiesList(property);

                return property;
            }

            /// <summary>
            /// Retrieves an existing target, if any.
            /// </summary>
            public ProjectTargetInstance GetTarget(string targetName)
            {
                ProjectTargetInstance target;

                Targets.TryGetValue(targetName, out target);

                return target;
            }

            /// <summary>
            /// Adds the specified target, overwriting any existing target with the same name.
            /// </summary>
            public void AddTarget(ProjectTargetInstance target)
            {
                Targets[target.Name] = target;
            }

            /// <summary>
            /// Record an import opened during evaluation.
            /// This is used to check later whether any of them have been changed.
            /// </summary>
            /// <remarks>
            /// This may include imported files that ended up contributing nothing to the evaluated project.
            /// These might otherwise have no strong references to them at all.
            /// If they are dirtied, though, they might affect the evaluated project; and that's why we record them. 
            /// Mostly these will be common imports, so they'll be shared anyway.
            /// </remarks>
            public void RecordImport(ProjectImportElement importElement, ProjectRootElement import, int versionEvaluated)
            {
                ImportClosure.Add(new Triple<ProjectImportElement, ProjectRootElement, int>(importElement, import, versionEvaluated));
                RecordImportWithDuplicates(importElement, import, versionEvaluated);
            }

            /// <summary>
            /// Record a duplicate import, possible a duplicate import opened during evaluation.
            /// </summary>
            public void RecordImportWithDuplicates(ProjectImportElement importElement, ProjectRootElement import, int versionEvaluated)
            {
                ImportClosureWithDuplicates.Add(new Triple<ProjectImportElement, ProjectRootElement, int>(importElement, import, versionEvaluated));
            }

            /// <summary>
            /// Evaluates the provided string by expanding items and properties,
            /// using the current items and properties available.
            /// This is useful for the immediate window.
            /// Does not expand bare metadata expressions.
            /// </summary>
            /// <comment>
            /// Not for internal use.
            /// </comment>
            string IEvaluatorData<ProjectProperty, ProjectItem, ProjectMetadata, ProjectItemDefinition>.ExpandString(string unexpandedValue)
            {
                return _project.ExpandString(unexpandedValue);
            }

            /// <summary>
            /// Evaluates the provided string as a condition by expanding items and properties,
            /// using the current items and properties available, then doing a logical evaluation.
            /// This is useful for the immediate window.
            /// Does not expand bare metadata expressions.
            /// </summary>
            /// <comment>
            /// Not for internal use.
            /// </comment>
            public bool EvaluateCondition(string condition)
            {
                // This is for the debugger, which should not get a live Project object,
                // so this is not implemented.
                ErrorUtilities.ThrowInternalErrorUnreachable();
                return false;
            }

            #region IItemProvider<ProjectItem> Members

            /// <summary>
            /// Returns a list of items of the specified type.
            /// If there are none, returns an empty list.
            /// </summary>
            /// <comments>
            /// ItemDictionary returns a read-only collection, so no need to wrap it here.
            /// </comments>
            /// <param name="itemType">The type of items to return.</param>
            /// <returns>A list of matching items.</returns>
            public ICollection<ProjectItem> GetItems(string itemType)
            {
                return _items[itemType];
            }

            #endregion

            #region IPropertyProvider<ProjectProperty> Members

            /// <summary>
            /// Returns the property with the specified name or null if it was not present
            /// </summary>
            /// <param name="name">The property name.</param>
            /// <returns>The property.</returns>
            public ProjectProperty GetProperty(string name)
            {
                return Properties[name];
            }

            /// <summary>
            /// Returns the property with the specified name or null if it was not present
            /// </summary>
            /// <param name="name">The property name.</param>
            /// <returns>The property.</returns>
            public ProjectProperty GetProperty(string name, int startIndex, int endIndex)
            {
                return Properties.GetProperty(name, startIndex, endIndex);
            }

            #endregion

            /// <summary>
            /// Clears out certain cached values. 
            /// FOR UNIT TESTING ONLY
            /// </summary>
            internal static void ClearCachedFlags()
            {
                s_shouldTreatHigherToolsVersionsAsCurrent = false;
                s_shouldTreatOtherToolsVersionsAsCurrent = false;
            }

            /// <summary>
            /// Removes an item.
            /// Returns true if it was previously present, otherwise false.
            /// </summary>
            internal bool RemoveItem(ProjectItem item)
            {
                bool result = _items.Remove(item);

                // This remove will not succeed if the item include was changed.
                // If many items are modified and then removed, this will leak them
                // until the next reevaluation.                
                _itemsByEvaluatedInclude.Remove(item.EvaluatedInclude, item);

                ItemsIgnoringCondition.Remove(item);

                return result;
            }

            /// <summary>
            /// Returns all items that have the specified evaluated include.
            /// For example, all items that have the evaluated include "bar.cpp".
            /// Typically there will be no more than one, but sometimes there are two items with the
            /// same path and different item types, or even the same item types. This will return
            /// them all.
            /// </summary>
            /// <remarks>
            /// Assumes that the evaluated include value is unescaped.
            /// </remarks>
            internal ICollection<ProjectItem> GetItemsByEvaluatedInclude(string evaluatedInclude)
            {
                // Even if there are no items in itemsByEvaluatedInclude[], it will return an IEnumerable, which is non-null
                ICollection<ProjectItem> items = new ReadOnlyCollection<ProjectItem>(_itemsByEvaluatedInclude[evaluatedInclude]);

                return items;
            }

            /// <summary>
            /// Get the value of a property in this project, or 
            /// an empty string if it does not exist.
            /// Returns the unescaped value.
            /// </summary>
            /// <remarks>
            /// A property with a value of empty string and no property
            /// at all are not distinguished between by this method.
            /// That makes it easier to use. To find out if a property is set at
            /// all in the project, use GetProperty(name).
            /// </remarks>
            internal string GetPropertyValue(string name)
            {
                ErrorUtilities.VerifyThrowArgumentLength(name, "name");

                ProjectProperty property = Properties[name];
                string value = (property == null) ? String.Empty : property.EvaluatedValue;
                return value;
            }
        }
    }

    /// <summary>
    /// Bit flag enum that specifies how a string representing an item matched against an itemspec.
    /// </summary>
    [Flags]
    public enum Provenance
    {
        /// <summary>
        /// Undefined is the bottom element and should not appear in actual results 
        /// </summary>
        Undefined = 0,

        /// <summary>
        /// A string matched against a string literal from an itemspec
        /// </summary>
        StringLiteral = 1,

        /// <summary>
        /// A string matched against a glob pattern from an itemspec
        /// </summary>
        Glob = 2,

        /// <summary>
        /// Inconclusive means that the match is indirect, coming from either property or item references.
        /// </summary>
        Inconclusive = 4
    }

    /// <summary>
    /// Enum that specifies how an item element references an item
    /// </summary>
    public enum Operation
    {
        Include, 
        Exclude
    }

    /// <summary>
    /// Data class representing a result from <see cref="Project.GetItemProvenance(string)"/> and its overloads.
    /// </summary>
    public class ProvenanceResult
    {
        public Operation Operation { get; private set; }
        public ProjectItemElement ItemElement { get; private set; }
        public Provenance Provenance { get; private set; }
        public int Occurrences { get; private set; }

        public ProvenanceResult(ProjectItemElement itemElement, Operation operation, Provenance provenance, int occurrences)
        {
            ItemElement = itemElement;
            Operation = operation;
            Provenance = provenance;
            Occurrences = occurrences;
        }
    }
}
