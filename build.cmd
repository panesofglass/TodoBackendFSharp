@echo off
.nuget\NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion
.nuget\NuGet.exe install FsReveal -Version 0.2.0 -OutputDirectory packages -ExcludeVersion
packages\FAKE\tools\FAKE.exe build.fsx %*
