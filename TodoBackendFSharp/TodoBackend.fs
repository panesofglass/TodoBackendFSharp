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
    with
    static member Empty = { Title = "EMPTY"; Completed = false; Order = -1 }

type TodoPatch =
    { Title : string option
      Completed : bool option
      Order : int option }

type TodoOperation = 
    | GetAll of channel: AsyncReplyChannel<NewTodo[]>
    | Post of todo: NewTodo * channel: AsyncReplyChannel<int>
    | Clear
    | Get of index: int * channel: AsyncReplyChannel<NewTodo option>
    | Update of index: int * patch: TodoPatch * channel: AsyncReplyChannel<NewTodo option>
    | Remove of index: int * channel: AsyncReplyChannel<unit option>

let todoStorage = 
    let getTodo index (todos: NewTodo[]) =
        if todos.Length > index then
            let todo = todos.[index]
            if todo = NewTodo.Empty then
                None
            else Some todo
        else None

    Agent<_>.Start(fun inbox -> 
        let rec loop todos = 
            async { 
                let! msg = inbox.Receive()
                match msg with
                | GetAll ch -> 
                    ch.Reply (todos |> Array.filter (fun x -> x <> NewTodo.Empty))
                    return! loop todos
                | Post(todo, ch) -> 
                    ch.Reply todos.Length
                    return! loop (Array.append todos [|todo|])
                | Clear -> 
                    return! loop [||]
                | Get(index, ch) ->
                    let todo = getTodo index todos
                    ch.Reply todo
                    return! loop todos
                | Update(index, todo, ch) ->
                    let todo =
                        match getTodo index todos with
                        | Some temp ->
                            let update =
                                { temp with
                                    Title = defaultArg todo.Title temp.Title
                                    Completed = defaultArg todo.Completed temp.Completed
                                    Order = defaultArg todo.Order temp.Order }
                            todos.[index] <- update
                            Some update
                        | None -> None
                    ch.Reply todo
                    return! loop todos
                | Remove(index, ch) ->
                    let result = 
                        match getTodo index todos with
                        | Some _ ->
                            todos.[index] <- NewTodo.Empty
                            Some ()
                        | None -> None
                    ch.Reply result
                    return! loop todos
            }
        loop [||])

open System.IO
open System.Threading.Tasks
open Dyfrig
open Newtonsoft.Json

let serializerSettings = JsonSerializerSettings(ContractResolver = Serialization.CamelCasePropertyNamesContractResolver())
serializerSettings.Converters.Add(OptionConverter())

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
    
(**
 * Root resource handlers
 *)

let getTodos (env: OwinEnv) = async {
    let environ = Environment.toEnvironment env
    let baseUri = Uri(environ.GetBaseUri().Value)
    let! todos = todoStorage.PostAndAsyncReply(fun ch -> GetAll ch)
    let todos' =
        todos
        |> Array.mapi (fun i x ->
            { Url = Uri(baseUri, sprintf "/%i" i)
              Title = x.Title
              Completed = x.Completed
              Order = x.Order })
    let result = serialize todos'
    let stream : Stream = unbox env.[Constants.responseBody]
    do! stream.AsyncWrite(result, 0, result.Length) }

let postTodo (env: OwinEnv) = async {
    // Retrieve the request body
    let stream : Stream = unbox env.[Constants.requestBody]
    let newTodo : NewTodo = deserialize stream
    // TODO: Handle invalid result

    // Persist the new todo
    let! index = todoStorage.PostAndAsyncReply(fun ch -> Post(newTodo, ch))

    // Return the new todo item
    let environ = Environment.toEnvironment env
    let baseUri = Uri(environ.GetBaseUri().Value)
    let todo =
        { Url = Uri(baseUri, sprintf "/%i" index)
          Title = newTodo.Title
          Completed = newTodo.Completed
          Order = newTodo.Order }
    env.[Constants.responseStatusCode] <- 201
    env.[Constants.responseReasonPhrase] <- "Created"
    let headers : OwinHeaders = unbox env.[Constants.responseHeaders]
    headers.Add("Location", [| todo.Url.AbsoluteUri |])
    let result = serialize todo
    let stream : Stream = unbox env.[Constants.responseBody]
    do! stream.AsyncWrite(result, 0, result.Length) }

let deleteTodos (env: OwinEnv) =
    todoStorage.Post Clear
    env.[Constants.responseStatusCode] <- 204
    env.[Constants.responseReasonPhrase] <- "No Content"
    async.Return()


(**
 * Item resource handlers
 *)

let getTodo index (env: OwinEnv) = async {
    let environ = Environment.toEnvironment env
    let baseUri = Uri(environ.GetBaseUri().Value)
    let! todo = todoStorage.PostAndAsyncReply(fun ch -> Get(index, ch))
    match todo with
    | Some todo ->
        let todo' = 
            { Url = Uri(baseUri, sprintf "/%i" todo.Order)
              Title = todo.Title
              Completed = todo.Completed
              Order = todo.Order }
        let result = serialize todo'
        let stream : Stream = unbox env.[Constants.responseBody]
        do! stream.AsyncWrite(result, 0, result.Length)
    | None -> do! notFound env }

let patchTodo index (env: OwinEnv) = async {
    // Retrieve the request body
    let stream : Stream = unbox env.[Constants.requestBody]
    let patch : TodoPatch = deserialize stream
    // TODO: Handle invalid result

    // Try to patch the todo
    let! newTodo = todoStorage.PostAndAsyncReply(fun ch -> Update(index, patch, ch))

    match newTodo with
    | Some newTodo ->
        // Return the new todo item
        let environ = Environment.toEnvironment env
        let baseUri = Uri(environ.GetBaseUri().Value)
        let todo =
            { Url = Uri(baseUri, sprintf "/%i" index)
              Title = newTodo.Title
              Completed = newTodo.Completed
              Order = newTodo.Order }
        env.[Constants.responseStatusCode] <- 200
        env.[Constants.responseReasonPhrase] <- "OK"
        let result = serialize todo
        let stream : Stream = unbox env.[Constants.responseBody]
        do! stream.AsyncWrite(result, 0, result.Length)
    | None -> do! notFound env }

let deleteTodo index (env: OwinEnv) = async {
    let! result = todoStorage.PostAndAsyncReply(fun ch -> Remove(index, ch))
    match result with
    | Some _ ->
        env.[Constants.responseStatusCode] <- 204
        env.[Constants.responseReasonPhrase] <- "No Content"
    | None -> do! notFound env }

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
        | "PATCH" -> patchTodo index env
        | "DELETE" -> deleteTodo index env
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
