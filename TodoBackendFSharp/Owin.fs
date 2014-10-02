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

module TodoBackend.Owin

open System
open System.IO
open System.Threading.Tasks
open Dyfrig
open Newtonsoft.Json
open TodoStorage
open TodoStorage.InMemory

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

let makeItemUri env index =
    let environ = Environment.toEnvironment env
    let baseUri = Uri(environ.GetBaseUri().Value)
    if environ.RequestPathBase = "/" then
        Uri(baseUri, sprintf "/%i" index)
    else
        Uri(baseUri, sprintf "%s/%i" environ.RequestPathBase index)

(**
 * Root resource handlers
 *)

let getTodos (env: OwinEnv) = async {
    let! todos = store.GetAll()
    let todos' =
        todos
        |> Array.map (fun x ->
            { Url = makeItemUri env x.Id
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
    let! index = store.Post newTodo

    // Return the new todo item
    let todo =
        { Url = makeItemUri env index
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
    store.Clear()
    env.[Constants.responseStatusCode] <- 204
    env.[Constants.responseReasonPhrase] <- "No Content"
    async.Return()


(**
 * Item resource handlers
 *)

let getTodo index (env: OwinEnv) = async {
    let! todo = store.Get index
    match todo with
    | Some todo ->
        let todo' = 
            { Url = makeItemUri env index
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
    let! newTodo = store.Update(index, patch)

    match newTodo with
    | Some newTodo ->
        // Return the new todo item
        let todo =
            { Url = makeItemUri env index
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
    let! result = store.Remove index
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
    let basePath = environ.RequestPathBase
    let template' = if String.IsNullOrEmpty basePath then template else basePath + template
    let uriTemplate = UriTemplate(template', ignoreTrailingSlash = true)
    let baseUri =
        // Get the base URI without the owin.RequestPathBase
        let temp = environ.GetBaseUri().Value
        if String.IsNullOrEmpty basePath then
            Uri(temp)
        else
        let index = temp.IndexOf(environ.RequestPathBase)
        Uri(temp.Substring(0, index))
    let requestUri = Uri(environ.GetRequestUri().Value)
    let result = uriTemplate.Match(baseUri, requestUri)
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
