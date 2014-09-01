namespace TodoBackendFSharp

open Owin
open Microsoft.Owin

type Startup() =

    member __.Configuration(app: IAppBuilder) =
        app .UseCors(Cors.CorsOptions.AllowAll)
            |> ignore

[<assembly: OwinStartupAttribute(typeof<Startup>)>]
do ()