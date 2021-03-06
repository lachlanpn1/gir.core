using System;
using System.IO;
using System.Threading.Tasks;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace Generator
{
    public abstract class TemplateLoader : ITemplateLoader
    {
        #region Methods

        public virtual string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
            => Path.Combine(Environment.CurrentDirectory + "/../Generator/Templates/Shared/", templateName);

        public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
            => File.ReadAllText(templatePath);

        public ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
            => new ValueTask<string>(File.ReadAllTextAsync(templatePath));

        #endregion
    }
}
