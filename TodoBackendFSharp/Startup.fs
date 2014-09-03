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

open System
open Owin
open Microsoft.Owin
open TodoBackend

type Startup() =

    // Store some todos for the initial request
    let initialTodo =
        { Url = Uri("/1", UriKind.Relative)
          Title = "Create todo items"
          Completed = false
          Order = 1 }
    do todoStorage.Post (TodoOperation.Post initialTodo)

    let cors next env =
        Cors.CorsMiddleware(Dyfrig.OwinAppFunc next, Cors.CorsOptions.AllowAll).Invoke env

    member __.Configuration(builder: IAppBuilder) =
        builder.Use(fun _ ->
            app
            |> cors)
        |> ignore

[<assembly: OwinStartupAttribute(typeof<Startup>)>]
do ()
