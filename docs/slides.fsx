(**
- title : 0 to Production in Twelve Weeks with F# on the Web
- description : The story of how Tachyus developed an application using F# in twelve weeks and replaced a customer's existing system.
- author : Ryan Riley
- theme : night
- transition : default

***

*)

(*** hide ***)
#I "../packages/FSharp.Data.SqlClient.1.3.3/lib/net40"
#r "Microsoft.SqlServer.Types.dll"
#r "FSharp.Data.SqlClient.dll"

#r "System.Net.Http.dll"
#r "../packages/Microsoft.AspNet.WebApi.Client.5.2.2/lib/net45/System.Net.Http.Formatting.dll"
#r "../packages/Microsoft.AspNet.WebApi.Core.5.2.2/lib/net45/System.Web.Http.dll"
#r "../packages/Microsoft.Owin.3.0.0/lib/net45/Microsoft.Owin.dll"
#r "../packages/Newtonsoft.Json.6.0.4/lib/net40/Newtonsoft.Json.dll"
#r "../packages/Owin.1.0/lib/net40/owin.dll"
#r "../packages/Frank.3.0.0.9/lib/net45/Frank.dll"

open System
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open System.Web.Http

AppDomain.CurrentDomain.SetData("ConnectionString",
    "Data Source=.;Initial Catalog=dbElephant;Integrated Security=SSPI")

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
- Data access
- Web APIs
- Domain mapping
- Questions

***

# Tachyus Software

---

## ASP.NET Web API via Azure Web Sites

---

## AngularJS SPA for back office

(Straight JavaScript; not currently using F# -> JavaScript compilation)

---

## iOS data collection for field

---

## Replaced Existing Systems:

* PHP-based web app for reporting
* Manual data collection from oil fields

***

# Why F#?

![F# Software Foundation](images/fssf.png)

---

## CEO Initially Skeptical

---

## CTO Created Protoype

---

## CEO Convinced

* Solved a talent problem
* Attract really good people
* Fit for data analysis

---

## CTO Convinced

* Domain-focused programming
* Math / science-focused solution
* Full .NET compatibility
* Cross-platform

---

## Active, Strong Community

<blockquote>
  I like this [tech] community better than any community I've seen out there.
  <footer>
    <cite>Dakin Sloss, Tachyus Founder &amp; CEO</cite>
  </footer>
</blockquote>

***

# F# and Data Access

---

## LINQ to SQL or Entity Framework?

---

## [**FSharp.Data.SqlClient**](https://fsprojects.github.io/FSharp.Data.SqlClient/)

<blockquote>
  SQL is the best DSL for working with data
  <footer>
    <cite><a href="http://www.infoq.com/articles/ORM-Saffron-Conery">Rob Conery</a></cite>
  </footer>
</blockquote>

---

## Quick Sample

*)

open FSharp.Data

let [<Literal>] connectionString =
    "Data Source=.;Initial Catalog=Todo;Integrated Security=SSPI"

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

let todo = GetTodo.Record(0, "New todo", false, 1)

(**

***

# F# on the Web

![I think you should be more explicit here in step two](images/explicit.gif)

---

## Getting Started

* [Web Programming with F#](http://fsharp.org/guides/web/)
* [F# MVC 5 Templates](https://visualstudiogallery.msdn.microsoft.com/39ae8dec-d11a-4ac9-974e-be0fdadec71b)
* [F# Nancy Templates](https://visualstudiogallery.msdn.microsoft.com/b55b8aac-b11a-4a6a-8a77-2153f46f4e2f)
* [Web Application Server (F# with SignalR)](http://visualstudiogallery.msdn.microsoft.com/c7ea6e81-b383-40e4-899c-4a5ab9d68f02)

---

## Simplest HTTP Application

*)

type SimplestHttpApp =
    HttpRequestMessage -> HttpResponseMessage

(**

---

## HTTP "Services"

    GET /
    POST /additems
    POST /setresult?foo=bar
    GET /setresult?foo=bar

---

## HTTP Resources

    GET /item/1
    POST /item/1
    PUT /item/1
    DELETE /item/1
    OPTIONS /item/1

---

## HTTP in ASP.NET Web API

*)

[<Route("api/simple")>]
type SimplestWebApiController() =
    inherit ApiController()
    member this.Get() =
        // Do stuff
        this.Request.CreateResponse()

(**

---

## *Simplest* HTTP in ASP.NET Web API

*)

type SimplestWebApi() =
    inherit DelegatingHandler()
    override this.SendAsync(request, cancelationToken) =
        // Do stuff
        let response = request.CreateResponse()
        Task.FromResult(response)

(**

---

## HTTP in F#

*)

type SimplestFSharpHttpApp =
    HttpRequestMessage -> Async<HttpResponseMessage>

let handler (request: HttpRequestMessage) =
    async {
        // Do stuff
        return request.CreateResponse()
    }

(**

---

## Putting it All Together

*)

let handleGet (request: HttpRequestMessage) = async {
    // Do stuff
    return request.CreateResponse()
}

let handlePost value (request: HttpRequestMessage) = async {
    // Do something with value
    return request.CreateResponse(HttpStatusCode.Created, value)
}

[<Route("api/together")>]
type TogetherController() =
    inherit ApiController()
    member this.Get() =
        handleGet this.Request |> Async.StartAsTask :> Task
    member this.Post(value) =
        handlePost value this.Request |> Async.StartAsTask :> Task

(**

---

## Or Skip Web API Altogether

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

## Why Extract the Handlers?

<blockquote>
    The web is a delivery mechanism.
    <footer>
        <cite><a href="https://vimeo.com/68215570">"Uncle" Bob Martin</a></cite>
    </footer>
</blockquote>

---

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

---

## Handling Domain Exceptions

*)

let remove = async {
    let! result =
        async {
            failwith "Oh no!"
            return 1 }
        |> Async.Catch
    match result with
    | Choice1Of2 _ -> return Some()
    | Choice2Of2 _ -> return None }

(**

---

## [Railway-Oriented Programming](http://fsharpforfunandprofit.com/posts/recipe-part2/)

<iframe src="https://player.vimeo.com/video/97344498" width="500" height="281" frameborder="0" webkitallowfullscreen mozallowfullscreen allowfullscreen></iframe> <p><a href="http://vimeo.com/97344498">Scott Wlaschin - Railway Oriented Programming -- error handling in functional languages</a> from <a href="http://vimeo.com/ndcoslo">NDC Conferences</a> on <a href="https://vimeo.com">Vimeo</a>.</p>

---

## [Domain Modeling](http://www.slideshare.net/ScottWlaschin/ddd-with-fsharptypesystemlondonndc2013)

<iframe src="https://player.vimeo.com/video/97507575" width="500" height="281" frameborder="0" webkitallowfullscreen mozallowfullscreen allowfullscreen></iframe> <p><a href="http://vimeo.com/97507575">Scott Wlaschin - Domain modelling with the F# type system</a> from <a href="http://vimeo.com/ndcoslo">NDC Conferences</a> on <a href="https://vimeo.com">Vimeo</a>.</p>

***

# Resources

---

## [F# Software Foundation](http://fsharp.org/guides/web)

---

## [Community for F#](http://c4fsharp.net/)

---

## Sergey Tihon's [F# Weekly](http://sergeytihon.wordpress.com/category/f-weekly/)

---

## [F# for Fun and Profit](http://fsharpforfunandprofit.com/)

---

## [Real World Functional Programming](http://msdn.microsoft.com/en-us/library/vstudio/hh314518(v=vs.100).aspx) on MSDN

***

# Questions?

*)
