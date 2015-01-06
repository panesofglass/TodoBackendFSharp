(**
- title : 0 to Production in Twelve Weeks with F# on the Web
- description : The story of how Tachyus developed an application using F# in twelve weeks and replaced a customer's existing system.
- author : Ryan Riley
- theme : night
- transition : default

***

*)

(*** hide ***)
#r "System.Configuration.dll"
#I "../packages/FSharp.Data.SqlClient.1.5.6/lib/net40"
#r "Microsoft.SqlServer.Types.dll"
#r "FSharp.Data.SqlClient.dll"

#r "System.Net.Http.dll"
#r "../packages/Microsoft.AspNet.Cors.5.2.2/lib/net45/System.Web.Cors.dll"
#r "../packages/Microsoft.AspNet.WebApi.Client.5.2.2/lib/net45/System.Net.Http.Formatting.dll"
#r "../packages/Microsoft.AspNet.WebApi.Core.5.2.2/lib/net45/System.Web.Http.dll"
#r "../packages/Microsoft.AspNet.WebApi.Owin.5.2.2/lib/net45/System.Web.Http.Owin.dll"
#r "../packages/Microsoft.Owin.3.0.0/lib/net45/Microsoft.Owin.dll"
#r "../packages/Microsoft.Owin.Cors.3.0.0/lib/net45/Microsoft.Owin.Cors.dll"
#r "../packages/Newtonsoft.Json.6.0.4/lib/net40/Newtonsoft.Json.dll"
#r "../packages/Owin.1.0/lib/net40/owin.dll"
#r "../packages/Frank.3.1.1/lib/net45/Frank.dll"

open Owin
open Microsoft.Owin
open System
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open System.Web.Http

let (|Success|Failure|) = function
    | Choice1Of2 x -> Success x
    | Choice2Of2 x -> Failure x

let [<Literal>] connectionString =
    "Data Source=.;Initial Catalog=Todo;Integrated Security=SSPI"
let [<Literal>] key = "ConnectionString"
AppDomain.CurrentDomain.SetData(key, connectionString)
let connString = lazy AppDomain.CurrentDomain.GetData(key)

#load "../TodoBackend/OptionConverter.fs"
#load "../TodoBackend/TodoStorage.fs"

open TodoBackend
open TodoBackend.TodoStorage

(**

# F# on the Web

## 0 to Production in 12 Weeks

***

## Ryan Riley

<img src="images/tachyus.png" alt="Tachyus logo" style="background-color: #fff; display: block; margin: 10px auto;" />

- Lead [Community for F#](http://c4fsharp.net/)
- Microsoft Visual F# MVP
- ASPInsider
- [OWIN](http://owin.org/) Management Committee

***

## Agenda

- Tachyus' success with F#
- Build an F# web API
  - Getting started
  - Data access
  - Web APIs
  - Domain mapping
- Questions

***

# Tachyus Software

***

## ASP.NET Web API via Azure Web Sites

***

## AngularJS SPA for back office

(Straight JavaScript; not currently using F# -> JavaScript compilation)

***

## iOS data collection for field

***

## Replaced Existing Systems:

* PHP-based web app for reporting
* Manual data collection from oil fields

***

# Why F#?

![F# Software Foundation](images/fssf.png)

***

## CEO Initially Skeptical

***

## CTO Created Prototype

***

## CEO Convinced

* Solved a talent problem
* Attract really good people
* Fit for data analysis

***

## CTO Convinced

* Domain-focused programming
* Math / science-focused solution
* Full .NET compatibility
* Cross-platform

***

## Active, Strong Community

<blockquote>
  I like this community better than any community I've seen out there.
  <footer>
    <cite>Dakin Sloss, Tachyus Founder &amp; CEO</cite>
  </footer>
</blockquote>

***

# F# on the Web

***

## Getting Started

* [Web Programming with F#](http://fsharp.org/guides/web/)
* [F# MVC 5 Templates](https://visualstudiogallery.msdn.microsoft.com/39ae8dec-d11a-4ac9-974e-be0fdadec71b)

***

## Demo:
## F# MVC 5 Template

1. File -> New -> Project
2. F# ASP.NET MVC 5 and Web API 2
3. Choose empty Web API project

***

# Project:
## Todo Backend

http://todo-backend.thepete.net/

***

# F# and Data Access

***

## LINQ to SQL or Entity Framework?

***

## [**FSharp.Data.SqlClient**](https://fsprojects.github.io/FSharp.Data.SqlClient/)

<blockquote>
  SQL is the best DSL for working with data
  <footer>
    <cite><a href="http://www.infoq.com/articles/ORM-Saffron-Conery">Rob Conery</a></cite>
  </footer>
</blockquote>

***

## Quick Sample

*)

open FSharp.Data

type GetTodos = SqlCommandProvider<"
    select Id, Title, Completed, [Order] from Todo", connectionString>
type GetTodo = SqlCommandProvider<"
    select Id, Title, Completed, [Order] from Todo where Id = @id
    ", connectionString, SingleRow = true>

let result =
    async {
        use cmd = new GetTodo()
        return! cmd.AsyncExecute(id = 1) }
    |> Async.RunSynchronously

let todo = GetTodo.Record(Id = 0, Title = "New todo", Completed = false, Order = 1)

(**

***

## Project:
## Implement Todo Data Access

***

# Leveraging F#

![I think you should be more explicit here in step two](images/explicit.gif)

***

## HTTP is Functional

*)

type SimplestHttpApp =
    HttpRequestMessage -> HttpResponseMessage

(**

***

## Project:
## Implement Todo Backend

Add the following NuGet packages:

 * Microsoft.AspNet.WebApi.Owin
 * Microsoft.Owin.Cors
 * Microsoft.Owin.Host.SystemWeb

***

## Configure

*)

[<Sealed>]
type Startup() =
    member __.Configuration(builder: IAppBuilder) =
        let config = new HttpConfiguration()
        config.MapHttpAttributeRoutes()
        builder
            .UseCors(Cors.CorsOptions.AllowAll)
            .UseWebApi(config)
        |> ignore

(**

***

## HTTP in ASP.NET Web API

*)

[<RoutePrefix("webapi")>]
[<Route("")>]
type TodosController() =
    inherit ApiController()

    member this.GetTodos() =
        async {
            let! todos = Sql.store.GetAll()
            let todos': TodoBackend.TodoStorage.Todo[] =
                todos
                |> Array.map (fun x ->
                    let baseUri = this.Request.RequestUri.AbsoluteUri
                    { Url = Uri(baseUri + x.Id.ToString())
                      Title = x.Title
                      Completed = x.Completed
                      Order = x.Order })
            return this.Request.CreateResponse(todos') }
        |> Async.StartAsTask

(**

***

## Extract the handler function

*)

type SimplestFSharpHttpApp =
    HttpRequestMessage -> Async<HttpResponseMessage>

let handler (request: HttpRequestMessage) = async {
    let! todos = Sql.store.GetAll()
    let todos' = todos // Do stuff
    return request.CreateResponse(todos') }

[<RoutePrefix("webapi")>]
[<Route("")>]
type TodoController() =
    inherit ApiController()
    member this.GetTodos() =
        this.Request
        |> handler
        |> Async.StartAsTask

(**

***

## Aside: simplest application signature?

*)

type SimplestFSharpApp =
    HttpRequestMessage -> Async<HttpResponseMessage>

type StateTransitions<'T> =
    HttpRequestMessage ->
     (HttpRequestMessage -> Async<'T>) ->
     ('T -> Async<'T>) ->
     Async<HttpResponseMessage>

type ExplicitTransitions<'T> =
    HttpRequestMessage -> HttpResponseMessage -> Async<'T -> HttpResponseMessage * 'T>

(**

***

## Extract the domain function

*)

let mapTodos (request: HttpRequestMessage) (todos: seq<NewTodo>) =
    todos
    |> Seq.map (fun x ->
        { Url = Uri(request.RequestUri.AbsoluteUri + x.Id.ToString())
          Title = x.Title
          Completed = x.Completed
          Order = x.Order })
    |> Seq.toArray

(**

***

## Why Extract the Handlers?

<blockquote>
    The web is a delivery mechanism.
    <footer>
        <cite><a href="https://vimeo.com/68215570">"Uncle" Bob Martin</a></cite>
    </footer>
</blockquote>

***

## Map Domain into the Web

*)

module Domain =
    let domainLogic (value: string) = value.ToUpper()

module Web =
    let unwrapRequest (request: HttpRequestMessage) = async {
        return! request.Content.ReadAsStringAsync() |> Async.AwaitTask
    }
    let wrapResponse value (request: HttpRequestMessage) =
        request.CreateResponse(value)

module App =
    let handle request = async {
        let! value = Web.unwrapRequest request
        let value' = Domain.domainLogic value
        return Web.wrapResponse value' request
    }

(**

***

## Handling Domain Exceptions

*)

type Remove = SqlCommandProvider<"
    delete from Todo
    where Id = @id", connectionString>

let remove index = async {
    let! result =
        async {
            let cmd = new Remove()
            return! cmd.AsyncExecute(index) }
        |> Async.Catch
    match result with
    | Success _ -> return Some()
    | Failure _ -> return None }

(**

***

## [Railway-Oriented Programming](http://fsharpforfunandprofit.com/posts/recipe-part2/)

<iframe src="https://player.vimeo.com/video/97344498" width="500" height="281" frameborder="0" webkitallowfullscreen mozallowfullscreen allowfullscreen></iframe> <p><a href="http://vimeo.com/97344498">Scott Wlaschin - Railway Oriented Programming -- error handling in functional languages</a> from <a href="http://vimeo.com/ndcoslo">NDC Conferences</a> on <a href="https://vimeo.com">Vimeo</a>.</p>

***

## [Domain Modeling](http://www.slideshare.net/ScottWlaschin/ddd-with-fsharptypesystemlondonndc2013)

<iframe src="https://player.vimeo.com/video/97507575" width="500" height="281" frameborder="0" webkitallowfullscreen mozallowfullscreen allowfullscreen></iframe> <p><a href="http://vimeo.com/97507575">Scott Wlaschin - Domain modelling with the F# type system</a> from <a href="http://vimeo.com/ndcoslo">NDC Conferences</a> on <a href="https://vimeo.com">Vimeo</a>.</p>

***

# Modularize

***

## Remaining Problems:

* The Web API controller cannot be nested under a `module`
* Web API controller must be named with `Controller` as the suffix

***

## Skip Web API Altogether

*)

open Frank
open System.Web.Http.HttpResource

module Sample =
    let getHandler (request: HttpRequestMessage) = async {
        return request.CreateResponse()
    }
    let postHandler (request: HttpRequestMessage) = async {
        let! value = request.Content.ReadAsStringAsync() |> Async.AwaitTask
        // Do something with value
        return request.CreateResponse(HttpStatusCode.Created, value)
    }

    let sampleResource =
        routeResource "/api/sample"
                    [ get getHandler
                      post postHandler ]
    
    let registerSample config = config |> register [sampleResource]

(**

***

# Resources

***

## [F# Software Foundation](http://fsharp.org/guides/web)

***

## [Community for F#](http://c4fsharp.net/)

***

## Sergey Tihon's [F# Weekly](http://sergeytihon.wordpress.com/category/f-weekly/)

***

## [F# for Fun and Profit](http://fsharpforfunandprofit.com/)

***

## [Real World Functional Programming](http://msdn.microsoft.com/en-us/library/vstudio/hh314518(v=vs.100).aspx) on MSDN

***

# Questions?

*)
