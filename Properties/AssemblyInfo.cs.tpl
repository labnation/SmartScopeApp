using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
#if ANDROID
using Android.App; 
[assembly: Application(Debuggable=
#if DEBUG
    true    
#else
    false
#endif
    ,
    Label = "SmartScope",
    Icon = "@drawable/icon")]
#endif
[assembly: AssemblyTitle("SmartScope")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("LabNation")]
[assembly: AssemblyProduct("SmartScope")]
[assembly: AssemblyCopyright("Copyright ©  2014")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("a44c7381-e9f6-4132-a32c-fcad897421a4")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
#if DEBUG
[assembly: AssemblyVersion("0.0.*")]
#else
[assembly: AssemblyVersion("0.0.0.0")]
#endif
[assembly: AssemblyFileVersion("0.0.0.0")]
