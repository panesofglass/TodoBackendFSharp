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
open System.Diagnostics.Contracts
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open System.Web.Http
open Newtonsoft.Json
open TodoStorage
open TodoStorage.InMemory

module private Utils =

    let makeItemUri (request: HttpRequestMessage) index =
        // TODO: Get the UrlHelper working
        //Uri(request.GetUrlHelper().Link("GetTodo", dict ["id", index]))
        let uri = request.RequestUri
        let path = uri.AbsolutePath.Substring(1) // Skip the first '/'
        if path.[path.Length - 1] = '/' then
            Uri(uri.AbsoluteUri + string index)
        else uri

    let makeTodo request (todo: NewTodo) =
        { Url = makeItemUri request todo.Id
          Title = todo.Title
          Completed = todo.Completed
          Order = todo.Order }

[<RoutePrefix("webapi")>]
[<Route("")>]
type TodosController() =
    inherit ApiController()

    member this.GetTodos() =
        async {
            let! todos = store.GetAll()
            let todos' = todos |> Array.map (Utils.makeTodo this.Request)
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
            let! index = store.Post newTodo

            // Return the new todo item
            // TODO: Debug `this.Url.Link`.
            //let newUrl = Uri(this.Url.Link("GetTodo", dict ["id", index]))
            let newUrl = Uri(this.Request.RequestUri.AbsoluteUri + index.ToString())
            let todo = Utils.makeTodo this.Request { newTodo with Id = index }
            let response = this.Request.CreateResponse(HttpStatusCode.Created, todo)
            response.Headers.Location <- newUrl
            return response }
        |> Async.StartAsTask

    member this.DeleteTodos() =
        store.Clear()
        this.Request.CreateResponse(HttpStatusCode.NoContent)

[<RoutePrefix("webapi")>]
[<Route("{id}")>]
type TodoController() =
    inherit ApiController()

    [<Route("{id}", Name = "GetTodo")>]
    member this.GetTodo(id) = // <-- NOTE: the parameter name MUST match the name in the route template.
        async {
            let! todo = store.Get id
            match todo with
            | Some todo ->
                let todo' = Utils.makeTodo this.Request todo
                return this.Request.CreateResponse(todo')
            | None -> return this.Request.CreateResponse(HttpStatusCode.NotFound) }
        |> Async.StartAsTask

    member this.PatchTodo(id) =
        async {
            let! content = this.Request.Content.ReadAsStringAsync() |> Async.AwaitTask
            // Retrieve the configuration in order to retrieve the configured JSON formatter.
            // Why deserialize manually? Web API responds to the POST with the following:
            //     {"message":"The request contains an entity body but no Content-Type header. The inferred media type 'application/octet-stream' is not supported for this resource."}
            // Therefore, we have to tell Web API how we want to deserialize the content.
            let config = this.Request.GetConfiguration()
            let settings = config.Formatters.JsonFormatter.SerializerSettings
            let patch = JsonConvert.DeserializeObject<TodoPatch>(content, settings)
            // TODO: Handle invalid result

            // Try to patch the todo
            let! newTodo = store.Update(id, patch)

            match newTodo with
            | Some newTodo ->
                // Return the new todo item
                let todo = Utils.makeTodo this.Request newTodo
                return this.Request.CreateResponse(todo)
            | None -> return this.Request.CreateResponse(HttpStatusCode.NotFound) }
        |> Async.StartAsTask

    member this.DeleteTodo(id) =
        async {
            let! result = store.Remove id
            match result with
            | Some _ -> return this.Request.CreateResponse(HttpStatusCode.NoContent)
            | None   -> return this.Request.CreateResponse(HttpStatusCode.NotFound) }
        |> Async.StartAsTask

type internal AsyncCallableHandler (handler) =
    inherit DelegatingHandler(handler)
    member internal x.CallSendAsync(request, cancellationToken) =
        base.SendAsync(request, cancellationToken)

module WebApi =
    // TODO: Move this into Frank
    // Converted from http://aspnetwebstack.codeplex.com/SourceControl/latest#src/System.Web.Http.Owin/WebApiAppBuilderExtensions.cs
    let createOptions (server, configuration: HttpConfiguration, token) =
        Contract.Assert(server <> Unchecked.defaultof<_>)
        Contract.Assert(configuration <> Unchecked.defaultof<_>)
        Contract.Assert(token <> Unchecked.defaultof<_>)

        let services = configuration.Services
        Contract.Assert(services <> Unchecked.defaultof<_>)

        let bufferPolicySelector =
            let temp = services.GetHostBufferPolicySelector()
            if temp = Unchecked.defaultof<_> then
                new System.Web.Http.Owin.OwinBufferPolicySelector() :> Hosting.IHostBufferPolicySelector
            else temp
        let exceptionLogger = System.Web.Http.ExceptionHandling.ExceptionServices.GetLogger(services)
        let exceptionHandler = System.Web.Http.ExceptionHandling.ExceptionServices.GetHandler(services)

        new System.Web.Http.Owin.HttpMessageHandlerOptions(
            MessageHandler = server,
            BufferPolicySelector = bufferPolicySelector,
            ExceptionLogger = exceptionLogger,
            ExceptionHandler = exceptionHandler,
            AppDisposing = token)

    // TODO: Move this into Frank
    /// See https://katanaproject.codeplex.com/SourceControl/latest#src/Microsoft.Owin/Infrastructure/AppFuncTransition.cs
    [<Sealed>]
    type AppFuncTransition(next: Dyfrig.OwinAppFunc) =
        inherit Microsoft.Owin.OwinMiddleware(null)
        default x.Invoke(context: Microsoft.Owin.IOwinContext) =
            // TODO: check for null
            next.Invoke(context.Environment)
     
    // TODO: Move this into Frank
    /// Explicit wrapper for HttpMessageHandlerAdapter
    type OwinMessageHandlerMiddleware(next: Dyfrig.OwinAppFunc, options) =
        let nextKatana = AppFuncTransition(next) :> Microsoft.Owin.OwinMiddleware
        let webApiKatana = new System.Web.Http.Owin.HttpMessageHandlerAdapter(nextKatana, options)
        member x.Invoke(environment: Dyfrig.OwinEnv) =
            // TODO: check for null
            let context = new Microsoft.Owin.OwinContext(environment)
            webApiKatana.Invoke(context) 

    let register (config: HttpConfiguration) =
        config.MapHttpAttributeRoutes()
        let serializerSettings = config.Formatters.JsonFormatter.SerializerSettings
        serializerSettings.ContractResolver <- Serialization.CamelCasePropertyNamesContractResolver()
        serializerSettings.Converters.Add(OptionConverter())

    let app token : Dyfrig.OwinEnv -> Task =
        let config = new HttpConfiguration()
        register config
        let server = new HttpServer(config)
        // TODO: Map exception handling to OWIN exception handling.
        let options = createOptions(server, config, token)
        let adapter = new OwinMessageHandlerMiddleware(Unchecked.defaultof<_>, options)
        adapter.Invoke
