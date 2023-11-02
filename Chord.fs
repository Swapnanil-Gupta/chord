namespace ChordNode

open System
open Akka.Actor
open Akka.FSharp
open LoggerModule
open ConfigModule
open System.Threading

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
    let mutable hashSpace = pown 2 maxLengthOfTable
    let chordSystem = ActorSystem.Create("ChordSystem", ConfigModule.akkaConfiguration)
    let printerRef = spawn chordSystem "logActor" LoggerModule.logActor

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

                | Notify(predecessorId, predecessorRef) ->
                    myPredecessor <- predecessorId
                    myPredecessorRef <- predecessorRef

                | FixFingers ->
                    let mutable ithFinger = 0
                    for i in 1..maxLengthOfTable-1 do
                        ithFinger <- ( myId + ( pown 2 i ) ) % int(hashSpace)
                        mailbox.Self <! FindithSuccessor(i, ithFinger, mailbox.Self)

                | FindithSuccessor(i, key, tellRef) ->
                    if mySuccessor < myId && (key > myId || key < mySuccessor) then
                        tellRef <! FoundFingerEntry(i, mySuccessor, mySuccessorRef)
                    elif key <= mySuccessor && key > myId then 
                        tellRef <! FoundFingerEntry(i, mySuccessor, mySuccessorRef)
                    else 
                        let mutable Break = false 
                        let mutable x = maxLengthOfTable
                        let mutable tempVal = key
                        if myId > key then 
                            tempVal <- key + hashSpace
                        while not Break do
                            x <- x - 1
                            if x < 0 then   
                                mySuccessorRef <! FindithSuccessor(i, key, tellRef)
                                Break <- true
                            else
                                let ithFinger = myFingerTable.[x].GetId()
                                if (ithFinger > myId && ithFinger <= tempVal) then 
                                    let ithRef = myFingerTable.[x].GetRef()
                                    ithRef <! FindithSuccessor(i, key, tellRef)
                                    Break <- true                       
                        done                 

                | FoundFingerEntry(i, fingerId, fingerRef) ->
                    let tuple = FingerTableEntry(fingerId, fingerRef)
                    myFingerTable.[i] <- tuple

                | Stabilize ->
                    if mySuccessor <> 0 then 
                        mySuccessorRef <! PredecessorRequest

                | PredecessorResponse(predecessorOfSuccessor, itsRef) ->                    
                    if predecessorOfSuccessor <> myId then
                        mySuccessor <- predecessorOfSuccessor
                        mySuccessorRef <- itsRef
                    // Notify mysuccessor
                    mySuccessorRef <! Notify(myId, mailbox.Self)
                    
                | PredecessorRequest->    
                    sender <! PredecessorResponse(myPredecessor, myPredecessorRef)

                | FoundNewNodeSuccessor(isId, isRef) ->
                    // Update successor information of self
                    mySuccessor <- isId
                    mySuccessorRef <- isRef
                    // populate fingertable entry with successor - it will get corrected in next FixFingers call
                    for i in 0..maxLengthOfTable-1 do
                        let tuple = FingerTableEntry(mySuccessor, mySuccessorRef)
                        myFingerTable.[i] <- tuple
                    // start Stabilize scheduler
                    chordSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(0.0),TimeSpan.FromMilliseconds(ConfigModule.stabilizationInterval), mailbox.Self, Stabilize)
                    // start FixFingers scheduler
                    chordSystem.Scheduler.ScheduleTellRepeatedly(TimeSpan.FromMilliseconds(0.0),TimeSpan.FromMilliseconds(ConfigModule.fingerTableUpdateInterval), mailbox.Self, FixFingers)
                    // Notify Successor
                    mySuccessorRef <! Notify(myId, mailbox.Self)
            
                | KeyLookup(key, hopCount, initiatedBy) ->
                    if mySuccessor < myId && (key > myId || key <= mySuccessor) then
                        printerRef <! FoundKey(hopCount)
                    elif key <= mySuccessor && key > myId then
                        printerRef <! FoundKey(hopCount)
                    else
                        let mutable loop = true 
                        let mutable x = maxLengthOfTable
                        let mutable tempVal = key
                        if myId > key then 
                            tempVal <- key + hashSpace
                        while loop do
                            x <- x - 1
                            if x < 0 then   
                                mySuccessorRef <! KeyLookup(key, hopCount + 1, initiatedBy)
                                loop <- false
                            else
                                let ithFinger = myFingerTable.[x].GetId()
                                if (ithFinger > myId && ithFinger <= tempVal) then 
                                    let ithRef = myFingerTable.[x].GetRef()
                                    ithRef <! KeyLookup(key, hopCount + 1, initiatedBy)
                                    loop <- false                       
                        done 
                    
                | StartLookups(numRequests) ->
                    let mutable tempKey = 0
                    if mySuccessor <> firstNodeId then 
                        mySuccessorRef <! StartLookups(numRequests)
                    for x in 1..numRequests do
                        tempKey <- Random().Next(1, int(hashSpace))
                        mailbox.Self <! KeyLookup(tempKey, 1, myId)
                
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
