namespace LoggerModule

open Akka.FSharp
open ConfigModule

type MainCommands =
    | FoundKey of (int)

module LoggerModule =
    let logActor (actorMailbox: Actor<_>) =
        let mutable totalHopCount = 0
        let mutable totalRequests = 0

        let rec processMessages () =
            actor {
                let! message = actorMailbox.Receive()
                let sender = actorMailbox.Sender()

                match message with
                | FoundKey hopCount ->
                    totalHopCount <- totalHopCount + hopCount
                    totalRequests <- totalRequests + 1
                    printfn "\n %d FoundKey = %d" totalRequests hopCount
                    if totalRequests = ConfigModule.completionThreshold then 
                        let averageHopCount = float(totalHopCount) / float(totalRequests)
                        printfn "%.2f" averageHopCount
                        actorMailbox.Context.System.Terminate() |> ignore
                return! processMessages()
            }
        processMessages()