﻿namespace Trik
open System
open System.Net
open System.Net.Sockets
open System.Reactive.Linq
open System.Text
open System.Collections.Generic


[<RequireQualifiedAccess>]
type PadEvent = 
    | Pad of int * ( int * int ) option
    | Button of int
    | Wheel of int
    | Stop

type PadServer(?port) =
    let padPortVal = defaultArg port 4444
    let observers = new HashSet<IObserver<PadEvent> >()
    let obs = Observable.Create(fun observer -> 
        observers.Add(observer) |> ignore; 
        { new IDisposable with 
            member this.Dispose() = () } )
    let obsNext (x:PadEvent) = observers |> Seq.iter (fun obs -> obs.OnNext(x) ) 
    let mutable working = false
    let mutable request_accumulator = ""
    let notSEmpty (s:string) = (s.Length > 0)
    let handleRequst (req:String) = 
        match req.TrimEnd([| '\r' |]).Split([| ' ' |]) |> Array.toList |> List.filter (fun s -> s.Length > 0) with
        | ["wheel"; x ] -> PadEvent.Wheel(Int32.Parse(x) ) |> obsNext 
        | ["pad"; n; "up"] -> PadEvent.Pad(Int32.Parse(n), None ) |> obsNext 
        | ["pad"; n; x; y] -> PadEvent.Pad(Int32.Parse(n), Some (Int32.Parse(x), Int32.Parse(y) ) ) |> obsNext 
        | ["btn"; n; "down"] -> PadEvent.Button(Int32.Parse(n) ) |> obsNext 
        | _ -> ()
    let server = async {
        let listener = new TcpListener(IPAddress.Any, padPortVal)
        listener.Start()
        printfn "Listening  now on %d..." padPortVal
        let rec loop() = async {
            let client = listener.AcceptTcpClient()
            if not working then 
                ()
            else 
                let rec clientLoop() = async {
                    let isDone = ref false
                    while true do 
                        if not client.Connected then isDone := true
                        else 
                            let buf = Array.create client.ReceiveBufferSize <| byte 0
                            let count = client.GetStream().Read(buf, 0, buf.Length) 
                            let part = Encoding.ASCII.GetString(buf, 0, count)   
                            request_accumulator <- request_accumulator + part
                            if String.exists ( (=) '\n') request_accumulator then
                                let lines = request_accumulator.Split([| '\n' |])
                                request_accumulator <- lines.[lines.Length - 1]
                                lines 
                                |> Array.toSeq 
                                |> Seq.take (lines.Length - 1) 
                                |> Seq.filter (notSEmpty) 
                                |> Seq.iter handleRequst
                            else 
                                ()
                }
                Async.Start(clientLoop() )
            return! loop() 
        }
        Async.Start(loop() )
    }
    do working <- true
    do Async.Start server
    let btns = obs |> Observable.choose (function PadEvent.Button(x) -> Some(x) | _ -> None)
    let pads = obs |> Observable.choose (function PadEvent.Pad(p, c) -> Some(p, c) | _ -> None)
    member val Observable = obs
    member val Buttons = btns
    member val Pads = pads
    interface IDisposable with
        member x.Dispose() = 
            working <- false

            