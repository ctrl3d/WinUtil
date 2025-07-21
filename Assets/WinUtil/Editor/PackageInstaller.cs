using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEngine;

namespace work.ctrl3d.WinUtil
{
    [InitializeOnLoad]
    public class PackageInstaller
    {
        private const string UniTaskName = "com.cysharp.unitask";
        private const string UniTaskGitUrl = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask";
        
        private const string WinAPIName = "work.ctrl3d.winapi";
        private const string WinAPIGitUrl = "https://github.com/ctrl3d/WinAPI.git?path=Assets/WinAPI";
        
        static PackageInstaller()
        {
            var isUniTaskInstalled = CheckPackageInstalled(UniTaskName);
            var isWinAPIInstalled = CheckPackageInstalled(WinAPIName);
            
            if (!isUniTaskInstalled) AddGitPackage(UniTaskName, UniTaskGitUrl);
            if (!isWinAPIInstalled) AddGitPackage(WinAPIName, WinAPIGitUrl);
            
            if(HasScriptingDefineSymbol("UNITASK_SUPPORT")) return;
            AddScriptingDefineSymbol("UNITASK_SUPPORT");
        }
        
        private static void AddGitPackage(string packageName, string gitUrl)
        {
            var path = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            var jsonString = File.ReadAllText(path);

            var indexOfLastBracket = jsonString.IndexOf("}", StringComparison.Ordinal);
            var dependenciesSubstring = jsonString[..indexOfLastBracket];
            var endOfLastPackage = dependenciesSubstring.LastIndexOf("\"", StringComparison.Ordinal);

            jsonString = jsonString.Insert(endOfLastPackage + 1, $", \n \"{packageName}\": \"{gitUrl}\"");

            File.WriteAllText(path, jsonString);
            Client.Resolve();
        }

        private static bool CheckPackageInstalled(string packageName)
        {
            var path = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            var jsonString = File.ReadAllText(path);
            return jsonString.Contains(packageName);
        }
        
        private static void AddScriptingDefineSymbol(string symbol)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            
            var symbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            if (!symbols.Contains(symbol))
            {
                symbols += $";{symbol}";
            }
            
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, symbols);
        }
        
        private static bool HasScriptingDefineSymbol(string symbol)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var targetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            
            var symbols = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            var symbolArray = symbols.Split(';');
            return symbolArray.Any(existingSymbol => existingSymbol == symbol);
        }
    }
}