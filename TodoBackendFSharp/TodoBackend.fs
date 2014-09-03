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
    { Url : Uri
      Title : string
      Completed : bool
      Order : int }

type TodoOperation = 
    | GetAll of channel: AsyncReplyChannel<Todo[]>
    | Get of index: int * channel: AsyncReplyChannel<Todo>
    | Post of todo : Todo
    | Update of index: int * todo : Todo
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
                | Get(index, ch) ->
                    // TODO: Handle 404 scenario
                    ch.Reply(todos.[index])
                    return! loop todos
                | Post todo -> 
                    // TODO: Return a new URI?
                    return! loop (Array.append todos [|todo|])
                | Update(index, todo) ->
                    // TODO: Handle 404 scenario
                    todos.[index] <- todo
                    return! loop todos
                | Clear -> 
                    return! loop [||]
            }
        loop [||])

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


(**
 * Handlers
 *)

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

let getTodo index (env: OwinEnv) = async {
    let! todo = todoStorage.PostAndAsyncReply(fun ch -> Get(index, ch))
    let result = serialize todo
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
        { Url = uri
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


(**
 * Routing
 *)

let matchUri template env =
    let environ = Environment.toEnvironment env
    let uriTemplate = UriTemplate(template)
    let baseUri = environ.GetBaseUri().Value
    let requestUri = environ.GetRequestUri().Value
    let path = environ.RequestPath
    let result = uriTemplate.Match(Uri(baseUri), Uri(requestUri))
    if result <> null then Some result else None

let (|Root|Item|NotFound|) env =
    match matchUri "/" env with
    | Some _ -> Root
    | None ->
        match matchUri "/{id}" env with
        | Some result ->
            let index = int result.BoundVariables.["id"]
            Item index
        | None -> NotFound

let app (env: OwinEnv) =
    match env with
    | Root ->
        let httpMethod = unbox env.[Constants.requestMethod]
        match httpMethod with
        | "GET" -> getTodos env |> Async.StartAsTask :> Task
        | "POST" -> postTodo env
        | "DELETE" -> deleteTodos env
        | _ -> methodNotAllowed env
    | Item index ->
        let httpMethod = unbox env.[Constants.requestMethod]
        match httpMethod with
        | "GET" -> getTodo index env |> Async.StartAsTask :> Task
        | _ -> methodNotAllowed env
    | NotFound -> notFound env
