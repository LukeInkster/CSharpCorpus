﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>
// Helper class which converts Xaml rules into data structures 
// suitable for command-line processing
// </summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xaml;
using System.Xml;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

using XamlTypes = Microsoft.Build.Framework.XamlTypes;

namespace Microsoft.Build.Tasks.Xaml
{
    /// <summary>
    /// The TaskParser class takes an xml file and parses the parameters for a task.
    /// </summary>
    internal class TaskParser
    {
        /// <summary>
        /// The name of the task e.g., CL
        /// </summary>
        private string _name;

        /// <summary>
        /// The name of the executable e.g., cl.exe
        /// </summary>
        private string _toolName;

        /// <summary>
        /// The base class 
        /// </summary>
        private string _baseClass = "DataDrivenToolTask";

        /// <summary>
        /// The namespace to generate the class into
        /// </summary>
        private string _namespaceValue = "XamlTaskNamespace";

        /// <summary>
        /// The resource namespace to pass to the base class, if any
        /// </summary>
        private string _resourceNamespaceValue = null;

        /// <summary>
        /// The prefix to append before a switch is emitted.
        /// Is typically a "/", but can also be a "-"
        /// </summary>
        private string _defaultPrefix = String.Empty;

        /// <summary>
        /// The list that contains all of the properties that can be set on a task
        /// </summary>
        private LinkedList<Property> _properties = new LinkedList<Property>();

        /// <summary>
        /// The list that contains all of the properties that have a default value
        /// </summary>
        private LinkedList<Property> _defaultSet = new LinkedList<Property>();

        /// <summary>
        /// The list of properties that serve as fallbacks for other properties.
        /// That is, if a certain property is not set, but has a fallback, we need to check
        /// to see if that fallback is set.
        /// </summary>
        private Dictionary<string, string> _fallbackSet = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The set of switches added so far.
        /// </summary>
        private HashSet<string> _switchesAdded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The ordered list of how the switches get emitted.
        /// </summary>
        private List<string> _switchOrderList = new List<string>();

        /// <summary>
        /// The errors that occurred while parsing the xml file or generating the code
        /// </summary>
        private LinkedList<string> _errorLog = new LinkedList<string>();

        /// <summary>
        /// The constructor.
        /// </summary>
        public TaskParser()
        {
            // do nothing
        }

        #region Properties

        /// <summary>
        /// The name of the task
        /// </summary>
        public string GeneratedTaskName
        {
            get
            {
                return _name;
            }

            set
            {
                _name = value;
            }
        }

        /// <summary>
        /// The base type of the class
        /// </summary>
        public string BaseClass
        {
            get
            {
                return _baseClass;
            }
        }

        /// <summary>
        /// The namespace of the class
        /// </summary>
        public string Namespace
        {
            get
            {
                return _namespaceValue;
            }
        }

        /// <summary>
        /// Namespace for the resources
        /// </summary>
        public string ResourceNamespace
        {
            get
            {
                return _resourceNamespaceValue;
            }
        }

        /// <summary>
        /// The name of the executable
        /// </summary>
        public string ToolName
        {
            get
            {
                return _toolName;
            }
        }

        /// <summary>
        /// The default prefix for each switch
        /// </summary>
        public string DefaultPrefix
        {
            get
            {
                return _defaultPrefix;
            }
        }

        /// <summary>
        /// All of the parameters that were parsed
        /// </summary>
        public LinkedList<Property> Properties
        {
            get
            {
                return _properties;
            }
        }

        /// <summary>
        /// All of the parameters that have a default value
        /// </summary>
        public LinkedList<Property> DefaultSet
        {
            get
            {
                return _defaultSet;
            }
        }

        /// <summary>
        /// All of the properties that serve as fallbacks for unset properties
        /// </summary>
        public Dictionary<string, string> FallbackSet
        {
            get
            {
                return _fallbackSet;
            }
        }

        /// <summary>
        /// The ordered list of properties
        /// </summary>
        public IEnumerable<string> SwitchOrderList
        {
            get
            {
                return _switchOrderList;
            }
        }

        /// <summary>
        /// Returns the log of errors
        /// </summary>
        public LinkedList<string> ErrorLog
        {
            get
            {
                return _errorLog;
            }
        }
        #endregion

        /// <summary>
        /// Parse the specified string, either as a file path or actual XML content.
        /// </summary>
        public bool Parse(string contentOrFile, string desiredRule)
        {
            ErrorUtilities.VerifyThrowArgumentLength(contentOrFile, "contentOrFile");
            ErrorUtilities.VerifyThrowArgumentLength(desiredRule, "desiredRule");

            string fullPath = null;
            bool parseSuccessful = false;
            try
            {
                fullPath = Path.GetFullPath(contentOrFile);
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                // We will get an exception if the contents are not a path (for instance, they are actual XML.)
            }

            if (fullPath != null)
            {
                if (!File.Exists(fullPath))
                {
                    throw new ArgumentException(ResourceUtilities.FormatResourceString("Xaml.RuleFileNotFound", fullPath));
                }

                parseSuccessful = ParseXamlDocument(new StreamReader(fullPath), desiredRule);
            }
            else
            {
                parseSuccessful = ParseXamlDocument(new StringReader(contentOrFile), desiredRule);
            }

            if (!parseSuccessful)
            {
                StringBuilder parseErrors = new StringBuilder();
                parseErrors.AppendLine();
                foreach (string error in ErrorLog)
                {
                    parseErrors.AppendLine(error);
                }

                throw new ArgumentException(ResourceUtilities.FormatResourceString("Xaml.RuleParseFailed", parseErrors.ToString()));
            }

            return parseSuccessful;
        }

        /// <summary>
        /// Parse a Xaml document from a TextReader
        /// </summary>
        internal bool ParseXamlDocument(TextReader reader, string desiredRule)
        {
            ErrorUtilities.VerifyThrowArgumentNull(reader, "reader");
            ErrorUtilities.VerifyThrowArgumentLength(desiredRule, "desiredRule");

            object rootObject = XamlServices.Load(reader);
            if (null != rootObject)
            {
                XamlTypes.ProjectSchemaDefinitions schemas = rootObject as XamlTypes.ProjectSchemaDefinitions;
                if (schemas != null)
                {
                    foreach (XamlTypes.IProjectSchemaNode node in schemas.Nodes)
                    {
                        XamlTypes.Rule rule = node as XamlTypes.Rule;
                        if (rule != null)
                        {
                            if (String.Equals(rule.Name, desiredRule, StringComparison.OrdinalIgnoreCase))
                            {
                                return ParseXamlDocument(rule);
                            }
                        }
                    }

                    throw new XamlParseException(ResourceUtilities.FormatResourceString("Xaml.RuleNotFound", desiredRule));
                }
                else
                {
                    throw new XamlParseException(ResourceUtilities.FormatResourceString("Xaml.InvalidRootObject"));
                }
            }

            return false;
        }

        /// <summary>
        /// Parse a Xaml document from a rule
        /// </summary>
        internal bool ParseXamlDocument(XamlTypes.Rule rule)
        {
            if (rule == null)
            {
                return false;
            }

            _defaultPrefix = rule.SwitchPrefix;

            _toolName = rule.ToolName;
            _name = rule.Name;

            // Dictionary of property name strings to property objects. If a property is in the argument list of the current property then we want to make sure
            // that the argument property is a dependency of the current property.

            // As properties are parsed they are added to this dictionary so that after we can find the property instances from the names quickly.
            Dictionary<string, Property> argumentDependencyLookup = new Dictionary<string, Property>(StringComparer.OrdinalIgnoreCase);

            // baseClass = attribute.InnerText;
            // namespaceValue = attribute.InnerText;
            // resourceNamespaceValue = attribute.InnerText;
            foreach (XamlTypes.BaseProperty property in rule.Properties)
            {
                if (!ParseParameterGroupOrParameter(property, _properties, null, argumentDependencyLookup /*Add to the dictionary properties as they are parsed*/))
                {
                    return false;
                }
            }

            // Go through each property and their arguments to set up the correct dependency mappings.
            foreach (Property property in Properties)
            {
                // Get the arguments on the property itself
                List<Argument> arguments = property.Arguments;

                // Find all of the properties in arguments list.
                foreach (Argument argument in arguments)
                {
                    Property argumentProperty = null;
                    if (argumentDependencyLookup.TryGetValue(argument.Parameter, out argumentProperty))
                    {
                        property.DependentArgumentProperties.AddLast(argumentProperty);
                    }
                }

                // Properties may be enumeration types, this would mean they have sub property values which themselves can have arguments.
                List<Value> values = property.Values;

                // Find all of the properties for the aruments in sub property.
                foreach (Value value in values)
                {
                    List<Argument> valueArguments = value.Arguments;
                    foreach (Argument argument in valueArguments)
                    {
                        Property argumentProperty = null;

                        if (argumentDependencyLookup.TryGetValue(argument.Parameter, out argumentProperty))
                        {
                            // If the property contains a value sub property that has a argument then we will declare that the original property has the same dependenecy.
                            property.DependentArgumentProperties.AddLast(argumentProperty);
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Reads in the nodes of the xml file one by one and builds the data structure of all existing properties
        /// </summary>
        private bool ParseParameterGroupOrParameter(XamlTypes.BaseProperty baseProperty, LinkedList<Property> propertyList, Property property, Dictionary<string, Property> argumentDependencyLookup)
        {
            // node is a property
            if (!ParseParameter(baseProperty, propertyList, property, argumentDependencyLookup))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Fills in the property data structure
        /// </summary>
        private bool ParseParameter(XamlTypes.BaseProperty baseProperty, LinkedList<Property> propertyList, Property property, Dictionary<string, Property> argumentDependencyLookup)
        {
            Property propertyToAdd = ObtainAttributes(baseProperty, property);

            if (String.IsNullOrEmpty(propertyToAdd.Name))
            {
                propertyToAdd.Name = "AlwaysAppend";
            }

            // generate the list of parameters in order
            if (!_switchesAdded.Contains(propertyToAdd.Name))
            {
                _switchOrderList.Add(propertyToAdd.Name);
            }

            // Inherit the Prefix from the Tool
            if (String.IsNullOrEmpty(propertyToAdd.Prefix))
            {
                propertyToAdd.Prefix = DefaultPrefix;
            }

            // If the property is an enum type, parse that.
            XamlTypes.EnumProperty enumProperty = baseProperty as XamlTypes.EnumProperty;
            if (enumProperty != null)
            {
                foreach (XamlTypes.EnumValue enumValue in enumProperty.AdmissibleValues)
                {
                    Value value = new Value();

                    value.Name = enumValue.Name;
                    value.SwitchName = enumValue.Switch;
                    if (value.SwitchName == null)
                    {
                        value.SwitchName = String.Empty;
                    }

                    value.DisplayName = enumValue.DisplayName;
                    value.Description = enumValue.Description;
                    value.Prefix = enumValue.SwitchPrefix;
                    if (String.IsNullOrEmpty(value.Prefix))
                    {
                        value.Prefix = enumProperty.SwitchPrefix;
                    }

                    if (String.IsNullOrEmpty(value.Prefix))
                    {
                        value.Prefix = DefaultPrefix;
                    }

                    if (enumValue.Arguments.Count > 0)
                    {
                        value.Arguments = new List<Argument>();
                        foreach (XamlTypes.Argument argument in enumValue.Arguments)
                        {
                            Argument arg = new Argument();
                            arg.Parameter = argument.Property;
                            arg.Separator = argument.Separator;
                            arg.Required = argument.IsRequired;
                            value.Arguments.Add(arg);
                        }
                    }

                    if (value.Prefix == null)
                    {
                        value.Prefix = propertyToAdd.Prefix;
                    }

                    propertyToAdd.Values.Add(value);
                }
            }

            // build the dependencies and the values for a parameter
            foreach (XamlTypes.Argument argument in baseProperty.Arguments)
            {
                // To refactor into a separate func
                if (propertyToAdd.Arguments == null)
                {
                    propertyToAdd.Arguments = new List<Argument>();
                }

                Argument arg = new Argument();
                arg.Parameter = argument.Property;
                arg.Separator = argument.Separator;
                arg.Required = argument.IsRequired;
                propertyToAdd.Arguments.Add(arg);
            }

            if (argumentDependencyLookup != null && !argumentDependencyLookup.ContainsKey(propertyToAdd.Name))
            {
                argumentDependencyLookup.Add(propertyToAdd.Name, propertyToAdd);
            }

            // We've read any enumerated values and any dependencies, so we just 
            // have to add the property
            propertyList.AddLast(propertyToAdd);
            return true;
        }

        /// <summary>
        /// Gets all the attributes assigned in the xml file for this parameter or all of the nested switches for 
        /// this parameter group
        /// </summary>
        private Property ObtainAttributes(XamlTypes.BaseProperty baseProperty, Property parameterGroup)
        {
            Property parameter;
            if (parameterGroup != null)
            {
                parameter = parameterGroup.Clone();
            }
            else
            {
                parameter = new Property();
            }

            XamlTypes.BoolProperty boolProperty = baseProperty as XamlTypes.BoolProperty;
            XamlTypes.DynamicEnumProperty dynamicEnumProperty = baseProperty as XamlTypes.DynamicEnumProperty;
            XamlTypes.EnumProperty enumProperty = baseProperty as XamlTypes.EnumProperty;
            XamlTypes.IntProperty intProperty = baseProperty as XamlTypes.IntProperty;
            XamlTypes.StringProperty stringProperty = baseProperty as XamlTypes.StringProperty;
            XamlTypes.StringListProperty stringListProperty = baseProperty as XamlTypes.StringListProperty;

            parameter.IncludeInCommandLine = baseProperty.IncludeInCommandLine;

            if (baseProperty.Name != null)
            {
                parameter.Name = baseProperty.Name;
            }

            if (boolProperty != null && !String.IsNullOrEmpty(boolProperty.ReverseSwitch))
            {
                parameter.Reversible = "true";
            }

            // Determine the type for this property.
            if (boolProperty != null)
            {
                parameter.Type = PropertyType.Boolean;
            }
            else if (enumProperty != null)
            {
                parameter.Type = PropertyType.String;
            }
            else if (dynamicEnumProperty != null)
            {
                parameter.Type = PropertyType.String;
            }
            else if (intProperty != null)
            {
                parameter.Type = PropertyType.Integer;
            }
            else if (stringProperty != null)
            {
                parameter.Type = PropertyType.String;
            }
            else if (stringListProperty != null)
            {
                parameter.Type = PropertyType.StringArray;
            }

            // We might need to override this type based on the data source, if it specifies a source type of 'Item'.
            if (baseProperty.DataSource != null)
            {
                if (!String.IsNullOrEmpty(baseProperty.DataSource.SourceType))
                {
                    if (baseProperty.DataSource.SourceType.Equals("Item", StringComparison.OrdinalIgnoreCase))
                    {
                        parameter.Type = PropertyType.ItemArray;
                    }
                }
            }

            if (intProperty != null)
            {
                parameter.Max = intProperty.MaxValue != null ? intProperty.MaxValue.ToString() : null;
                parameter.Min = intProperty.MinValue != null ? intProperty.MinValue.ToString() : null;
            }

            if (boolProperty != null)
            {
                parameter.ReverseSwitchName = boolProperty.ReverseSwitch;
            }

            if (baseProperty.Switch != null)
            {
                parameter.SwitchName = baseProperty.Switch;
            }

            if (stringListProperty != null)
            {
                parameter.Separator = stringListProperty.Separator;
            }

            if (baseProperty.Default != null)
            {
                parameter.DefaultValue = baseProperty.Default;
            }

            parameter.Required = baseProperty.IsRequired.ToString().ToLower(CultureInfo.InvariantCulture);

            if (baseProperty.Category != null)
            {
                parameter.Category = baseProperty.Category;
            }

            if (baseProperty.DisplayName != null)
            {
                parameter.DisplayName = baseProperty.DisplayName;
            }

            if (baseProperty.Description != null)
            {
                parameter.Description = baseProperty.Description;
            }

            if (baseProperty.SwitchPrefix != null)
            {
                parameter.Prefix = baseProperty.SwitchPrefix;
            }

            return parameter;
        }
    }
}
