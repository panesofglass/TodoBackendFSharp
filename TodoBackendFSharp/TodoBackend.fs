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
    | Clear

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
                | Clear -> 
                    return! loop []
            }
        loop [])

open System.IO
open System.Threading.Tasks
open Dyfrig
open Newtonsoft.Json

let serializerSettings = JsonSerializerSettings(ContractResolver = Serialization.CamelCasePropertyNamesContractResolver())

let serialize data =
    JsonConvert.SerializeObject(data, serializerSettings)
    |> Text.Encoding.UTF8.GetBytes

let deserialize<'T> (stream: Stream) =
    let reader = new StreamReader(stream)
    let body = reader.ReadToEnd()
    JsonConvert.DeserializeObject<'T>(body, serializerSettings)

let notFound (env: OwinEnv) =
    env.[Constants.responseStatusCode] <- 404
    env.[Constants.responseReasonPhrase] <- "Not Found"
    Task.FromResult<obj>(null) :> Task

let methodNotAllowed (env: OwinEnv) =
    env.[Constants.responseStatusCode] <- 405
    env.[Constants.responseReasonPhrase] <- "Method Not Allowed"
    Task.FromResult<obj>(null) :> Task
    
let getTodos (env: OwinEnv) = async {
    let! todos = todoStorage.PostAndAsyncReply(fun ch -> GetAll ch)
    let result = serialize todos 
    let stream : Stream = unbox env.[Constants.responseBody]
    do! stream.AsyncWrite(result, 0, result.Length) }

type NewTodo =
    { Title : string
      Completed : bool
      Order : int }

let postTodo (env: OwinEnv) =
    // Retrieve the request body
    let stream : Stream = unbox env.[Constants.requestBody]
    let result : NewTodo = deserialize stream
    // TODO: Handle invalid result

    // Persist the new todo
    let uri = Uri(sprintf "%s/%i" (unbox env.[Constants.requestPathBase]) result.Order, UriKind.Relative)
    let todo =
        { Uri = uri
          Title = result.Title
          Completed = result.Completed
          Order = result.Order }
    todoStorage.Post (Post todo)

    // Return the new todo item
    env.[Constants.responseStatusCode] <- 201
    env.[Constants.responseReasonPhrase] <- "Created"
    let headers : OwinHeaders = unbox env.[Constants.responseHeaders]
    headers.Add("Location", [| uri.OriginalString |])
    let result = serialize todo
    let stream : Stream = unbox env.[Constants.responseBody]
    stream.WriteAsync(result, 0, result.Length)

let deleteTodos (env: OwinEnv) =
    todoStorage.Post Clear
    env.[Constants.responseStatusCode] <- 204
    env.[Constants.responseReasonPhrase] <- "No Content"
    Task.FromResult<obj>(null) :> Task

let app (env: OwinEnv) =
    let path = unbox env.[Constants.requestPath]
    match path with
    | "/" ->
        let httpMethod = unbox env.[Constants.requestMethod]
        match httpMethod with
        | "GET" -> getTodos env |> Async.StartAsTask :> Task
        | "POST" -> postTodo env
        | "DELETE" -> deleteTodos env
        | _ -> methodNotAllowed env
    | _ -> notFound env
