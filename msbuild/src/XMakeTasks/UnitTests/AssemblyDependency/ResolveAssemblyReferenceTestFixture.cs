using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Win32;
using Xunit;
using ProcessorArchitecture = System.Reflection.ProcessorArchitecture;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    public class ResolveAssemblyReferenceTestFixture : IDisposable
    {
        // Create the mocks.
        internal static Microsoft.Build.Shared.FileExists fileExists = new Microsoft.Build.Shared.FileExists(FileExists);
        internal static Microsoft.Build.Shared.DirectoryExists directoryExists = new Microsoft.Build.Shared.DirectoryExists(DirectoryExists);
        internal static Microsoft.Build.Tasks.GetDirectories getDirectories = new Microsoft.Build.Tasks.GetDirectories(GetDirectories);
        internal static Microsoft.Build.Tasks.GetAssemblyName getAssemblyName = new Microsoft.Build.Tasks.GetAssemblyName(GetAssemblyName);
        internal static Microsoft.Build.Tasks.GetAssemblyMetadata getAssemblyMetadata = new Microsoft.Build.Tasks.GetAssemblyMetadata(GetAssemblyMetadata);
        internal static Microsoft.Build.Shared.GetRegistrySubKeyNames getRegistrySubKeyNames = new Microsoft.Build.Shared.GetRegistrySubKeyNames(GetRegistrySubKeyNames);
        internal static Microsoft.Build.Shared.GetRegistrySubKeyDefaultValue getRegistrySubKeyDefaultValue = new Microsoft.Build.Shared.GetRegistrySubKeyDefaultValue(GetRegistrySubKeyDefaultValue);
        internal static Microsoft.Build.Tasks.GetLastWriteTime getLastWriteTime = new Microsoft.Build.Tasks.GetLastWriteTime(GetLastWriteTime);
        internal static Microsoft.Build.Tasks.GetAssemblyRuntimeVersion getRuntimeVersion = new Microsoft.Build.Tasks.GetAssemblyRuntimeVersion(GetRuntimeVersion);
        internal static Microsoft.Build.Tasks.GetAssemblyPathInGac checkIfAssemblyIsInGac = new Microsoft.Build.Tasks.GetAssemblyPathInGac(GetPathForAssemblyInGac);
        internal static Microsoft.Build.Shared.OpenBaseKey openBaseKey = new Microsoft.Build.Shared.OpenBaseKey(GetBaseKey);
        internal Microsoft.Build.UnitTests.MockEngine.GetStringDelegate resourceDelegate = new Microsoft.Build.UnitTests.MockEngine.GetStringDelegate(AssemblyResources.GetString);
        internal static Microsoft.Build.Tasks.IsWinMDFile isWinMDFile = new Microsoft.Build.Tasks.IsWinMDFile(IsWinMDFile);
        internal static Microsoft.Build.Tasks.ReadMachineTypeFromPEHeader readMachineTypeFromPEHeader = new Microsoft.Build.Tasks.ReadMachineTypeFromPEHeader(ReadMachineTypeFromPEHeader);

        // Performance checks.
        internal static Hashtable uniqueFileExists = null;
        internal static Hashtable uniqueGetAssemblyName = null;
        internal static bool useFrameworkFileExists = false;
        internal const string REDISTLIST = @"<FileList  Redist=""Microsoft-Windows-CLRCoreComp.4.0"" Name="".NET Framework 4"" RuntimeVersion=""4.0"" ToolsVersion=""12.0"">
  <File AssemblyName=""Accessibility"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""CustomMarshalers"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""ISymWrapper"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.Build"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.Build.Conversion.v4.0"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.Build.Engine"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.Build.Framework"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.Build.Tasks.v4.0"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.Build.Utilities.v4.0"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.CSharp"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.JScript"" Version=""10.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.VisualBasic"" Version=""10.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.VisualBasic.Compatibility"" Version=""10.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.VisualBasic.Compatibility.Data"" Version=""10.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.VisualC"" Version=""10.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""Microsoft.VisualC.STLCLR"" Version=""2.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""mscorlib"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationBuildTasks"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationCore"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationFramework.Aero"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationFramework.Classic"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationFramework"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationFramework.Luna"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""PresentationFramework.Royale"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""ReachFramework"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""sysglobl"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Activities"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Activities.Core.Presentation"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Activities.DurableInstancing"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Activities.Presentation"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.AddIn.Contract"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.AddIn"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ComponentModel.Composition"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ComponentModel.DataAnnotations"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Configuration"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Configuration.Install"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Core"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.DataSetExtensions"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.Entity.Design"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.Entity"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.Linq"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.OracleClient"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.Services.Client"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.Services.Design"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.Services"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Data.SqlXml"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Deployment"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Design"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Device"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.DirectoryServices.AccountManagement"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.DirectoryServices"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.DirectoryServices.Protocols"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Drawing.Design"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Drawing"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Dynamic"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.EnterpriseServices"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.IdentityModel"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.IdentityModel.Selectors"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" /> 
  <File AssemblyName=""System.IO.Log"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Management"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Management.Instrumentation"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Messaging"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Net"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Numerics"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Printing"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Runtime.DurableInstancing"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Runtime.Caching"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Runtime.Remoting"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Runtime.Serialization"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Runtime.Serialization.Formatters.Soap"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Security"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel.Activation"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel.Activities"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel.Channels"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel.Discovery"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel.Routing"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceModel.Web"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.ServiceProcess"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Speech"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" /> 
  <File AssemblyName=""System.Transactions"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Abstractions"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.ApplicationServices"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.DataVisualization.Design"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.DataVisualization"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.DynamicData.Design"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.DynamicData"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Entity.Design"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Entity"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Extensions.Design"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Extensions"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Mobile"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.RegularExpressions"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Routing"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Web.Services"" Version=""4.0.0.0"" PublicKeyToken=""b03f5f7f11d50a3a"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Windows.Forms.DataVisualization.Design"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Windows.Forms.DataVisualization"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Windows.Forms"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Windows.Input.Manipulations"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Windows.Presentation"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Workflow.Activities"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Workflow.ComponentModel"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Workflow.Runtime"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.WorkflowServices"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Xaml"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Xml"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""System.Xml.Linq"" Version=""4.0.0.0"" PublicKeyToken=""b77a5c561934e089"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""UIAutomationClient"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""UIAutomationClientsideProviders"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""UIAutomationProvider"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""UIAutomationTypes"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""WindowsBase"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""WindowsFormsIntegration"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
  <File AssemblyName=""XamlBuildTask"" Version=""4.0.0.0"" PublicKeyToken=""31bf3856ad364e35"" Culture=""neutral"" ProcessorArchitecture=""MSIL"" InGac=""true"" />
</FileList>"
            ;

        public ResolveAssemblyReferenceTestFixture()
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE", "1");
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("MSBUILDDISABLEASSEMBLYFOLDERSEXCACHE", null);
        }

        /// <summary>
        /// Search paths to use.
        /// </summary>
        private static readonly string[] s_defaultPaths = new string[]
        {
            "{RawFileName}",
            "{CandidateAssemblyFiles}",
            @"c:\MyProject",
            @"c:\MyComponents\misc\",
            @"c:\MyComponents\1.0",
            @"c:\MyComponents\2.0",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion",
            @"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}",
            "{AssemblyFolders}",
            "{HintPathFromItem}"
        };

        /// <summary>
        /// Return the default search paths.
        /// </summary>
        /// <value></value>
        internal string[] DefaultPaths
        {
            get { return s_defaultPaths; }
        }

        /// <summary>
        /// Start monitoring IO calls.
        /// </summary>
        internal void StartIOMonitoring()
        {
            // If tables are present then the corresponding IO function will do some monitoring.
            uniqueFileExists = new Hashtable();
            uniqueGetAssemblyName = new Hashtable();
        }

        /// <summary>
        /// Stop monitoring IO calls and assert if any unnecessary IO was used.
        /// </summary>
        internal void StopIOMonitoringAndAssert_Minimal_IOUse()
        {
            // Check for minimal IO in File.Exists.
            foreach (DictionaryEntry entry in uniqueFileExists)
            {
                string path = (string)entry.Key;
                int count = (int)entry.Value;
                if (count > 1)
                {
                    string message = String.Format("File.Exists() was called {0} times with path {1}.", count, path);
                    Assert.True(false, message);
                }
            }


            uniqueFileExists = null;
            uniqueGetAssemblyName = null;
        }

        /// <summary>
        /// Stop monitoring IO calls and assert if any IO was used.
        /// </summary>
        internal void StopIOMonitoringAndAssert_Zero_IOUse()
        {
            // Check for minimal IO in File.Exists.
            foreach (DictionaryEntry entry in uniqueFileExists)
            {
                string path = (string)entry.Key;
                int count = (int)entry.Value;
                if (count > 0)
                {
                    string message = String.Format("File.Exists() was called {0} times with path {1}.", count, path);
                    Assert.True(false, message);
                }
            }


            // Check for zero IO in GetAssemblyName.
            foreach (DictionaryEntry entry in uniqueGetAssemblyName)
            {
                string path = (string)entry.Key;
                int count = (int)entry.Value;
                if (count > 0)
                {
                    string message = String.Format("GetAssemblyName() was called {0} times with path {1}.", count, path);
                    Assert.True(false, message);
                }
            }

            uniqueFileExists = null;
            uniqueGetAssemblyName = null;
        }

        private static List<string> s_existentFiles = new List<string>
        {
            @"c:\Frameworks\DependsOnFoo4Framework.dll",
            @"c:\Frameworks\DependsOnFoo45Framework.dll",
            @"c:\Frameworks\DependsOnFoo35Framework.dll",
            @"c:\Frameworks\IndirectDependsOnFoo45Framework.dll",
            @"c:\Frameworks\IndirectDependsOnFoo4Framework.dll",
            @"c:\Frameworks\IndirectDependsOnFoo35Framework.dll",
            Path.Combine(Path.GetTempPath(), @"RawFileNameRelative\System.Xml.dll"),
            Path.Combine(Path.GetTempPath(), @"RelativeAssemblyFiles\System.Xml.dll"),
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Data.dll",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.dll",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.pdb",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Xml.xml",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en\System.Xml.resources.dll",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en\System.Xml.resources.pdb",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en\System.Xml.resources.config",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\xx\System.Xml.resources.dll",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en-GB\System.Xml.resources.dll",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en-GB\System.Xml.resources.pdb",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\en-GB\System.Xml.resources.config",
            @"c:\MyProject\MyPrivateAssembly.exe",
            @"c:\MyProject\MyCopyLocalAssembly.dll",
            @"c:\MyProject\MyDontCopyLocalAssembly.dll",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\BadImage.dll",            // An assembly that will give a BadImageFormatException from GetAssemblyName
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\BadImage.pdb",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\MyGacAssembly.dll",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\MyGacAssembly.pdb",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\xx\MyGacAssembly.resources.dll",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.dll",
            @"c:\WINNT\Microsoft.NET\Framework\v4.0.MyVersion\System.dll",
            @"c:\WINNT\Microsoft.NET\Framework\v9.0.MyVersion\System.dll",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\mscorlib.dll",
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion.PocketPC\mscorlib.dll",
            @"C:\myassemblies\My.Assembly.dll",
            @"c:\MyProject\mscorlib.dll",                                            // This is an mscorlib.dll that has no metadata (i.e. GetAssemblyName returns null)
            @"c:\MyProject\System.Data.dll",                                         // This is a System.Data.dll that has the wrong pkt, it shouldn't be matched.
            @"C:\MyComponents\MyGrid.dll",                                           // A vendor component that we should find in the registry.
            @"C:\MyComponentsA\CustomComponent.dll",                                           // A vendor component that we should find in the registry.
            @"C:\MyComponentsB\CustomComponent.dll",                                           // A vendor component that we should find in the registry.
            @"C:\MyWinMDComponents7\MyGridWinMD.winmd",
            @"C:\MyWinMDComponents9\MyGridWinMD.winmd",
            @"C:\MyWinMDComponents\MyGridWinMD.winmd",
            @"C:\MyWinMDComponents2\MyGridWinMD.winmd",
            @"C:\MyWinMDComponentsA\CustomComponentWinMD.winmd",
            @"C:\MyWinMDComponentsB\CustomComponentWinMD.winmd",
            @"C:\MyWinMDComponentsVv1\MyGridWinMD2.winmd",
            @"C:\MyWinMDComponentsV1\MyGridWinMD3.winmd",
            @"C:\MyRawDropControls\MyRawDropControl.dll",                             // A control installed by VSREG under v2.0.x86chk
            @"C:\MyComponents\HKLM Components\MyHKLMControl.dll",                    // A vendor component that is installed under HKLM but not HKCU.
            @"C:\MyComponents\HKCU Components\MyHKLMandHKCUControl.dll",             // A vendor component that is installed under HKLM and HKCU.
            @"C:\MyComponents\HKLM Components\MyHKLMandHKCUControl.dll",             // A vendor component that is installed under HKLM and HKCU.
            @"C:\MyWinMDComponents\HKLM Components\MyHKLMControlWinMD.winmd",                    // A vendor component that is installed under HKLM but not HKCU.
            @"C:\MyWinMDComponents\HKCU Components\MyHKLMandHKCUControlWinMD.winmd",             // A vendor component that is installed under HKLM and HKCU.
            @"C:\MyWinMDComponents\HKLM Components\MyHKLMandHKCUControlWinMD.winmd",             // A vendor component that is installed under HKLM and HKCU.
            @"C:\MyComponents\v3.0\MyControlWithFutureTargetNDPVersion.dll",         // The future version of a component.
            @"C:\MyComponents\v2.0\MyControlWithFutureTargetNDPVersion.dll",         // The current version of a component.
            @"C:\MyComponents\v1.0\MyNDP1Control.dll",                               // A control that only has an NDP 1.0 version
            @"C:\MyComponents\v2.0\MyControlWithPastTargetNDPVersion.dll",           // The current version of a component.
            @"C:\MyComponents\v1.0\MyControlWithPastTargetNDPVersion.dll",           // The past version of a component.
            @"C:\MyComponentServicePack\MyControlWithServicePack.dll",               // The service pack 1 version of the control
            @"C:\MyComponentBase\MyControlWithServicePack.dll",                      // The non-service pack version of the control.
            @"C:\MyComponentServicePack2\MyControlWithServicePack.dll",              // The service pack 1 version of the control
            @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion.PocketPC\mscorlib.dll",  // A devices mscorlib. 
            @"c:\MyLibraries\A.dll",
            @"c:\MyExecutableLibraries\A.exe",
            @"c:\MyLibraries\B.dll",
            @"c:\MyLibraries\C.dll",
            @"c:\MyLibraries\v1\D.dll",
            @"c:\MyLibraries\v1\E\E.dll",
            @"c:\RogueLibraries\v1\D.dll",
            @"c:\MyLibraries\v2\D.dll",
            @"c:\MyStronglyNamed\A.dll",
            @"c:\MyWeaklyNamed\A.dll",
            @"c:\MyInaccessible\A.dll",
            @"c:\MyNameMismatch\Foo.dll",
            @"c:\MyEscapedName\=A=.dll",
            @"c:\MyEscapedName\__'ASP'dw0024ry.dll",
            @"c:\MyApp\DependsOnSimpleA.dll",
            @"C:\Regress312873\a.dll",
            @"C:\Regress312873\b.dll",
            @"C:\Regress312873-2\a.dll",
            @"C:\Regress275161\a.dll",
            @"C:\Regress317975\a.dll",
            @"C:\Regress317975\b.dll",
            @"C:\Regress317975\v2\b.dll",
            @"c:\Regress313086\mscorlib.dll",
            @"c:\V1Control\MyDeviceControlAssembly.dll",
            @"c:\V1ControlSP1\MyDeviceControlAssembly.dll",
            @"C:\Regress339786\FolderA\a.dll",
            @"C:\Regress339786\FolderA\c.dll", // v1 of c
            @"C:\Regress339786\FolderB\b.dll",
            @"C:\Regress339786\FolderB\c.dll", // v2 of c
            @"c:\OldClrBug\MyFileLoadExceptionAssembly.dll",
            @"c:\OldClrBug\DependsMyFileLoadExceptionAssembly.dll",
            @"c:\Regress563286\DependsOnBadImage.dll",
            @"C:\Regress407623\CrystalReportsAssembly.dll",
            @"C:\Regress435487\microsoft.build.engine.dll",
            @"C:\Regress313747\Microsoft.Office.Interop.Excel.dll",
            @"C:\Regress313747\MS.Internal.Test.Automation.Office.Excel.dll",
            @"C:\Regress442570\A.dll",
            @"C:\Regress442570\B.dll",
            @"C:\Regress454863\A.dll",
            @"C:\Regress454863\B.dll",
            @"C:\Regress393931\A.metadata_dll",
            @"c:\Regress387218\A.dll",
            @"c:\Regress387218\B.dll",
            @"c:\Regress387218\v1\D.dll",
            @"c:\Regress387218\v2\D.dll",
            @"c:\Regress390219\A.dll",
            @"c:\Regress390219\B.dll",
            @"c:\Regress390219\v1\D.dll",
            @"c:\Regress390219\v2\D.dll",
            @"c:\Regress315619\A\MyAssembly.dll",
            @"c:\Regress315619\B\MyAssembly.dll",
            @"c:\SGenDependeicies\mycomponent.dll",
            @"c:\SGenDependeicies\mycomponent.XmlSerializers.dll",
            @"c:\SGenDependeicies\mycomponent2.dll",
            @"c:\SGenDependeicies\mycomponent2.XmlSerializers.dll",
            @"c:\Regress315619\A\MyAssembly.dll",
            @"c:\Regress315619\B\MyAssembly.dll",
            @"c:\MyRedist\MyRedistRootAssembly.dll",
            @"c:\MyRedist\MyOtherAssembly.dll",
            @"c:\MyRedist\MyThirdAssembly.dll",
            // ==[Related File Extensions Testing]================================================================================================
            @"C:\AssemblyFolder\SomeAssembly.dll",
            @"C:\AssemblyFolder\SomeAssembly.pdb",
            @"C:\AssemblyFolder\SomeAssembly.xml",
            @"C:\AssemblyFolder\SomeAssembly.pri",
            @"C:\AssemblyFolder\SomeAssembly.licenses",
            @"C:\AssemblyFolder\SomeAssembly.config",
            // ==[Related File Extensions Testing]================================================================================================
                
            // ==[Unification Testing]============================================================================================================
            //@"C:\MyComponents\v0.5\UnifyMe.dll",                                 // For unification testing, a version that doesn't exist.
            @"C:\MyComponents\v1.0\UnifyMe.dll",
            @"C:\MyComponents\v2.0\UnifyMe.dll",
            @"C:\MyComponents\v3.0\UnifyMe.dll",
            //@"C:\MyComponents\v4.0\UnifyMe.dll",
            @"C:\MyApp\v0.5\DependsOnUnified.dll",
            @"C:\MyApp\v1.0\DependsOnUnified.dll",
            @"C:\MyApp\v2.0\DependsOnUnified.dll",
            @"C:\MyApp\v3.0\DependsOnUnified.dll",
            @"C:\MyApp\DependsOnWeaklyNamedUnified.dll",
            @"C:\MyApp\v1.0\DependsOnEverettSystem.dll",
            @"C:\Framework\Everett\System.dll",
            @"C:\Framework\Whidbey\System.dll",
            // ==[Unification Testing]============================================================================================================

            // ==[Test assemblies reference higher versions than the current target framework=====================================================
            @"c:\MyComponents\misc\DependsOnOnlyv4Assemblies.dll",  // Only depends on 4.0.0 assemblies
            @"c:\MyComponents\misc\ReferenceVersion9.dll", //Is in redist list and is a 9.0 assembly
            @"c:\MyComponents\misc\DependsOn9.dll", //Depends on 9.0 assemblies
            @"c:\MyComponents\misc\DependsOn9Also.dll", // Depends on 9.0 assemblies
            @"c:\MyComponents\1.0\DependsOn9.dll", // Depends on 9.0 assemblies
            @"c:\MyComponents\2.0\DependsOn9.dll", // Depends on 9.0 assemblies
            @"c:\Regress444809\A.dll",
            @"c:\Regress444809\v2\A.dll",
            @"c:\Regress444809\B.dll",
            @"c:\Regress444809\C.dll",
            @"c:\Regress444809\D.dll",
            @"c:\MyComponents\4.0Component\DependsOnOnlyv4Assemblies.dll",
            @"C:\Regress714052\MSIL\a.dll",
            @"C:\Regress714052\X86\a.dll",
            @"C:\Regress714052\NONE\a.dll",
            @"C:\Regress714052\Mix\a.dll",
            @"C:\Regress714052\Mix\a.winmd",
            @"C:\Regress714052\MSIL\b.dll",
            @"C:\Regress714052\X86\b.dll",
            @"C:\Regress714052\NONE\b.dll",
            @"C:\Regress714052\Mix\b.dll",
            @"C:\Regress714052\Mix\b.winmd",

            @"C:\MyComponents\V.dll",
            @"C:\MyComponents2\W.dll",
            @"C:\MyComponents\X.dll",
            @"C:\MyComponents\Y.dll",
            @"C:\MyComponents\Z.dll",

            @"C:\MyComponents\Microsoft.Build.dll",
            @"C:\MyComponents\DependsOnMSBuild12.dll",

            // WinMD sample files
            @"C:\WinMD\v4\mscorlib.dll",  // Fake 4.0 mscorlib so we can actually resolve it for one of the tests. With a version of 4
            @"C:\WinMD\v255\mscorlib.dll",  // Fake 4.0 mscorlib so we can actually resolve it for one of the tests. With a version of 255
            @"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll",
            @"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll",
            @"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeAndCLR.dll",
            @"C:\WinMD\SampleWindowsRuntimeAndOther.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeOnly.dll",
            @"C:\WinMD\SampleWindowsRuntimeOnly.pri",
            @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd",
            @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd",
            @"C:\WinMD\SampleClrOnly.Winmd",
            @"C:\WinMD\SampleBadWindowsRuntime.Winmd",
            @"C:\WinMD\WinMDWithVersion255.Winmd",
            @"C:\WinMDArchVerification\DependsOnInvalidPeHeader.Winmd",
            @"C:\WinMDArchVerification\DependsOnInvalidPeHeader.dll",
            @"C:\WinMDArchVerification\DependsOnAmd64.Winmd",
            @"C:\WinMDArchVerification\DependsOnAmd64.dll",
            @"C:\WinMDArchVerification\DependsOnArm.Winmd",
            @"C:\WinMDArchVerification\DependsOnArm.dll",
            @"C:\WinMDArchVerification\DependsOnArmv7.Winmd",
            @"C:\WinMDArchVerification\DependsOnArmv7.dll",
            @"C:\WinMDArchVerification\DependsOnX86.Winmd",
            @"C:\WinMDArchVerification\DependsOnX86.dll",
            @"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.Winmd",
            @"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.dll",
            @"C:\WinMDArchVerification\DependsOnIA64.Winmd",
            @"C:\WinMDArchVerification\DependsOnIA64.dll",
            @"C:\WinMDArchVerification\DependsOnUnknown.Winmd",
            @"C:\WinMDArchVerification\DependsOnUnknown.dll",
            @"C:\FakeSDK\References\Debug\X86\DebugX86SDKWinMD.Winmd",
            @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKWinMD.Winmd",
            @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKWinMD.Winmd",
            @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKWinMD.Winmd",
            @"C:\FakeSDK\References\Debug\X86\DebugX86SDKRA.dll",
            @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKRA.dll",
            @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKRA.dll",
            @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKRA.dll",
            @"C:\FakeSDK\References\Debug\X86\SDKReference.dll",
            @"C:\DirectoryContainsOnlyDll\a.dll",
            @"C:\DirectoryContainsdllAndWinmd\b.dll",
            @"C:\DirectoryContainsdllAndWinmd\c.winmd",
            @"C:\DirectoryContainstwoWinmd\a.winmd",
            @"C:\DirectoryContainstwoWinmd\c.winmd",
            @"C:\SystemRuntime\System.Runtime.dll",
            @"C:\SystemRuntime\Portable.dll",
            @"C:\SystemRuntime\Regular.dll",
        };

        /// <summary>
        /// Mocked up GetFiles.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static string[] GetFiles(string path, string pattern)
        {
            if (path.Length > 240)
            {
                throw new PathTooLongException();
            }

            string extension = null;
            if (pattern == "*.xml")
            {
                extension = ".xml";
            }
            else if (pattern == "*.pdb")
            {
                extension = ".pdb";
            }
            else
            {
                Assert.True(false, "Unsupported GetFiles pattern " + pattern);
            }

            ArrayList matches = new ArrayList();
            foreach (string file in s_existentFiles)
            {
                string baseDir = Path.GetDirectoryName(file);

                if (0 == String.Compare(baseDir, path, StringComparison.OrdinalIgnoreCase))
                {
                    string fileExtension = Path.GetExtension(file);

                    if (0 == String.Compare(fileExtension, extension, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(file);
                    }
                }
            }

            return (string[])matches.ToArray(typeof(string));
        }

        /// <summary>
        /// Reads the machine type out of the PEHeader of the native dll
        /// </summary>
        private static UInt16 ReadMachineTypeFromPEHeader(string dllPath)
        {
            if (@"C:\WinMDArchVerification\DependsOnInvalidPeHeader.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_INVALID;
            }
            else if (@"C:\WinMDArchVerification\DependsOnAmd64.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_AMD64;
            }
            else if (@"C:\WinMDArchVerification\DependsOnX86.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_I386;
            }
            else if (@"C:\WinMDArchVerification\DependsOnArm.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_ARM;
            }
            else if (@"C:\WinMDArchVerification\DependsOnArmV7.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_ARMV7;
            }
            else if (@"C:\WinMDArchVerification\DependsOnIA64.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_IA64;
            }
            else if (@"C:\WinMDArchVerification\DependsOnUnknown.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_R4000;
            }
            else if (@"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_UNKNOWN;
            }
            else if (@"C:\WinMD\SampleWindowsRuntimeOnly.dll".Equals(dllPath, StringComparison.OrdinalIgnoreCase))
            {
                return NativeMethods.IMAGE_FILE_MACHINE_I386;
            }

            return NativeMethods.IMAGE_FILE_MACHINE_INVALID;
        }

        /// <summary>
        ///  Checks to see if the file is a winmd file.
        /// </summary>
        private static bool IsWinMDFile(string fullPath, GetAssemblyRuntimeVersion getAssemblyRuntimeVersion, FileExists fileExists, out string imageRuntimeVersion, out bool isManagedWinMD)
        {
            imageRuntimeVersion = getAssemblyRuntimeVersion(fullPath);
            isManagedWinMD = false;

            if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                isManagedWinMD = true;
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\WinMDWithVersion255.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (fullPath.StartsWith(@"C:\MyWinMDComponents", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\FakeSDK\WindowsMetadata\SDKWinMD2.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else if (fullPath.StartsWith(@"C:\DirectoryContains", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(fullPath).Equals(".winmd", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (fullPath.StartsWith(@"C:\WinMDArchVerification", StringComparison.OrdinalIgnoreCase) && Path.GetExtension(fullPath).Equals(".winmd", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else if (String.Compare(fullPath, @"C:\FakeSDK\WindowsMetadata\SDKWinMD.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Checks to see if the assemblyName passed in is in the GAC.
        /// </summary>
        private static string GetPathForAssemblyInGac(AssemblyNameExtension assemblyName, ProcessorArchitecture targetProcessorArchitecture, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVersion, FileExists fileExists, bool fullFusionName, bool specificVersion)
        {
            if (assemblyName.Equals(new AssemblyNameExtension("V, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")))
            {
                return null;
            }
            else if (assemblyName.Equals(new AssemblyNameExtension("W, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")))
            {
                return @"C:\MyComponents2\W.dll";
            }
            else if (assemblyName.Equals(new AssemblyNameExtension("Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")))
            {
                return null;
            }
            else if (assemblyName.Equals(new AssemblyNameExtension("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")))
            {
                return @"C:\MyComponents\X.dll";
            }
            else if (assemblyName.Equals(new AssemblyNameExtension("Y, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")))
            {
                return null;
            }
            else
            {
                string gacLocation = null;
                if (assemblyName.Version != null)
                {
                    gacLocation = GlobalAssemblyCache.GetLocation(assemblyName, targetProcessorArchitecture, getRuntimeVersion, targetedRuntimeVersion, fullFusionName, fileExists, null, null, specificVersion /* this value does not matter if we are passing a full fusion name*/);
                }
                return gacLocation;
            }
        }

        /// <summary>
        /// Mock the File.Exists method.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>'true' if the file is supposed to exist</returns>
        internal static bool FileExists(string path)
        {
            // For very long paths, File.Exists just returns false
            if (path.Length > 240)
            {
                return false;
            }

            // Do a real File.Exists to make it throw exceptions for illegal paths.
            if (File.Exists(path) && useFrameworkFileExists)
            {
                return true;
            }

            // Do IO monitoring if needed.
            if (uniqueFileExists != null)
            {
                string lowerPath = path.ToLower();
                if (uniqueFileExists[lowerPath] == null)
                {
                    uniqueFileExists[lowerPath] = 0;
                }
                else
                {
                    uniqueFileExists[lowerPath] = (int)uniqueFileExists[lowerPath] + 1;
                }
            }


            // First, MyMissingAssembly doesn't exist anywhere.
            if (path.IndexOf("MyMissingAssembly") != -1)
            {
                return false;
            }

            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path);
            }


            foreach (string file in s_existentFiles)
            {
                if (0 == String.Compare(path, file, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }


            // Everything else doesn't exist.
            return false;
        }

        /// <summary>
        /// Mock the Directory.Exists method.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>'true' if the directory is supposed to exist</returns>
        internal static bool DirectoryExists(string path)
        {
            // Now specify the remaining files.
            string[] existentDirs = new string[]
            {
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion",
                @"c:\SGenDependeicies",
                Path.GetTempPath()
            };

            foreach (string dir in existentDirs)
            {
                if (0 == String.Compare(path, dir, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Everything else doesn't exist.
            return false;
        }

        /// <summary>
        /// A mock delagate for Directory.GetDirectories. 
        /// </summary>
        /// <param name="file">The file path.</param>
        /// <param name="file">The file pattern.</param>
        /// <returns>A set of subdirectories</returns>
        internal static string[] GetDirectories(string path, string pattern)
        {
            if (path.EndsWith(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion"))
            {
                string[] paths = new string[] {
                    Path.Combine(path, "en"), Path.Combine(path, "en-GB"), Path.Combine(path, "xx")
                };

                return paths;
            }
            else if (String.Compare(path, @".", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Pretend the current directory has a few subfolders.
                return new string[] {
                    Path.Combine(path, "en"), Path.Combine(path, "en-GB"), Path.Combine(path, "xx")
                };
            }

            return new string[0];
        }

        /// <summary>
        /// Given a path return the corosponding CLR runtime version
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <returns>Image runtime version</returns>
        internal static string GetRuntimeVersion(string path)
        {
            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "WindowsRuntime 1.0, CLR V2.0.50727";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\WinMDWithVersion255.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleClrOnly.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "CLR V2.0.50727";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleBadWindowsRuntime.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "Windows Runtime";
            }
            else if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeAndOther.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "WindowsRuntime 1.0, Other V2.0.50727";
            }

            else if (String.Compare(path, @"C:\DirectoryContainsOnlyDll\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "V2.0.50727";
            }
            else if (String.Compare(path, @"C:\DirectoryContainsdllAndWinmd\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "V2.0.50727";
            }
            else if (String.Compare(path, @"C:\DirectoryContainsdllAndWinmd\c.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\DirectoryContainstwoWinmd\a.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "WindowsRuntime 1.0";
            }
            else if (String.Compare(path, @"C:\DirectoryContainstwoWinmd\c.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return "WindowsRuntime 1.0";
            }
            else if (path.StartsWith(@"C:\MyWinMDComponents", StringComparison.OrdinalIgnoreCase))
            {
                return "Windows Runtime";
            }
            else if (path.StartsWith(@"C:\WinMDArchVerification", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".winmd"))
            {
                return "WindowsRuntime 1.0";
            }
            else if (path.EndsWith(".dll") || path.EndsWith(".exe") || path.EndsWith(".winmd"))
            {
                return "v2.0.50727";
            }
            else
            {
                return "";
            }
        }

        /// <summary>
        /// Given a path, return the corresponding AssemblyName
        /// </summary>
        /// <param name="path">Path to the assembly.</param>
        /// <returns>The assemblyname.</returns>
        internal static AssemblyNameExtension GetAssemblyName(string path)
        {
            // Do IO monitoring if needed.
            if (uniqueGetAssemblyName != null)
            {
                string lowerPath = path.ToLower();
                if (uniqueGetAssemblyName[lowerPath] == null)
                {
                    uniqueGetAssemblyName[lowerPath] = 0;
                }
                else
                {
                    uniqueGetAssemblyName[lowerPath] = (int)uniqueGetAssemblyName[lowerPath] + 1;
                }
            }

            // For very long paths, GetAssemblyName throws an exception.
            if (path.Length > 240)
            {
                throw new FileNotFoundException(path);
            }

            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path);
            }

            if
                (
                String.Compare(path, @"c:\OldClrBug\MyFileLoadExceptionAssembly.dll", StringComparison.OrdinalIgnoreCase) == 0
                )
            {
                // An older LKG of the CLR could throw a FileLoadException if it doesn't recognize
                // the assembly. We need to support this for dogfooding purposes.
                throw new FileLoadException("Could not load " + path);
            }

            if
                (
                String.Compare(path, @"c:\Regress313086\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
                )
            {
                // This is an mscorlib that returns null for its assembly name.
                return null;
            }

            if
                (
                String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\BadImage.dll", StringComparison.OrdinalIgnoreCase) == 0
                )
            {
                throw new System.BadImageFormatException(@"The format of the file 'c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\BadImage.dll' is invalid");
            }

            if
                (
                String.Compare(path, @"c:\MyProject\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
                || String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
                || String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion.PocketPC\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
                )
            {
                // This is an mscorlib.dll with no metadata.
                return null;
            }

            if
                (
                String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
                || String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion.PocketPC\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
                )
            {
                // This is an mscorlib.dll with no metadata.
                return null;
            }

            if (path.Contains("MyMissingAssembly"))
            {
                throw new FileNotFoundException(path);
            }

            if (String.Compare(path, @"c:\Frameworks\DependsOnFoo45Framework.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnFoo45Framework, Version=4.5.0.0, PublicKeyToken=null, Culture=Neutral");
            }

            if (String.Compare(path, @"c:\Frameworks\DependsOnFoo4Framework.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnFoo4Framework, Version=4.0.0.0, PublicKeyToken=null, Culture=Neutral");
            }

            if (String.Compare(path, @"c:\Frameworks\DependsOnFoo35Framework.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnFoo35Framework, Version=3.5.0.0, PublicKeyToken=null, Culture=Neutral");
            }

            if (String.Compare(path, @"c:\Regress315619\A\MyAssembly.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyAssembly, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\Regress315619\B\MyAssembly.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyAssembly, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\Regress442570\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
            }
            if (String.Compare(path, @"c:\Regress387218\v1\D.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("D, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\Regress442570\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\Regress387218\v2\D.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("D, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\Regress390219\v1\D.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("D, Version=1.0.0.0, Culture=fr, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\Regress390219\v2\D.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("D, Version=2.0.0.0, Culture=en, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\Regress442570\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\Regress442570\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\MyStronglyNamed\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\MyNameMismatch\Foo.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Notice the metadata assembly name does not match the base file name.
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\MyEscapedName\=A=.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Notice the metadata assembly name does not match the base file name.
                return new AssemblyNameExtension("\\=A\\=, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089", true);
            }

            if (String.Compare(path, @"c:\MyEscapedName\__'ASP'dw0024ry.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Notice the metadata assembly name does not match the base file name.
                return new AssemblyNameExtension("__\\'ASP\\'dw0024ry", true);
            }

            if (String.Compare(path, @"c:\MyInaccessible\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate an assembly that throws an UnauthorizedAccessException upon access.
                throw new UnauthorizedAccessException();
            }

            if (String.Compare(path, Path.Combine(Path.GetTempPath(), @"RawFileNameRelative\System.Xml.dll"), StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension(AssemblyRef.SystemXml);
            }

            if (String.Compare(path, Path.Combine(Path.GetTempPath(), @"RelativeAssemblyFiles\System.Xml.dll"), StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension(AssemblyRef.SystemXml);
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.XML.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension(AssemblyRef.SystemXml);
            }

            // This is an assembly with an earlier version.
            if (String.Compare(path, @"c:\MyProject\System.Xml.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension(AssemblyRef.SystemXml);
            }

            // This is an assembly with an incorrect PKT.
            if (String.Compare(path, @"c:\MyProject\System.Data.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=A77a5c561934e089");
            }

            if (path.EndsWith(@"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\MyGacAssembly.dll"))
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGacAssembly, Version=9.2.3401.1, Culture=neutral, PublicKeyToken=a6694b450823df78");
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("System, VeRSion=2.0.0.0, Culture=neutRAl, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v4.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("System, VeRSion=4.0.0.0, Culture=neutRAl, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v9.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("System, VeRSion=9.0.0.0, Culture=neutRAl, PublicKeyToken=b77a5c561934e089");
            }

            if
                (
                String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.Data.dll", StringComparison.OrdinalIgnoreCase) == 0
                )
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension(AssemblyRef.SystemData);
            }

            if (path.EndsWith(@"c:\MyLibraries\v1\D.dll"))
            {
                // Version 1 of D
                return new AssemblyNameExtension("D, Version=1.0.0.0, CulTUre=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa");
            }

            if (path.EndsWith(@"c:\RogueLibraries\v1\D.dll"))
            {
                // Version 1 of D, but with a different PKT
                return new AssemblyNameExtension("D, VERsion=1.0.0.0, Culture=neutral, PublicKeyToken=bbbbbbbbbbbbbbbb");
            }

            if (path.EndsWith(@"c:\MyLibraries\v1\E\E.dll"))
            {
                return new AssemblyNameExtension("E, Version=0.0.0.0, Culture=neutral, PUBlicKeyToken=null");
            }


            if (String.Compare(path, @"C:\MyComponents\v0.5\UnifyMe.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                throw new FileNotFoundException();
            }

            if (String.Compare(path, @"C:\MyComponents\v1.0\UnifyMe.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("UnifyMe, Version=1.0.0.0, Culture=nEUtral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
            }

            if (String.Compare(path, @"C:\Framework\Everett\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("System, Version=1.0.5000.0, Culture=neutral, PublICKeyToken=" + AssemblyRef.EcmaPublicKey);
            }

            if (String.Compare(path, @"C:\Framework\Whidbey\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=" + AssemblyRef.EcmaPublicKey);
            }


            if (String.Compare(path, @"C:\MyApp\v1.0\DependsOnEverettSystem.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnEverettSystem, VersION=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe");
            }

            if (String.Compare(path, @"C:\MyApp\v0.5\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnUnified, Version=0.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\Regress339786\FolderA\C.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("C, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\Regress339786\FolderB\C.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("C, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\MyApp\v1.0\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnUnified, VERSion=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\MyApp\v2.0\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnUnified, VeRSIon=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\MyApp\v3.0\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnUnified, Version=3.0.0.0, Culture=neutral, PublicKEYToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\MyComponents\v2.0\UnifyMe.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyTOKEn=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\MyComponents\v3.0\UnifyMe.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("UnifyMe, Version=3.0.0.0, Culture=neutral, PublICkeyToken=b77a5c561934e089");
            }

            if (path.EndsWith(@"c:\MyLibraries\v2\D.dll"))
            {
                return new AssemblyNameExtension("D, VErsion=2.0.0.0, CulturE=neutral, PublicKEyToken=aaaaaaaaaaaaaaaa");
            }

            if (String.Compare(path, @"C:\Regress317975\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            if (String.Compare(path, @"C:\Regress317975\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            if (String.Compare(path, @"C:\Regress317975\v2\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            // Set up assembly names for testing target framework version checks
            // Is version 4 and will only depends on 4.0 assemblies
            if (String.Compare(path, @"c:\MyComponents\4.0Component\DependsOnOnlyv4Assemblies.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnOnlyv4Assemblies, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            }

            // Is version 9 and will not have any dependencies, will be in the redist list
            if (String.Compare(path, @"c:\MyComponents\misc\ReferenceVersion9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("ReferenceVersion9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            }

            // Is a third party assembly which depends on a version 9 assembly
            if (String.Compare(path, @"c:\MyComponents\misc\DependsOn9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            }

            //A second assembly which depends on version 9 framework assemblies.
            if (String.Compare(path, @"c:\MyComponents\misc\DependsOn9Also.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOn9Also, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            }

            if (String.Compare(path, @"c:\MyComponents\1.0\DependsOn9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOn9, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            }

            if (String.Compare(path, @"c:\MyComponents\2.0\DependsOn9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOn9, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            }

            if (String.Compare(path, @"c:\Regress444809\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\Regress444809\v2\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\Regress444809\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("B, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\Regress444809\C.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("C, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\Regress444809\D.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("D, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\Regress714052\X86\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86");
            }
            if (String.Compare(path, @"C:\Regress714052\Mix\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86");
            }
            if (String.Compare(path, @"C:\Regress714052\Mix\a.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL");
            }

            if (String.Compare(path, @"C:\Regress714052\MSIL\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL");
            }

            if (String.Compare(path, @"C:\Regress714052\None\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            if (String.Compare(path, @"C:\Regress714052\X86\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86");
            }
            if (String.Compare(path, @"C:\Regress714052\Mix\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86");
            }
            if (String.Compare(path, @"C:\Regress714052\Mix\b.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL");
            }
            if (String.Compare(path, @"C:\Regress714052\MSIL\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL");
            }
            if (String.Compare(path, @"C:\Regress714052\None\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            if (String.Compare(path, @"c:\MyComponents\V.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("V, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            if (String.Compare(path, @"c:\MyComponents2\W.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("W, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            if (String.Compare(path, @"c:\MyComponents\X.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\MyComponents\Z.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\MyComponents\Y.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("Y, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"c:\MyComponents\Microsoft.Build.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("Microsoft.Build, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            }

            if (String.Compare(path, @"c:\MyComponents\DependsOnMSBuild12.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension("DependsOnMSBuild12, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\WinMD\v4\MsCorlib.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("mscorlib, Version=4.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\WinMD\v255\MsCorlib.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
            }

            if (String.Compare(path, @"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DotNetAssemblyDependsOnWinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DotNetAssemblyDependsOn255WinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeOnly, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnInvalidPeHeader.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnInvalidPeHeader, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnAmd64.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnAmd64, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnArm.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnArm, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnIA64.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnIA64, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnArmv7.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnArmv7, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnX86.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnX86, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnUnknown.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnUnknown, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DependsOnAnyCPUUnknown, Version=1.0.0.0");
            }
            if (String.Compare(path, @"C:\WinMD\WinMDWithVersion255.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("WinMDWithVersion255, Version=255.255.255.255");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeOnly2, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeOnly3, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeOnly4, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeReferencingSystem, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeReferencingSystemDNE, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SampleWindowsRuntimeAndCLR, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\MyWinMDComponents\MyGridWinMD.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGridWinMD, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\MyWinMDComponents2\MyGridWinMD.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGridWinMD, Version=2.0.0.0");
            }

            if (String.Compare(path, @"C:\MyWinMDComponent7s\MyGridWinMD.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGridWinMD, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\MyWinMDComponents9\MyGridWinMD.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGridWinMD, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\MyWinMDComponentsVv1\MyGridWinMD2.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGridWinMD2, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\MyWinMDComponentsV1\MyGridWinMD3.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("MyGridWinMD3, Version=1.0.0.0");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\Debug\X86\DebugX86SDKWinMD.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DebugX86SDKWinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKWinMD.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DebugNeutralSDKWinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKWinMD.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("X86SDKWinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKWinMD.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("NeutralSDKWINMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\Debug\X86\DebugX86SDKRA.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("Debugx86SDKRA, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKRA.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("DebugNeutralSDKRA, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKRA.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("X86SDKRA, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKRA.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("NeutralSDKRA, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\FakeSDK\References\Debug\X86\SDKReference.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("SDKReference, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\DirectoryContainsOnlyDll\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\DirectoryContainsdllAndWinmd\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("b, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\DirectoryContainsdllAndWinmd\c.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("C, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\DirectoryContainstwoWinmd\a.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("A, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            if (String.Compare(path, @"C:\DirectoryContainstwoWinmd\c.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension("C, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            string defaultName = String.Format("{0}, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral", Path.GetFileNameWithoutExtension(path));
            return new AssemblyNameExtension(defaultName);
        }


        /// <summary>
        /// Cached implementation. Given an assembly name, crack it open and retrieve the list of dependent 
        /// assemblies and  the list of scatter files.
        /// </summary>
        /// <param name="path">Path to the assembly.</param>
        /// <param name="dependencies">Receives the list of dependencies.</param>
        /// <param name="scatterFiles">Receives the list of associated scatter files.</param>
        internal static void GetAssemblyMetadata
            (
            string path,
            out AssemblyNameExtension[] dependencies,
            out string[] scatterFiles,
            out FrameworkName frameworkName
            )
        {
            dependencies = GetDependencies(path);
            scatterFiles = null;
            frameworkName = GetTargetFrameworkAttribute(path);

            if (@"C:\Regress275161\a.dll" == path)
            {
                scatterFiles = new string[]
                {
                    @"m1.netmodule",
                    @"m2.netmodule"
                };
            }
        }

        /// <summary>
        /// Cached implementation. Given an assembly name, crack it open and retrieve the TargetFrameworkAttribute
        /// </summary>
        internal static FrameworkName GetTargetFrameworkAttribute
            (
            string path
            )
        {
            FrameworkName frameworkName = null;

            if (String.Equals(path, @"c:\Frameworks\DependsOnFoo4Framework.dll", StringComparison.OrdinalIgnoreCase))
            {
                frameworkName = new FrameworkName("FoO, Version=v4.0");
            }
            else if (String.Equals(path, @"c:\Frameworks\DependsOnFoo45Framework.dll", StringComparison.OrdinalIgnoreCase))
            {
                frameworkName = new FrameworkName("FoO, Version=v4.5");
            }
            else if (String.Equals(path, @"c:\Frameworks\DependsOnFoo35Framework.dll", StringComparison.OrdinalIgnoreCase))
            {
                frameworkName = new FrameworkName("FoO, Version=v3.5");
            }
            else if (String.Equals(path, @"c:\Frameworks\IndirectDependsOnFoo4Framework.dll", StringComparison.OrdinalIgnoreCase))
            {
                frameworkName = new FrameworkName("FoO, Version=v4.0");
            }
            else if (String.Equals(path, @"c:\Frameworks\IndirectDependsOnFoo45Framework.dll", StringComparison.OrdinalIgnoreCase))
            {
                frameworkName = new FrameworkName("FoO, Version=v4.0");
            }
            else if (String.Equals(path, @"c:\Frameworks\IndirectDependsOnFoo35Framework.dll", StringComparison.OrdinalIgnoreCase))
            {
                frameworkName = new FrameworkName("FoO, Version=v4.0");
            }

            return frameworkName;
        }

        /// <summary>
        /// Given an assembly, with optional assemblyName return all of the dependent assemblies.
        /// </summary>
        /// <param name="path">The full path to the parent assembly</param>
        /// <returns>The array of dependent assembly names.</returns>
        internal static AssemblyNameExtension[] GetDependencies(string path)
        {
            if (String.Compare(path, @"c:\Frameworks\IndirectDependsOnFoo4Framework.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("DependsOnFoo4Framework, Version=4.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"c:\Frameworks\IndirectDependsOnFoo45Framework.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("DependsOnFoo45Framework, Version=4.5.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"c:\Frameworks\IndirectDependsOnFoo35Framework.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("DependsOnFoo35Framework, Version=3.5.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress454863\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("B, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress442570\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension(" A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\Regress313747\Microsoft.Office.Interop.Excel.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension(" Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c")
                };
            }

            if (String.Compare(path, @"C:\Regress313747\MS.Internal.Test.Automation.Office.Excel.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension(" Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=94de0004b6e3fcc5")
                };
            }

            if (String.Compare(path, @"c:\Regress387218\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, Version=1.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"c:\Regress387218\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, Version=2.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"c:\Regress390219\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, Version=1.0.0.0, PublicKeyToken=b77a5c561934e089, Culture=fr")
                };
            }

            if (String.Compare(path, @"c:\Regress390219\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, Version=2.0.0.0,  PublicKeyToken=b77a5c561934e089, Culture=en")
                };
            }

            if (String.Compare(path, @"C:\Regress454863\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("B, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress442570\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension(" A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\Regress313747\Microsoft.Office.Interop.Excel.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension(" Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c")
                };
            }

            if (String.Compare(path, @"C:\Regress313747\MS.Internal.Test.Automation.Office.Excel.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension(" Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=94de0004b6e3fcc5")
                };
            }

            if (String.Compare(path, @"c:\OldClrBug\DependsMyFileLoadExceptionAssembly.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("MyFileLoadExceptionAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\Regress563286\DependsOnBadImage.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("BadImage, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"c:\MyInaccessible\A.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                throw new UnauthorizedAccessException();
            }

            if (String.Compare(path, @"c:\Regress313086\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[] { };
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("SampleWindowsRuntimeOnly, Version=1.0.0.0")
                };
            }

            if (String.Compare(path, @"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("WinMDWithVersion255, Version=255.255.255.255")
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeAndClr.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=4.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\WinMD\WinMDWithVersion255.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("SampleWindowsRuntimeOnly, Version=1.0.0.0")
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new AssemblyNameExtension("SampleWindowsRuntimeOnly, Version=1.0.0.0"),
                    new AssemblyNameExtension("SampleWindowsRuntimeReferencingSystem, Version=1.0.0.0"),
                    new AssemblyNameExtension("WinMDWithVersion255, Version=255.255.255.255")
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new AssemblyNameExtension("SampleWindowsRuntimeReferencingSystemDNE, Version=1.0.0.0"),
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new AssemblyNameExtension("System, Version=255.255.255.255, Culture=Neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new AssemblyNameExtension("System.DoesNotExist, Version=255.255.255.255")
                };
            }

            if
                (
                String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
                || String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion.PocketPC\mscorlib.dll", StringComparison.OrdinalIgnoreCase) == 0
                )
            {
                return new AssemblyNameExtension[]
                {
                };
            }

            if (String.Compare(path, @"MyRelativeAssembly.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                };
            }

            if (String.Compare(path, @"c:\MyApp\DependsOnSimpleA.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("A, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress312873\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("A, Version=0.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress339786\FolderA\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress339786\FolderB\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("C, Version=2.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\Regress317975\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("B, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\myassemblies\My.Assembly.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=2.0.0.0, Culture=NEUtraL, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\MyComponents\MyGrid.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, VeRsIon=2.0.0.0, Culture=neuTRal, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\MyRawDropControls\MyRawDropControl.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, VeRsIon=2.0.0.0, Culture=neuTRal, PublicKeyToken=b77a5c561934e089")
                };
            }


            if (String.Compare(path, @"c:\MyLibraries\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, Version=1.0.0.0, CuLtUrE=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
                };
            }

            if (String.Compare(path, @"c:\MyLibraries\t.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, VeRsIon=1.0.0.0, Culture=neutral, PublicKeyToken=bbbbbbbbbbbbbbbb")
                };
            }

            if (String.Compare(path, @"c:\MyLibraries\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("D, Version=2.0.0.0, Culture=neutral, PuBlIcKeYToken=aaaaaaaaaaaaaaaa")
                };
            }

            if (String.Compare(path, @"c:\MyLibraries\v1\d.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("E, VERSIOn=0.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\MyLibraries\v2\d.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("E, Version=0.0.0.0, Culture=neutRAL, PUblicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\MyLibraries\v1\E\E.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                };
            }

            if (String.Compare(path, @"C:\MyApp\v0.5\DependsOnWeaklyNamedUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("UnifyMe, Version=0.0.0.0, PUBLICKeyToken=null, CuLTURE=Neutral")
                };
            }

            if (String.Compare(path, @"C:\MyApp\v1.0\DependsOnEverettSystem.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("System, VeRsiON=1.0.5000.0, Culture=neutral, PublicKeyToken="+AssemblyRef.EcmaPublicKey)
                };
            }

            if (String.Compare(path, @"C:\MyApp\v0.5\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("UnifyMe, Version=0.5.0.0, CuLTUre=neUTral, PubLICKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\MyApp\v1.0\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("UNIFyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\MyApp\v2.0\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("UniFYme, Version=2.0.0.0, Culture=NeutraL, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\MyApp\v3.0\DependsOnUnified.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("UnIfyMe, Version=3.0.0.0, Culture=nEutral, PublicKEyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\MyProject\MyMissingAssembly.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                throw new FileNotFoundException(path);
            }

            // Set up assembly names for testing target framework version checks
            // Is version 4 and will only depends on 4.0 assemblies
            if (String.Compare(path, @"c:\MyComponents\4.0Component\DependsOnOnlyv4Assemblies.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            // Is version 9 and will not have any dependencies, will be in the redist list
            if (String.Compare(path, @"c:\MyComponents\misc\ReferenceVersion9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("mscorlib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new AssemblyNameExtension("RandomAssembly, Version=9.0.0.0, Culture=neutral, PublicKeyToken=c77a5c561934e089")
                };
            }

            // Is a third party assembly which depends on a version 9 assembly
            if (String.Compare(path, @"c:\MyComponents\misc\DependsOn9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new AssemblyNameExtension("System.Data, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            //A second assembly which depends on version 9 framework assemblies.
            if (String.Compare(path, @"c:\MyComponents\misc\DependsOn9Also.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\MyComponents\1.0\DependsOn9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\MyComponents\2.0\DependsOn9.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\Regress444809\C.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("B, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"),
                    new AssemblyNameExtension("A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\Regress444809\B.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\Regress444809\D.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("A, Version=20.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\MyComponents\V.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("W, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\MyComponents2\W.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[] { };
            }

            if (String.Compare(path, @"c:\MyComponents\X.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\MyComponents\Z.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[] { };
            }

            if (String.Compare(path, @"c:\MyComponents\Y.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                };
            }

            if (String.Compare(path, @"c:\MyComponents\Microsoft.Build.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[] { };
            }

            if (String.Compare(path, @"c:\MyComponents\DependsOnMSBuild12.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("Microsoft.Build, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")
                };
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v4.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"c:\WINNT\Microsoft.NET\Framework\v9.0.MyVersion\System.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089")
                };
            }

            if (String.Compare(path, @"C:\DirectoryContainsOnlyDll\a.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\DirectoryContainsdllAndWinmd\b.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\DirectoryContainsdllAndWinmd\c.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[0];
            }

            if (String.Compare(path, @"C:\DirectoryContainstwoWinmd\a.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                    new AssemblyNameExtension("C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral")
                };
            }

            if (String.Compare(path, @"C:\DirectoryContainstwoWinmd\c.winmd", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return new AssemblyNameExtension[0];
            }

            if (path.StartsWith(@"C:\FakeSDK\", StringComparison.OrdinalIgnoreCase))
            {
                return new AssemblyNameExtension[0];
            }

            if (String.Compare(path, @"C:\SystemRuntime\Portable.dll", StringComparison.OrdinalIgnoreCase) == 0)
            {
                // Simulate a strongly named assembly.
                return new AssemblyNameExtension[]
                {
                    GetAssemblyName(@"C:\SystemRuntime\System.Runtime.dll")
                };
            }


            // Use a default list.
            return new AssemblyNameExtension[]
            {
                new AssemblyNameExtension("SysTem, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77A5c561934e089"),
                new AssemblyNameExtension("msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089")
            };
        }

        /// <summary>
        /// Registry access delegate. Given a hive and a view, return the registry base key.
        /// </summary>
        private static RegistryKey GetBaseKey(RegistryHive hive, RegistryView view)
        {
            if (hive == RegistryHive.CurrentUser)
            {
                return Registry.CurrentUser;
            }
            else if (hive == RegistryHive.LocalMachine)
            {
                return Registry.LocalMachine;
            }

            return null;
        }

        /// <summary>
        /// Simplified registry access delegate. Given a baseKey and a subKey, get all of the subkey
        /// names.
        /// </summary>
        /// <param name="baseKey">The base registry key.</param>
        /// <param name="subKey">The subkey</param>
        /// <returns>An enumeration of strings.</returns>
        private static IEnumerable<string> GetRegistrySubKeyNames(RegistryKey baseKey, string subKey)
        {
            if (baseKey == Registry.CurrentUser)
            {
                if (String.Compare(subKey, @"Software\Regress714052", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\X86", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\MSIL", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\Mix", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\Mix\Mix", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\None", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\X86\X86", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\MSIL\MSIL", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\None\None", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "", "vBogusVersion", "v1.a.2.3", "v1.0", "v3.0", "v2.0.50727", "v2.0.x86chk", "RandomJunk" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "ZControlA", "ZControlB", "Infragistics.GridControl.1.0", "Infragistics.MyHKLMControl.1.0", "Infragistics.MyControlWithFutureTargetNDPVersion.1.0", "Infragistics.MyControlWithPastTargetNDPVersion.1.0", "Infragistics.MyControlWithServicePack.1.0" };
                }
                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.x86chk\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "RawDropControls" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v3.0\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "Infragistics.MyControlWithFutureTargetNDPVersion.1.0" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "Infragistics.MyNDP1Control.1.0", "Infragistics.MyControlWithPastTargetNDPVersion.1.0" };
                }

                if
                    (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.GridControl.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v3.0\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyNDP1Control.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.x86chk\AssemblyFoldersEx\RawDropControls", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlA", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlB", StringComparison.OrdinalIgnoreCase) == 0
                    )
                {
                    return new string[] { };
                }

                if
                    (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    )
                {
                    // This control has a service pack
                    return new string[] { "sp1", "sp2" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "v2.0.3600" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework\v2.0.3600", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "PocketPC" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "AFETestDeviceControl" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "1234" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\Microsoft SDKs", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "Windows" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\Microsoft SDKs\Windows", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "7.0", "8.0", "v8.0", "9.0" };
                }
            }

            if (baseKey == Registry.LocalMachine)
            {
                if (String.Compare(subKey, @"Software\Regress714052", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "v2.0.0" };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "A", "B" };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\AssemblyFoldersEx\A", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\AssemblyFoldersEx\B", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\X86", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "X86" };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\MSIL", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "MSIL" };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\None", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "None" };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\Mix", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "Mix" };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\Mix\Mix", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\X86\X86", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\MSIL\MSIL", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\None\None", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "vBogusVersion", "v2.0.50727" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "Infragistics.FancyControl.1.0", "Infragistics.MyHKLMControl.1.0" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.FancyControl.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "v2.0.3600" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework\v2.0.3600", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "PocketPC" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { };
                }

                if (String.Compare(subKey, @"Software\Microsoft\Microsoft SDKs\Windows", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "8.0" };
                }
            }

            Console.WriteLine("subKey={0}", subKey);
            Assert.True(false, "New GetRegistrySubKeyNames parameters encountered, need to add unittesting support");
            return null;
        }

        /// <summary>
        /// Simplified registry access delegate. Given a baseKey and subKey, get the default value
        /// of the subKey.
        /// </summary>
        /// <param name="baseKey">The base registry key.</param>
        /// <param name="subKey">The subkey</param>
        /// <returns>A string containing the default value.</returns>
        private static string GetRegistrySubKeyDefaultValue(RegistryKey baseKey, string subKey)
        {
            if (baseKey == Registry.CurrentUser)
            {
                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlA", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponentsA";
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlB", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponentsB";
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.GridControl.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponents";
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.x86chk\AssemblyFoldersEx\RawDropControls", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyRawDropControls";
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponents\HKCU Components";
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v3.0\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponents\v3.0";
                }

                if
                    (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    )
                {
                    return @"C:\MyComponents\v2.0";
                }

                if
                    (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    )
                {
                    return @"C:\MyComponentBase";
                }

                if
                    (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0\sp1", StringComparison.OrdinalIgnoreCase) == 0
                    )
                {
                    return @"C:\MyComponentServicePack1";
                }

                if
                    (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0\sp2", StringComparison.OrdinalIgnoreCase) == 0
                    )
                {
                    return @"C:\MyComponentServicePack2";
                }

                if
                    (
                    String.Compare(subKey, @"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyNDP1Control.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    || String.Compare(subKey, @"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0", StringComparison.OrdinalIgnoreCase) == 0
                    )
                {
                    return @"C:\MyComponents\v1.0";
                }

                if (String.Compare(subKey, @"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\V1Control";
                }
                if (String.Compare(subKey, @"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl\1234", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\V1ControlSP1";
                }
            }

            if (baseKey == Registry.LocalMachine)
            {
                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.FancyControl.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponents\HKLM Components";
                }

                if (String.Compare(subKey, @"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\MyComponents\HKLM Components";
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\AssemblyFoldersEx\B", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\X86";
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\AssemblyFoldersEx\A", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\MSIL";
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\X86\X86", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\X86";
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\Mix\Mix", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\Mix";
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\MSIL\MSIL", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\MSIL";
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\X86\X86", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\X86";
                }
                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\MSIL\MSIL", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\MSIL";
                }

                if (String.Compare(subKey, @"Software\Regress714052\v2.0.0\None\None", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return @"C:\Regress714052\None";
                }
            }

            Console.WriteLine("subKey={0}", subKey);
            Assert.True(false, "New GetRegistrySubKeyDefaultValue parameters encountered, need to add unittesting support");
            return null;
        }

        /// <summary>
        /// Delegate for System.IO.File.GetLastWriteTime
        /// </summary>
        /// <param name="path">The file name</param>
        /// <returns>The last write time.</returns>
        private static DateTime GetLastWriteTime(string path)
        {
            return DateTime.FromOADate(0.0);
        }

        /// <summary>
        /// Assert that two strings are equal without regard to case.
        /// </summary>
        /// <param name="expected">The expected string.</param>
        /// <param name="actual">The actual string.</param>
        protected internal static void AssertNoCase(string expected, string actual)
        {
            if (0 != String.Compare(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                string message = String.Format("Expected value '{0}' but received '{1}'", expected, actual);
                Console.WriteLine(message);
                Assert.True(false, message);
            }
        }

        /// <summary>
        /// Assert that two strings are equal without regard to case.
        /// </summary>
        /// <param name="expected">The expected string.</param>
        /// <param name="actual">The actual string.</param>
        protected internal static void AssertNoCase(string message, string expected, string actual)
        {
            if (0 != String.Compare(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(message);
                Assert.True(false, message);
            }
        }

        /// <summary>
        /// Write out an appConfig file.
        /// Return the filename that was written.
        /// </summary>
        /// <param name="appConfigFile"></param>
        /// <param name="redirects"></param>
        protected static string WriteAppConfig(string redirects)
        {
            string appConfigContents =
                "<configuration>\n" +
                "    <runtime>\n" +
                redirects +
                "    </runtime>\n" +
                "</configuration>";

            string appConfigFile = FileUtilities.GetTemporaryFile();
            File.WriteAllText(appConfigFile, appConfigContents);
            return appConfigFile;
        }

        /// <summary>
        /// Determines whether the given item array has an item with the given spec.
        /// </summary>
        /// <param name="items">The item array.</param>
        /// <param name="spec">The spec to search for.</param>
        /// <returns>True if the spec was found.</returns>
        protected static bool ContainsItem(ITaskItem[] items, string spec)
        {
            foreach (ITaskItem item in items)
            {
                if (0 == String.Compare(item.ItemSpec, spec, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Execute the task.
        /// </summary>
        /// <remarks>
        /// NOTE! This test is not in fact completely isolated from its environment: it is reading the real redist lists. 
        /// </remarks>
        protected static bool Execute(ResolveAssemblyReference t)
        {
            return Execute(t, true);
        }

        /// <summary>
        /// Execute the task. Without confirming that the number of files resolved with and without find dependencies is identical. 
        /// This is because profiles could cause the number of primary references to be different.
        /// </summary>
        protected static bool Execute(ResolveAssemblyReference t, bool buildConsistencyCheck)
        {
            string tempPath = Path.GetTempPath();
            string redistListPath = Path.Combine(tempPath, Guid.NewGuid() + ".xml");
            string rarCacheFile = Path.Combine(tempPath, Guid.NewGuid() + ".RarCache");
            s_existentFiles.Add(rarCacheFile);

            bool succeeded = false;

            try
            {
                // Set the InstalledAssemblyTables parameter.
                if (t.InstalledAssemblyTables.Length == 0)
                {
                    File.WriteAllText(redistListPath, REDISTLIST);
                    t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                }

                // First, run it in loading-a-project mode.
                t.Silent = true;
                t.FindDependencies = false;
                t.FindSatellites = false;
                t.FindSerializationAssemblies = false;
                t.FindRelatedFiles = false;
                t.StateFile = null;
                t.Execute(fileExists, directoryExists, getDirectories, getAssemblyName, getAssemblyMetadata, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getLastWriteTime, getRuntimeVersion, openBaseKey, checkIfAssemblyIsInGac, isWinMDFile, readMachineTypeFromPEHeader);

                // A few checks. These should always be true or it may be a perf issue for project load.
                ITaskItem[] loadModeResolvedFiles = new TaskItem[0];
                if (t.ResolvedFiles != null)
                {
                    loadModeResolvedFiles = (ITaskItem[])t.ResolvedFiles.Clone();
                }
                Assert.Equal(0, t.ResolvedDependencyFiles.Length);
                Assert.Equal(0, t.SatelliteFiles.Length);
                Assert.Equal(0, t.RelatedFiles.Length);
                Assert.Equal(0, t.SuggestedRedirects.Length);
                Assert.Equal(0, t.FilesWritten.Length);

                // Now, run it in building-a-project mode.
                MockEngine e = (MockEngine)t.BuildEngine;
                e.Warnings = 0;
                e.Errors = 0;
                e.Log = "";
                t.Silent = false;
                t.FindDependencies = true;
                t.FindSatellites = true;
                t.FindSerializationAssemblies = true;
                t.FindRelatedFiles = true;
                string cache = rarCacheFile;
                t.StateFile = cache;
                File.Delete(t.StateFile);
                succeeded = t.Execute(fileExists, directoryExists, getDirectories, getAssemblyName, getAssemblyMetadata, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getLastWriteTime, getRuntimeVersion, openBaseKey, checkIfAssemblyIsInGac, isWinMDFile, readMachineTypeFromPEHeader);
                if (FileUtilities.FileExistsNoThrow(t.StateFile))
                {
                    Assert.Equal(1, t.FilesWritten.Length);
                    Assert.True(t.FilesWritten[0].ItemSpec.Equals(cache, StringComparison.OrdinalIgnoreCase));
                }

                File.Delete(t.StateFile);

                if (buildConsistencyCheck)
                {
                    // Some consistency checks between load mode and build mode.
                    Assert.Equal(loadModeResolvedFiles.Length, t.ResolvedFiles.Length);
                    for (int i = 0; i < loadModeResolvedFiles.Length; i++)
                    {
                        Assert.Equal(loadModeResolvedFiles[i].ItemSpec, t.ResolvedFiles[i].ItemSpec);
                        Assert.Equal(loadModeResolvedFiles[i].GetMetadata("CopyLocal"), t.ResolvedFiles[i].GetMetadata("CopyLocal"));
                        Assert.Equal(loadModeResolvedFiles[i].GetMetadata("ResolvedFrom"), t.ResolvedFiles[i].GetMetadata("ResolvedFrom"));
                    }
                }

                // Check attributes on resolve files.
                for (int i = 0; i < t.ResolvedFiles.Length; i++)
                {
                    // OriginalItemSpec attribute on resolved items is to support VS in figuring out which
                    // project file reference caused a particular resolved file.
                    string originalItemSpec = t.ResolvedFiles[i].GetMetadata("OriginalItemSpec");
                    Assert.True(ContainsItem(t.Assemblies, originalItemSpec) || ContainsItem(t.AssemblyFiles, originalItemSpec)); //                         "Expected to find OriginalItemSpec in Assemblies or AssemblyFiles task parameters"
                }
            }
            finally
            {
                s_existentFiles.Remove(rarCacheFile);
                if (File.Exists(redistListPath))
                {
                    FileUtilities.DeleteNoThrow(redistListPath);
                }

                if (File.Exists(rarCacheFile))
                {
                    FileUtilities.DeleteNoThrow(rarCacheFile);
                }
            }
            return succeeded;
        }

        /// <summary>
        /// Helper method which allows tests to specify additional assembly search paths.
        /// </summary>
        /// <param name="e"></param>
        internal void ExecuteRAROnItemsAndRedist(ResolveAssemblyReference t, MockEngine e, ITaskItem[] items, string redistString, bool consistencyCheck)
        {
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, consistencyCheck, null);
        }

        /// <summary>
        /// Helper method to get rid of some of the code duplication
        /// </summary>
        internal void ExecuteRAROnItemsAndRedist(ResolveAssemblyReference t, MockEngine e, ITaskItem[] items, string redistString, bool consistencyCheck, List<string> additionalSearchPaths)
        {
            t.BuildEngine = e;
            List<string> searchPaths = new List<string>(DefaultPaths);

            if (additionalSearchPaths != null)
            {
                searchPaths.AddRange(additionalSearchPaths);
            }

            t.Assemblies = items;
            t.SearchPaths = searchPaths.ToArray();
            string redistFile = FileUtilities.GetTemporaryFile();
            try
            {
                File.Delete(redistFile);
                File.WriteAllText
                    (
                        redistFile,
                        redistString
                    );

                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistFile) };

                Execute(t, consistencyCheck);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }
    }
}