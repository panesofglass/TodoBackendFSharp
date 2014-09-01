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

module TodoBackendFSharp.TodoBackend

open System

type Agent<'T> = MailboxProcessor<'T>

type Todo = 
    { Uri : Uri
      Title : string
      Completed : bool
      Order : int }

type TodoOperation = 
    | GetAll of reply : AsyncReplyChannel<Todo list>
    | Post of todo : Todo
    | Update of todo : Todo
    | Delete of uri : Uri

let todoStorage = 
    Agent<_>.Start(fun inbox -> 
        let rec loop todos = 
            async { 
                let! msg = inbox.Receive()
                match msg with
                | GetAll ch -> 
                    ch.Reply todos
                    return! loop todos
                | Post todo -> 
                    // TODO: Return a new URI?
                    return! loop (todo :: todos)
                | Update todo -> 
                    let todos' = todos |> List.filter (fun t -> t.Uri = todo.Uri)
                    if todos'.Length = todos.Length then return! loop todos // TODO: Handle the 404 scenario
                    else return! loop (todo :: todos')
                | Delete uri -> 
                    let todos' = todos |> List.filter (fun t -> t.Uri = uri)
                    return! loop todos'
            }
        loop [])

open System.IO
open Dyfrig
open Newtonsoft.Json

let serializerSettings = JsonSerializerSettings(ContractResolver = Serialization.CamelCasePropertyNamesContractResolver())
let serialize data =
    JsonConvert.SerializeObject(data, serializerSettings)
    |> Text.Encoding.UTF8.GetBytes

let getTodos (env: OwinEnv) = async {
    let! todos = todoStorage.PostAndAsyncReply(fun ch -> GetAll ch)
    let result = serialize todos 
    let stream : Stream = unbox env.[Constants.responseBody]
    do! stream.AsyncWrite result }
