using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static System.Net.Mime.MediaTypeNames;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Nuke.Common.Tools.MSBuild;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>();


    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;

    AbsolutePath SourceDirectory => RootDirectory / "Src";
    AbsolutePath OutputDirectory => RootDirectory / "Output";

    private Configuration GetBranchBasedConfiguration()
    {
        return GitVersion.BranchName switch
        {
            "develop" => Configuration.Debug,
            string s when s.StartsWith("feature/") => Configuration.Debug,

            "master" => Configuration.Release,
            string s when s.StartsWith("release/") => Configuration.Release,
            string s when s.StartsWith("hotfix/") => Configuration.Release,

            null => throw new Exception("Unable to determine the git branch!"),
            string unrecognizedBranch => throw new Exception(
                $"Unable to determine build configuration from branch name \"{unrecognizedBranch}\""),
        };
    }

    Target ShowInfo => _ => _
        .Executes(() =>
        {
            string LocalOrRemoteText() => IsLocalBuild ? "Local Build" : "Remote (CI/CD) Build";

            Log.Information("Standard GitVersion Formats:");
            Log.Information($"                         SemVer: {GitVersion.SemVer}");
            Log.Information($"                     FullSemVer: {GitVersion.FullSemVer}");
            Log.Information($"                   LegacySemVer: {GitVersion.LegacySemVer}");
            Log.Information($"             LegacySemVerPadded: {GitVersion.LegacySemVerPadded}\n");

            Log.Information("Standard GitVersion Assembly Formats:");
            Log.Information($"                 AssemblySemVer: {GitVersion.AssemblySemVer}");
            Log.Information($"             AssemblySemFileVer: {GitVersion.AssemblySemFileVer}");
            Log.Information($"           InformationalVersion: {GitVersion.InformationalVersion}\n");

            Log.Information("Standard GitVersion NuGet Formats:");
            Log.Information($"                   NuGetVersion: {GitVersion.NuGetVersion}");
            Log.Information($"                 NuGetVersionV2: {GitVersion.NuGetVersionV2}");
            Log.Information($"             NuGetPreReleaseTag: {GitVersion.NuGetPreReleaseTag}");
            Log.Information($"           NuGetPreReleaseTagV2: {GitVersion.NuGetPreReleaseTagV2}\n");

            Log.Information("Other GitVersion Information:");
            Log.Information($"                     BranchName: {GitVersion.BranchName}");
            Log.Information($"                  BuildMetaData: {GitVersion.BuildMetaData}");
            Log.Information($"            BuildMetaDataPadded: {GitVersion.BuildMetaDataPadded}");
            Log.Information($"      CommitsSinceVersionSource: {GitVersion.CommitsSinceVersionSource}");
            Log.Information($"CommitsSinceVersionSourcePadded: {GitVersion.CommitsSinceVersionSourcePadded}");
            Log.Information($"                     CommitDate: {GitVersion.CommitDate}");
            Log.Information($"             UncommittedChanges: {GitVersion.UncommittedChanges}");
            Log.Information($"               VersionSourceSha: {GitVersion.VersionSourceSha}");
            Log.Information($"                            Sha: {GitVersion.Sha}");
            Log.Information($"                       ShortSha: {GitVersion.ShortSha}\n");

            Log.Information("Build Configuration Information:");
            Log.Information($"  Local or Remote (CI/CD) Build: {LocalOrRemoteText()}");
            Log.Information($"        Configuration by Branch: {GetBranchBasedConfiguration()}");
            Log.Information($"            Final Configuration: {Configuration}\n");

            Log.Information($"Generic Version Information:");
            Log.Information($"                MajorMinorPatch: {GitVersion.MajorMinorPatch}");
            Log.Information($"                  PreReleaseTag: {GitVersion.PreReleaseTag}");
            Log.Information($"                PreReleaseLabel: {GitVersion.PreReleaseLabel}");
            Log.Information($"               PreReleaseNumber: {GitVersion.PreReleaseNumber}");
            Log.Information($"       WeightedPreReleaseNumber: {GitVersion.WeightedPreReleaseNumber}");
            Log.Information($"              FullBuildMetaData: {GitVersion.FullBuildMetaData}");
        });


    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(path => path.DeleteDirectory());
            OutputDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution)
            );
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(GitVersion.NuGetVersion)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
            );
        });

    Target UnitTest => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(x => x
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .EnableNoBuild()
            );
        });

    Target Package => _ => _
        .DependsOn(Clean)
        .DependsOn(UnitTest)
        .Executes(() =>
        {
            DotNetPack(cfg => cfg
                .SetProject(Solution.GetProject("BLS")?.Path ?? "<Project Not Found>")
                .SetConfiguration(Configuration)
                .SetVersion(GitVersion.NuGetVersion)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .SetOutputDirectory(OutputDirectory)
            );
        });
}
