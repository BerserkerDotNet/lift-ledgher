using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
[GitHubActions(
    "pr",
    GitHubActionsImage.UbuntuLatest,
    On = new [] { GitHubActionsTrigger.PullRequest },
    InvokedTargets = new[] { nameof(Compile), nameof(Test) })]
[GitHubActions(
    "continuos",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    OnPushBranches = new []{ "master" },
    ImportSecrets = new []{ nameof(AzureSPNCreds)},
    InvokedTargets = new[] { nameof(Compile), nameof(Test), nameof(PublishMobile), nameof(PublishAPI) })]
[GitHubActions(
    "deploy",
    GitHubActionsImage.UbuntuLatest,
    On = new [] { GitHubActionsTrigger.WorkflowDispatch },
    ImportSecrets = new []{ nameof(AzureSPNCreds)},
    AutoGenerate = false)]
partial class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Test);

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;
    
    readonly AbsolutePath ArtifactsDirectory = RootDirectory / "artifacts";
    
    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("Should publish test results to GitHub")]
    readonly bool PublishTestResults;

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            DotNetClean(_ => _
                .SetProject(Solution));
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetWorkloadRestore(_ => _
                .SetProject(Solution));
            
            DotNetRestore(_ => _
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(_ => _
                .EnableNoRestore()
                .SetFramework("net9.0")
                .SetProjectFile(Solution.src.LiftLedger_API));

            DotNet($"build {Solution.src.LiftLedger_Mobile} -t:InstallAndroidDependencies -f net9.0-android --no-restore");
        });
    
    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(_ => _
                    .EnableNoRestore()
                    .SetProjectFile(Solution)
                    .When(_=> PublishTestResults, _ => _
                        .SetLoggers("trx")
                        .SetResultsDirectory(ArtifactsDirectory / "test-results")));
        });
}
