# Argon build process
In this documentation, this repository is termed the *upstream* repository. It contains a workflow definition called [build-core](/.github/workflows/build-core.yaml).  Any number of *downstream* repositories can be created to build the projects in *upstream* repository with their own set of parameters. Each downstream repository should contain a workflow can referencing [build-core](/.github/workflows/build-core.yaml) but supplying its own set of values for these parameters:

| Name | Description |
|---|---|
| `ARGON_PRIVATE_KEY` | Base64 encoded version of the app domain's private key |
| `ARGON_PUBLIC_KEY` | Base64 encoded version of the app domain's public key |
| `ARGON_PACKAGE_NAME_BASE` | Prefix for the final package names. Will be suffixed with `.Argon.Client`, `Argon.Create`, `Argon.Service`, and `Argon.Test` |
| `ARGON_NOISE` | Base64 encoded version if 32 bytes of consistent noise to be used in the encryption algorithm |
| `ARGON_DOWNSTREAM_VERSION` | A Semver2 fragment containing a Patch integer and (optionally) a pre-release version, e.g. "123-beta1". See [Version numbering](#version-numbering).  |

The parameters must be kept consistent throughout the lifetime of the downstream repository because any changes will prevent backward/forward compatibilty between the components.

## Example: The FooBar suite
In this example, the FooBar suite is a set of application that will references a build of `Argon.Common` to evaluate licensing claims. A private repository is created for `Foo.Bar.Argon` containing a GitHub Actions workflow containing this:
```yaml
name: Argon FooBar build
on:
  push:
    branches: [ "main" ]
  workflow_dispatch: # Allows manual trigger

jobs:
  call-public-build:
    # Syntax: owner/repo/path/to/workflow@ref
    uses: gameshowpro/Argon/.github/workflows/build-core.yml@main
    
    # Pass non-sensitive config
    with:
      ARGON_PACKAGE_NAME_BASE: "Foo.Bar"
      ARGON_DOWNSTREAM_VERSION: "123-beta1"
    
    # Pass secrets from the Private Repo Settings
    secrets:
      ARGON_PRIVATE_KEY: ${{ secrets.ARGON_PRIVATE_KEY }}
      ARGON_PUBLIC_KEY: ${{ secrets.ARGON_PUBLIC_KEY }}
      ARGON_NOISE: ${{ secrets.ARGON_NOISE }}
```
As shown, many of the parameters are stored as secrets within the FooBar repository and passed into the workflow.

All of the FooBar applications will reference Foo.Bar.Argon.Client. Licenses generated with the Foo.Bar.Argon.Create will work with all applications referencing Foo.Bar.Argon.Client, but not any other downstream builds. Conversely, applications referencing other downstream builds of Argon.Client will not be able to decrypt Foo.Bar.Argon.Create licenses for validation.

## Version numbering
The version number of all packages and assemblies is based on [SemVer2](https://semver.org/). The Major and Minor parts of the version number come from the core repository. They track the actual features and bug fixes within Argon. The .NET assemblies have many parallel version numbers. These that use the [`Version` class](https://learn.microsoft.com/en-us/dotnet/api/system.version) will be mapped like this:

| Source | SemVer2 | .NET Version |
| - | - | - |
| GameshowPro.Argon | Major | `Major` |
| GameshowPro.Argon | Minor | `Minor` |
| Private build | Patch | `Build` |
| Private build | Pre-release | Not mapped |
| Constant | 0 | `Revision` |

 The .NET ['InformationalVersion'](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assemblyinformationalversionattribute.informationalversion) of a .NET assembly *can* contain the full SemVer2 version string because it does not enforce any particular format. in a .Net  version number of the .NET assemblies and nupkg packages is a combination of the current version number in this repository and the supplied build number by the the executing repository.
Major and Minor numbers come from here, signifying functionality changes and bugfixes in the code. 

## Action steps
1. Prebuild steps
    1. Replace literal value of `internal static readonly byte[] s_private` in [Private.cs](../src/GameshowPro.Argon.Create/Private.cs) with data from `ARGON_PRIVATE_KEY`.
    1. Replace literal value of `internal static readonly byte[] s_public` in [Public.cs](../src/GameshowPro.Argon.Common/Public.cs) with data from `ARGON_PUBLIC_KEY`.
    1. Replace literal value of `internal static readonly byte[] s_noise` in [Public.cs](../src/GameshowPro.Argon.Common/Public.cs) with data from `ARGON_NOISE`.
    1. Prefix the package IDs in all publish.nuspec files with `ARGON_PACKAGE_NAME_BASE`.
1. Build steps
    1. Build nuget package:
        * `{ARGON_PACKAGE_NAME_BASE}.Argon.Common`
    1. Build chocolatey packages:
        * `{ARGON_PACKAGE_NAME_BASE}.Argon.Client`
        * `{ARGON_PACKAGE_NAME_BASE}.Argon.Service`
        * `{ARGON_PACKAGE_NAME_BASE}.Argon.Create`
        * `{ARGON_PACKAGE_NAME_BASE}.Argon.Test`
    1. Build self-contained single-file executable file for linux-x64, for later automation use
        * `{ARGON_PACKAGE_NAME_BASE}.Argon.Create`

After the release is succefully created, the downstream workflow may continue with additional steps, including the publishing of artefacts from the new release to a specific package source.
