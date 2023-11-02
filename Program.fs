namespace ProgramModule

open System
open Akka.FSharp
open System.Collections.Generic
open System.Threading
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
                    // create the first node
                    ChordNodeModule.firstNodeId <- Random().Next(int(ChordNodeModule.hashSpace))
                    printfn "Added node 1 with ID: %d" ChordNodeModule.firstNodeId
                    initialNodeRef <- spawn ChordNodeModule.chordSystem (sprintf "%d" ChordNodeModule.firstNodeId) (ChordNodeModule.ChordNode ChordNodeModule.firstNodeId)
                    
                    // create the second node
                    nodeTwoId <- Random().Next(int(ChordNodeModule.hashSpace))
                    printfn "Added node 2 with ID: %d" nodeTwoId
                    subsequentNodeRef <- spawn ChordNodeModule.chordSystem (sprintf "%d" nodeTwoId) (ChordNodeModule.ChordNode nodeTwoId)
                    initialNodeRef <! Create(nodeTwoId, subsequentNodeRef)
                    subsequentNodeRef <! Create(ChordNodeModule.firstNodeId, initialNodeRef)

                    // loop and create the rest of the nodes
                    for item in 3..numNodes do
                        temporaryNodeId <- [ 1 .. ChordNodeModule.hashSpace ]
                            |> List.filter (fun x -> (not (list.Contains(item))))
                            |> fun y -> y.[Random().Next(y.Length - 1)]
                        list.Add(temporaryNodeId)
                        printfn "Added node %d with ID: %d" item temporaryNodeId
                        temporaryNodeRef <- spawn ChordNodeModule.chordSystem (sprintf "%d" temporaryNodeId) (ChordNodeModule.ChordNode temporaryNodeId)
                        initialNodeRef <! FindNewNodeSuccessor(temporaryNodeId, temporaryNodeRef)  
                    
                    printfn "---------------------------------"
                    printfn "Ring formation completed"
                    printfn "---------------------------------"
                    Thread.Sleep(100)

                    // initiate the lookups and requests
                    initialNodeRef <! StartLookups(numRequests)

                | _ -> ()

                return! loop()
            }
        loop()


    [<EntryPoint>]
    let main argv =
        // parse command line arguments
        // and read number of nodes and number of requests
        let numberOfNodes =  argv.[0] |> int
        let numberOfRequests = argv.[1] |> int
        
        if numberOfNodes < 2 then
            printfn "A single node cannot form a ring. Terminating."
            Environment.Exit(-1)

        // spawn the main actor and start the ring formation
        ConfigModule.completionThreshold <- numberOfNodes * numberOfRequests
        primaryActorReference <- spawn ChordNodeModule.chordSystem "MainActor" MainActor
        primaryActorReference <! StartAlgorithm(numberOfNodes, numberOfRequests)
        
        // wait till the chord system terminates
        ChordNodeModule.chordSystem.WhenTerminated.Wait();
        0