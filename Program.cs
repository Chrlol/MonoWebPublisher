using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace MonoWebPublisher
{
    class Program
    {
        /// <example>mono MonoWebPublisher.exe sample.csproj /var/www/sample-dest-publish-dir</example>
        static void Main(string[] args)
        {
            if (args.Length != 2 && args.Length != 3)
            {
                Console.WriteLine("Parameter not match!");
                Environment.Exit(1);
            }
            var projectFile = args[0];
            var destDir = args[1];
            var configuration = "Release";

            if (args.Length == 3)
            {
                if (!string.IsNullOrWhiteSpace(args[2]))
                    configuration = args[2];
            }

            var sourceDir = Path.GetDirectoryName(projectFile);

            // Find out if web.config should be transformed
            var webConfig = Path.Combine(sourceDir, "Web.config");
            var webConfigTransform = Path.Combine(sourceDir, "Web." + configuration + ".config");
            var shouldTransformWebConfig = File.Exists(webConfig) && File.Exists(webConfigTransform);

            //delete everything in destDir but .git folder
            if (Directory.Exists(destDir))
            {
                var destDirs = Directory.GetDirectories(destDir, "*", SearchOption.TopDirectoryOnly);
                destDirs.ToList<string>().ForEach(n =>
                {
                    if (Path.GetFileName(n) != ".git")
                    {
                        Directory.Delete(n, true);
                    }
                });
                Directory.GetFiles(destDir, "*", SearchOption.TopDirectoryOnly).ToList().ForEach(File.Delete);

            }

            //copy included files
            var fileList = GetIncludedFiles(projectFile);
            fileList.ForEach(n =>
            {
                bool isWebConfig = n.StartsWith("Web.") && n.EndsWith(".config");
                if (!shouldTransformWebConfig || !isWebConfig)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(destDir, n)));
                    File.Copy(Path.Combine(sourceDir, n), Path.Combine(destDir, n), true);
                }
            });

            //copy bin folder
            string[] binFiles = Directory.GetFiles(Path.Combine(sourceDir, "bin"));
            Directory.CreateDirectory(Path.Combine(destDir, "bin"));
            binFiles.ToList<string>().ForEach(n =>
            {
                File.Copy(n, Path.Combine(Path.Combine(destDir, "bin"), Path.GetFileName(n)), true);
            });

            // Transform web.config
            if (shouldTransformWebConfig)
            {
                var xmlDoc = new XmlDataDocument
                {
                    PreserveWhitespace = true
                };
                xmlDoc.Load(webConfig);

                var transformation = new Microsoft.Web.XmlTransform.XmlTransformation(webConfigTransform);
                transformation.Apply(xmlDoc);

                var outputWebConfig = Path.Combine(destDir, "Web.config");
                var xmlWriter = XmlWriter.Create(outputWebConfig);
                xmlDoc.WriteTo(xmlWriter);
                xmlWriter.Close();
            }
        }

        private static List<string> GetIncludedFiles(string projectFile)
        {
            var xmlDoc = XDocument.Load(projectFile);
            var result = xmlDoc.Descendants(xmlDoc.Root.Name.Namespace + "Content")
                .Where(node => node.Attribute("Include") != null)
                .Select(node =>  System.Net.WebUtility.UrlDecode(node.Attribute("Include").Value.Replace("\\", Path.DirectorySeparatorChar.ToString())));

            return result.ToList<string>();
        }
    }
}
