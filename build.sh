#!/bin/bash

mono .nuget/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion
mono .nuget/NuGet.exe install FsReveal -Version 0.2.0 -OutputDirectory packages -ExcludeVersion
mono packages/FAKE/tools/FAKE.exe $@ --fsiargs -d:MONO build.fsx 
