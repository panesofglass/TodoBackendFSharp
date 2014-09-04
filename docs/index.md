# Todo-Backend

TodoBackendFSharp is a Todo-Backend implementation in [F#](http://fsharp.org/)
following the guidelines provided at http://todo-backend.thepete.net/.
You can [run the specs](http://todo-backend.thepete.net/specs/index.html?http://todomvcfsharp.azurewebsites.net/) or
[try the client](http://todo-backend.thepete.net/client/index.html?http://todomvcfsharp.azurewebsites.net/).

## Why Raw OWIN?

The first implementation was written as directly upon the [OWIN](http://owin.org/) spec as possible in order
to demonstrate the ease with which you can build web applications in F#. Specific features of F# demonstrated
in this implementation include:

* [Function composition for composing applications](https://github.com/panesofglass/TodoBackendFSharp/blob/master/TodoBackendFSharp/Startup.fs#L56-58)
* [Active Patterns for routing](https://github.com/panesofglass/TodoBackendFSharp/blob/master/TodoBackendFSharp/TodoBackend.fs#L251-279)
* [MailboxProcessor for in-memory storage](https://github.com/panesofglass/TodoBackendFSharp/blob/master/TodoBackendFSharp/TodoBackend.fs#L53-94)
* [Simple, Async functions as HTTP handlers](https://github.com/panesofglass/TodoBackendFSharp/blob/master/TodoBackendFSharp/TodoBackend.fs#L114-237)

## Libraries

TodoBackendFSharp uses two libraries for a very simple implementation using [OWIN](http://owin.org/), or the Open Web Interface for .NET.
These libraries are [Katana](https://katanaproject.codeplex.com/) and [Dyfrig](https://github.com/fsprojects/dyfrig).

### Katana

Microsoft's Katana components provide a number of hosts and many, reusable middlewares to ease the burden of
building web applications. Katana hosts make use of a [`Startup` class](https://github.com/panesofglass/TodoBackendFSharp/blob/master/TodoBackendFSharp/Startup.fs#L50)
with a single member conventionally named [`Configuration`](https://github.com/panesofglass/TodoBackendFSharp/blob/master/TodoBackendFSharp/Startup.fs#L54).
`Configuration` takes an `IAppBuilder` into which you mount middleware components.
In F#, you can write middleware components as simple functions taking the next `OwinAppFunc` handler
and an `OwinEnv` environment dictionary. F# allows you to chain these together naturally using the
`|>` operator. You can of course flip the order and use the `<|` operator if that reads better to you.
In the todo-backend implementation, we chain the actual `TodoBackend.app` into the `Link.middleware`,
and then into the `Cors.middleware`. By doing this, Katana will pass all requests first through the
`Cors.middleware`, then add the `Link` header, then run the application.

### Dyfrig

In this first version of the application, Dyfrig is used only for its type aliases and OWIN `Constants`.
In a few places, it's `Environment` wrapper type is used to reconstruct the request URI, though this hardly
takes advantage of the library. Future versions will leverage more features, including error handling through
the `OwinRailway` module.

## What's Next?

Next up, I'd like to provide implementations using the following tools:

- [ ] [ASP.NET Web API](http://asp.net/web-api)
- [ ] [Dyfrig's `OwinRailway`](https://github.com/fsprojects/dyfrig/blob/master/src/Dyfrig/OwinRailway.fsi)
- [ ] [Dyfrig's System.Net.Http adapter](https://github.com/fsprojects/dyfrig/blob/master/src/Dyfrig/SystemNetHttpAdapter.fsi)
- [ ] [Dyfrig's `OwinMonad`](https://github.com/fsprojects/dyfrig/blob/master/src/Dyfrig/OwinApp.fsi#L35)
- [ ] [Frost](https://github.com/xyncro/frost)
- [ ] [Taliesin for routing](https://github.com/frank-fs/taliesin)
- [ ] [Frank](http://frankfs.net/)
- [ ] [HyperF](https://github.com/eulerfx/HyperF)
- [ ] [Suave](http://suave.io/)

I think it would also be interesting to set up an Azure Worker Role running an F# server, such as [Fracture I/O](https://github.com/fracture-io/fracture) or even a [simple F# wrapper over `HttpListener`](http://msdn.microsoft.com/en-us/library/vstudio/hh297120(v=vs.100).aspx).
