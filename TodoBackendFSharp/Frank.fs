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

module TodoBackend.Frank

open System
open System.Net
open System.Net.Http
open System.Threading.Tasks
open System.Web.Http
open System.Web.Http.HttpResource
open Newtonsoft.Json
open TodoStorage
open TodoStorage.InMemory

let private makeTodo (uri: Uri) (todo: NewTodo) =
    { Url = Uri(uri.AbsoluteUri + todo.Id.ToString()) // TODO: Uri(this.Url.Link("GetTodo", dict ["id", i]))
      Title = todo.Title
      Completed = todo.Completed
      Order = todo.Order }

let getTodos (request: HttpRequestMessage) = async {
    let! todos = store.GetAll()
    let todos' = todos |> Array.map (makeTodo request.RequestUri)
    return request.CreateResponse(todos') }

let postTodo (request: HttpRequestMessage) = async {
    let! content = request.Content.ReadAsStringAsync() |> Async.AwaitTask
    let config = request.GetConfiguration()
    let settings = config.Formatters.JsonFormatter.SerializerSettings
    let newTodo = JsonConvert.DeserializeObject<NewTodo>(content, settings)

    // Persist the new todo
    let! index = store.Post newTodo

    // Return the new todo item
    // TODO: Debug `this.Url.Link`.
    //let newUrl = Uri(this.Url.Link("GetTodo", dict ["id", index]))
    let todo = makeTodo request.RequestUri { newTodo with Id = index }
    let response = request.CreateResponse(HttpStatusCode.Created, todo)
    response.Headers.Location <- todo.Url
    return response }

let deleteTodos (request: HttpRequestMessage) =
    store.Clear()
    request.CreateResponse(HttpStatusCode.NoContent)
    |> async.Return

let getTodo (request: HttpRequestMessage) = async {
    match getParam request "id" with
    | Some id ->
        let id = Int32.Parse id
        let! todo = store.Get id
        match todo with
        | Some todo ->
            let todo' = makeTodo request.RequestUri todo
            return request.CreateResponse(todo')
        | None -> return request.CreateResponse(HttpStatusCode.NotFound)
    | None -> return request.CreateResponse(HttpStatusCode.NotFound) }

let patchTodo (request: HttpRequestMessage) = async {
    match getParam request "id" with
    | Some id ->
        let id = Int32.Parse id
        let! content = request.Content.ReadAsStringAsync() |> Async.AwaitTask
        let config = request.GetConfiguration()
        let settings = config.Formatters.JsonFormatter.SerializerSettings
        let patch = JsonConvert.DeserializeObject<TodoPatch>(content, settings)
        // TODO: Handle invalid result

        // Try to patch the todo
        let! newTodo = store.Update(id, patch)

        match newTodo with
        | Some newTodo ->
            // Return the new todo item
            let todo = makeTodo request.RequestUri newTodo
            return request.CreateResponse(todo)
        | None -> return request.CreateResponse(HttpStatusCode.NotFound)
    | None -> return request.CreateResponse(HttpStatusCode.NotFound) }

let deleteTodo (request: HttpRequestMessage) = async {
    match getParam request "id" with
    | Some id ->
        let id = Int32.Parse id
        let! result = store.Remove id
        let statusCode =
            match result with
            | Some _ -> HttpStatusCode.NoContent
            | None -> HttpStatusCode.NotFound
        return request.CreateResponse statusCode
    | None -> return request.CreateResponse(HttpStatusCode.NotFound) }

let todosResource basePath =
    let path = "/" + basePath
    route path (get getTodos <|> post postTodo <|> delete deleteTodos)

let patch handler =
    mapResourceHandler(HttpMethod "PATCH", handler)

let todoResource basePath =
    let path = sprintf "/%s/{id}" basePath
    route path (get getTodo <|> patch patchTodo <|> delete deleteTodo)

let register basePath (config: HttpConfiguration) =
    config |> register [todoResource basePath; todosResource basePath]
    let serializerSettings = config.Formatters.JsonFormatter.SerializerSettings
    serializerSettings.ContractResolver <- Serialization.CamelCasePropertyNamesContractResolver()
    serializerSettings.Converters.Add(OptionConverter())

let app basePath : Dyfrig.OwinEnv -> Task =
    let config = new HttpConfiguration()
    register basePath config
    let server = new HttpServer(config)
    // TODO: Map exception handling to OWIN exception handling.
    let handler = new AsyncCallableHandler(server)
    let cts = new System.Threading.CancellationTokenSource()
    Dyfrig.SystemNetHttpAdapter.fromSystemNetHttp(fun request -> handler.CallSendAsync(request, cts.Token)).Invoke
