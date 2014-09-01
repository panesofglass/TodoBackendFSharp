module TodoBackendFSharp.TodoBackend

open System

type Agent<'T> = MailboxProcessor<'T>

type Todo = 
    { Uri : Uri
      Title : string
      Completed : bool
      Order : int }

type TodoOperation = 
    | GetAll of reply : AsyncReplyChannel<Todo list>
    | Post of todo : Todo
    | Update of todo : Todo
    | Delete of uri : Uri

let todoStorage = 
    Agent<_>.Start(fun inbox -> 
        let rec loop todos = 
            async { 
                let! msg = inbox.Receive()
                match msg with
                | GetAll ch -> 
                    ch.Reply todos
                    return! loop todos
                | Post todo -> 
                    // TODO: Return a new URI?
                    return! loop (todo :: todos)
                | Update todo -> 
                    let todos' = todos |> List.filter (fun t -> t.Uri = todo.Uri)
                    if todos'.Length = todos.Length then return! loop todos // TODO: Handle the 404 scenario
                    else return! loop (todo :: todos')
                | Delete uri -> 
                    let todos' = todos |> List.filter (fun t -> t.Uri = uri)
                    return! loop todos'
            }
        loop [])
