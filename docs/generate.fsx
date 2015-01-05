#r "../packages/FAKE/tools/FakeLib.dll"
open Fake
open System.IO

let output     = __SOURCE_DIRECTORY__ @@ "output"
let files      = __SOURCE_DIRECTORY__ @@ "files"

let copyFiles() =
    if Directory.Exists files then
        CopyRecursive files output true |> Log "Copying file: "
    else ()

#load "../packages/FsReveal/fsreveal/fsreveal.fsx"
open FsReveal

let outDir = Path.Combine(__SOURCE_DIRECTORY__, "output")
let inputFsx = Path.Combine( __SOURCE_DIRECTORY__, "slides.fsx")
FsReveal.GenerateOutputFromScriptFile outDir "index.html" inputFsx
copyFiles()
