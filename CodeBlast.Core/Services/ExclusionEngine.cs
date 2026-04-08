using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBlast.Core.Models;

namespace CodeBlast.Core.Services;

public class ExclusionEngine
{
    private readonly GitIgnoreParser _gitIgnoreParser;
    private readonly CustomRulesService _customRulesService;
    private bool _respectGitIgnore = true;

    public ExclusionEngine(GitIgnoreParser gitIgnoreParser, CustomRulesService customRulesService)
    {
        _gitIgnoreParser = gitIgnoreParser;
        _customRulesService = customRulesService;
    }

    public void SetRespectGitIgnore(bool value)
    {
        _respectGitIgnore = value;
    }

    public bool IsExcluded(string fullPath, string relativePath, bool isDirectory)
    {
        // Revisar primero .gitignore y .codeblastignore
        // Ahora pasamos relativePath que requiere la nueva firma
        if (_respectGitIgnore && _gitIgnoreParser.IsIgnored(fullPath, relativePath, isDirectory))
            return true;

        // Revisar reglas globales y locales de CodeBlast
        if (_customRulesService.IsExcluded(relativePath, isDirectory))
            return true;

        return false;
    }
}
