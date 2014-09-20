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

namespace TodoBackend

open System
open System.Net
open System.Net.Http
open System.Threading.Tasks
open System.Web.Http
open Newtonsoft.Json
open TodoStorage

[<RoutePrefix("webapi")>]
[<Route("")>]
type TodosController() =
    inherit ApiController()

    member this.GetTodos() =
        async {
            let! todos = store.PostAndAsyncReply(fun ch -> GetAll ch)
            let todos' =
                todos
                |> Array.mapi (fun i x ->
                    { Url = Uri(this.Request.RequestUri.AbsoluteUri + i.ToString()) // TODO: Uri(this.Url.Link("GetTodo", dict ["id", i]))
                      Title = x.Title
                      Completed = x.Completed
                      Order = x.Order })
            return this.Request.CreateResponse(todos') }
        |> Async.StartAsTask

    member this.PostTodo() =
        async {
            let! content = this.Request.Content.ReadAsStringAsync() |> Async.AwaitTask
            // Retrieve the configuration in order to retrieve the configured JSON formatter.
            // Why deserialize manually? Web API responds to the POST with the following:
            //     {"message":"The request contains an entity body but no Content-Type header. The inferred media type 'application/octet-stream' is not supported for this resource."}
            // Therefore, we have to tell Web API how we want to deserialize the content.
            let config = this.Request.GetConfiguration()
            let settings = config.Formatters.JsonFormatter.SerializerSettings
            let newTodo = JsonConvert.DeserializeObject<NewTodo>(content, settings)

            // Persist the new todo
            let! index = store.PostAndAsyncReply(fun ch -> Post(newTodo, ch))

            // Return the new todo item
            // TODO: Debug `this.Url.Link`.
            //let newUrl = Uri(this.Url.Link("GetTodo", dict ["id", index]))
            let newUrl = Uri(this.Request.RequestUri.AbsoluteUri + index.ToString())
            let todo =
                { Url = newUrl
                  Title = newTodo.Title
                  Completed = newTodo.Completed
                  Order = newTodo.Order }
            let response = this.Request.CreateResponse(HttpStatusCode.Created, todo)
            response.Headers.Location <- newUrl
            return response }
        |> Async.StartAsTask

    member this.DeleteTodos() =
        store.Post Clear
        this.Request.CreateResponse(HttpStatusCode.NoContent)

[<RoutePrefix("webapi")>]
[<Route("{id}")>]
type TodoController() =
    inherit ApiController()

    [<Route("{id}", Name = "GetTodo")>]
    member this.GetTodo(id) = // <-- NOTE: the parameter name MUST match the name in the route template.
        async {
            let! todo = store.PostAndAsyncReply(fun ch -> Get(id, ch))
            match todo with
            | Some todo ->
                let todo' = 
                    { Url = this.Request.RequestUri
                      Title = todo.Title
                      Completed = todo.Completed
                      Order = todo.Order }
                return this.Request.CreateResponse(todo')
            | None -> return this.Request.CreateResponse(HttpStatusCode.NotFound) }
        |> Async.StartAsTask

type internal AsyncCallableHandler (handler) =
    inherit DelegatingHandler(handler)
    member internal x.CallSendAsync(request, cancellationToken) =
        base.SendAsync(request, cancellationToken)

module WebApi =
    let register (config: HttpConfiguration) =
        config.MapHttpAttributeRoutes()
        let serializerSettings = config.Formatters.JsonFormatter.SerializerSettings
        serializerSettings.ContractResolver <- Serialization.CamelCasePropertyNamesContractResolver()
        serializerSettings.Converters.Add(OptionConverter())

    let app : Dyfrig.OwinEnv -> Task =
        let config = new HttpConfiguration()
        register config
        let server = new HttpServer(config)
        // TODO: Map exception handling to OWIN exception handling.
        let handler = new AsyncCallableHandler(server)
        let cts = new System.Threading.CancellationTokenSource()
        Dyfrig.SystemNetHttpAdapter.fromSystemNetHttp(fun request -> handler.CallSendAsync(request, cts.Token)).Invoke
