using Nuke.Common.CI.GitHubActions;
using Xerris.Nuke.Components;

[GitHubActions(
    "continuous",
    GitHubActionsImage.WindowsLatest,
    GitHubActionsImage.UbuntuLatest,
    GitHubActionsImage.MacOsLatest,
    FetchDepth = 0,
    OnPullRequestBranches = new[] { "main" },
    OnPushBranches = new[] { "main", "release/v*" },
    PublishArtifacts = true,
    InvokedTargets = new[] { nameof(IPack.Pack) },
    CacheKeyFiles = new[] { "global.json", "src/**/*.csproj" })]
[GitHubActions(
    "release",
    GitHubActionsImage.UbuntuLatest,
    FetchDepth = 0,
    OnPushTags = new[] { "v*" },
    PublishArtifacts = true,
    InvokedTargets = new[] { nameof(IPack.Pack), nameof(IPush.Push) },
    CacheKeyFiles = new[] { "global.json", "src/**/*.csproj" },
    ImportSecrets = new[] { nameof(IPush.NuGetApiKey) })]
partial class Build
{
}
