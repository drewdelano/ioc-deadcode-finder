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
		string _viewsFolder;
		string _projectPath;
		string _buildDir;
		public AspNetMvcIncludeViewsPlugin(string projectPath, string viewsFolder, string buildDir)
		{
			_projectPath = projectPath;
			_viewsFolder = viewsFolder;
			_buildDir = buildDir;
		}

		public override List<Assembly> ListOtherFoundAssemblies()
		{
			var webConfigRazorSettings = GetWebConfigRazorSettings();

			var razorFilesInfo = Directory.GetFiles(_viewsFolder, "*.cshtml", SearchOption.AllDirectories).Select(f => new { RazorFile = f, NameMangle = true })
				.Concat(Directory.GetFiles(Path.Combine(_viewsFolder, @"..\App_Code\"), "*.cshtml", SearchOption.TopDirectoryOnly).Select(f => new { RazorFile = f, NameMangle = false }));


			var provider = new CSharpCodeProvider();
			var host = new RazorEngineHost(new CSharpRazorCodeLanguage());
			host.DefaultNamespace = "ASP";
			
			host.GeneratedClassContext = new GeneratedClassContext(
				GeneratedClassContext.DefaultExecuteMethodName,
				"Write",
				GeneratedClassContext.DefaultWriteLiteralMethodName,
				"WriteTo",
				"WriteLiteralTo",
				"System.Web.WebPages.HelperResult",
				"DefineSection",
				"BeginContext",
				"EndContext");

			var engine = new RazorTemplateEngine(host);

			var compileUnits = new List<CodeCompileUnit>();

			foreach (var razorFileInfo in razorFilesInfo) {
				try {
					var razorFile = razorFileInfo.RazorFile;

					host.NamespaceImports.Clear();
					host.NamespaceImports.Add("System");
					host.NamespaceImports.Add("System.Web");
					host.NamespaceImports.Add("System.Linq");
					host.NamespaceImports.Add("System.Collections.Generic");
					host.NamespaceImports.Add("System.ComponentModel.DataAnnotations");
					host.NamespaceImports.Add("System.Web.Helpers");
					host.NamespaceImports.Add("System.Web.WebPages");
					foreach (var ns in webConfigRazorSettings.Namespaces) {
						host.NamespaceImports.Add(ns);
					}

					if (razorFileInfo.NameMangle) {
						var possibleClassName = Path.GetFileName(razorFile.Substring(_viewsFolder.Length).TrimStart('\\').Replace("\\", "_").Replace(".", "_"));
						host.DefaultClassName = possibleClassName;
					} else {
						// this is a helper with an important name
						host.DefaultClassName = Path.GetFileNameWithoutExtension(razorFile);
					}
					host.DefaultBaseClass = webConfigRazorSettings.PageBaseType;
					host.DesignTimeMode = true;

					var stringReader = new StringReader(File.ReadAllText(razorFile));
					var generatorResults = engine.GenerateCode(stringReader);


					foreach (var block in generatorResults.Document.Children.OfType<SyntaxTree.Block>()) {
						var children = block.Children.ToList();
						if (children.Count != 2) {
							continue;
						}

						var spans = children.OfType<SyntaxTree.Span>().ToList();
						if (spans.Count != 2) {
							continue;
						}

						if (spans[0].Kind == SyntaxTree.SpanKind.Transition && spans[0].Content == "@" &&
							spans[1].Kind == SyntaxTree.SpanKind.Code && spans[1].Content.StartsWith("using ")) {

							var nsToAdd = spans[1].Content.Substring("using ".Length).TrimEnd(';');
							host.NamespaceImports.Add(nsToAdd);
						}


						if (spans[0].Kind == SyntaxTree.SpanKind.Transition && spans[0].Content == "@" &&
							spans[1].Kind == SyntaxTree.SpanKind.Code && spans[1].Content == "model") {

							// for some reason, if the cshtml has a html tag right after the @model line it likes to run them together
							// (e.g. @model MyViewModel\r\n\r\n<div>)
							var modelType = spans[1].Next.Content.Split('\n')[0].Trim('\n', '\r', ' ');
							host.DefaultBaseClass = webConfigRazorSettings.PageBaseType + "<" + modelType + ">";
							break;
						}
					}

					// now compile it again now that we know the model + usings
					stringReader = new StringReader(File.ReadAllText(razorFile));
					generatorResults = engine.GenerateCode(stringReader);

					var genCode = generatorResults.GeneratedCode;
					compileUnits.Add(genCode);
				} catch (Exception ex) {
				}
			}

			try {
				var compilerParams = new CompilerParameters {
					GenerateInMemory = true,
					TempFiles = new TempFileCollection(@"C:\Users\Drew\Desktop\compiley", true)
				};
				compilerParams.ReferencedAssemblies.AddRange(
					Directory.GetFiles(_buildDir, "*.dll") // TODO: this isn't great
						.Concat(GetSystemDependencies())
					.ToArray());

				var compilerResults = provider.CompileAssemblyFromDom(compilerParams, compileUnits.ToArray());

				var errs = compilerResults.Errors.Cast<CompilerError>().OrderBy(ce => ce.FileName).ToList();
				var asm = compilerResults.CompiledAssembly;
			} catch (Exception ex) {
			}

			return base.ListOtherFoundAssemblies();
		}

		IEnumerable<string> GetSystemDependencies()
		{
			yield return typeof(ValueType).Assembly.Location;
			yield return typeof(System.Collections.IEnumerable).Assembly.Location;
			yield return typeof(System.Collections.Generic.List<>).Assembly.Location;
			yield return typeof(System.Type).Assembly.Location;
			yield return typeof(System.Linq.IQueryable<>).Assembly.Location;
			yield return typeof(System.Web.IHtmlString).Assembly.Location;
			yield return typeof(System.Web.WebPages.HelperResult).Assembly.Location;
			yield return typeof(RouteData).Assembly.Location;
			yield return typeof(System.Web.Mvc.UrlHelper).Assembly.Location;
			yield return typeof(DynamicAttribute).Assembly.Location;
			yield return typeof(ConfigurationManager).Assembly.Location;
			yield return typeof(System.Net.Configuration.SettingsSection).Assembly.Location;
			yield return typeof(System.Xml.Linq.Extensions).Assembly.Location;
			yield return typeof(System.Web.Mvc.WebViewPage).Assembly.Location;
			yield return typeof(System.Activities.Statements.StateMachine).Assembly.Location;
			yield return typeof(System.Web.WebPages.WebPageBase).Assembly.Location;
			yield return typeof(DataTypeAttribute).Assembly.Location;
			yield return typeof(System.Web.Script.Serialization.JavaScriptSerializer).Assembly.Location;
			yield return typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly.Location;
		}

		WebConfigRazorSettings GetWebConfigRazorSettings()
		{
			var xdoc = XDocument.Parse(File.ReadAllText(Path.Combine(_viewsFolder, @"web.config")));

			var razorConfig = xdoc.Descendants("system.web.webPages.razor").Single();
			var pagesConfig = razorConfig.Element("pages");

			var pageBaseType = pagesConfig.Attribute("pageBaseType").Value;
			
			var namespaces = pagesConfig.Element("namespaces").Descendants("add").Select(d => d.Attribute("namespace").Value).ToList();
			return new WebConfigRazorSettings {
				PageBaseType = pageBaseType,
				Namespaces = namespaces
			};
		}
		
		class WebConfigRazorSettings
		{
			public string PageBaseType { get; set; }

			public List<string> Namespaces { get; set; }
		}
	}
}
