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

type Agent<'T> = MailboxProcessor<'T>

/// Todo Model
type Todo = 
    { Url : Uri
      Title : string
      Completed : bool
      Order : int }

type NewTodo =
    { Title : string
      Completed : bool
      Order : int }
    with
    static member Empty = { Title = "EMPTY"; Completed = false; Order = -1 }

type TodoPatch =
    { Title : string option
      Completed : bool option
      Order : int option }

module TodoStorage =
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

        Agent<_>.Start(fun inbox -> 
            let rec loop todos = 
                async { 
                    let! msg = inbox.Receive()
                    match msg with
                    | GetAll ch -> 
                        ch.Reply (todos |> Array.filter (fun x -> x <> NewTodo.Empty))
                        return! loop todos
                    | Post(todo, ch) -> 
                        ch.Reply todos.Length
                        return! loop (Array.append todos [|todo|])
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
