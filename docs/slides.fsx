(**
- title : 0 to Production in Twelve Weeks with F# on the Web
- description : The story of how Tachyus developed an application using F# in twelve weeks and replaced a customer's existing system.
- author : Ryan Riley
- theme : night
- transition : default

***

*)

(*** hide ***)
#I "../packages"

#r "System.Configuration.dll"
#r "System.Net.Http.dll"

#I "../packages/FSharp.Data.SqlClient.1.5.6/lib/net40"
#r "Microsoft.SqlServer.Types.dll"
#r "FSharp.Data.SqlClient.dll"

#r "Microsoft.AspNet.Cors.5.2.2/lib/net45/System.Web.Cors.dll"
#r "Microsoft.AspNet.WebApi.Client.5.2.2/lib/net45/System.Net.Http.Formatting.dll"
#r "Microsoft.AspNet.WebApi.Core.5.2.2/lib/net45/System.Web.Http.dll"
#r "Microsoft.AspNet.WebApi.Owin.5.2.2/lib/net45/System.Web.Http.Owin.dll"
#r "Microsoft.Owin.3.0.0/lib/net45/Microsoft.Owin.dll"
#r "Microsoft.Owin.Cors.3.0.0/lib/net45/Microsoft.Owin.Cors.dll"
#r "Newtonsoft.Json.6.0.4/lib/net40/Newtonsoft.Json.dll"
#r "Owin.1.0/lib/net40/owin.dll"
#r "Frank.3.1.1/lib/net45/Frank.dll"

open Owin
open Microsoft.Owin
open System
open System.Collections.Generic
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

- [Community for F#](http://c4fsharp.net/) Founder
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
  - Simplify
- Questions

***

# Tachyus

### Measure
### Analyze
### Produce

Est. 2014

***

## Starting Point

* No team
* First client's existing systems:
  * PHP-based web app for reporting
  * Paper/pencil data collection from oil fields

***

## Twelve Weeks Later ...

***

## Team

* 11 total
* 5 Software Engineering Team
* 3 Experienced F# Developers
* 2 Visual F# MVPs

***

## Modular SPA

* Monitoring
* Reporting
* Data Correction
* Administration

***

## Mobile

* Data collection
* Synchronization

***

## Web APIs

* Mobile sync API
* Calculation engines for reports and monitoring
* Domain to Web API mapping **_<- our focus_**

***

# Why F#?

![F# Software Foundation](images/fssf.png)

***

## Initially Skeptical

***

## Prototyping

***

## Talent Pool Assessment

***

## Why We Chose F#
### People

* Solved a talent problem
* Attract really good people
* Good fit for data analysts

***

## Active, Strong Community

<blockquote>
  I like this community better than any community I've seen out there.
  <footer>
    <cite>Dakin Sloss, Tachyus Founder &amp; CEO</cite>
  </footer>
</blockquote>

***

## Why We Chose F#
### Technical

* Domain-focused programming
* Math / science-focused solution
* Full .NET compatibility
* Cross-platform

***

# Getting Started

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

http://todobackend.com/

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

let todo1 = GetTodo.Record(0, "New todo", false, 1)
let todo2 = GetTodo.Record(Id = 0, Title = "New todo",
                           Completed = false, Order = 2)

(**

***

## Project:
## Implement Todo Data Access

***

# F# on the Web

***

## HTTP is Functional

*)

type Req = HttpRequestMessage
type Res = HttpResponseMessage

type Http = Req -> Res

(**

***

## HTTP in ASP.NET Web API

*)

type HttpWebApi = Req -> Task<Res>

(**

***

## HTTP in F#

*)

type HttpFSharp = Req -> Async<Res>

(**

***

## What About Resource State?

*)

type HiddenState = Req -> Async<Res>

type ExplicitState<'T> =
    Req ->
     // Retrieve resource state from the `Req`
     (Req -> Async<'T>) ->
     // Execute domain logic
     ('T -> 'T) ->    // or ('T -> Async<'T>)
     // Generate the response
     Async<Res>

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
### Startup.fs

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
            let todos' =
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

# Modularize for Composition

***

## Extract the handler function

*)

let handle (request: Req) = async {
    let! todos = Sql.store.GetAll()
    let todos' =
        todos
        |> Array.map (fun x ->
            let baseUri = request.RequestUri.AbsoluteUri
            { Url = Uri(baseUri + x.Id.ToString())
              Title = x.Title
              Completed = x.Completed
              Order = x.Order })
    return request.CreateResponse(todos') }

[<Route("webapi")>]
type TodoController() =
    inherit ApiController()
    member this.GetTodos(request) =
        handle request |> Async.StartAsTask

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

## Extract the Request Mapping Function

*)

let getResource (request: Req) = async {
    let! todos = Sql.store.GetAll()
    return request.RequestUri, todos }

(**

***

## Extract the Domain Function

*)

let mapValues (uri: Uri, todos: NewTodo[]) =
    let todos' =
        todos
        |> Array.map (fun x ->
            { Url = Uri(uri.AbsoluteUri + x.Id.ToString())
              Title = x.Title
              Completed = x.Completed
              Order = x.Order })
        |> Seq.toArray
    uri, todos'

(**

***

## Compose

*)

let makeHandler (request: Req) requestF domainF = async {
    let! res = requestF request
    let (_, values) = domainF res
    return request.CreateResponse(values) }

let handler request = makeHandler request getResource mapValues

(**

***

## Map Domain onto the Web

*)

module Domain =
    let domainLogic (value: string) = value.ToUpper()

module Web =
    let unwrapRequest (request: Req) = async {
        return! request.Content.ReadAsStringAsync() |> Async.AwaitTask }

    let wrapResponse value (request: Req) = request.CreateResponse(value)

module App =
    let handle request = async {
        let! value = Web.unwrapRequest request
        let value' = Domain.domainLogic value
        return Web.wrapResponse value' request }
(**

***

## Remaining Problems

![I think you should be more explicit here in step two](images/explicit.gif)

***

## Remaining Problems

* The Web API controller cannot be nested under a `module`
* Web API controller must be named with `Controller` as the suffix
* Routing has implementation-related limitations

***

## Skip Web API Altogether

*)

open Frank
open System.Web.Http.HttpResource

module Sample =
    let getHandler (request: Req) = async {
        return request.CreateResponse() }

    let postHandler (request: Req) = async {
        let! value = request.Content.ReadAsStringAsync() |> Async.AwaitTask
        // Do something with value
        return request.CreateResponse(HttpStatusCode.Created, value) }

    let sampleResource =
        routeResource "/api/sample"
                    [ get getHandler
                      post postHandler ]
    
    let registerSample config = config |> register [sampleResource]

(**

***

## Demo:
## Simplify

***

## F# + Web Take Aways

* Production-ready
* Facilitates rapid development (and prototyping)
* Shifts focus to domain
* Provides simple composition

***

## Resources

* [F# Software Foundation](http://fsharp.org/guides/web)
* [Community for F#](http://c4fsharp.net/)
* Sergey Tihon's [F# Weekly](http://sergeytihon.wordpress.com/category/f-weekly/)
* [F# for Fun and Profit](http://fsharpforfunandprofit.com/)
* [Real World Functional Programming](http://msdn.microsoft.com/en-us/library/vstudio/hh314518(v=vs.100).aspx) on MSDN

***

# Questions?

*)
