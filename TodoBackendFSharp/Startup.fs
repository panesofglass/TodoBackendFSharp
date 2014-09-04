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
    /// Cross Origin Resource Sharing (CORS) middleware wrapper for the `Microsoft.Owin.Cors` `CorsMiddleware`.
    let middleware next env =
        Cors.CorsMiddleware(Dyfrig.OwinAppFunc next, Cors.CorsOptions.AllowAll).Invoke env

module Link =
    open Dyfrig

    /// Middleware that inserts an HTTP `Link` header pointing to the TodoBackendFSharp source code repository on GitHub.
    let middleware next (env: OwinEnv) =
        let headers : OwinHeaders = unbox env.[Constants.responseHeaders]
        headers.Add("Link", [|"<https://github.com/panesofglass/TodoBackendFSharp>; rel=meta"|])
        next env

[<Sealed>]
type Startup() =
    member __.Configuration(builder: IAppBuilder) =
        builder.Use(fun _ ->
            TodoBackend.app
            |> Link.middleware
            |> Cors.middleware)
        |> ignore

[<assembly: OwinStartupAttribute(typeof<Startup>)>]
do ()
