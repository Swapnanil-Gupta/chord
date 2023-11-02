namespace ProgramModule

open System
open Akka.FSharp
open System.Collections.Generic
open ChordNode
open ConfigModule

module ProgramModule = 

    let mutable primaryActorReference = null
    let mutable initialNodeRef = null
    let mutable subsequentNodeRef = null
    let MainActor (mailbox:Actor<_>) =    

        let mutable nodeTwoId = 0
        let mutable temporaryNodeId = 0
        let mutable temporaryNodeRef = null
        let list = new List<int>()

        let rec loop () = 
            actor {
                let! (message) = mailbox.Receive()

                match message with 
                | StartAlgorithm(numNodes, numRequests) ->
                    ChordNodeModule.firstNodeId <- Random().Next(int(ChordNodeModule.hashSpace))
                    printfn "\n\n ADDING %d" ChordNodeModule.firstNodeId
                    initialNodeRef <- spawn ChordNodeModule.chordSystem (sprintf "%d" ChordNode.ChordNodeModule.firstNodeId) (ChordNode.ChordNodeModule.ChordNode ChordNode.ChordNodeModule.firstNodeId)
                    // Second Node
                    nodeTwoId <- Random().Next(int(ChordNodeModule.hashSpace))
                    printfn "\n\n ADDING %d" nodeTwoId
                    subsequentNodeRef <- spawn ChordNodeModule.chordSystem (sprintf "%d" nodeTwoId) (ChordNode.ChordNodeModule.ChordNode nodeTwoId)
                    initialNodeRef <! Create(nodeTwoId, subsequentNodeRef)
                    subsequentNodeRef <! Create(ChordNodeModule.firstNodeId, initialNodeRef)

                    for item in 3..numNodes do
                        // System.Threading.Thread.Sleep(300)
                        //tempNodeId <- Random().Next(1, hashSpace)
                        temporaryNodeId <- [ 1 .. ChordNodeModule.hashSpace ]
                            |> List.filter (fun x -> (not (list.Contains(item))))
                            |> fun y -> y.[Random().Next(y.Length - 1)]
                        list.Add(temporaryNodeId)
                        printfn "\n\n%d ADDING %d" item temporaryNodeId
                        temporaryNodeRef <- spawn ChordNodeModule.chordSystem (sprintf "%d" temporaryNodeId) (ChordNode.ChordNodeModule.ChordNode temporaryNodeId)
                        initialNodeRef <! FindNewNodeSuccessor(temporaryNodeId, temporaryNodeRef)  
                    
                    printfn "\n Ring stabilized"
                    System.Threading.Thread.Sleep(100)
                    initialNodeRef <! StartLookups(numRequests)

                | _ -> ()

                return! loop()
            }
        loop()


    [<EntryPoint>]
    let main argv =
        let numberOfNodes =  argv.[0] |> int
        let numberOfRequests = argv.[1] |> int
        ConfigModule.completionThreshold <- numberOfNodes * numberOfRequests
        primaryActorReference <- spawn ChordNodeModule.chordSystem "MainActor" MainActor
        primaryActorReference <! StartAlgorithm(numberOfNodes, numberOfRequests)
        ChordNode.ChordNodeModule.chordSystem.WhenTerminated.Wait();
        0