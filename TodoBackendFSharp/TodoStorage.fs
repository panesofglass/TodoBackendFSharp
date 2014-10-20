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

module TodoBackend.TodoStorage

open System

let (|Success|Failure|) = function
    | Choice1Of2 x -> Success x
    | Choice2Of2 x -> Failure x

/// Todo Model
type Todo = 
    { Url : Uri
      Title : string
      Completed : bool
      Order : int }

type NewTodo =
    { Id: int
      Title : string
      Completed : bool
      Order : int }
    with
    static member Empty = { Id = -1; Title = "EMPTY"; Completed = false; Order = -1 }

type TodoPatch =
    { Title : string option
      Completed : bool option
      Order : int option }

type IContainer =
    abstract GetAll : unit -> Async<NewTodo[]>
    abstract Post : todo: NewTodo -> Async<int> 
    abstract Clear : unit -> unit
    abstract Get : index: int -> Async<NewTodo option>
    abstract Update : index: int * patch: TodoPatch -> Async<NewTodo option>
    abstract Remove : index: int -> Async<unit option>

module InMemory =
    type Agent<'T> = MailboxProcessor<'T>

    type TodoOperation =
        | GetAll of channel: AsyncReplyChannel<NewTodo[]>
        | Post of todo: NewTodo * channel: AsyncReplyChannel<int>
        | Clear
        | Get of index: int * channel: AsyncReplyChannel<NewTodo option>
        | Update of index: int * patch: TodoPatch * channel: AsyncReplyChannel<NewTodo option>
        | Remove of index: int * channel: AsyncReplyChannel<unit option>

    let store = 
        let getTodo index (todos: NewTodo[]) =
            if todos.Length > index then
                let todo = todos.[index]
                if todo = NewTodo.Empty then
                    None
                else Some todo
            else None

        let agent =
            Agent<_>.Start(fun inbox -> 
                let rec loop todos = 
                    async { 
                        let! msg = inbox.Receive()
                        match msg with
                        | GetAll ch -> 
                            ch.Reply (todos |> Array.filter (fun x -> x <> NewTodo.Empty))
                            return! loop todos
                        | Post(todo, ch) -> 
                            let index = todos.Length
                            ch.Reply index
                            return! loop (Array.append todos [| { todo with Id = index } |])
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
                                            Id = index
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

        { new IContainer with
            member __.GetAll() = agent.PostAndAsyncReply GetAll
            member __.Post(todo) = agent.PostAndAsyncReply(fun ch -> Post(todo, ch))
            member __.Clear() = agent.Post Clear
            member __.Get(index) = agent.PostAndAsyncReply(fun ch -> Get(index, ch))
            member __.Update(index, patch) = agent.PostAndAsyncReply(fun ch -> Update(index, patch, ch))
            member __.Remove(index) = agent.PostAndAsyncReply(fun ch -> Remove(index, ch)) }

module Sql =
    open System.Configuration
    open FSharp.Data

    /// Configured database connection string
    let [<Literal>] dbTodo = "name=Todo"

    /// See below.
    let private key = "ConnectionString"

    /// The cached connection string.
    let private connectionString =
        lazy
        match AppDomain.CurrentDomain.GetData(key) with
        | :? string as r -> r
        | _ -> match System.Configuration.ConfigurationManager.ConnectionStrings.["Todo"] with
               | null -> failwithf "invalid connection string name: %s" "Todo"
               | settings -> settings.ConnectionString

    type GetAll = SqlCommandProvider<"Sql/GetAll.sql", dbTodo>
    type Post = SqlCommandProvider<"Sql/Post.sql", dbTodo>
    type Clear = SqlCommandProvider<"Sql/Clear.sql", dbTodo>
    type Get = SqlCommandProvider<"
        select Id, Title, Completed, [Order]
        from Todo
        where Id = @id", dbTodo>
    type Update = SqlCommandProvider<"
        update Todo
        set Title = @title
          , Completed = @completed
          , [Order] = @order
        where Id = @id", dbTodo>
    type Remove = SqlCommandProvider<"
        delete from Todo
        where Id = @id", dbTodo>

    let store =
        { new IContainer with
            member __.GetAll() = async {
                use cmd = new GetAll(connectionString.Value)
                let! data = cmd.AsyncExecute()
                let todos =
                    [| for todo in data do
                        yield { Id = todo.Id; Title = todo.Title; Completed = todo.Completed; Order = todo.Order } |]
                return todos }

            member __.Post(todo) = async {
                use cmd = new Post(connectionString.Value)
                let! result = cmd.AsyncExecute(todo.Title, todo.Completed, todo.Order)
                return int (Seq.head result).Value }

            member __.Clear() =
                use cmd = new Clear(connectionString.Value)
                cmd.Execute() |> ignore

            member __.Get(index) = async {
                use cmd = new Get(connectionString.Value)
                let! data = cmd.AsyncExecute(index)
                if Seq.isEmpty data then
                    return None
                else
                    let todo = Seq.head data
                    return Some { Id = todo.Id; Title = todo.Title; Completed = todo.Completed; Order = todo.Order } }

            member __.Update(index, patch) = async {
                let! result = __.Get(index)
                match result with
                | Some todo ->
                    let update =
                        Get.Record(id = todo.Id,
                                   title = defaultArg patch.Title todo.Title,
                                   completed = defaultArg patch.Completed todo.Completed,
                                   order = defaultArg patch.Order todo.Order)
                    use cmd = new Update(connectionString.Value)
                    let! _ = cmd.AsyncExecute(update.Title, update.Completed, update.Order, update.Id)
                    return Some { Id = update.Id; Title = update.Title; Completed = update.Completed; Order = update.Order }
                | None -> return None }

            member __.Remove(index) = async {
                let! result =
                    async {
                        let cmd = new Remove(connectionString.Value)
                        return! cmd.AsyncExecute(index) }
                    |> Async.Catch
                match result with
                | Success _ -> return Some()
                | Failure _ -> return None }
        }

    let demo this that = this + that
    type Demo (this) =
        member x.Invoke(that) = this + that
