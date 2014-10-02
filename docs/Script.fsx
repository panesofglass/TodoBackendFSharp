#load @"..\packages\FsReveal.0.0.5-beta\fsreveal\fsreveal.fsx"
open FsReveal
open System.IO

let outDir = Path.Combine(__SOURCE_DIRECTORY__, "output")
let inputFsx = Path.Combine( __SOURCE_DIRECTORY__, "slides.fsx")
FsReveal.GenerateOutputFromScriptFile outDir "index.html" inputFsx
