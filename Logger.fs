namespace LoggerModule

open Akka.FSharp
open ConfigModule
open System.Threading

type MainCommands =
    | FoundKey of (int)

module LoggerModule =
    let logActor (actorMailbox: Actor<_>) =
        let mutable totalHopCount = 0
        let mutable totalRequests = 0

        let rec processMessages () =
            actor {
                let! message = actorMailbox.Receive()
                match message with
                | FoundKey hopCount ->
                    // count the toal number of requests and hop counts
                    totalHopCount <- totalHopCount + hopCount
                    totalRequests <- totalRequests + 1
                    printfn "Request no.: %d, Hop Count: %d" totalRequests hopCount
                    Thread.Sleep(1000)

                    // if the hop count reaches the completion threshold
                    // calculate the average hop count and terminate
                    if totalRequests = ConfigModule.completionThreshold then 
                        let averageHopCount = float(totalHopCount) / float(totalRequests)
                        printfn "---------------------------------"
                        printfn "Average Hop count: %.2f" averageHopCount
                        printfn "---------------------------------"
                        actorMailbox.Context.System.Terminate() |> ignore
                return! processMessages()
            }
        processMessages()