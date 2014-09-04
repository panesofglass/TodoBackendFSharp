//----------------------------------------------------------------------------
//
// Copyright (c) 2014 Ryan Riley (@panesofglass)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------

namespace TodoBackendFSharp

open Owin
open Microsoft.Owin

module Cors =
    /// Cross Origin Resource Sharing (CORS) middleware wrapper for the `Microsoft.Owin.Cors.CorsMiddleware`.
    let middleware next env =
        Cors.CorsMiddleware(Dyfrig.OwinAppFunc next, Cors.CorsOptions.AllowAll).Invoke env

module Link =
    open Dyfrig

    /// Middleware that inserts an HTTP `Link` header pointing to the TodoBackendFSharp source code repository on GitHub.
    let middleware next (env: OwinEnv) =
        let headers : OwinHeaders = unbox env.[Constants.responseHeaders]
        headers.Add("Link", [|"<https://github.com/panesofglass/TodoBackendFSharp>; rel=meta"|])
        next env

(*
Microsoft's Katana components make use of a `Startup` class with a single member conventionally named
`Configuration`. `Configuration` takes an `IAppBuilder` into which you mount middleware components.
In F#, you can write middleware components as simple functions taking the next `OwinAppFunc` handler
and an `OwinEnv` environment dictionary. F# allows you to chain these together naturally using the
`|>` operator. You can of course flip the order and use the `<|` operator if that reads better to you.
In the todo-backend implementation, we chain the actual `TodoBackend.app` into the `Link.middleware`,
and then into the `Cors.middleware`. By doing this, Katana will pass all requests first through the
`Cors.middleware`, then add the `Link` header, then run the application.
*)

/// Todo-backend startup used by Katana.
[<Sealed>]
type Startup() =
    /// Configures the Katana `IAppBuilder` to run the todo-backend application and
    /// add the Link header. The `Cors.middleware` wraps every request and will respond
    /// immediately to CORS preflight requests.
    member __.Configuration(builder: IAppBuilder) =
        builder.Use(fun _ ->
            TodoBackend.app
            |> Link.middleware
            |> Cors.middleware)
        |> ignore

/// Tell the Katana host to use our `Startup` class as the application bootstrapper.
[<assembly: OwinStartupAttribute(typeof<Startup>)>]
do ()
