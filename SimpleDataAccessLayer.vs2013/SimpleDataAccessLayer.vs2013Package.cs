using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextTemplating.VSHost;
using System.ComponentModel.Design;
using Microsoft.Win32;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;

namespace SimpleDataAccessLayer_vs2013
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    // This attribute tells the PkgDef creation utility (CreatePkgDef.exe) that this class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideDirectiveProcessor(typeof(HelperAssemblyIncluder), "HelperAssemblyIncluder", "Includes a helper Assembly")]
    // This attribute is used to register the information needed to show this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideEditorExtension(typeof(EditorFactory), ".dal", 50,
              ProjectGuid = "{A2FE74E1-B743-11d0-AE1A-00A0C90FFFC3}",
              TemplateDir = "Templates",
              NameResourceID = 105,
              DefaultName = "SimpleDataAccessLayer.vs2013.Editor")]
    [Guid(GuidList.guidSimpleDataAccessLayer_vs2013PkgString)]
    public sealed class SimpleDataAccessLayer_vs2013Package : Package
    {
        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public SimpleDataAccessLayer_vs2013Package()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }



        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation
        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine (string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            base.Initialize();

            RegisterEditorFactory(new EditorFactory(this));
        }

        /// <summary>
        /// this method makes the EnvDTE object public so I can use it in my editor
        /// </summary>
        /// <returns></returns>
        public EnvDTE.DTE GetEnvDTE()
        {
            return (EnvDTE.DTE)GetService(typeof(EnvDTE.DTE));
        }
        #endregion

    }
}
