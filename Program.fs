namespace ProgramModule

open System
open Akka.FSharp
open System.Collections.Generic
open ChordNode
open ConfigModule

module ProgramModule = 

    let mutable mainActorRef = null
    let mutable firstNodeReference = null
    let mutable secondNodeReference = null
    let MainActor (mailbox:Actor<_>) =    
        let mutable secondNodeId = 0
        let mutable tempNodeId = 0
        let mutable tempNodeRef = null
        let list = new List<int>()

        let rec loop () = 
            actor {
                let! (message) = mailbox.Receive()

                match message with 
                | StartAlgorithm(numNodes, numRequests) ->
                    ChordNodeModule.firstNodeId <- Random().Next(int(ChordNodeModule.hashSpace))
                    printfn "\n\n ADDING %d" ChordNodeModule.firstNodeId
                    firstNodeReference <- spawn ChordNodeModule.chordSystem (sprintf "%d" ChordNode.ChordNodeModule.firstNodeId) (ChordNode.ChordNodeModule.ChordNode ChordNode.ChordNodeModule.firstNodeId)
                    // Second Node
                    secondNodeId <- Random().Next(int(ChordNodeModule.hashSpace))
                    printfn "\n\n ADDING %d" secondNodeId
                    secondNodeReference <- spawn ChordNodeModule.chordSystem (sprintf "%d" secondNodeId) (ChordNode.ChordNodeModule.ChordNode secondNodeId)
                    firstNodeReference <! Create(secondNodeId, secondNodeReference)
                    secondNodeReference <! Create(ChordNodeModule.firstNodeId, firstNodeReference)

                    for x in 3..numNodes do
                        // System.Threading.Thread.Sleep(300)
                        //tempNodeId <- Random().Next(1, hashSpace)
                        tempNodeId <- [ 1 .. ChordNodeModule.hashSpace ]
                            |> List.filter (fun x -> (not (list.Contains(x))))
                            |> fun y -> y.[Random().Next(y.Length - 1)]
                        list.Add(tempNodeId)
                        printfn "\n\n%d ADDING %d" x tempNodeId
                        tempNodeRef <- spawn ChordNodeModule.chordSystem (sprintf "%d" tempNodeId) (ChordNode.ChordNodeModule.ChordNode tempNodeId)
                        firstNodeReference <! FindNewNodeSuccessor(tempNodeId, tempNodeRef)  
                    
                    printfn "\n Ring stabilized"
                    System.Threading.Thread.Sleep(100)
                    firstNodeReference <! StartLookups(numRequests)

                | _ -> ()

                return! loop()
            }
        loop()


    [<EntryPoint>]
    let main argv =
        let numberOfNodes =  argv.[0] |> int
        let numberOfRequests = argv.[1] |> int
        ConfigModule.completionThreshold <- numberOfNodes * numberOfRequests
        mainActorRef <- spawn ChordNodeModule.chordSystem "MainActor" MainActor
        mainActorRef <! StartAlgorithm(numberOfNodes, numberOfRequests)
        ChordNode.ChordNodeModule.chordSystem.WhenTerminated.Wait();
        0