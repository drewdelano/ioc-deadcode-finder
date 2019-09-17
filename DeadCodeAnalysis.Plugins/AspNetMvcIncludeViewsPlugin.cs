using DeadCodeAnalysis.Library;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Razor.TagHelpers;
//using Microsoft.AspNetCore.Mvc.ViewFeatures;
//using Microsoft.AspNetCore.Razor.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Web.Razor;
using System.Web.Razor.Generator;
using System.Web.Routing;
using System.Xml.Linq;
using SyntaxTree = System.Web.Razor.Parser.SyntaxTree;

namespace DeadCodeAnalysis.Plugins
{
	public class AspNetMvcIncludeViewsPlugin : Plugin
	{
		string _projectPath;
		public AspNetMvcIncludeViewsPlugin(string projectPath)
		{
			_projectPath = projectPath;
		}

		public override List<Assembly> ListOtherFoundAssemblies()
		{
			var tempFolder = Path.Combine(Path.GetTempPath(), "compiled_site");
			Process.Start("aspnet_compiler", $"-c -v temp -p \"{_projectPath}\" -f -errorstack \"{tempFolder}\"");



			return base.ListOtherFoundAssemblies();
		}
	}
}
