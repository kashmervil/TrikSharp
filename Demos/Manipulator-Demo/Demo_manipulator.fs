﻿open Trik
open System.Threading

let exit = new EventWaitHandle(false, EventResetMode.AutoReset)

[<EntryPoint>]
let main argv = 
    printfn "Started"
    use model = new Model()
    model.ServosConfig.[E1] <- Defaults.Servo7
    model.ServosConfig.[E2] <- Defaults.Servo7
   
    let servo1 = model.Servos.[E2]
    let servo2 = model.Servos.[E1]
    let p1 = model.Motors.[M2]
    let p2 = model.Motors.[M3]
    let pad = model.Pad
    use dbtn = pad.Buttons.Subscribe (function 
        | x when x = 5 -> 
            printfn "Exiting (start)"
            exit.Set() |> ignore
        | num -> printfn "%A" num)
    use disp = pad.Pads.Subscribe ( fun (num, coord) ->  
        match num, coord with
        | (1, Some(x, y) ) -> 
            printfn "servo1.SetPower(%d)" x
            servo1.SetPower(x)
            servo2.SetPower(-y * 2)
        | (2, Some(x, y) ) -> 
            printfn "power %d %d" x y
            let base' = y
            let add' = x / 2
            p1.SetPower(base' + add')
            p2.SetPower(base' - add')
        | (2, None) -> 
            printfn "None 2" 
        | _ -> () 
    )

    printfn "Ready (5 pad btn to finish)"
    exit.WaitOne() |> ignore
    printfn "Exiting (after wait)"
    0
   