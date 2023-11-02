namespace ChordNode

open System
open Akka.Actor
open Akka.FSharp
open LoggerModule
open ConfigModule

type FingerTableEntry(x:int, y:IActorRef) =
    let id = x
    let idRef = y
    member this.GetId() = x
    member this.GetRef() = y

type MainCommands =
    | StartAlgorithm of (int*int)
    | Create of (int*IActorRef)
    | Notify of (int*IActorRef)
    | Stabilize
    | FindNewNodeSuccessor of (int*IActorRef)
    | FoundNewNodeSuccessor of (int*IActorRef)
    | PredecessorRequest
    | PredecessorResponse of (int*IActorRef)
    | KeyLookup of (int*int*int)
    | FixFingers
    | FindithSuccessor of (int*int*IActorRef)
    | FoundFingerEntry of (int*int*IActorRef)
    | StartLookups of (int)

module ChordNodeModule = 
    let mutable numberOfNodes = 0
    let mutable numberOfRequests = 0
    let mutable maxLengthOfTable = 20
    let mutable firstNodeId = 0
    let mutable spaceSize = pown 2 maxLengthOfTable
    let chordSystem = ActorSystem.Create("ChordSystem", ConfigModule.akkaConfiguration)
    let logger = spawn chordSystem "logActor" LoggerModule.logActor

    let ChordNode (myId:int) (mailbox:Actor<_>) =    
        let mutable mySuccessor = 0
        let mutable mySuccessorRef = null
        let mutable myPredecessor = 0
        let mutable myPredecessorRef = null
        let entry = FingerTableEntry(0, null)
        let myFingerTable : FingerTableEntry[] = Array.create maxLengthOfTable entry

        let rec loop () = 
            actor {
                let! (message) = mailbox.Receive()
                let sender = mailbox.Sender()

                match message with 
                // Initializes node's successor and predecessor with the given ID and reference, and populates the finger table.
                // Sets up recurring stabilization and finger table maintenance tasks for the Chord protocol.
                | Create (otherId, otherRef) ->
                    // First two nodes in the Chord Ring
                    mySuccessor <- otherId
                    myPredecessor <- otherId
                    mySuccessorRef <- otherRef
                    myPredecessorRef <- otherRef
                    for i in 0..maxLengthOfTable-1 do
                        let tuple = FingerTableEntry(mySuccessor, mySuccessorRef)
                        myFingerTable.[i] <- tuple
                    chordSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(0.0),TimeSpan.FromMilliseconds(ConfigModule.stabilizationInterval), mailbox.Self, Stabilize)
                    chordSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromSeconds(0.0),TimeSpan.FromMilliseconds(ConfigModule.fingerTableUpdateInterval), mailbox.Self, FixFingers)

                // Update the node's predecessor info for Chord ring maintenance on node changes.
                | Notify(predecessorId, predecessorRef) ->
                    myPredecessor <- predecessorId
                    myPredecessorRef <- predecessorRef

                // Periodically refreshes entries in the finger table to ensure accurate lookup.
                | FixFingers ->
                    let mutable ithFinger = 0
                    for i in 1..maxLengthOfTable-1 do
                        ithFinger <- ( myId + ( pown 2 i ) ) % int(spaceSize)
                        mailbox.Self <! FindithSuccessor(i, ithFinger, mailbox.Self)

                // Locates the successor for the ith entry in the finger table and notifies the inquiring node.
                | FindithSuccessor(i, key, tellRef) ->
                    if mySuccessor < myId && (key > myId || key < mySuccessor) then
                        tellRef <! FoundFingerEntry(i, mySuccessor, mySuccessorRef)
                    elif key <= mySuccessor && key > myId then 
                        tellRef <! FoundFingerEntry(i, mySuccessor, mySuccessorRef)
                    else 
                        let mutable stop = false 
                        let mutable size = maxLengthOfTable
                        let mutable tempVal = key
                        if myId > key then 
                            tempVal <- key + spaceSize
                        while not stop do
                            size <- size - 1
                            if size < 0 then   
                                mySuccessorRef <! FindithSuccessor(i, key, tellRef)
                                stop <- true
                            else
                                let ithFinger = myFingerTable.[size].GetId()
                                if (ithFinger > myId && ithFinger <= tempVal) then 
                                    let ithRef = myFingerTable.[size].GetRef()
                                    ithRef <! FindithSuccessor(i, key, tellRef)
                                    stop <- true                       
                        done                 

                // Updates the ith entry of the finger table with the provided successor node information.
                | FoundFingerEntry(i, fingerId, fingerRef) ->
                    let tuple = FingerTableEntry(fingerId, fingerRef)
                    myFingerTable.[i] <- tuple

                // Initiates the stabilization process by requesting the predecessor information from the current node's successor.
                | Stabilize ->
                    if mySuccessor <> 0 then 
                        mySuccessorRef <! PredecessorRequest

                // Processes the response containing the successor's predecessor info and updates the current node's successor if needed.
                | PredecessorResponse(predecessorOfSuccessor, itsRef) ->                    
                    if predecessorOfSuccessor <> myId then
                        mySuccessor <- predecessorOfSuccessor
                        mySuccessorRef <- itsRef
                    // Notify mysuccessor
                    mySuccessorRef <! Notify(myId, mailbox.Self)
                    
                 // Responds to a request for the current node's predecessor by sending back the predecessor's ID and reference.
                | PredecessorRequest->    
                    sender <! PredecessorResponse(myPredecessor, myPredecessorRef)

                // Sets the successor for a new node in the network and initializes its finger table, also schedules periodic stabilization and finger table fix tasks.
                | FoundNewNodeSuccessor(isId, isRef) ->
                    mySuccessor <- isId
                    mySuccessorRef <- isRef
                    for i in 0..maxLengthOfTable-1 do
                        let tuple = FingerTableEntry(mySuccessor, mySuccessorRef)
                        myFingerTable.[i] <- tuple
                    chordSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(0.0),TimeSpan.FromMilliseconds(ConfigModule.stabilizationInterval), mailbox.Self, Stabilize)
                    chordSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(0.0),TimeSpan.FromMilliseconds(ConfigModule.fingerTableUpdateInterval), mailbox.Self, FixFingers)
                    mySuccessorRef <! Notify(myId, mailbox.Self)
            
                // Initiates or continues a key lookup process within the Chord ring, incrementing the hop count as it traverses nodes.
                | KeyLookup(key, hopCount, initiatedBy) ->
                    if mySuccessor < myId && (key > myId || key <= mySuccessor) then
                        logger <! HopCount(hopCount)
                    elif key <= mySuccessor && key > myId then
                        logger <! HopCount(hopCount)
                    else
                        let mutable loop = true 
                        let mutable size = maxLengthOfTable
                        let mutable value = key
                        if myId > key then 
                            value <- key + spaceSize
                        while loop do
                            size <- size - 1
                            if size < 0 then   
                                mySuccessorRef <! KeyLookup(key, hopCount + 1, initiatedBy)
                                loop <- false
                            else
                                let ithFinger = myFingerTable.[size].GetId()
                                if (ithFinger > myId && ithFinger <= value) then 
                                    let ithRef = myFingerTable.[size].GetRef()
                                    ithRef <! KeyLookup(key, hopCount + 1, initiatedBy)
                                    loop <- false                       
                        done 

                 // Triggers a series of key lookup operations to simulate usage of the Chord distributed hash table.
                | StartLookups(numRequests) ->
                    let mutable tempKey = 0
                    if mySuccessor <> firstNodeId then 
                        mySuccessorRef <! StartLookups(numRequests)
                    for x in 1..numRequests do
                        tempKey <- Random().Next(1, int(spaceSize))
                        mailbox.Self <! KeyLookup(tempKey, 1, myId)

                // This method locates the successor of a new node in the Chord ring and notifies the node that sought this information.
                | FindNewNodeSuccessor(newId, seekerRef) ->
                    if mySuccessor < myId && (newId > myId || newId < mySuccessor) then 
                        seekerRef <! FoundNewNodeSuccessor(mySuccessor, mySuccessorRef)
                    elif newId <= mySuccessor && newId > myId then 
                        seekerRef <! FoundNewNodeSuccessor(mySuccessor, mySuccessorRef)
                    else 
                        mySuccessorRef <! FindNewNodeSuccessor(newId, seekerRef)

                | _ -> ()

                return! loop()
            }
        loop()
