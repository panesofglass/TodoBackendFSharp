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

type NewTodo =
    { Title : string
      Completed : bool
      Order : int }

type TodoOperation = 
    | GetAll of channel: AsyncReplyChannel<NewTodo[]>
    | Get of index: int * channel: AsyncReplyChannel<NewTodo option>
    | Post of todo: NewTodo
    | Update of index: int * todo: NewTodo
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
                | Get(order, ch) ->
                    // TODO: Handle 404 scenario
                    let todo = todos |> Array.tryFind (fun x -> x.Order = order)
                    ch.Reply todo
                    return! loop todos
                | Post todo -> 
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
    async.Return()

let methodNotAllowed (env: OwinEnv) =
    env.[Constants.responseStatusCode] <- 405
    env.[Constants.responseReasonPhrase] <- "Method Not Allowed"
    async.Return()

type Todo = 
    { Url : Uri
      Title : string
      Completed : bool
      Order : int }
    
let getTodos (env: OwinEnv) = async {
    let environ = Environment.toEnvironment env
    let baseUri = Uri(environ.GetBaseUri().Value)
    let! todos = todoStorage.PostAndAsyncReply(fun ch -> GetAll ch)
    let todos' =
        todos
        |> Array.map (fun x ->
            { Url = Uri(baseUri, sprintf "/%i" x.Order)
              Title = x.Title
              Completed = x.Completed
              Order = x.Order })
    let result = serialize todos'
    let stream : Stream = unbox env.[Constants.responseBody]
    do! stream.AsyncWrite(result, 0, result.Length) }

let getTodo index (env: OwinEnv) = async {
    let environ = Environment.toEnvironment env
    let baseUri = Uri(environ.GetBaseUri().Value)
    let! todo = todoStorage.PostAndAsyncReply(fun ch -> Get(index, ch))
    match todo with
    | None ->
        do! notFound env
    | Some todo ->
        let todo' = 
            { Url = Uri(baseUri, sprintf "/%i" todo.Order)
              Title = todo.Title
              Completed = todo.Completed
              Order = todo.Order }
        let result = serialize todo'
        let stream : Stream = unbox env.[Constants.responseBody]
        do! stream.AsyncWrite(result, 0, result.Length) }

let postTodo (env: OwinEnv) =
    // Retrieve the request body
    let stream : Stream = unbox env.[Constants.requestBody]
    let newTodo : NewTodo = deserialize stream
    // TODO: Handle invalid result

    // Persist the new todo
    todoStorage.Post(Post newTodo)

    // Return the new todo item
    let environ = Environment.toEnvironment env
    let baseUri = Uri(environ.GetBaseUri().Value)
    let todo =
        { Url = Uri(baseUri, sprintf "/%i" newTodo.Order)
          Title = newTodo.Title
          Completed = newTodo.Completed
          Order = newTodo.Order }
    env.[Constants.responseStatusCode] <- 201
    env.[Constants.responseReasonPhrase] <- "Created"
    let headers : OwinHeaders = unbox env.[Constants.responseHeaders]
    headers.Add("Location", [| todo.Url.AbsoluteUri |])
    let result = serialize todo
    let stream : Stream = unbox env.[Constants.responseBody]
    stream.AsyncWrite(result, 0, result.Length)

let deleteTodos (env: OwinEnv) =
    todoStorage.Post Clear
    env.[Constants.responseStatusCode] <- 204
    env.[Constants.responseReasonPhrase] <- "No Content"
    async.Return()


(**
 * Routing
 *)

let matchUri template env =
    let environ = Environment.toEnvironment env
    let uriTemplate = UriTemplate(template)
    let baseUri = environ.GetBaseUri().Value
    let requestUri = environ.GetRequestUri().Value
    let result = uriTemplate.Match(Uri(baseUri), Uri(requestUri))
    if result <> null then Some result else None

let (|Item|_|) env =
    match matchUri "/{id}" env with
    | Some result ->
        let index = int result.BoundVariables.["id"]
        match unbox env.[Constants.requestMethod] with
        | "GET" -> getTodo index env
        | _ -> methodNotAllowed env
        |> Some
    | None -> None

let (|Root|_|) env =
    match matchUri "/" env with
    | Some _ ->
        match unbox env.[Constants.requestMethod] with
        | "GET" -> getTodos env
        | "POST" -> postTodo env
        | "DELETE" -> deleteTodos env
        | _ -> methodNotAllowed env
        |> Some
    | None -> None

let app env =
    match env with
    | Item task -> task
    | Root task -> task
    | _ -> notFound env
    |> Async.StartAsTask :> Task
